using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using Skippy.Core;
using Skippy.Core.Configuration;
using Skippy.Core.Data;
using Skippy.Core.Diagnostics;
using Skippy.Core.Game;
using Skippy.Core.Interop;
using Skippy.Core.Net;
using Skippy.Core.Services;
using Skippy.Core.Skips;

namespace Skippy.App.Services;

public enum ToastKind
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>
/// 应用运行时的组合根：持有配置、服务与最新状态，并驱动“探测 → 决策 → 下发”的循环。
/// </summary>
public sealed class AppHost : IDisposable
{
    private readonly Dispatcher dispatcher;
    private readonly Timer timer;
    private readonly object gate = new();

    private bool disposed;
    private int ticking;
    private ulong lastTesterCheckContentId;
    private bool testerCheckInProgress;
    private bool partyCheckActive;

    public AppHost(Dispatcher dispatcher)
    {
        this.dispatcher = dispatcher;

        AppPaths.EnsureCreated();

        this.Logger = new AppLogger(AppPaths.LogDirectory);
        this.Store = new ConfigStore(AppPaths.ConfigFile);
        this.Config = this.LoadConfig();
        this.TerritoryNames = TerritoryNames.LoadEmbedded();
        this.Research = new ResearchClient();
        this.Channel = new SharedMemoryChannel();
        this.NativeLibrary = new NativeLibraryProvider();
        this.PartyCheck = new PartyCheckService(this.Research, this.Logger);
        this.Coordinator = new SkipCoordinator();
        this.Monitor = new GameMonitor(this);

        this.PartyCheck.ResultChanged += this.OnPartyCheckResult;
        this.Snapshot = GameSnapshot.Empty;
        this.Coordination = this.Coordinator.Evaluate(this.Config, SkipContext.Empty);

        this.timer = new Timer(this.OnTick, null, Timeout.Infinite, Timeout.Infinite);

        this.Logger.Info($"Skippy 独立版启动，配置：{AppPaths.ConfigFile}");
    }

    public static AppHost Instance { get; private set; } = null!;

    public ConfigStore Store { get; }

    public SkippyConfig Config { get; private set; }

    public AppLogger Logger { get; }

    public TerritoryNames TerritoryNames { get; }

    public ResearchClient Research { get; }

    public SharedMemoryChannel Channel { get; }

    public NativeLibraryProvider NativeLibrary { get; }

    public PartyCheckService PartyCheck { get; }

    public SkipCoordinator Coordinator { get; }

    public GameMonitor Monitor { get; }

    public GameSnapshot Snapshot { get; private set; }

    public CoordinationResult Coordination { get; private set; }

    public (string Nickname, string Message)? TesterInfo { get; private set; }

    public string UserId => UserIdentity.GetUserId(this.Snapshot.ContentId);

    public string TerritoryName => this.TerritoryNames.GetName(this.Snapshot.TerritoryId);

    public bool NativeReady => this.Channel.NativeReady;

    public SharedStateLayout.NativeError NativeError => this.Channel.LastError;

    public event Action? StateChanged;

    public event Action? ConfigChanged;

    public event Action<string, ToastKind>? Notification;

    public void Start()
    {
        Instance = this;

        if (!this.Channel.Open())
            this.Logger.Warning("无法创建共享内存，原生组件将无法连接。");

        this.Monitor.Start();
        this.timer.Change(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(300));
        this.Logger.Info("宿主已启动。");
    }

    private SkippyConfig LoadConfig()
    {
        // 只读取 SkippyStandalone 自己的配置文件。首次运行时使用默认值
        // （所有跳过类别默认关闭），不会读取或继承 Dalamud 插件的配置。
        var config = this.Store.Load();

        if (!File.Exists(this.Store.FilePath))
        {
            this.Store.Save(config);
            this.Logger.Info("未找到配置文件，已创建默认配置（所有跳过类别默认关闭）。");
        }

        config.Normalize();
        return config;
    }

    private void OnTick(object? state)
    {
        if (this.disposed)
            return;

        if (Interlocked.Exchange(ref this.ticking, 1) != 0)
            return;

        try
        {
            if (!this.Channel.IsValid && !this.Channel.Open())
            {
                this.RaiseStateChanged();
                return;
            }

            this.Channel.WriteHeartbeat();

            var snapshot = this.Channel.ReadSnapshot();
            var changed = snapshot.TerritoryId != this.Snapshot.TerritoryId
                          || snapshot.IntendedUse != this.Snapshot.IntendedUse
                          || snapshot.PartySize != this.Snapshot.PartySize
                          || snapshot.IsInWorkshop != this.Snapshot.IsInWorkshop
                          || snapshot.IsLoggedIn != this.Snapshot.IsLoggedIn
                          || snapshot.ContentId != this.Snapshot.ContentId
                          || snapshot.NativeReady != this.Snapshot.NativeReady;
            this.Snapshot = snapshot;

            var coordination = this.Coordinator.Evaluate(this.Config, snapshot.ToContext());
            this.Coordination = coordination;

            this.Channel.WriteDecision(coordination.Decision, this.Config.IsEnabled, this.Config.ResearchExtraLogs);

            this.UpdatePartyCheck(snapshot);

            if (this.Coordinator.AutoParty.TakeMessage(coordination.AutoParty) is { } message)
                this.Notify(message, ToastKind.Info);

            this.TryCheckTester(snapshot);

            if (changed || this.NativeReady)
                this.RaiseStateChanged();
        }
        catch (Exception ex)
        {
            this.Logger.Warning($"Host tick: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref this.ticking, 0);
        }
    }

    private void UpdatePartyCheck(GameSnapshot snapshot)
    {
        var inMsq = TerritoryRules.IsMsqTerritory((ushort)snapshot.TerritoryId);
        var shouldCheck = this.Config.AutoEnable4Man
                          && this.Config.CheckPartySkippy
                          && inMsq
                          && snapshot.PartySize == 4
                          && snapshot.ContentId != 0;

        if (shouldCheck && !this.partyCheckActive)
        {
            this.partyCheckActive = true;

            var duty = this.TerritoryNames.GetName(snapshot.TerritoryId);
            var members = snapshot.PartyMembers.Take(snapshot.PartySize).ToArray();
            this.PartyCheck.Begin(duty, snapshot.ContentId, members, snapshot.PartySize);
        }
        else if (!shouldCheck && this.partyCheckActive)
        {
            this.partyCheckActive = false;
            this.PartyCheck.Cancel();
            this.Coordinator.AutoParty.AllPartyHasSkippy = null;
        }
    }

    private void OnPartyCheckResult(bool? allHave)
    {
        this.Coordinator.AutoParty.AllPartyHasSkippy = allHave;
        this.RaiseStateChanged();
    }

    private void TryCheckTester(GameSnapshot snapshot)
    {
        if (snapshot.ContentId == 0 || snapshot.ContentId == this.lastTesterCheckContentId || this.testerCheckInProgress)
            return;

        this.lastTesterCheckContentId = snapshot.ContentId;
        this.testerCheckInProgress = true;

        _ = Task.Run(async () =>
        {
            try
            {
                var info = await this.Research.CheckTesterAsync(this.UserId).ConfigureAwait(false);
                this.TesterInfo = info;
                if (info is not null)
                    this.Logger.Info($"已识别 Skippy Tester：{info.Value.Nickname}");
            }
            catch
            {
                this.TesterInfo = null;
            }
            finally
            {
                this.testerCheckInProgress = false;
                this.RaiseStateChanged();
            }
        });
    }

    // ── 配置操作 ───────────────────────────────────────────────────────────

    public void SaveConfig()
    {
        try
        {
            this.Config.Normalize();
            this.Store.Save(this.Config);
        }
        catch (Exception ex)
        {
            this.Logger.Error($"保存配置失败：{ex.Message}");
        }
    }

    public void NotifyConfigChanged()
    {
        this.SaveConfig();

        // 立即重新计算并下发，避免用户切换开关后要等下一个 tick。
        var coordination = this.Coordinator.Evaluate(this.Config, this.Snapshot.ToContext());
        this.Coordination = coordination;
        this.Channel.WriteDecision(coordination.Decision, this.Config.IsEnabled, this.Config.ResearchExtraLogs);

        this.ConfigChanged?.Invoke();
        this.RaiseStateChanged();
    }

    public void TogglePower()
    {
        this.Config.IsEnabled = !this.Config.IsEnabled;
        this.NotifyConfigChanged();

        this.Notify(
            this.Config.IsEnabled ? "Skippy 已启用。" : "Skippy 已禁用。",
            this.Config.IsEnabled ? ToastKind.Success : ToastKind.Info);
    }

    public void ResetAutoParty()
    {
        this.Coordinator.Reset();
        this.PartyCheck.Cancel();
        this.partyCheckActive = false;
    }

    // ── 命令与工具 ─────────────────────────────────────────────────────────

    public string? ExportTroubleshootingLog()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var exported = this.Logger.ExportTo(desktop);

        if (exported is null)
        {
            this.Notify("无法导出日志。", ToastKind.Error);
            return null;
        }

        this.Notify($"日志已导出到桌面：{Path.GetFileName(exported)}", ToastKind.Success);
        return exported;
    }

    public string GetUserId() => UserIdentity.GetUserId(this.Snapshot.ContentId);

    public void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            this.Logger.Warning($"打开链接失败：{ex.Message}");
        }
    }

    public async Task<(bool Ok, string Message)> TryEnableDevModeAsync(string password)
    {
        try
        {
            var expected = await this.Research.FetchPasswordAsync().ConfigureAwait(false);

            if (!string.IsNullOrEmpty(expected) && password.Trim() == expected)
            {
                this.Config.DevMode = true;
                this.Config.DevPassword = expected;
                this.NotifyConfigChanged();
                return (true, "开发者模式已启用。请仅在测试期间使用研究 hook，测试结束后及时关闭。");
            }

            this.Config.DevMode = false;
            this.Config.DevPassword = string.Empty;
            this.SaveConfig();
            return (false, "密码不正确，开发者模式未启用。");
        }
        catch (Exception ex)
        {
            return (false, $"验证失败：{ex.Message}");
        }
    }

    public void DisableDevMode()
    {
        this.Config.DevMode = false;
        this.Config.DevPassword = string.Empty;
        this.Config.ResearchMSQHook = false;
        this.Config.ResearchMassivePCHook = false;
        this.Config.ResearchGoldSaucerHook = false;
        this.Config.ResearchCustomTalkHook = false;
        this.Config.ResearchNormalCutscenesHook = false;
        this.Config.ResearchInnHook = false;
        this.Config.ResearchFeedBuddyHook = false;
        this.Config.ResearchGrandCompanyRankUpHook = false;
        this.Config.ResearchHairMakeHook = false;
        this.Config.ResearchExtraLogs = false;

        this.NotifyConfigChanged();
        this.Notify("开发者模式已关闭，研究 hook 已全部停用。", ToastKind.Info);
    }

    public void Notify(string message, ToastKind kind) => this.Notification?.Invoke(message, kind);

    public void RaiseStateChanged()
    {
        if (this.disposed)
            return;

        this.dispatcher.BeginInvoke(() => this.StateChanged?.Invoke(), DispatcherPriority.Background);
    }

    /// <summary>原生组件每次命中 hook 时调用（在后台线程）。</summary>
    internal void OnNativeLog(NativeLogRecord record)
    {
        if (record.Level != SharedStateLayout.LogLevel.HookFired)
            return;

        var userId = this.UserId;
        var place = this.TerritoryNames.GetName(record.TerritoryId);

        this.Logger.Info($"[跳过] {record.Message} | 地域 {record.TerritoryId}（{place}）");

        _ = this.Research.ReportHookAsync(
            record.Hook.ToString(),
            record.TerritoryId,
            place,
            record.IntendedUse,
            record.Hook == SharedStateLayout.LogHookId.CutsceneSeen,
            0,
            userId,
            this.Config.DevMode);
    }

    public void Dispose()
    {
        if (this.disposed)
            return;

        this.disposed = true;
        this.timer.Change(Timeout.Infinite, Timeout.Infinite);
        this.timer.Dispose();

        try
        {
            this.Channel.RequestShutdown();
        }
        catch
        {
        }

        this.Monitor.Dispose();
        this.PartyCheck.Dispose();
        this.Research.Dispose();
        this.Channel.Dispose();
        this.Logger.Dispose();
    }
}



using System.Diagnostics;
using Skippy.Core.Game;
using Skippy.Core.Interop;

namespace Skippy.App.Services;

/// <summary>
/// 负责发现游戏进程、注入原生组件、转发原生日志。运行在后台线程，不直接触碰 UI。
/// </summary>
public sealed class GameMonitor : IDisposable
{
    private readonly AppHost host;
    private readonly object gate = new();
    private readonly Timer timer;

    private Process? gameProcess;
    private bool injectionInProgress;
    private DateTime lastInjectionAttempt = DateTime.MinValue;
    private bool disposed;

    public GameMonitor(AppHost host)
    {
        this.host = host;
        this.timer = new Timer(this.OnTick, null, Timeout.Infinite, Timeout.Infinite);
    }

    public bool GameRunning { get; private set; }

    public bool NativeReady => this.host.Channel.NativeReady;

    public string? InjectionError { get; private set; }

    public int GameProcessId { get; private set; }

    public event Action? StatusChanged;

    public void Start() => this.timer.Change(TimeSpan.FromMilliseconds(600), TimeSpan.FromMilliseconds(900));

    public void Stop() => this.timer.Change(Timeout.Infinite, Timeout.Infinite);

    /// <summary>用户手动点击“立即注入”。</summary>
    public void RequestInjection()
    {
        this.lastInjectionAttempt = DateTime.MinValue;
        _ = Task.Run(this.EnsureInjectedAsync);
    }

    private void OnTick(object? state)
    {
        if (this.disposed)
            return;

        try
        {
            this.ForwardNativeLogs();
            this.RefreshProcessState();

            if (this.GameRunning && this.host.Config.AutoInject && !this.host.Channel.NativeReady)
                _ = Task.Run(this.EnsureInjectedAsync);
        }
        catch (Exception ex)
        {
            this.host.Logger.Warning($"GameMonitor: {ex.Message}");
        }
    }

    private void RefreshProcessState()
    {
        if (this.gameProcess is { HasExited: true })
        {
            this.host.Logger.Info("游戏进程已退出。");
            this.DisposeProcess();
            this.GameRunning = false;
            this.InjectionError = null;

            // 原生组件随进程一起消失，清掉共享内存里的旧状态。
            this.host.Channel.ResetNativeState();
            this.StatusChanged?.Invoke();
            return;
        }

        if (this.gameProcess is not null)
        {
            this.GameRunning = true;
            return;
        }

        var process = GameInstallation.FindRunningProcess();
        if (process is null)
        {
            var wasRunning = this.GameRunning;
            this.GameRunning = false;
            if (wasRunning)
                this.StatusChanged?.Invoke();
            return;
        }

        this.gameProcess = process;
        this.GameProcessId = process.Id;
        this.GameRunning = true;
        this.InjectionError = null;
        this.lastInjectionAttempt = DateTime.MinValue;
        this.host.Logger.Info($"检测到游戏进程 ffxiv_dx11.exe（PID {process.Id}）。");
        this.StatusChanged?.Invoke();
    }

    private async Task EnsureInjectedAsync()
    {
        lock (this.gate)
        {
            if (this.injectionInProgress)
                return;

            if (this.host.Channel.NativeReady)
                return;

            if (DateTime.UtcNow - this.lastInjectionAttempt < TimeSpan.FromSeconds(5))
                return;

            this.lastInjectionAttempt = DateTime.UtcNow;
            this.injectionInProgress = true;
        }

        try
        {
            var process = this.gameProcess;
            if (process is null || process.HasExited)
                return;

            var dllPath = this.host.NativeLibrary.Resolve(out var resolveError);
            if (dllPath is null)
            {
                this.SetError(resolveError ?? "找不到原生组件。");
                return;
            }

            this.host.Logger.Info($"正在注入原生组件：{dllPath}");
            var result = NativeInjector.Inject(process, dllPath);

            if (!result.Success)
            {
                this.SetError(result.Error ?? "注入失败。");
                return;
            }

            this.InjectionError = null;

            // 等待原生组件完成共享内存握手（最长 10 秒）。
            for (var i = 0; i < 40; i++)
            {
                await Task.Delay(250).ConfigureAwait(false);
                if (this.host.Channel.NativeReady)
                    break;
            }

            if (this.host.Channel.NativeReady)
            {
                this.host.Logger.Info("原生组件已就绪。");
            }
            else
            {
                this.SetError("原生组件已注入但尚未就绪，可能正在等待游戏加载。");
            }

            this.StatusChanged?.Invoke();
        }
        catch (Exception ex)
        {
            this.SetError($"注入异常：{ex.Message}");
        }
        finally
        {
            lock (this.gate)
                this.injectionInProgress = false;
        }
    }

    private void SetError(string message)
    {
        if (this.InjectionError == message)
            return;

        this.InjectionError = message;
        this.host.Logger.Warning(message);
        this.StatusChanged?.Invoke();
    }

    private void ForwardNativeLogs()
    {
        foreach (var record in this.host.Channel.DrainLogs())
        {
            var level = record.Level switch
            {
                SharedStateLayout.LogLevel.Error => Core.Diagnostics.AppLogLevel.Error,
                SharedStateLayout.LogLevel.Warning => Core.Diagnostics.AppLogLevel.Warning,
                _ => Core.Diagnostics.AppLogLevel.Info,
            };

            this.host.Logger.Log(level, $"[Native] {record.Message}");
            this.host.OnNativeLog(record);
        }
    }

    private void DisposeProcess()
    {
        try
        {
            this.gameProcess?.Dispose();
        }
        catch
        {
        }

        this.gameProcess = null;
        this.GameProcessId = 0;
    }

    public void Dispose()
    {
        this.disposed = true;
        this.Stop();
        this.timer.Dispose();
        this.DisposeProcess();
    }
}


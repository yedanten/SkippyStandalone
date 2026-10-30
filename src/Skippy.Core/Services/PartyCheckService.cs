using Skippy.Core.Diagnostics;
using Skippy.Core.Net;
using Skippy.Core.Skips;

namespace Skippy.Core.Services;

/// <summary>
/// “检查队友是否也装了 Skippy”的异步实现。
/// 进入 MSQ 副本时登记队伍标识，随后轮询共享表；任何一次查询失败都视为“不满足条件”。
/// </summary>
public sealed class PartyCheckService : IDisposable
{
    private const int MaxAttempts = 12;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly ResearchClient client;
    private readonly AppLogger logger;
    private readonly object gate = new();

    private CancellationTokenSource? cancellation;
    private string? activePartyId;
    private string activeUserId = UserIdentity.NoUserId;

    public PartyCheckService(ResearchClient client, AppLogger logger)
    {
        this.client = client;
        this.logger = logger;
    }

    /// <summary>检测结果变化时触发；null 表示正在检测。</summary>
    public event Action<bool?>? ResultChanged;

    public bool IsRunning
    {
        get
        {
            lock (this.gate)
                return this.cancellation is not null;
        }
    }

    /// <summary>为当前队伍启动一次检测。</summary>
    public void Begin(string dutyName, ulong selfContentId, IReadOnlyList<ulong> partyMemberIds, int partySize)
    {
        var userId = UserIdentity.GetUserId(selfContentId);
        var partyId = UserIdentity.BuildPartyId(partyMemberIds, selfContentId);

        if (partyId is null)
        {
            this.ResultChanged?.Invoke(false);
            return;
        }

        this.CancelInternal(deleteRemote: true);

        var cts = new CancellationTokenSource();
        lock (this.gate)
        {
            this.cancellation = cts;
            this.activePartyId = partyId;
            this.activeUserId = userId;
        }

        this.ResultChanged?.Invoke(null);
        this.logger.Info($"PartyCheck: 开始检测队伍 {partyId[..8]}…（{partySize} 人，{dutyName}）");

        _ = Task.Run(() => this.RunAsync(partyId, partySize, dutyName, userId, cts.Token), CancellationToken.None);
    }

    private async Task RunAsync(string partyId, int partySize, string dutyName, string userId, CancellationToken token)
    {
        try
        {
            await this.client.PostMatchmakingAsync(partyId, partySize, dutyName, userId, token).ConfigureAwait(false);

            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                await Task.Delay(PollInterval, token).ConfigureAwait(false);

                var allHave = await this.client.CheckAllPartyHasSkippyAsync(partyId, partySize, token).ConfigureAwait(false);
                if (allHave)
                {
                    this.logger.Info("PartyCheck: 全队均已登记 Skippy，允许跳过。");
                    this.ResultChanged?.Invoke(true);
                    return;
                }
            }

            this.logger.Warning("PartyCheck: 检测超时或存在未安装 Skippy 的队友，本次不启用跳过。");
            this.ResultChanged?.Invoke(false);
        }
        catch (OperationCanceledException)
        {
            // 正常取消。
        }
        catch (Exception ex)
        {
            this.logger.Warning($"PartyCheck: 检测失败（{ex.Message}），本次不启用跳过。");
            this.ResultChanged?.Invoke(false);
        }
    }

    /// <summary>结束检测（离开副本或关闭功能时调用）。</summary>
    public void Cancel()
    {
        this.CancelInternal(deleteRemote: true);
        this.ResultChanged?.Invoke(null);
    }

    private void CancelInternal(bool deleteRemote)
    {
        CancellationTokenSource? cts;
        string? partyId;
        string userId;

        lock (this.gate)
        {
            cts = this.cancellation;
            partyId = this.activePartyId;
            userId = this.activeUserId;

            this.cancellation = null;
            this.activePartyId = null;
        }

        if (cts is null)
            return;

        try
        {
            cts.Cancel();
        }
        catch
        {
        }

        if (deleteRemote && partyId is not null)
            _ = this.client.DeleteMatchmakingAsync(partyId, userId);

        cts.Dispose();
    }

    public void Dispose()
    {
        this.CancelInternal(deleteRemote: false);
    }
}

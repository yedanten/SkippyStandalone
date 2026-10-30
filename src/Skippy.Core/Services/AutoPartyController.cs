using Skippy.Core.Configuration;
using Skippy.Core.Skips;

namespace Skippy.Core.Services;

/// <summary>Auto-Party 的当前状态。</summary>
public readonly record struct AutoPartySnapshot(
    bool Active,
    bool SkipOverride,
    bool AwaitingPartyCheck,
    bool Locked,
    string? Message);

/// <summary>
/// Auto-Party 状态机。原插件在队列弹出（CfPop）与进入/离开地域时切换，
/// 独立版没有网络事件钩子，改为在“进入 MSQ 副本地域”时激活、离开时关闭，
/// 覆盖同样的使用场景（进入副本才需要跳过过场）。
/// </summary>
public sealed class AutoPartyController
{
    private bool active;
    private bool insideMsq;
    private string? lastMessage;

    /// <summary>由外部（PartyCheckService）提供：null 表示尚未检测，true/false 为检测结果。</summary>
    public bool? AllPartyHasSkippy { get; set; }

    public void Reset()
    {
        this.active = false;
        this.insideMsq = false;
        this.lastMessage = null;
        this.AllPartyHasSkippy = null;
    }

    public AutoPartySnapshot Update(SkipContext context, SkippyConfig config)
    {
        var inMsq = TerritoryRules.IsMsqTerritory(context.TerritoryId);

        if (!config.AutoEnable4Man)
        {
            this.active = false;
            this.insideMsq = false;
            return new AutoPartySnapshot(false, false, false, false, null);
        }

        if (inMsq)
        {
            this.insideMsq = true;

            if (config.CheckPartySkippy && this.AllPartyHasSkippy is null)
            {
                this.active = false;
                return new AutoPartySnapshot(false, false, true, false,
                    "Auto-Party: 已进入 MSQ 副本，正在等待队伍插件检测完成…");
            }

            if (config.CheckPartySkippy && this.AllPartyHasSkippy == false)
            {
                this.active = false;
                return new AutoPartySnapshot(false, false, false, false,
                    "Auto-Party: 有队员未安装 Skippy，本次副本不会跳过过场（保护账号安全）。");
            }

            // 4 人小队，或单人/无队伍（对应 Unrestricted Party 直接进入）。
            if (context.PartySize is 4 or <= 1)
            {
                this.active = true;
                return new AutoPartySnapshot(true, true, false, true,
                    "Auto-Party: 已进入 MSQ 副本，MSQ 过场跳过已自动启用。");
            }

            this.active = false;
            return new AutoPartySnapshot(false, false, false, false,
                $"Auto-Party: 当前队伍为 {context.PartySize} 人，不是 4 人小队，本次副本不会自动跳过过场。");
        }

        if (this.insideMsq)
        {
            var wasActive = this.active;
            this.insideMsq = false;
            this.active = false;
            this.AllPartyHasSkippy = null;

            return new AutoPartySnapshot(false, false, false, false,
                wasActive ? "Auto-Party: 已离开 MSQ 副本，MSQ 过场跳过已恢复为关闭。" : null);
        }

        return new AutoPartySnapshot(this.active, this.active, false, this.active, null);
    }

    /// <summary>取出尚未展示过的一次性消息，避免每 250ms 弹一次提示。</summary>
    public string? TakeMessage(AutoPartySnapshot snapshot)
    {
        if (snapshot.Message is null || snapshot.Message == this.lastMessage)
            return null;

        this.lastMessage = snapshot.Message;
        return snapshot.Message;
    }
}

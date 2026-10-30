using Skippy.Core.Configuration;
using Skippy.Core.Skips;

namespace Skippy.Core.Services;

/// <summary>一次协调的完整结果。</summary>
public sealed record CoordinationResult(
    SkippyConfig EffectiveConfig,
    SkipDecision Decision,
    AutoPartySnapshot AutoParty)
{
    public bool MsqSettingsLocked => this.AutoParty.Locked;
}

/// <summary>
/// 把配置、游戏状态与 Auto-Party 结合起来，产出可直接交给原生组件的决策。
/// </summary>
public sealed class SkipCoordinator
{
    private readonly AutoPartyController autoParty = new();

    public AutoPartyController AutoParty => this.autoParty;

    public CoordinationResult Evaluate(SkippyConfig config, SkipContext context)
    {
        var autoPartySnapshot = this.autoParty.Update(context, config);

        var effective = config.Clone();
        if (autoPartySnapshot.SkipOverride)
        {
            effective.SkipMSQRoulette = true;
            effective.ExemptPrae = false;
            effective.ExemptCastrum = false;
            effective.ExemptPorta = false;
        }

        var decision = SkipDecisionEngine.Evaluate(effective, context);

        return new CoordinationResult(effective, decision, autoPartySnapshot);
    }

    public void Reset() => this.autoParty.Reset();
}

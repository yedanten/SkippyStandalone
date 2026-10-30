namespace Skippy.Core.Skips;

/// <summary>原生 hook 的逻辑种类。</summary>
public enum HookKind
{
    /// <summary>MSQ Roulette 与金碟共用的 ContentDirector 目标。</summary>
    ContentDirector,
    MassivePC,
    CustomTalk,
    NormalCutscenes,
    Inn,
    FeedBuddy,
    CutsceneSeen,
    GrandCompanyRankUp,
    HairMake,
}

/// <summary>
/// 单个 hook 的目标状态：是否安装，以及命中时是否直接跳过（返回 1）。
/// </summary>
public readonly record struct HookState(bool Enabled, bool Skip)
{
    public static readonly HookState Disabled = new(false, false);

    public static HookState For(bool enabled, bool skip) => enabled ? new HookState(true, skip) : Disabled;
}

/// <summary>
/// 一次完整决策的结果。原生组件按此表安装 hook、打补丁。
/// </summary>
public sealed record SkipDecision
{
    public required HookState ContentDirector { get; init; }
    public required HookState MassivePC { get; init; }
    public required HookState CustomTalk { get; init; }
    public required HookState NormalCutscenes { get; init; }
    public required HookState Inn { get; init; }
    public required HookState FeedBuddy { get; init; }
    public required HookState CutsceneSeen { get; init; }
    public required HookState GrandCompanyRankUp { get; init; }
    public required HookState HairMake { get; init; }

    /// <summary>是否应写入两处过场条件跳转补丁。</summary>
    public bool MemoryPatchActive { get; init; }

    /// <summary>当前地域是否属于任一启用的跳过类别。</summary>
    public bool TerritoryActive { get; init; }

    /// <summary>当前生效类别标签，用于日志/状态显示。</summary>
    public string? ActiveTag { get; init; }

    /// <summary>是否有任意一个 hook 处于启用状态。</summary>
    public bool AnyHookEnabled =>
        this.ContentDirector.Enabled || this.MassivePC.Enabled || this.CustomTalk.Enabled
        || this.NormalCutscenes.Enabled || this.Inn.Enabled || this.FeedBuddy.Enabled
        || this.CutsceneSeen.Enabled || this.GrandCompanyRankUp.Enabled || this.HairMake.Enabled;

    public static SkipDecision None { get; } = new()
    {
        ContentDirector = HookState.Disabled,
        MassivePC = HookState.Disabled,
        CustomTalk = HookState.Disabled,
        NormalCutscenes = HookState.Disabled,
        Inn = HookState.Disabled,
        FeedBuddy = HookState.Disabled,
        CutsceneSeen = HookState.Disabled,
        GrandCompanyRankUp = HookState.Disabled,
        HairMake = HookState.Disabled,
        MemoryPatchActive = false,
        TerritoryActive = false,
        ActiveTag = null,
    };
}

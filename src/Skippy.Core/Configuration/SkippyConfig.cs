
namespace Skippy.Core.Configuration;

/// <summary>
/// 独立版 Skippy 的持久化配置。
/// 字段与原 Dalamud 插件 <c>Skippy.Config</c> 一一对应，便于直接导入旧配置；
/// 结尾附加少量独立程序自身的宿主设置。
/// </summary>
public sealed class SkippyConfig
{
    /// <summary>当前配置结构版本。原插件为 7，独立版从 8 开始。</summary>
    public const int CurrentVersion = 8;

    public int Version { get; set; } = CurrentVersion;

    // ── 总开关 ──────────────────────────────────────────────────────────────
    public bool IsEnabled { get; set; } = true;

    // ── MSQ Roulette ───────────────────────────────────────────────────────
    public bool SkipMSQRoulette { get; set; }
    public bool ExemptPrae { get; set; }
    public bool ExemptCastrum { get; set; }
    public bool ExemptPorta { get; set; }

    // ── Large-Scale Content ────────────────────────────────────────────────
    public bool SkipMassivePC { get; set; }
    public bool SkipCosmicExploration { get; set; }

    // ── Gold Saucer ────────────────────────────────────────────────────────
    public bool SkipGoldSaucer { get; set; }
    public bool ExemptChocoboRace { get; set; }
    public bool ExemptVerminion { get; set; }
    public bool ExemptTripleTriad { get; set; }
    public bool FallGuysEventActive { get; set; }
    public bool ExemptFallGuys { get; set; }
    public bool ExemptKeyboundBrawler { get; set; }
    public bool ExemptAirForceOne { get; set; }
    public bool ExemptMahjong { get; set; }

    // ── NPC Dialogue / World & Quest / Feed Buddy ──────────────────────────
    public bool SkipCustomTalk { get; set; }
    public bool ExemptSubmarines { get; set; }
    public bool SkipNormalCutscenes { get; set; }
    public bool SkipFeedBuddy { get; set; }

    // ── Risky skips ────────────────────────────────────────────────────────
    public bool SkipOceanFishing { get; set; }
    public bool SkipCrystallineConflict { get; set; }
    public bool SkipInn { get; set; }

    // ── Developer mode ─────────────────────────────────────────────────────
    public bool DevMode { get; set; }
    public string DevPassword { get; set; } = string.Empty;
    public bool ResearchMSQHook { get; set; }
    public bool ResearchMassivePCHook { get; set; }
    public bool ResearchGoldSaucerHook { get; set; }
    public bool ResearchCustomTalkHook { get; set; }
    public bool ResearchNormalCutscenesHook { get; set; }
    public bool ResearchInnHook { get; set; }
    public bool ResearchFeedBuddyHook { get; set; }
    public bool ResearchExtraLogs { get; set; }
    public bool ResearchGrandCompanyRankUpHook { get; set; }
    public bool ResearchHairMakeHook { get; set; }

    // ── Auto-Party ─────────────────────────────────────────────────────────
    public bool AutoEnable4Man { get; set; }
    public bool CheckPartySkippy { get; set; }

    // ── Behaviour ──────────────────────────────────────────────────────────
    public bool AllowCutsceneSeenGlobally { get; set; }

    // ── UI ─────────────────────────────────────────────────────────────────
    public bool HideWarning { get; set; }
    public bool HideBadge { get; set; }

    // ══ 以下为独立版宿主设置，不参与原插件语义 ═══════════════════════════

    /// <summary>检测到游戏启动后自动注入原生组件。</summary>
    public bool AutoInject { get; set; } = true;

    /// <summary>手动指定的游戏根目录（包含 <c>game\ffxiv_dx11.exe</c>），为空则自动检测。</summary>
    public string? GamePath { get; set; }

    /// <summary>关闭窗口时最小化到托盘，而不是退出。</summary>
    public bool MinimizeToTray { get; set; } = true;

    /// <summary>在切换开关时于界面底部显示提示信息。</summary>
    public bool ShowToasts { get; set; } = true;
    /// <summary>UI 上次选中的页面标识。</summary>
    public string LastPage { get; set; } = "MSQRoulette";

    /// <summary>
    /// 保证配置处于合法组合：
    /// Auto-Party 接管 MSQ 类别时清空手动 MSQ 设置；开发模式关闭时清空研究 hook。
    /// </summary>
    public void Normalize()
    {
        if (this.Version < CurrentVersion)
            this.Version = CurrentVersion;

        if (this.AutoEnable4Man)
        {
            this.SkipMSQRoulette = false;
            this.ExemptPrae = false;
            this.ExemptCastrum = false;
            this.ExemptPorta = false;
        }
        else
        {
            this.CheckPartySkippy = false;
        }

        if (!this.DevMode)
        {
            this.DevPassword = string.Empty;
            this.ResearchMSQHook = false;
            this.ResearchMassivePCHook = false;
            this.ResearchGoldSaucerHook = false;
            this.ResearchCustomTalkHook = false;
            this.ResearchNormalCutscenesHook = false;
            this.ResearchInnHook = false;
            this.ResearchFeedBuddyHook = false;
            this.ResearchGrandCompanyRankUpHook = false;
            this.ResearchHairMakeHook = false;
            this.ResearchExtraLogs = false;
        }

        if (string.IsNullOrWhiteSpace(this.LastPage))
            this.LastPage = "MSQRoulette";
    }

    public SkippyConfig Clone() => (SkippyConfig)this.MemberwiseClone();

    public void CopyFrom(SkippyConfig other)
    {
        this.Version = other.Version;
        this.IsEnabled = other.IsEnabled;

        this.SkipMSQRoulette = other.SkipMSQRoulette;
        this.ExemptPrae = other.ExemptPrae;
        this.ExemptCastrum = other.ExemptCastrum;
        this.ExemptPorta = other.ExemptPorta;

        this.SkipMassivePC = other.SkipMassivePC;
        this.SkipCosmicExploration = other.SkipCosmicExploration;

        this.SkipGoldSaucer = other.SkipGoldSaucer;
        this.ExemptChocoboRace = other.ExemptChocoboRace;
        this.ExemptVerminion = other.ExemptVerminion;
        this.ExemptTripleTriad = other.ExemptTripleTriad;
        this.FallGuysEventActive = other.FallGuysEventActive;
        this.ExemptFallGuys = other.ExemptFallGuys;
        this.ExemptKeyboundBrawler = other.ExemptKeyboundBrawler;
        this.ExemptAirForceOne = other.ExemptAirForceOne;
        this.ExemptMahjong = other.ExemptMahjong;

        this.SkipCustomTalk = other.SkipCustomTalk;
        this.ExemptSubmarines = other.ExemptSubmarines;
        this.SkipNormalCutscenes = other.SkipNormalCutscenes;
        this.SkipFeedBuddy = other.SkipFeedBuddy;

        this.SkipOceanFishing = other.SkipOceanFishing;
        this.SkipCrystallineConflict = other.SkipCrystallineConflict;
        this.SkipInn = other.SkipInn;

        this.DevMode = other.DevMode;
        this.DevPassword = other.DevPassword;
        this.ResearchMSQHook = other.ResearchMSQHook;
        this.ResearchMassivePCHook = other.ResearchMassivePCHook;
        this.ResearchGoldSaucerHook = other.ResearchGoldSaucerHook;
        this.ResearchCustomTalkHook = other.ResearchCustomTalkHook;
        this.ResearchNormalCutscenesHook = other.ResearchNormalCutscenesHook;
        this.ResearchInnHook = other.ResearchInnHook;
        this.ResearchFeedBuddyHook = other.ResearchFeedBuddyHook;
        this.ResearchExtraLogs = other.ResearchExtraLogs;
        this.ResearchGrandCompanyRankUpHook = other.ResearchGrandCompanyRankUpHook;
        this.ResearchHairMakeHook = other.ResearchHairMakeHook;

        this.AutoEnable4Man = other.AutoEnable4Man;
        this.CheckPartySkippy = other.CheckPartySkippy;

        this.AllowCutsceneSeenGlobally = other.AllowCutsceneSeenGlobally;

        this.HideWarning = other.HideWarning;
        this.HideBadge = other.HideBadge;

        this.AutoInject = other.AutoInject;
        this.GamePath = other.GamePath;
        this.MinimizeToTray = other.MinimizeToTray;
        this.ShowToasts = other.ShowToasts;
        this.LastPage = other.LastPage;
    }
}



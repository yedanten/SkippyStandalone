using Skippy.Core.Configuration;

namespace Skippy.Core.Skips;

/// <summary>
/// 跳过决策引擎。逐条移植自原插件的 <c>SigHooks.RefreshHooks</c>、<c>ShouldSkip</c>、
/// <c>CutsceneSeenDetour</c> 与 <c>DevSkip</c>，并保持相同的优先级顺序。
/// </summary>
public static class SkipDecisionEngine
{
    public static SkipDecision Evaluate(SkippyConfig config, SkipContext context)
    {
        var dev = config.DevMode;
        var enabled = config.IsEnabled;
        var territoryActive = TerritoryRules.TerritoryActive(context, config);
        var patchActive = enabled && territoryActive;

        // ContentDirector：原插件中“MSQ Roulette”与“Gold Saucer”使用的是同一个函数签名/地址，
        // 这里合并为单个 hook，避免对同一地址重复挂钩。两个类别各自保留自己的启用条件。
        var msqBusiness = enabled && (config.SkipMSQRoulette || config.SkipOceanFishing
                                      || config.SkipCrystallineConflict || config.ExemptPrae
                                      || config.ExemptCastrum || config.ExemptPorta);
        var goldBusiness = enabled && (config.SkipGoldSaucer || config.ExemptChocoboRace
                                       || config.ExemptVerminion || config.ExemptTripleTriad
                                       || config.ExemptFallGuys || config.ExemptAirForceOne
                                       || config.ExemptKeyboundBrawler || config.ExemptMahjong);
        var msqResearch = dev && config.ResearchMSQHook;
        var goldResearch = dev && config.ResearchGoldSaucerHook;

        var msqSkip = msqBusiness && ShouldSkip(HookKind.ContentDirector, false, context, config);
        var goldSkip = goldBusiness && ShouldSkipGoldSaucer(context, config);
        var msqDevSkip = msqResearch && DevSkip(context, config);
        var goldDevSkip = goldResearch && DevSkip(context, config);

        var contentDirector = HookState.For(
            msqBusiness || goldBusiness || msqResearch || goldResearch,
            msqSkip || goldSkip || msqDevSkip || goldDevSkip);

        var massiveBusiness = enabled && (config.SkipMassivePC || config.SkipCosmicExploration);
        var massiveResearch = dev && config.ResearchMassivePCHook;
        var massive = HookState.For(
            massiveBusiness || massiveResearch,
            (massiveBusiness && ShouldSkip(HookKind.MassivePC, config.SkipMassivePC, context, config))
            || (massiveResearch && DevSkip(context, config)));

        var customTalkBusiness = enabled && (config.SkipCustomTalk || TerritoryRules.SubmarinesSkip(config));
        var customTalkResearch = dev && config.ResearchCustomTalkHook;
        var customTalk = HookState.For(
            customTalkBusiness || customTalkResearch,
            (customTalkBusiness && ShouldSkip(HookKind.CustomTalk, config.SkipCustomTalk, context, config))
            || (customTalkResearch && DevSkip(context, config)));

        var normalBusiness = enabled && (config.SkipNormalCutscenes || config.SkipCosmicExploration
                                          || TerritoryRules.SubmarinesSkip(config));
        var normalResearch = dev && config.ResearchNormalCutscenesHook;
        var normal = HookState.For(
            normalBusiness || normalResearch,
            (normalBusiness && ShouldSkip(HookKind.NormalCutscenes, config.SkipNormalCutscenes, context, config))
            || (normalResearch && DevSkip(context, config)));

        var innBusiness = enabled && config.SkipInn;
        var innResearch = dev && config.ResearchInnHook;
        var inn = HookState.For(
            innBusiness || innResearch,
            (innBusiness && ShouldSkip(HookKind.Inn, false, context, config))
            || (innResearch && DevSkip(context, config)));

        var feedBusiness = enabled && config.SkipFeedBuddy;
        var feedResearch = dev && config.ResearchFeedBuddyHook;
        var feedBuddy = HookState.For(
            feedBusiness || feedResearch,
            (feedBusiness && ShouldSkip(HookKind.FeedBuddy, config.SkipFeedBuddy, context, config))
            || (feedResearch && DevSkip(context, config)));

        // IsCutsceneSeen 只在内存补丁启用期间挂钩。
        var cutsceneSeen = HookState.For(patchActive, patchActive && ShouldSkipCutsceneSeen(context, config));

        var gcRankUp = HookState.For(dev && config.ResearchGrandCompanyRankUpHook,
            dev && config.ResearchGrandCompanyRankUpHook && DevSkip(context, config));
        var hairMake = HookState.For(dev && config.ResearchHairMakeHook,
            dev && config.ResearchHairMakeHook && DevSkip(context, config));

        return new SkipDecision
        {
            ContentDirector = contentDirector,
            MassivePC = massive,
            CustomTalk = customTalk,
            NormalCutscenes = normal,
            Inn = inn,
            FeedBuddy = feedBuddy,
            CutsceneSeen = cutsceneSeen,
            GrandCompanyRankUp = gcRankUp,
            HairMake = hairMake,
            MemoryPatchActive = patchActive,
            TerritoryActive = territoryActive,
            ActiveTag = ResolveActiveTag(context, config, territoryActive),
        };
    }

    /// <summary>
    /// 复刻原插件 <c>ResolveManaged</c>/<c>ShouldSkip</c>：地域规则优先于各类别的默认开关。
    /// </summary>
    public static bool ShouldSkip(HookKind hook, bool unmanagedSkip, SkipContext context, SkippyConfig config)
    {
        if (TerritoryRules.IsRiskyTerritory(context.IntendedUse, config, out var risky))
            return risky;

        if (TerritoryRules.IsPvpVanilla(context.IntendedUse))
            return false;

        if (TerritoryRules.IsMsqTerritory(context.TerritoryId))
            return hook == HookKind.ContentDirector && TerritoryRules.MsqSkipsIn(context.TerritoryId, config);

        if (context.IntendedUse == TerritoryIntendedUse.CosmicExploration)
            return hook is HookKind.MassivePC or HookKind.NormalCutscenes && config.SkipCosmicExploration;

        if (TerritoryRules.IsGoldSaucerTerritory(context.TerritoryId, context.IntendedUse))
            return false; // 金碟由单独的 GoldSaucer 分支处理，见 ShouldSkipGoldSaucer。

        if (context.IsInWorkshop)
            return hook is HookKind.CustomTalk or HookKind.NormalCutscenes && TerritoryRules.SubmarinesSkip(config);

        return unmanagedSkip;
    }

    private static bool ShouldSkipGoldSaucer(SkipContext context, SkippyConfig config) =>
        !TerritoryRules.IsRiskyTerritory(context.IntendedUse)
        && !TerritoryRules.IsPvpVanilla(context.IntendedUse)
        && !TerritoryRules.IsMsqTerritory(context.TerritoryId)
        && context.IntendedUse != TerritoryIntendedUse.CosmicExploration
        && TerritoryRules.IsGoldSaucerTerritory(context.TerritoryId, context.IntendedUse)
        && TerritoryRules.GoldSaucerSkipsIn(context.TerritoryId, context.IntendedUse, config);

    /// <summary>复刻 <c>CutsceneSeenDetour</c>。</summary>
    public static bool ShouldSkipCutsceneSeen(SkipContext context, SkippyConfig config)
    {
        if (TerritoryRules.IsRiskyTerritory(context.IntendedUse, config, out var risky))
            return risky;

        if (TerritoryRules.IsPvpVanilla(context.IntendedUse))
            return false;

        if (TerritoryRules.IsMsqTerritory(context.TerritoryId))
            return TerritoryRules.MsqSkipsIn(context.TerritoryId, config);

        if (context.IntendedUse == TerritoryIntendedUse.CosmicExploration)
            return config.SkipCosmicExploration;

        if (TerritoryRules.IsGoldSaucerTerritory(context.TerritoryId, context.IntendedUse))
            return TerritoryRules.GoldSaucerSkipsIn(context.TerritoryId, context.IntendedUse, config);

        if (context.IsInWorkshop)
            return TerritoryRules.SubmarinesSkip(config);

        if (context.IntendedUse == TerritoryIntendedUse.HousingOutdoor && config.SkipFeedBuddy)
            return true;

        return config.AllowCutsceneSeenGlobally;
    }

    /// <summary>复刻 <c>DevSkip</c>：研究 hook 对高风险区域与 PvP 不生效。</summary>
    public static bool DevSkip(SkipContext context, SkippyConfig config) =>
        !TerritoryRules.IsRiskyTerritory(context.IntendedUse)
        && !TerritoryRules.IsPvpVanilla(context.IntendedUse);

    private static string? ResolveActiveTag(SkipContext context, SkippyConfig config, bool territoryActive)
    {
        if (!territoryActive)
            return null;

        if (TerritoryRules.IsRiskyTerritory(context.IntendedUse))
            return "RiskySkips";

        if (TerritoryRules.IsMsqTerritory(context.TerritoryId))
            return "MSQRoulette";

        if (TerritoryRules.IsGoldSaucerTerritory(context.TerritoryId, context.IntendedUse))
            return "GoldSaucer";

        if (context.IntendedUse == TerritoryIntendedUse.CosmicExploration)
            return "MassivePC";

        if (context.IsInWorkshop)
            return "CustomTalk";

        if (config.SkipMassivePC)
            return "MassivePC";

        if (config.SkipCustomTalk)
            return "CustomTalk";

        if (config.SkipNormalCutscenes)
            return "NormalCutscenes";

        if (config.SkipFeedBuddy)
            return "FeedBuddy";

        return "Behaviour";
    }
}

namespace Skippy.Core.Skips;

/// <summary>
/// 地域判定规则，逐条移植自原插件 <c>Skips/Territory.cs</c>。
/// 所有方法均为纯函数，便于单元测试。
/// </summary>
public static class TerritoryRules
{
    public static readonly ushort[] MsqPraetorium = [1044];
    public static readonly ushort[] MsqCastrum = [1043];
    public static readonly ushort[] MsqPorta = [1048];

    private static readonly ushort[] Mahjong = [831];

    private static readonly TerritoryIntendedUse[] GoldSaucerIntendedUses =
    [
        TerritoryIntendedUse.ChocoboSquareOld,
        TerritoryIntendedUse.ChocoboRacing,
        TerritoryIntendedUse.GoldSaucer,
        TerritoryIntendedUse.OriginalStepsOfFaith,
        TerritoryIntendedUse.LordOfVerminion,
        TerritoryIntendedUse.TripleTriadBattlehall,
        TerritoryIntendedUse.LeapOfFaith,
        TerritoryIntendedUse.TripleTriadOpenTournament,
        TerritoryIntendedUse.TripleTriadInvitationalParlor,
        TerritoryIntendedUse.Blunderville,
        TerritoryIntendedUse.AirForceOne,
        TerritoryIntendedUse.KeyboundBrawler,
    ];

    public static bool IsMsqTerritory(ushort territoryId) =>
        Array.IndexOf(MsqPraetorium, territoryId) >= 0 ||
        Array.IndexOf(MsqCastrum, territoryId) >= 0 ||
        Array.IndexOf(MsqPorta, territoryId) >= 0;

    public static bool IsPraetorium(ushort territoryId) => Array.IndexOf(MsqPraetorium, territoryId) >= 0;

    public static bool IsCastrum(ushort territoryId) => Array.IndexOf(MsqCastrum, territoryId) >= 0;

    public static bool IsPorta(ushort territoryId) => Array.IndexOf(MsqPorta, territoryId) >= 0;

    public static bool IsPvpVanilla(TerritoryIntendedUse use) =>
        use is TerritoryIntendedUse.Frontline or TerritoryIntendedUse.RivalWings;

    public static bool IsGoldSaucerTerritory(ushort territoryId, TerritoryIntendedUse use) =>
        Array.IndexOf(GoldSaucerIntendedUses, use) >= 0 || Array.IndexOf(Mahjong, territoryId) >= 0;

    /// <summary>
    /// 高风险区域：海钓、水晶冲突、旅馆。返回 true 时 <paramref name="skip"/> 表示对应开关是否启用。
    /// </summary>
    public static bool IsRiskyTerritory(TerritoryIntendedUse use, Configuration.SkippyConfig config, out bool skip)
    {
        switch (use)
        {
            case TerritoryIntendedUse.OceanFishing:
                skip = config.SkipOceanFishing;
                return true;

            case TerritoryIntendedUse.CrystallineConflict:
            case TerritoryIntendedUse.CrystallineConflictCustomMatch:
                skip = config.SkipCrystallineConflict;
                return true;

            case TerritoryIntendedUse.Inn:
                skip = config.SkipInn;
                return true;

            default:
                skip = false;
                return false;
        }
    }

    public static bool IsRiskyTerritory(TerritoryIntendedUse use) =>
        use is TerritoryIntendedUse.OceanFishing
            or TerritoryIntendedUse.CrystallineConflict
            or TerritoryIntendedUse.CrystallineConflictCustomMatch
            or TerritoryIntendedUse.Inn;

    /// <summary>
    /// MSQ 区域内是否跳过。注意原插件的语义：
    /// <c>SkipMSQRoulette</c> 开启时，Exempt* 表示“排除”；关闭时，Exempt* 表示“单独跳过”。
    /// </summary>
    public static bool MsqSkipsIn(ushort territoryId, Configuration.SkippyConfig config)
    {
        var tick =
            IsPraetorium(territoryId) ? config.ExemptPrae :
            IsCastrum(territoryId) ? config.ExemptCastrum :
            IsPorta(territoryId) && config.ExemptPorta;

        return config.SkipMSQRoulette ? !tick : tick;
    }

    /// <summary>金碟区域内是否跳过，语义与 <see cref="MsqSkipsIn"/> 相同。</summary>
    public static bool GoldSaucerSkipsIn(ushort territoryId, TerritoryIntendedUse use, Configuration.SkippyConfig config)
    {
        bool tick;

        switch (use)
        {
            case TerritoryIntendedUse.ChocoboRacing:
                tick = config.ExemptChocoboRace;
                break;

            case TerritoryIntendedUse.LordOfVerminion:
                tick = config.ExemptVerminion;
                break;

            case TerritoryIntendedUse.TripleTriadBattlehall:
            case TerritoryIntendedUse.TripleTriadOpenTournament:
            case TerritoryIntendedUse.TripleTriadInvitationalParlor:
                tick = config.ExemptTripleTriad;
                break;

            case TerritoryIntendedUse.Blunderville:
                tick = config.ExemptFallGuys;
                break;

            case TerritoryIntendedUse.AirForceOne:
                tick = config.ExemptAirForceOne;
                break;

            case TerritoryIntendedUse.KeyboundBrawler:
                tick = config.ExemptKeyboundBrawler;
                break;

            default:
                tick = Array.IndexOf(Mahjong, territoryId) >= 0 && config.ExemptMahjong;
                break;
        }

        return config.SkipGoldSaucer ? !tick : tick;
    }

    /// <summary>潜艇 / 工坊内容是否跳过。</summary>
    public static bool SubmarinesSkip(Configuration.SkippyConfig config) =>
        config.SkipCustomTalk ? !config.ExemptSubmarines : config.ExemptSubmarines;

    /// <summary>
    /// 当前地域是否存在任何需要生效的跳过类别（对应原插件 <c>TerritoryActive</c>）。
    /// </summary>
    public static bool TerritoryActive(SkipContext context, Configuration.SkippyConfig config)
    {
        var territoryId = context.TerritoryId;
        var use = context.IntendedUse;

        if (IsRiskyTerritory(use, config, out var risky))
            return risky;

        if (IsPvpVanilla(use))
            return false;

        if (IsMsqTerritory(territoryId))
            return MsqSkipsIn(territoryId, config);

        if (use == TerritoryIntendedUse.CosmicExploration)
            return config.SkipCosmicExploration;

        if (IsGoldSaucerTerritory(territoryId, use))
            return GoldSaucerSkipsIn(territoryId, use, config);

        if (context.IsInWorkshop)
            return SubmarinesSkip(config);

        return config.SkipMassivePC
               || config.SkipCustomTalk
               || config.SkipNormalCutscenes
               || config.SkipFeedBuddy
               || config.AllowCutsceneSeenGlobally;
    }
}

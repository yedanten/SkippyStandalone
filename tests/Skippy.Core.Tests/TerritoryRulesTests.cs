using Skippy.Core.Configuration;
using Skippy.Core.Skips;

namespace Skippy.Core.Tests;

public class TerritoryRulesTests
{
    private static SkippyConfig Config(Action<SkippyConfig>? mutate = null)
    {
        var config = new SkippyConfig();
        mutate?.Invoke(config);
        config.Normalize();
        return config;
    }

    [Theory]
    [InlineData(1043)]
    [InlineData(1044)]
    [InlineData(1048)]
    public void RecognizesMsqTerritories(ushort id)
    {
        Assert.True(TerritoryRules.IsMsqTerritory(id));
    }

    [Theory]
    [InlineData(132)]
    [InlineData(0)]
    [InlineData(831)]
    public void OtherTerritoriesAreNotMsq(ushort id)
    {
        Assert.False(TerritoryRules.IsMsqTerritory(id));
    }

    [Fact]
    public void MsqSkipSemanticsFollowGlobalToggle()
    {
        var skipAll = Config();
        Assert.True(TerritoryRules.MsqSkipsIn(1044, skipAll));
        Assert.True(TerritoryRules.MsqSkipsIn(1043, skipAll));

        var exempt = Config(c => c.ExemptPrae = true);
        Assert.False(TerritoryRules.MsqSkipsIn(1044, exempt));
        Assert.True(TerritoryRules.MsqSkipsIn(1043, exempt));

        var specific = Config(c =>
        {
            c.SkipMSQRoulette = false;
            c.ExemptPorta = true;
        });
        Assert.False(TerritoryRules.MsqSkipsIn(1044, specific));
        Assert.True(TerritoryRules.MsqSkipsIn(1048, specific));
    }

    [Fact]
    public void GoldSaucerSemanticsFollowGlobalToggle()
    {
        var skipAll = Config(c => c.SkipGoldSaucer = true);
        Assert.True(TerritoryRules.GoldSaucerSkipsIn(0, TerritoryIntendedUse.GoldSaucer, skipAll));

        var exempt = Config(c =>
        {
            c.SkipGoldSaucer = true;
            c.ExemptChocoboRace = true;
        });
        Assert.False(TerritoryRules.GoldSaucerSkipsIn(0, TerritoryIntendedUse.ChocoboRacing, exempt));
        Assert.True(TerritoryRules.GoldSaucerSkipsIn(0, TerritoryIntendedUse.GoldSaucer, exempt));

        var specific = Config(c =>
        {
            c.SkipGoldSaucer = false;
            c.ExemptMahjong = true;
        });
        Assert.True(TerritoryRules.GoldSaucerSkipsIn(831, TerritoryIntendedUse.Dungeon, specific));
        Assert.False(TerritoryRules.GoldSaucerSkipsIn(0, TerritoryIntendedUse.GoldSaucer, specific));
    }

    [Fact]
    public void SubmarinesSemanticsFollowCustomTalkToggle()
    {
        Assert.True(TerritoryRules.SubmarinesSkip(Config(c => c.SkipCustomTalk = true)));
        Assert.False(TerritoryRules.SubmarinesSkip(Config(c =>
        {
            c.SkipCustomTalk = true;
            c.ExemptSubmarines = true;
        })));
        Assert.True(TerritoryRules.SubmarinesSkip(Config(c => c.ExemptSubmarines = true)));
    }

    [Fact]
    public void RiskyTerritoryDetection()
    {
        var config = Config(c =>
        {
            c.SkipOceanFishing = true;
            c.SkipCrystallineConflict = true;
            c.SkipInn = true;
        });

        Assert.True(TerritoryRules.IsRiskyTerritory(TerritoryIntendedUse.OceanFishing, config, out var ocean));
        Assert.True(ocean);

        Assert.True(TerritoryRules.IsRiskyTerritory(TerritoryIntendedUse.CrystallineConflictCustomMatch, config, out var cc));
        Assert.True(cc);

        Assert.True(TerritoryRules.IsRiskyTerritory(TerritoryIntendedUse.Inn, config, out var inn));
        Assert.True(inn);

        Assert.False(TerritoryRules.IsRiskyTerritory(TerritoryIntendedUse.Overworld, config, out _));
    }
}

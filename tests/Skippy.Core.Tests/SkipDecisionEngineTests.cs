using Skippy.Core.Configuration;
using Skippy.Core.Skips;

namespace Skippy.Core.Tests;

public class SkipDecisionEngineTests
{
    private const ushort Praetorium = 1044;
    private const ushort Castrum = 1043;
    private const ushort Porta = 1048;

    private static SkippyConfig Config(Action<SkippyConfig>? mutate = null)
    {
        var config = new SkippyConfig();
        mutate?.Invoke(config);
        config.Normalize();
        return config;
    }

    private static SkipContext Ctx(ushort territory, TerritoryIntendedUse use, bool workshop = false, int party = 0) =>
        new(territory, use, workshop, 0, party);

    [Fact]
    public void DefaultConfig_SkipsAllThreeMsqInstances()
    {
        var config = Config();

        foreach (var (territory, name) in new[]
                 {
                     (Praetorium, "Prae"),
                     (Castrum, "Castrum"),
                     (Porta, "Porta"),
                 })
        {
            var decision = SkipDecisionEngine.Evaluate(config, Ctx(territory, TerritoryIntendedUse.Dungeon));

            Assert.True(decision.MemoryPatchActive, $"{name} 应写入内存补丁");
            Assert.True(decision.TerritoryActive, $"{name} 应处于活跃地域");
            Assert.True(decision.ContentDirector.Enabled, $"{name} 应安装 ContentDirector hook");
            Assert.True(decision.ContentDirector.Skip, $"{name} 应跳过过场");
            Assert.True(decision.CutsceneSeen.Enabled, $"{name} 应挂钩 IsCutsceneSeen");
            Assert.True(decision.CutsceneSeen.Skip, $"{name} 应把过场视为已看过");
        }
    }

    [Fact]
    public void MsqExemption_OnlyAffectsConfiguredInstance()
    {
        var config = Config(c => c.ExemptPrae = true);

        Assert.False(SkipDecisionEngine.Evaluate(config, Ctx(Praetorium, TerritoryIntendedUse.Dungeon)).ContentDirector.Skip);
        Assert.True(SkipDecisionEngine.Evaluate(config, Ctx(Castrum, TerritoryIntendedUse.Dungeon)).ContentDirector.Skip);
        Assert.True(SkipDecisionEngine.Evaluate(config, Ctx(Porta, TerritoryIntendedUse.Dungeon)).ContentDirector.Skip);
    }

    [Fact]
    public void MsqSpecificSkips_WorkWhenGlobalToggleIsOff()
    {
        var config = Config(c =>
        {
            c.SkipMSQRoulette = false;
            c.ExemptPorta = true;
        });

        var porta = SkipDecisionEngine.Evaluate(config, Ctx(Porta, TerritoryIntendedUse.Dungeon));
        var prae = SkipDecisionEngine.Evaluate(config, Ctx(Praetorium, TerritoryIntendedUse.Dungeon));

        Assert.True(porta.ContentDirector.Enabled);
        Assert.True(porta.ContentDirector.Skip);
        Assert.True(porta.MemoryPatchActive);

        Assert.False(prae.ContentDirector.Skip);
        Assert.False(prae.MemoryPatchActive);
    }

    [Fact]
    public void GoldSaucer_GlobalToggleAndExemptions()
    {
        var config = Config(c => c.SkipGoldSaucer = true);
        var racing = SkipDecisionEngine.Evaluate(config, Ctx(0, TerritoryIntendedUse.ChocoboRacing));
        Assert.True(racing.ContentDirector.Enabled);
        Assert.True(racing.ContentDirector.Skip);
        Assert.True(racing.MemoryPatchActive);
        Assert.True(racing.CutsceneSeen.Skip);

        var exempt = Config(c =>
        {
            c.SkipGoldSaucer = true;
            c.ExemptChocoboRace = true;
        });

        Assert.False(SkipDecisionEngine.Evaluate(exempt, Ctx(0, TerritoryIntendedUse.ChocoboRacing)).ContentDirector.Skip);
        Assert.True(SkipDecisionEngine.Evaluate(exempt, Ctx(0, TerritoryIntendedUse.GoldSaucer)).ContentDirector.Skip);
    }

    [Fact]
    public void GoldSaucer_MahjongIsMatchedByTerritoryId()
    {
        var config = Config(c =>
        {
            c.SkipGoldSaucer = false;
            c.ExemptMahjong = true;
        });

        var decision = SkipDecisionEngine.Evaluate(config, Ctx(831, TerritoryIntendedUse.Dungeon));

        Assert.True(decision.ContentDirector.Enabled);
        Assert.True(decision.ContentDirector.Skip);
        Assert.True(decision.TerritoryActive);
    }

    [Fact]
    public void RiskyOceanFishing_OnlySkipsWhenEnabled()
    {
        var enabled = Config(c => c.SkipOceanFishing = true);
        var decision = SkipDecisionEngine.Evaluate(enabled, Ctx(0, TerritoryIntendedUse.OceanFishing));

        Assert.True(decision.ContentDirector.Enabled);
        Assert.True(decision.ContentDirector.Skip);
        Assert.True(decision.MemoryPatchActive);
        Assert.Equal("RiskySkips", decision.ActiveTag);

        var disabled = Config(c => c.SkipOceanFishing = false);
        var off = SkipDecisionEngine.Evaluate(disabled, Ctx(0, TerritoryIntendedUse.OceanFishing));

        Assert.False(off.ContentDirector.Skip);
        Assert.False(off.MemoryPatchActive);
    }

    [Fact]
    public void RiskyCrystallineConflict_UsesItsOwnToggle()
    {
        var config = Config(c => c.SkipCrystallineConflict = true);
        var decision = SkipDecisionEngine.Evaluate(config, Ctx(0, TerritoryIntendedUse.CrystallineConflict));

        Assert.True(decision.ContentDirector.Skip);
        Assert.Equal("RiskySkips", decision.ActiveTag);
    }

    [Fact]
    public void PvpTerritories_NeverSkipOrPatch()
    {
        var config = Config(c =>
        {
            c.SkipMSQRoulette = true;
            c.SkipNormalCutscenes = true;
        });

        foreach (var use in new[] { TerritoryIntendedUse.Frontline, TerritoryIntendedUse.RivalWings })
        {
            var decision = SkipDecisionEngine.Evaluate(config, Ctx(0, use));

            Assert.False(decision.MemoryPatchActive);
            Assert.False(decision.CutsceneSeen.Enabled);
            Assert.False(decision.ContentDirector.Skip);
            Assert.False(decision.NormalCutscenes.Skip);
        }
    }

    [Fact]
    public void CosmicExploration_UsesMassivePcAndNormalCutsceneHooks()
    {
        var config = Config(c => c.SkipCosmicExploration = true);
        var decision = SkipDecisionEngine.Evaluate(config, Ctx(0, TerritoryIntendedUse.CosmicExploration));

        Assert.True(decision.MassivePC.Enabled);
        Assert.True(decision.MassivePC.Skip);
        Assert.True(decision.NormalCutscenes.Enabled);
        Assert.True(decision.NormalCutscenes.Skip);
        Assert.True(decision.MemoryPatchActive);
        Assert.Equal("MassivePC", decision.ActiveTag);
    }

    [Fact]
    public void Workshop_UsesSubmarineToggle()
    {
        var skipping = Config(c => c.SkipCustomTalk = true);
        var decision = SkipDecisionEngine.Evaluate(skipping, Ctx(0, TerritoryIntendedUse.HousingIndoor, workshop: true));

        Assert.True(decision.CustomTalk.Enabled);
        Assert.True(decision.CustomTalk.Skip);
        Assert.True(decision.NormalCutscenes.Enabled);
        Assert.True(decision.NormalCutscenes.Skip);
        Assert.True(decision.MemoryPatchActive);

        var exempt = Config(c =>
        {
            c.SkipCustomTalk = true;
            c.ExemptSubmarines = true;
        });
        var off = SkipDecisionEngine.Evaluate(exempt, Ctx(0, TerritoryIntendedUse.HousingIndoor, workshop: true));

        Assert.False(off.CustomTalk.Skip);
        Assert.False(off.NormalCutscenes.Skip);
        Assert.False(off.MemoryPatchActive);
    }

    [Fact]
    public void Overworld_NormalCutsceneToggleActivatesPatch()
    {
        var config = Config(c => c.SkipNormalCutscenes = true);
        var decision = SkipDecisionEngine.Evaluate(config, Ctx(132, TerritoryIntendedUse.Overworld));

        Assert.True(decision.NormalCutscenes.Enabled);
        Assert.True(decision.NormalCutscenes.Skip);
        Assert.True(decision.MemoryPatchActive);
        Assert.True(decision.CutsceneSeen.Enabled);
        Assert.False(decision.CutsceneSeen.Skip);
    }

    [Fact]
    public void HousingOutdoor_FeedBuddyAlsoOverridesCutsceneSeen()
    {
        var config = Config(c => c.SkipFeedBuddy = true);
        var decision = SkipDecisionEngine.Evaluate(config, Ctx(0, TerritoryIntendedUse.HousingOutdoor));

        Assert.True(decision.FeedBuddy.Enabled);
        Assert.True(decision.FeedBuddy.Skip);
        Assert.True(decision.CutsceneSeen.Skip);
    }

    [Fact]
    public void AllowCutsceneSeenGlobally_ActivatesPatchAndOverridesEverything()
    {
        var config = Config(c => c.AllowCutsceneSeenGlobally = true);
        var decision = SkipDecisionEngine.Evaluate(config, Ctx(132, TerritoryIntendedUse.Overworld));

        Assert.True(decision.MemoryPatchActive);
        Assert.True(decision.CutsceneSeen.Enabled);
        Assert.True(decision.CutsceneSeen.Skip);
        Assert.False(decision.NormalCutscenes.Skip);
    }

    [Fact]
    public void DeveloperResearchHook_WorksWithoutBusinessToggle()
    {
        var config = Config(c =>
        {
            c.DevMode = true;
            c.ResearchMSQHook = true;
            c.SkipMSQRoulette = false;
        });

        var decision = SkipDecisionEngine.Evaluate(config, Ctx(132, TerritoryIntendedUse.Overworld));

        Assert.True(decision.ContentDirector.Enabled);
        Assert.True(decision.ContentDirector.Skip);
        Assert.False(decision.MemoryPatchActive);
    }

    [Fact]
    public void DeveloperResearchHook_RespectsRiskyAndPvpExclusions()
    {
        var config = Config(c =>
        {
            c.DevMode = true;
            c.ResearchMSQHook = true;
            c.SkipOceanFishing = false;
        });

        var decision = SkipDecisionEngine.Evaluate(config, Ctx(0, TerritoryIntendedUse.OceanFishing));
        Assert.True(decision.ContentDirector.Enabled);
        Assert.False(decision.ContentDirector.Skip);
    }

    [Fact]
    public void DisabledPlugin_LeavesEverythingOff()
    {
        var config = Config(c => c.IsEnabled = false);
        var decision = SkipDecisionEngine.Evaluate(config, Ctx(Praetorium, TerritoryIntendedUse.Dungeon));

        Assert.False(decision.MemoryPatchActive);
        Assert.False(decision.ContentDirector.Enabled);
        Assert.False(decision.CutsceneSeen.Enabled);
        Assert.False(decision.AnyHookEnabled);
    }

    [Fact]
    public void PvpVanilla_UsesManagedRulesBeforeUnmanagedToggle()
    {
        var config = Config(c => c.SkipMassivePC = true);
        var decision = SkipDecisionEngine.Evaluate(config, Ctx(0, TerritoryIntendedUse.RivalWings));

        Assert.False(decision.MassivePC.Skip);
    }

    [Fact]
    public void MsqManagedRulesTakePrecedenceOverUnmanagedToggles()
    {
        var config = Config(c =>
        {
            c.SkipMassivePC = true;
            c.SkipCustomTalk = true;
        });

        var decision = SkipDecisionEngine.Evaluate(config, Ctx(Praetorium, TerritoryIntendedUse.Dungeon));

        Assert.True(decision.MassivePC.Enabled);
        Assert.False(decision.MassivePC.Skip);
        Assert.True(decision.CustomTalk.Enabled);
        Assert.False(decision.CustomTalk.Skip);
    }
}

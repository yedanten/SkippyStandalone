using Skippy.Core.Configuration;
using Skippy.Core.Services;
using Skippy.Core.Skips;

namespace Skippy.Core.Tests;

public class AutoPartyTests
{
    private const ushort Praetorium = 1044;
    private const ushort OverworldTerritory = 132;

    private static SkippyConfig Config(Action<SkippyConfig>? mutate = null)
    {
        var config = new SkippyConfig();
        mutate?.Invoke(config);
        config.Normalize();
        return config;
    }

    private static SkipContext Ctx(ushort territory, int party) =>
        new(territory, territory == OverworldTerritory ? TerritoryIntendedUse.Overworld : TerritoryIntendedUse.Dungeon, false, 0, party);

    [Fact]
    public void FourManParty_AutoEnablesAndRestores()
    {
        var config = Config(c => c.AutoEnable4Man = true);
        var coordinator = new SkipCoordinator();

        var inside = coordinator.Evaluate(config, Ctx(Praetorium, 4));
        Assert.True(inside.AutoParty.Active);
        Assert.True(inside.AutoParty.SkipOverride);
        Assert.True(inside.Decision.ContentDirector.Skip);
        Assert.True(inside.MsqSettingsLocked);

        var outside = coordinator.Evaluate(config, Ctx(OverworldTerritory, 0));
        Assert.False(outside.AutoParty.Active);
        Assert.False(outside.AutoParty.SkipOverride);
        Assert.False(outside.Decision.ContentDirector.Skip);
    }

    [Fact]
    public void SoloEntry_CountsAsUnrestrictedAndEnables()
    {
        var config = Config(c => c.AutoEnable4Man = true);
        var coordinator = new SkipCoordinator();

        var result = coordinator.Evaluate(config, Ctx(Praetorium, 1));

        Assert.True(result.AutoParty.Active);
        Assert.True(result.Decision.ContentDirector.Skip);
    }

    [Fact]
    public void TwoManParty_DoesNotAutoEnable()
    {
        var config = Config(c => c.AutoEnable4Man = true);
        var coordinator = new SkipCoordinator();

        var result = coordinator.Evaluate(config, Ctx(Praetorium, 2));

        Assert.False(result.AutoParty.Active);
        Assert.False(result.Decision.ContentDirector.Skip);
    }

    [Fact]
    public void PartyCheckRejectsWhenMemberMissing()
    {
        var config = Config(c =>
        {
            c.AutoEnable4Man = true;
            c.CheckPartySkippy = true;
        });

        var coordinator = new SkipCoordinator();
        coordinator.AutoParty.AllPartyHasSkippy = false;

        var result = coordinator.Evaluate(config, Ctx(Praetorium, 4));

        Assert.False(result.AutoParty.Active);
        Assert.False(result.Decision.ContentDirector.Skip);
        Assert.False(result.AutoParty.AwaitingPartyCheck);
    }

    [Fact]
    public void PartyCheckWaitsForResult()
    {
        var config = Config(c =>
        {
            c.AutoEnable4Man = true;
            c.CheckPartySkippy = true;
        });

        var coordinator = new SkipCoordinator();

        var waiting = coordinator.Evaluate(config, Ctx(Praetorium, 4));
        Assert.True(waiting.AutoParty.AwaitingPartyCheck);
        Assert.False(waiting.Decision.ContentDirector.Skip);

        coordinator.AutoParty.AllPartyHasSkippy = true;
        var confirmed = coordinator.Evaluate(config, Ctx(Praetorium, 4));
        Assert.True(confirmed.AutoParty.Active);
        Assert.True(confirmed.Decision.ContentDirector.Skip);
    }

    [Fact]
    public void AutoPartyOff_DoesNotOverrideManualSettings()
    {
        var config = Config(c =>
        {
            c.AutoEnable4Man = false;
            c.SkipMSQRoulette = false;
        });

        var coordinator = new SkipCoordinator();
        var result = coordinator.Evaluate(config, Ctx(Praetorium, 4));

        Assert.False(result.AutoParty.Active);
        Assert.False(result.Decision.ContentDirector.Skip);
    }

    [Fact]
    public void LeavingMsqResetsPartyCheckState()
    {
        var config = Config(c =>
        {
            c.AutoEnable4Man = true;
            c.CheckPartySkippy = true;
        });

        var coordinator = new SkipCoordinator();
        coordinator.AutoParty.AllPartyHasSkippy = true;
        coordinator.Evaluate(config, Ctx(Praetorium, 4));

        coordinator.Evaluate(config, Ctx(OverworldTerritory, 0));

        Assert.Null(coordinator.AutoParty.AllPartyHasSkippy);
    }
}

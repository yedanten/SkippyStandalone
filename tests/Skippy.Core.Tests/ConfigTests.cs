using Skippy.Core.Configuration;

namespace Skippy.Core.Tests;

public class ConfigTests
{
    [Fact]
    public void AutoPartyModeClearsManualMsqSettings()
    {
        var config = new SkippyConfig
        {
            AutoEnable4Man = true,
            SkipMSQRoulette = true,
            ExemptPrae = true,
            ExemptCastrum = true,
            ExemptPorta = true,
        };

        config.Normalize();

        Assert.False(config.SkipMSQRoulette);
        Assert.False(config.ExemptPrae);
        Assert.False(config.ExemptCastrum);
        Assert.False(config.ExemptPorta);
    }

    [Fact]
    public void DisablingAutoPartyClearsPartyCheck()
    {
        var config = new SkippyConfig
        {
            AutoEnable4Man = false,
            CheckPartySkippy = true,
        };

        config.Normalize();

        Assert.False(config.CheckPartySkippy);
    }

    [Fact]
    public void LeavingDeveloperModeClearsResearchFlags()
    {
        var config = new SkippyConfig
        {
            DevMode = false,
            DevPassword = "secret",
            ResearchMSQHook = true,
            ResearchMassivePCHook = true,
            ResearchGoldSaucerHook = true,
            ResearchCustomTalkHook = true,
            ResearchNormalCutscenesHook = true,
            ResearchInnHook = true,
            ResearchFeedBuddyHook = true,
            ResearchGrandCompanyRankUpHook = true,
            ResearchHairMakeHook = true,
            ResearchExtraLogs = true,
        };

        config.Normalize();

        Assert.Empty(config.DevPassword);
        Assert.False(config.ResearchMSQHook);
        Assert.False(config.ResearchMassivePCHook);
        Assert.False(config.ResearchGoldSaucerHook);
        Assert.False(config.ResearchCustomTalkHook);
        Assert.False(config.ResearchNormalCutscenesHook);
        Assert.False(config.ResearchInnHook);
        Assert.False(config.ResearchFeedBuddyHook);
        Assert.False(config.ResearchGrandCompanyRankUpHook);
        Assert.False(config.ResearchHairMakeHook);
        Assert.False(config.ResearchExtraLogs);
    }

    [Fact]
    public void ConfigStore_RoundTrips()
    {
        var directory = Path.Combine(Path.GetTempPath(), "skippy-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "config.json");

        try
        {
            var store = new ConfigStore(path);
            var config = new SkippyConfig
            {
                SkipGoldSaucer = true,
                ExemptChocoboRace = true,
                AllowCutsceneSeenGlobally = true,
                GamePath = @"E:\最终幻想XIV",
            };

            store.Save(config);
            var loaded = store.Load();

            Assert.True(File.Exists(path));
            Assert.True(loaded.SkipGoldSaucer);
            Assert.True(loaded.ExemptChocoboRace);
            Assert.True(loaded.AllowCutsceneSeenGlobally);
            Assert.Equal(@"E:\最终幻想XIV", loaded.GamePath);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ConfigStore_InvalidJsonFallsBackToDefaults()
    {
        var directory = Path.Combine(Path.GetTempPath(), "skippy-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "config.json");

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "{ this is not json");

            var store = new ConfigStore(path);
            var config = store.Load();

            Assert.True(config.SkipMSQRoulette);
            Assert.NotNull(store.LastError);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}

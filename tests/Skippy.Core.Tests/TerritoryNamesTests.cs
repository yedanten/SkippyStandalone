using Skippy.Core.Data;

namespace Skippy.Core.Tests;

public class TerritoryNamesTests
{
    [Fact]
    public void EmbeddedTableIsLoaded()
    {
        var names = TerritoryNames.LoadEmbedded();

        Assert.True(names.Count > 1000, $"内置地名表条目过少：{names.Count}");
        Assert.NotNull(names.GameVersion);
    }

    [Fact]
    public void MsqTerritoriesHaveChineseNames()
    {
        var names = TerritoryNames.LoadEmbedded();

        Assert.Equal("帝国南方堡", names.GetName(1043));
        Assert.Equal("天幕魔导城", names.GetName(1044));
        Assert.Equal("后营门", names.GetName(1048));
    }

    [Fact]
    public void UnknownTerritoryFallsBackToId()
    {
        var names = TerritoryNames.LoadEmbedded();

        Assert.Contains("999999", names.GetName(999999));
    }
}

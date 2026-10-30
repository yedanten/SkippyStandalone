namespace Skippy.Core.Skips;

/// <summary>
/// 决策所需的游戏状态上下文。
/// </summary>
public readonly record struct SkipContext(
    ushort TerritoryId,
    TerritoryIntendedUse IntendedUse,
    bool IsInWorkshop,
    ulong ContentId,
    int PartySize)
{
    public static SkipContext Empty => new(0, 0, false, 0, 0);

    public bool HasTerritory => this.TerritoryId != 0;
}

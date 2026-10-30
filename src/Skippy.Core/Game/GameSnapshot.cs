using Skippy.Core.Skips;

namespace Skippy.Core.Game;

/// <summary>
/// 原生组件探测到的游戏状态快照（经由共享内存传递）。
/// </summary>
public readonly record struct GameSnapshot(
    uint TerritoryId,
    TerritoryIntendedUse IntendedUse,
    ulong ContentId,
    int PartySize,
    ulong[] PartyMembers,
    bool IsInWorkshop,
    bool IsLoggedIn,
    bool NativeReady)
{
    public static GameSnapshot Empty => new(0, 0, 0, 0, [], false, false, false);

    public bool HasTerritory => this.TerritoryId != 0;

    public SkipContext ToContext() => new(
        (ushort)this.TerritoryId,
        this.IntendedUse,
        this.IsInWorkshop,
        this.ContentId,
        this.PartySize);
}

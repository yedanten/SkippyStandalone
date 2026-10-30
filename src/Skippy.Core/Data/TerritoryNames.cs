using System.Text.Json;

namespace Skippy.Core.Data;

/// <summary>
/// 地图名查找表。数据由 <c>tools/Skippy.DataTool</c> 从游戏 TerritoryType 表导出并嵌入程序，
/// 运行时不依赖游戏文件，也不依赖 Lumina。
/// </summary>
public sealed class TerritoryNames
{
    private readonly Dictionary<uint, string> names;

    private TerritoryNames(Dictionary<uint, string> names, string? gameVersion)
    {
        this.names = names;
        this.GameVersion = gameVersion;
    }

    public static TerritoryNames Empty { get; } = new([], null);

    public string? GameVersion { get; }

    public int Count => this.names.Count;

    public string GetName(uint territoryId) =>
        this.names.TryGetValue(territoryId, out var name) ? name : $"Unknown ({territoryId})";

    public static TerritoryNames LoadEmbedded()
    {
        try
        {
            using var stream = typeof(TerritoryNames).Assembly
                .GetManifestResourceStream("Skippy.Core.Data.TerritoryNames.json");

            if (stream is null)
                return Empty;

            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            var version = root.TryGetProperty("_gameVersion", out var versionElement)
                ? versionElement.GetString()
                : null;

            var names = new Dictionary<uint, string>();
            if (root.TryGetProperty("territories", out var territories))
            {
                foreach (var property in territories.EnumerateObject())
                {
                    if (uint.TryParse(property.Name, out var id) && property.Value.ValueKind == JsonValueKind.String)
                        names[id] = property.Value.GetString() ?? string.Empty;
                }
            }

            return new TerritoryNames(names, version);
        }
        catch
        {
            return Empty;
        }
    }
}

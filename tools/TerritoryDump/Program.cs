using System.Text.Json;
using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;

namespace Skippy.Tools.TerritoryDump;

/// <summary>
/// 从游戏数据导出 TerritoryType 行号 → 地名，供离线 UI 与日志显示使用。
/// 需要本机安装 Dalamud（提供 Lumina）以及 FFXIV 客户端数据。
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var gamePath = ResolveGamePath(args);
            var outputPath = ResolveOutputPath(args);

            if (gamePath is null)
            {
                Console.Error.WriteLine("找不到游戏安装目录。请使用 --game \"E:\\最终幻想XIV\" 指定。");
                return 2;
            }

            var gameDir = Directory.Exists(Path.Combine(gamePath, "game"))
                ? Path.Combine(gamePath, "game")
                : gamePath;

            var versionFile = Path.Combine(gameDir, "ffxivgame.ver");
            var gameVersion = File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : "unknown";

            var options = new LuminaOptions
            {
                LoadMultithreaded = true,
                PanicOnSheetChecksumMismatch = false,
                DefaultExcelLanguage = Language.ChineseSimplified,
            };

            using var gameData = new GameData(Path.Combine(gameDir, "sqpack"), options);
            var sheet = gameData.Excel.GetSheet<TerritoryType>(Language.ChineseSimplified);

            Console.WriteLine($"TerritoryType 行数: {sheet.Count}");

            var territories = new SortedDictionary<uint, string>();
            foreach (var row in sheet)
            {
                string name;
                try
                {
                    name = row.PlaceName.Value.Name.ToString();
                }
                catch
                {
                    name = string.Empty;
                }

                territories[row.RowId] = name ?? string.Empty;
            }

            var payload = new
            {
                _comment = "生成自游戏 TerritoryType 表，由 tools/TerritoryDump 导出。键为 TerritoryType 行号，值为地图名。",
                _generatedAt = DateTimeOffset.UtcNow.ToString("O"),
                _gameVersion = gameVersion,
                _language = "ChineseSimplified",
                territories,
            };

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));

            Console.WriteLine($"已写入 {outputPath}（{territories.Count} 条）");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static string? ResolveGamePath(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--game" or "-g")
                return args[i + 1];
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        foreach (var launcher in new[] { "XIVLauncherCN", "XIVLauncher" })
        {
            var config = Path.Combine(appData, launcher, "launcherConfigV3.json");
            if (!File.Exists(config))
                continue;

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(config));
                if (document.RootElement.TryGetProperty("GamePath", out var gamePath))
                {
                    var value = gamePath.GetString();
                    if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value))
                        return value;
                }
            }
            catch
            {
                // 尝试下一个启动器。
            }
        }

        return null;
    }

    private static string ResolveOutputPath(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--output" or "-o")
                return Path.GetFullPath(args[i + 1]);
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Skippy.Core", "Data", "TerritoryNames.json"));
    }
}

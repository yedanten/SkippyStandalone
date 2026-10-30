using System.Text.Json;

namespace Skippy.Core.Configuration;

/// <summary>
/// 从原 Dalamud 插件残留的配置文件中导入设置，让老用户无需重新勾选。
/// </summary>
public static class DalamudConfigImporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>按优先级列出原插件可能存放配置的位置。</summary>
    public static IEnumerable<string> CandidatePaths()
    {
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        yield return Path.Combine(roaming, "XIVLauncherCN", "pluginConfigs", "Skippy.json");
        yield return Path.Combine(roaming, "XIVLauncherCN", "pluginConfigs", "Skippy", "Skippy.json");
        yield return Path.Combine(roaming, "XIVLauncher", "pluginConfigs", "Skippy.json");
        yield return Path.Combine(roaming, "XIVLauncher", "pluginConfigs", "Skippy", "Skippy.json");
    }

    /// <summary>找到第一个可解析的旧配置。</summary>
    public static ImportResult TryImport()
    {
        foreach (var path in CandidatePaths())
        {
            if (!File.Exists(path))
                continue;

            try
            {
                var json = File.ReadAllText(path);
                var imported = JsonSerializer.Deserialize<SkippyConfig>(json, JsonOptions);
                if (imported is null)
                    continue;

                imported.Normalize();
                return new ImportResult(imported, path, null);
            }
            catch (Exception ex)
            {
                return new ImportResult(null, path, ex.Message);
            }
        }

        return new ImportResult(null, null, null);
    }

    /// <summary>
    /// 把导入配置中的“跳过设置”合并到现有配置，保留现有的宿主设置（游戏路径、托盘行为等）。
    /// </summary>
    public static void MergeSkipSettings(SkippyConfig target, SkippyConfig imported)
    {
        target.IsEnabled = imported.IsEnabled;

        target.SkipMSQRoulette = imported.SkipMSQRoulette;
        target.ExemptPrae = imported.ExemptPrae;
        target.ExemptCastrum = imported.ExemptCastrum;
        target.ExemptPorta = imported.ExemptPorta;

        target.SkipMassivePC = imported.SkipMassivePC;
        target.SkipCosmicExploration = imported.SkipCosmicExploration;

        target.SkipGoldSaucer = imported.SkipGoldSaucer;
        target.ExemptChocoboRace = imported.ExemptChocoboRace;
        target.ExemptVerminion = imported.ExemptVerminion;
        target.ExemptTripleTriad = imported.ExemptTripleTriad;
        target.FallGuysEventActive = imported.FallGuysEventActive;
        target.ExemptFallGuys = imported.ExemptFallGuys;
        target.ExemptKeyboundBrawler = imported.ExemptKeyboundBrawler;
        target.ExemptAirForceOne = imported.ExemptAirForceOne;
        target.ExemptMahjong = imported.ExemptMahjong;

        target.SkipCustomTalk = imported.SkipCustomTalk;
        target.ExemptSubmarines = imported.ExemptSubmarines;
        target.SkipNormalCutscenes = imported.SkipNormalCutscenes;
        target.SkipFeedBuddy = imported.SkipFeedBuddy;

        target.SkipOceanFishing = imported.SkipOceanFishing;
        target.SkipCrystallineConflict = imported.SkipCrystallineConflict;
        target.SkipInn = imported.SkipInn;

        target.AutoEnable4Man = imported.AutoEnable4Man;
        target.CheckPartySkippy = imported.CheckPartySkippy;

        target.AllowCutsceneSeenGlobally = imported.AllowCutsceneSeenGlobally;

        target.HideWarning = imported.HideWarning;
        target.HideBadge = imported.HideBadge;

        target.LastConfigImportUtc = DateTimeOffset.UtcNow;
        target.Normalize();
    }

    public sealed record ImportResult(SkippyConfig? Config, string? SourcePath, string? Error)
    {
        public bool Success => this.Config is not null && this.Error is null;
    }
}

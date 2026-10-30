using System.Text.Json;
using System.Text.Json.Serialization;

namespace Skippy.Core.Configuration;

/// <summary>
/// 配置文件的读写。写入采用“临时文件 + 替换”的方式，避免游戏/程序崩溃时留下半个 JSON。
/// </summary>
public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNameCaseInsensitive = true,
    };

    private readonly object gate = new();

    public ConfigStore(string filePath)
    {
        this.FilePath = filePath;
    }

    public string FilePath { get; }

    /// <summary>最近一次加载是否失败的说明，仅用于诊断。</summary>
    public string? LastError { get; private set; }

    public SkippyConfig Load()
    {
        lock (this.gate)
        {
            this.LastError = null;

            if (!File.Exists(this.FilePath))
                return new SkippyConfig();

            try
            {
                var json = File.ReadAllText(this.FilePath);
                var config = JsonSerializer.Deserialize<SkippyConfig>(json, JsonOptions);
                if (config is null)
                {
                    this.LastError = "配置文件为空，已使用默认设置。";
                    return new SkippyConfig();
                }

                config.Normalize();
                return config;
            }
            catch (Exception ex)
            {
                this.LastError = $"读取配置失败：{ex.Message}";
                return new SkippyConfig();
            }
        }
    }

    public void Save(SkippyConfig config)
    {
        lock (this.gate)
        {
            var directory = Path.GetDirectoryName(this.FilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(config, JsonOptions);
            var temp = this.FilePath + ".tmp";

            File.WriteAllText(temp, json);
            File.Move(temp, this.FilePath, overwrite: true);
        }
    }
}

using System.Diagnostics;
using System.Text.Json;

namespace Skippy.Core.Game;

/// <summary>
/// 定位 FFXIV 的安装目录。优先使用用户配置，其次 XIVLauncher 配置，最后常见安装路径。
/// </summary>
public sealed class GameInstallation
{
    public GameInstallation(string rootPath)
    {
        this.RootPath = rootPath;
    }

    /// <summary>游戏根目录（包含 <c>game\ffxiv_dx11.exe</c>）。</summary>
    public string RootPath { get; }

    public string GamePath => Path.Combine(this.RootPath, "game");

    public string ExecutablePath => Path.Combine(this.GamePath, "ffxiv_dx11.exe");

    public bool IsValid => File.Exists(this.ExecutablePath);

    /// <summary>客户端版本字符串（来自 <c>ffxivgame.ver</c>），读取失败时返回 null。</summary>
    public string? Version
    {
        get
        {
            try
            {
                var file = Path.Combine(this.GamePath, "ffxivgame.ver");
                return File.Exists(file) ? File.ReadAllText(file).Trim() : null;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>按优先级尝试发现游戏安装位置。</summary>
    public static GameInstallation? Discover(string? configuredPath)
    {
        foreach (var candidate in EnumerateCandidates(configuredPath))
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            try
            {
                var normalized = Path.GetFullPath(candidate.Trim());
                var installation = new GameInstallation(normalized);
                if (installation.IsValid)
                    return installation;
            }
            catch
            {
                // 路径非法时继续尝试下一个。
            }
        }

        return null;
    }

    private static IEnumerable<string?> EnumerateCandidates(string? configuredPath)
    {
        yield return configuredPath;

        yield return ReadXIVLauncherGamePath(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncherCN");
        yield return ReadXIVLauncherGamePath(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher");

        foreach (var drive in EnumerateFixedDrives())
        {
            yield return Path.Combine(drive, "最终幻想XIV");
            yield return Path.Combine(drive, "FFXIV");
            yield return Path.Combine(drive, "SquareEnix", "FINAL FANTASY XIV - A Realm Reborn");
            yield return Path.Combine(drive, "Program Files (x86)", "SquareEnix", "FINAL FANTASY XIV - A Realm Reborn");
            yield return Path.Combine(drive, "Program Files", "SquareEnix", "FINAL FANTASY XIV - A Realm Reborn");
        }
    }

    private static string? ReadXIVLauncherGamePath(string appData, string launcherFolder)
    {
        try
        {
            var config = Path.Combine(appData, launcherFolder, "launcherConfigV3.json");
            if (!File.Exists(config))
                return null;

            using var document = JsonDocument.Parse(File.ReadAllText(config));
            if (document.RootElement.TryGetProperty("GamePath", out var gamePath))
                return gamePath.GetString();
        }
        catch
        {
            // 忽略损坏的启动器配置。
        }

        return null;
    }

    private static IEnumerable<string> EnumerateFixedDrives()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
                continue;

            if (seen.Add(drive.RootDirectory.FullName))
                yield return drive.RootDirectory.FullName;
        }
    }

    /// <summary>查找正在运行的 ffxiv_dx11 进程（64 位）。返回的进程对象由调用方释放。</summary>
    public static Process? FindRunningProcess()
    {
        var processes = Process.GetProcessesByName("ffxiv_dx11");
        foreach (var process in processes)
        {
            var matched = false;
            try
            {
                matched = !process.HasExited && process.MainModule is not null;
            }
            catch
            {
                matched = false;
            }

            if (matched)
            {
                foreach (var other in processes)
                {
                    if (!ReferenceEquals(other, process))
                        other.Dispose();
                }

                return process;
            }

            process.Dispose();
        }

        return null;
    }
}

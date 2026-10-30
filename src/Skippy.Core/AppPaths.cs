namespace Skippy.Core;

/// <summary>
/// 独立版 Skippy 使用的所有磁盘位置。集中管理，避免各组件各自拼路径。
/// </summary>
public static class AppPaths
{
    public const string ProductName = "SkippyStandalone";

    public static string RoamingDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ProductName);

    public static string LocalDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductName);

    public static string ConfigFile => Path.Combine(RoamingDirectory, "config.json");

    public static string LogDirectory => Path.Combine(LocalDirectory, "logs");

    public static string RuntimeDirectory => Path.Combine(LocalDirectory, "runtime");

    /// <summary>当前程序所在目录（单文件发布时为 EXE 目录）。</summary>
    public static string BaseDirectory => AppContext.BaseDirectory;

    public static string NativeDllOnDisk => Path.Combine(BaseDirectory, "Skippy.Native.dll");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(RoamingDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(RuntimeDirectory);
    }
}

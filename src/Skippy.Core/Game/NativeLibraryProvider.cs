using System.Reflection;
using System.Security.Cryptography;

namespace Skippy.Core.Game;

/// <summary>
/// 提供可注入的原生组件路径。发布版从 EXE 内嵌资源释放到本机缓存，
/// 开发版则优先使用输出目录中的 <c>Skippy.Native.dll</c>，方便热重载调试。
/// </summary>
public sealed class NativeLibraryProvider
{
    /// <summary>嵌入资源名（见 StandaloneApp.csproj 的 LogicalName）。</summary>
    public const string ResourceName = "Skippy.Native.dll";

    private const string DllFileName = "Skippy.Native.dll";

    private readonly Assembly hostAssembly;

    public NativeLibraryProvider(Assembly? hostAssembly = null)
    {
        // 单文件发布时资源位于入口程序集（StandaloneApp），而不是 Skippy.Core。
        this.hostAssembly = hostAssembly
                            ?? Assembly.GetEntryAssembly()
                            ?? typeof(NativeLibraryProvider).Assembly;
    }

    /// <summary>返回可直接注入的 DLL 绝对路径；失败时返回 null 并给出原因。</summary>
    public string? Resolve(out string? error)
    {
        error = null;

        // 开发/便携场景：EXE 旁边就有原生 DLL。
        var local = Path.Combine(AppPaths.BaseDirectory, DllFileName);
        if (File.Exists(local))
            return local;

        try
        {
            using var stream = this.hostAssembly.GetManifestResourceStream(ResourceName);
            if (stream is null)
            {
                error = "程序内未找到原生组件资源，请重新构建项目（tools\\build.ps1）。";
                return null;
            }

            var version = this.hostAssembly.GetName().Version?.ToString(4) ?? "0.0.0.0";
            var versionDir = Path.Combine(AppPaths.RuntimeDirectory, version);
            Directory.CreateDirectory(versionDir);

            var target = Path.Combine(versionDir, DllFileName);

            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var bytes = buffer.ToArray();

            if (File.Exists(target) && File.ReadAllBytes(target).AsSpan().SequenceEqual(bytes))
                return target;

            var temp = target + ".tmp";
            File.WriteAllBytes(temp, bytes);
            File.Move(temp, target, overwrite: true);

            return target;
        }
        catch (Exception ex)
        {
            error = $"释放原生组件失败：{ex.Message}";
            return null;
        }
    }

    /// <summary>用于诊断显示的本地组件 SHA-256 前缀。</summary>
    public static string ShortHash(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash)[..16].ToLowerInvariant();
        }
        catch
        {
            return "unknown";
        }
    }
}

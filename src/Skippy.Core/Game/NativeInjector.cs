using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Skippy.Core.Game;

/// <summary>
/// 把原生组件注入 ffxiv_dx11.exe。实现方式与 Dalamud 的注入器一致：
/// 在目标进程中分配路径字符串并创建远程线程调用 <c>LoadLibraryW</c>。
/// </summary>
public static class NativeInjector
{
    private const uint ProcessCreateThread = 0x0002;
    private const uint ProcessQueryInformation = 0x0400;
    private const uint ProcessVmOperation = 0x0008;
    private const uint ProcessVmWrite = 0x0020;
    private const uint ProcessVmRead = 0x0010;
    private const uint MemCommit = 0x1000;
    private const uint MemReserve = 0x2000;
    private const uint MemRelease = 0x8000;
    private const uint PageReadWrite = 0x04;
    private const uint WaitObject0 = 0x00000000;
    private const uint WaitTimeout = 0x00000102;

    public sealed record InjectionResult(bool Success, bool AlreadyLoaded, IntPtr ModuleHandle, string? Error)
    {
        public static InjectionResult Ok(bool alreadyLoaded = false) => new(true, alreadyLoaded, IntPtr.Zero, null);

        public static InjectionResult Fail(string error) => new(false, false, IntPtr.Zero, error);
    }

    /// <summary>
    /// 注入指定 DLL。返回值只表示远程线程是否成功执行；调用方应通过共享内存确认原生组件就绪。
    /// </summary>
    public static InjectionResult Inject(Process process, string dllPath)
    {
        if (!File.Exists(dllPath))
            return InjectionResult.Fail($"找不到原生组件：{dllPath}");

        if (!Environment.Is64BitProcess)
            return InjectionResult.Fail("本程序必须以 64 位模式运行才能注入 64 位游戏进程。");

        if (IsModuleLoaded(process, Path.GetFileName(dllPath)))
            return InjectionResult.Ok(alreadyLoaded: true);

        var handle = OpenProcess(
            ProcessCreateThread | ProcessQueryInformation | ProcessVmOperation | ProcessVmWrite | ProcessVmRead,
            false,
            process.Id);

        if (handle == IntPtr.Zero)
            return InjectionResult.Fail($"无法打开游戏进程（错误 {Marshal.GetLastWin32Error()}）。请尝试以管理员身份运行本程序。");

        var remotePath = IntPtr.Zero;
        var thread = IntPtr.Zero;

        try
        {
            var pathBytes = System.Text.Encoding.Unicode.GetBytes(dllPath + '\0');
            remotePath = VirtualAllocEx(handle, IntPtr.Zero, (nuint)pathBytes.Length, MemCommit | MemReserve, PageReadWrite);
            if (remotePath == IntPtr.Zero)
                return InjectionResult.Fail($"无法在游戏进程中分配内存（错误 {Marshal.GetLastWin32Error()}）。");

            if (!WriteProcessMemory(handle, remotePath, pathBytes, (nuint)pathBytes.Length, out var written)
                || written != (nuint)pathBytes.Length)
            {
                return InjectionResult.Fail($"无法写入游戏进程内存（错误 {Marshal.GetLastWin32Error()}）。");
            }

            var kernel32 = GetModuleHandleW("kernel32.dll");
            var loadLibrary = GetProcAddress(kernel32, "LoadLibraryW");
            if (loadLibrary == IntPtr.Zero)
                return InjectionResult.Fail("无法解析 LoadLibraryW 地址。");

            thread = CreateRemoteThread(handle, IntPtr.Zero, 0, loadLibrary, remotePath, 0, out _);
            if (thread == IntPtr.Zero)
                return InjectionResult.Fail($"创建远程线程失败（错误 {Marshal.GetLastWin32Error()}）。");

            var wait = WaitForSingleObject(thread, 10_000);
            if (wait == WaitTimeout)
                return InjectionResult.Fail("等待 LoadLibraryW 超时。");

            return InjectionResult.Ok();
        }
        catch (Exception ex)
        {
            return InjectionResult.Fail($"注入失败：{ex.Message}");
        }
        finally
        {
            if (thread != IntPtr.Zero)
                CloseHandle(thread);

            if (remotePath != IntPtr.Zero)
                VirtualFreeEx(handle, remotePath, 0, MemRelease);

            CloseHandle(handle);
        }
    }

    /// <summary>检查目标进程是否已加载同名模块。</summary>
    public static bool IsModuleLoaded(Process process, string moduleName)
    {
        try
        {
            foreach (ProcessModule module in process.Modules)
            {
                if (module.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch
        {
            // 权限不足时无法枚举；按未加载处理，远程线程会再次加载同一 DLL（Windows 会返回已有句柄）。
        }

        return false;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAllocEx(IntPtr process, IntPtr address, nuint size, uint allocationType, uint protect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFreeEx(IntPtr process, IntPtr address, nuint size, uint freeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] buffer, nuint size, out nuint written);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string moduleName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr module, string procedureName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateRemoteThread(
        IntPtr process,
        IntPtr attributes,
        nuint stackSize,
        IntPtr startAddress,
        IntPtr parameter,
        uint creationFlags,
        out uint threadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
}

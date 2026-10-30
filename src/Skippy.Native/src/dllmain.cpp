#include <windows.h>
#include <cstdint>

#include "Worker.h"

namespace {

// 通用导出，便于宿主/诊断工具确认 DLL 版本（当前未强制使用）。
extern "C" __declspec(dllexport) uint32_t SkippyNativeGetVersion() {
    return 1;
}

} // namespace

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID reserved) {
    (void)reserved;

    if (reason != DLL_PROCESS_ATTACH)
        return TRUE;

    DisableThreadLibraryCalls(module);

    // 不在 DllMain 中做重活：只启动工作线程，由它连接共享内存、扫描签名并挂钩。
    const HANDLE thread = CreateThread(nullptr, 0, skippy::WorkerThreadProc, nullptr, 0, nullptr);
    if (thread != nullptr)
        CloseHandle(thread);

    return TRUE;
}


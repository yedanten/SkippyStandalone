#pragma once

#include <cstddef>
#include <cstdint>

#include "../ipc/SharedState.h"
#include "../ipc/SharedStateStore.h"
#include "GameProbe.h"
#include "HookTargets.h"
#include "ModuleInfo.h"

namespace skippy {

// 用 MinHook 管理所有游戏函数 hook。启用/停用完全由共享内存中的决策表驱动。
class HookManager {
public:
    HookManager() = default;
    ~HookManager();

    HookManager(const HookManager&) = delete;
    HookManager& operator=(const HookManager&) = delete;

    bool Initialize();
    void Shutdown();

    // 解析所有目标地址，返回 SignatureBits 掩码。
    uint32_t ResolveTargets(const ModuleInfo& module, GameProbe& probe);

    // 按决策表同步 hook 的安装状态。
    void Sync(const SharedState& state, SharedStateStore& store);

    void RemoveAll(SharedStateStore& store);

    uint32_t InstalledMask() const;

    // 返回已解析的 hook 目标（未找到时为 nullptr），用于把过场补丁定位到锚点附近。
    void* TargetForSlot(size_t index) const;

    // hook 回调需要读取最新的决策表。
    static void BindRuntime(SharedState* state, SharedStateStore* store);

    struct Slot {
        const HookTargetDefinition* definition = nullptr;
        void* target = nullptr;
        void* original = nullptr;
        bool installed = false;
        bool failed = false;
    };

private:
    bool Install(Slot& slot, SharedStateStore& store);
    void Uninstall(Slot& slot, SharedStateStore& store);

    Slot slots_[kHookTargetCount] = {};
    bool initialized_ = false;
};

} // namespace skippy

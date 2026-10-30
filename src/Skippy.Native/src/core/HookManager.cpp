#include "HookManager.h"

#include <windows.h>

#include <atomic>
#include <cstring>

#include "MinHook.h"

#include "Signature.h"

namespace skippy {
namespace {

using ContentDirectorFn = int64_t (*)(void*);
using NormalCutscenesFn = int64_t (*)(void*, void*);
using SinglePtrFn = int64_t (*)(void*);
using CutsceneSeenFunction = bool (*)(void*, uint32_t);

std::atomic<SharedState*> g_liveState{nullptr};
std::atomic<SharedStateStore*> g_store{nullptr};

HookManager::Slot* g_slots = nullptr;

void RecordHookFire(LogHookId hook, const char* category) {
    auto* store = g_store.load(std::memory_order_relaxed);
    if (store != nullptr)
        store->WriteLog(LogLevel::HookFired, hook, "过场已跳过（%s）", category);
}

void RecordExtra(const SharedState& state, LogHookId hook, bool skip) {
    auto* store = g_store.load(std::memory_order_relaxed);
    if (store == nullptr || state.ResearchExtraLogs == 0)
        return;

    store->WriteLog(LogLevel::Info, hook,
                    "hook 调用: skip=%d territory=%u intendedUse=%u workshop=%u",
                    skip ? 1 : 0, state.TerritoryId, state.IntendedUse, state.IsInWorkshop);
}

int64_t DetourContentDirector(void* luaState) {
    auto* state = g_liveState.load(std::memory_order_acquire);
    const bool skip = state != nullptr && state->ContentDirectorSkip != 0;

    if (state != nullptr)
        RecordExtra(*state, LogHookId::ContentDirector, skip);

    if (skip) {
        RecordHookFire(LogHookId::ContentDirector, "ContentDirector");
        return 1;
    }

    const auto original = reinterpret_cast<ContentDirectorFn>(g_slots[SlotContentDirector].original);
    return original != nullptr ? original(luaState) : 0;
}

int64_t DetourMassivePC(void* luaState) {
    auto* state = g_liveState.load(std::memory_order_acquire);
    const bool skip = state != nullptr && state->MassivePCSkip != 0;

    if (state != nullptr)
        RecordExtra(*state, LogHookId::MassivePC, skip);

    if (skip) {
        RecordHookFire(LogHookId::MassivePC, "MassivePC");
        return 1;
    }

    const auto original = reinterpret_cast<SinglePtrFn>(g_slots[SlotMassivePC].original);
    return original != nullptr ? original(luaState) : 0;
}

int64_t DetourCustomTalk(void* luaState) {
    auto* state = g_liveState.load(std::memory_order_acquire);
    const bool skip = state != nullptr && state->CustomTalkSkip != 0;

    if (state != nullptr)
        RecordExtra(*state, LogHookId::CustomTalk, skip);

    if (skip) {
        RecordHookFire(LogHookId::CustomTalk, "CustomTalk");
        return 1;
    }

    const auto original = reinterpret_cast<SinglePtrFn>(g_slots[SlotCustomTalk].original);
    return original != nullptr ? original(luaState) : 0;
}

int64_t DetourNormalCutscenes(void* luaState1, void* luaState2) {
    auto* state = g_liveState.load(std::memory_order_acquire);
    const bool skip = state != nullptr && state->NormalCutscenesSkip != 0;

    if (state != nullptr)
        RecordExtra(*state, LogHookId::NormalCutscenes, skip);

    if (skip) {
        RecordHookFire(LogHookId::NormalCutscenes, "NormalCutscenes");
        return 1;
    }

    const auto original = reinterpret_cast<NormalCutscenesFn>(g_slots[SlotNormalCutscenes].original);
    return original != nullptr ? original(luaState1, luaState2) : 0;
}

int64_t DetourInn(void* luaState) {
    auto* state = g_liveState.load(std::memory_order_acquire);
    const bool skip = state != nullptr && state->InnSkip != 0;

    if (state != nullptr)
        RecordExtra(*state, LogHookId::Inn, skip);

    if (skip) {
        RecordHookFire(LogHookId::Inn, "Inn");
        return 1;
    }

    const auto original = reinterpret_cast<SinglePtrFn>(g_slots[SlotInn].original);
    return original != nullptr ? original(luaState) : 0;
}

int64_t DetourFeedBuddy(void* luaState) {
    auto* state = g_liveState.load(std::memory_order_acquire);
    const bool skip = state != nullptr && state->FeedBuddySkip != 0;

    if (state != nullptr)
        RecordExtra(*state, LogHookId::FeedBuddy, skip);

    if (skip) {
        RecordHookFire(LogHookId::FeedBuddy, "FeedBuddy");
        return 1;
    }

    const auto original = reinterpret_cast<SinglePtrFn>(g_slots[SlotFeedBuddy].original);
    return original != nullptr ? original(luaState) : 0;
}

bool DetourCutsceneSeen(void* uiState, uint32_t cutsceneId) {
    auto* state = g_liveState.load(std::memory_order_acquire);
    const bool skip = state != nullptr && state->CutsceneSeenSkip != 0;

    if (state != nullptr)
        RecordExtra(*state, LogHookId::CutsceneSeen, skip);

    if (skip) {
        RecordHookFire(LogHookId::CutsceneSeen, "IsCutsceneSeen");
        return true;
    }

    const auto original = reinterpret_cast<CutsceneSeenFunction>(g_slots[SlotCutsceneSeen].original);
    return original != nullptr ? original(uiState, cutsceneId) : false;
}

int64_t DetourGrandCompanyRankUp(void* luaState) {
    auto* state = g_liveState.load(std::memory_order_acquire);
    if (state != nullptr)
        RecordExtra(*state, LogHookId::GrandCompanyRankUp, true);

    RecordHookFire(LogHookId::GrandCompanyRankUp, "Research/GC");
    return 1;
}

int64_t DetourHairMake(void* luaState) {
    auto* state = g_liveState.load(std::memory_order_acquire);
    if (state != nullptr)
        RecordExtra(*state, LogHookId::HairMake, true);

    RecordHookFire(LogHookId::HairMake, "Research/Aesthetician");
    return 1;
}

void* DetourForSlot(size_t index) {
    switch (index) {
    case SlotContentDirector:
        return reinterpret_cast<void*>(&DetourContentDirector);
    case SlotMassivePC:
        return reinterpret_cast<void*>(&DetourMassivePC);
    case SlotCustomTalk:
        return reinterpret_cast<void*>(&DetourCustomTalk);
    case SlotNormalCutscenes:
        return reinterpret_cast<void*>(&DetourNormalCutscenes);
    case SlotInn:
        return reinterpret_cast<void*>(&DetourInn);
    case SlotFeedBuddy:
        return reinterpret_cast<void*>(&DetourFeedBuddy);
    case SlotCutsceneSeen:
        return reinterpret_cast<void*>(&DetourCutsceneSeen);
    case SlotGrandCompanyRankUp:
        return reinterpret_cast<void*>(&DetourGrandCompanyRankUp);
    case SlotHairMake:
        return reinterpret_cast<void*>(&DetourHairMake);
    default:
        return nullptr;
    }
}

uint32_t EnabledForSlot(const SharedState& state, size_t index) {
    switch (index) {
    case SlotContentDirector:
        return state.ContentDirectorEnabled;
    case SlotMassivePC:
        return state.MassivePCEnabled;
    case SlotCustomTalk:
        return state.CustomTalkEnabled;
    case SlotNormalCutscenes:
        return state.NormalCutscenesEnabled;
    case SlotInn:
        return state.InnEnabled;
    case SlotFeedBuddy:
        return state.FeedBuddyEnabled;
    case SlotCutsceneSeen:
        return state.CutsceneSeenEnabled;
    case SlotGrandCompanyRankUp:
        return state.GrandCompanyRankUpEnabled;
    case SlotHairMake:
        return state.HairMakeEnabled;
    default:
        return 0;
    }
}

const char* StatusName(MH_STATUS status) {
    switch (status) {
    case MH_OK:
        return "MH_OK";
    case MH_ERROR_ALREADY_INITIALIZED:
        return "already initialized";
    case MH_ERROR_NOT_INITIALIZED:
        return "not initialized";
    case MH_ERROR_ALREADY_CREATED:
        return "already created";
    case MH_ERROR_NOT_CREATED:
        return "not created";
    case MH_ERROR_ENABLED:
        return "already enabled";
    case MH_ERROR_DISABLED:
        return "already disabled";
    case MH_ERROR_NOT_EXECUTABLE:
        return "target not executable";
    case MH_ERROR_UNSUPPORTED_FUNCTION:
        return "unsupported function";
    case MH_ERROR_MEMORY_ALLOC:
        return "memory allocation failed";
    case MH_ERROR_MEMORY_PROTECT:
        return "memory protect failed";
    case MH_ERROR_MODULE_NOT_FOUND:
        return "module not found";
    case MH_ERROR_FUNCTION_NOT_FOUND:
        return "function not found";
    default:
        return "unknown";
    }
}

} // namespace

void HookManager::BindRuntime(SharedState* state, SharedStateStore* store) {
    g_liveState.store(state, std::memory_order_release);
    g_store.store(store, std::memory_order_release);
}

HookManager::~HookManager() {
    this->Shutdown();
}

bool HookManager::Initialize() {
    if (this->initialized_)
        return true;

    g_slots = this->slots_;
    for (size_t i = 0; i < kHookTargetCount; ++i) {
        this->slots_[i].definition = &kHookTargets[i];
    }

    const MH_STATUS status = MH_Initialize();
    if (status != MH_OK && status != MH_ERROR_ALREADY_INITIALIZED)
        return false;

    this->initialized_ = true;
    return true;
}

void HookManager::Shutdown() {
    if (!this->initialized_)
        return;

    auto* store = g_store.load(std::memory_order_relaxed);

    for (auto& slot : this->slots_) {
        if (!slot.installed)
            continue;

        MH_DisableHook(slot.target);
        MH_RemoveHook(slot.target);
        slot.installed = false;
        slot.original = nullptr;

        if (store != nullptr)
            store->WriteLog(LogLevel::Info, LogHookId::None, "已移除 hook: %s", slot.definition->name);
    }

    MH_Uninitialize();
    this->initialized_ = false;
    g_slots = nullptr;
    BindRuntime(nullptr, nullptr);
}

uint32_t HookManager::ResolveTargets(const ModuleInfo& module, GameProbe& probe) {
    uint32_t mask = 0;

    for (size_t i = 0; i < kHookTargetCount; ++i) {
        auto& slot = this->slots_[i];
        if (slot.definition == nullptr)
            slot.definition = &kHookTargets[i];

        if (i == SlotCutsceneSeen) {
            slot.target = reinterpret_cast<void*>(probe.CutsceneSeen());
            if (slot.target != nullptr)
                mask |= slot.definition->signatureBit;
            continue;
        }

        const Signature signature = Signature::Parse(slot.definition->signature);
        if (!signature.Valid())
            continue;

        auto* match = signature.Scan(module);
        if (match == nullptr)
            continue;

        slot.target = match;
        mask |= slot.definition->signatureBit;
    }

    return mask;
}

void HookManager::Sync(const SharedState& state, SharedStateStore& store) {
    if (!this->initialized_)
        return;

    for (size_t i = 0; i < kHookTargetCount; ++i) {
        auto& slot = this->slots_[i];
        if (slot.target == nullptr)
            continue;

        const bool desired = EnabledForSlot(state, i) != 0;

        if (desired && !slot.installed)
            this->Install(slot, store);
        else if (!desired && slot.installed)
            this->Uninstall(slot, store);
    }

    uint32_t mask = 0;
    for (size_t i = 0; i < kHookTargetCount; ++i) {
        if (this->slots_[i].installed)
            mask |= this->slots_[i].definition->hookBit;
    }

    store.State()->HookMask = mask;
}

bool HookManager::Install(Slot& slot, SharedStateStore& store) {
    if (slot.failed || slot.target == nullptr)
        return false;

    void* detour = DetourForSlot(static_cast<size_t>(&slot - this->slots_));
    if (detour == nullptr)
        return false;

    MH_STATUS status = MH_CreateHook(slot.target, detour, &slot.original);
    if (status != MH_OK) {
        slot.failed = true;
        store.WriteLog(LogLevel::Warning, LogHookId::None, "创建 hook 失败: %s (%s)", slot.definition->name,
                       StatusName(status));
        store.State()->LastError = static_cast<int32_t>(NativeError::HookInstallFailed);
        return false;
    }

    status = MH_EnableHook(slot.target);
    if (status != MH_OK && status != MH_ERROR_ENABLED) {
        MH_RemoveHook(slot.target);
        slot.original = nullptr;
        slot.failed = true;
        store.WriteLog(LogLevel::Warning, LogHookId::None, "启用 hook 失败: %s (%s)", slot.definition->name,
                       StatusName(status));
        store.State()->LastError = static_cast<int32_t>(NativeError::HookInstallFailed);
        return false;
    }

    slot.installed = true;
    slot.failed = false;
    store.WriteLog(LogLevel::Info, LogHookId::None, "已安装 hook: %s @ %p", slot.definition->name, slot.target);
    return true;
}

void HookManager::Uninstall(Slot& slot, SharedStateStore& store) {
    if (!slot.installed)
        return;

    MH_DisableHook(slot.target);
    MH_RemoveHook(slot.target);

    slot.installed = false;
    slot.original = nullptr;
    slot.failed = false;

    store.WriteLog(LogLevel::Info, LogHookId::None, "已停用 hook: %s", slot.definition->name);
}

void HookManager::RemoveAll(SharedStateStore& store) {
    for (auto& slot : this->slots_) {
        if (slot.installed)
            this->Uninstall(slot, store);
    }
}

uint32_t HookManager::InstalledMask() const {
    uint32_t mask = 0;
    for (const auto& slot : this->slots_) {
        if (slot.installed && slot.definition != nullptr)
            mask |= slot.definition->hookBit;
    }

    return mask;
}

void* HookManager::TargetForSlot(size_t index) const {
    if (index >= kHookTargetCount)
        return nullptr;

    return this->slots_[index].target;
}

} // namespace skippy



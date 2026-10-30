#include "Worker.h"

#include <chrono>
#include <cstdio>
#include <new>

#include "core/GameProbe.h"
#include "core/HookManager.h"
#include "core/MemoryPatch.h"
#include "core/ModuleInfo.h"
#include "core/Signature.h"
#include "ipc/SharedState.h"
#include "ipc/SharedStateStore.h"

namespace skippy {
namespace {

// 原插件 CutsceneAddressResolver 中扫描的两处条件跳转。
constexpr const char* kCutscenePatch1Signature =
    "75 ?? 48 8B 0D ?? ?? ?? ?? BA ?? 00 00 00 48 83 C1 10 E8 ?? ?? ?? ?? 83 78 ?? ?? 74";
constexpr const char* kCutscenePatch2Signature = "74 18 8B D7 48 8D 0D";

constexpr uint64_t kHeartbeatTimeoutMs = 5000;

constexpr uint32_t kRequiredSignatures =
    SigCutscenePatch1 | SigCutscenePatch2 | SigContentDirector | SigMassivePC | SigCustomTalk |
    SigNormalCutscenes | SigInn | SigFeedBuddy | SigCutsceneSeen | SigGameMain | SigPlayerState |
    SigGroupManager;

uint64_t NowMs() {
    return static_cast<uint64_t>(
        std::chrono::duration_cast<std::chrono::milliseconds>(
            std::chrono::system_clock::now().time_since_epoch())
            .count());
}

struct SignatureLabel {
    uint32_t bit;
    const char* name;
};

constexpr SignatureLabel kSignatureLabels[] = {
    {SigCutscenePatch1, "过场补丁 #1"},
    {SigCutscenePatch2, "过场补丁 #2"},
    {SigContentDirector, "ContentDirector"},
    {SigMassivePC, "MassivePC"},
    {SigCustomTalk, "CustomTalk"},
    {SigNormalCutscenes, "NormalCutscenes"},
    {SigInn, "Inn"},
    {SigFeedBuddy, "FeedBuddy"},
    {SigCutsceneSeen, "IsCutsceneSeen"},
    {SigGrandCompanyRankUp, "GrandCompanyRankUp"},
    {SigHairMake, "HairMake"},
    {SigGameMain, "GameMain"},
    {SigPlayerState, "PlayerState"},
    {SigGroupManager, "GroupManager"},
    {SigHousingManager, "HousingManager"},
};

class Worker {
public:
    void Run();

private:
    bool InitializeRuntime();
    void CleanupRuntime();
    void Tick();

    SharedStateStore store_;
    GameProbe probe_;
    HookManager hooks_;
    MemoryPatch patch1_;
    MemoryPatch patch2_;

    bool runtimeReady_ = false;
    uint32_t signatureMask_ = 0;
    uint64_t lastInitializeAttempt_ = 0;
};

void Worker::Run() {
    this->store_.WriteLog(LogLevel::Info, LogHookId::None, "Skippy.Native 已加载，等待宿主连接…");

    for (;;) {
        if (!this->store_.IsValid()) {
            if (!this->store_.Open()) {
                Sleep(500);
                continue;
            }

            this->store_.WriteLog(LogLevel::Info, LogHookId::None, "已连接宿主共享内存");
        }

        auto* state = this->store_.State();
        if (state == nullptr) {
            Sleep(500);
            continue;
        }

        // 宿主要求停止，或心跳超时（宿主已退出）：恢复游戏原始状态并待机。
        const bool shutdown = state->Shutdown != 0;
        const bool hostAlive = NowMs() - state->HeartbeatMs < kHeartbeatTimeoutMs;

        if (shutdown || !hostAlive) {
            if (this->runtimeReady_) {
                if (!hostAlive)
                    this->store_.WriteLog(LogLevel::Warning, LogHookId::None, "宿主心跳丢失，正在恢复游戏原始状态");

                this->CleanupRuntime();
            }

            Sleep(250);
            continue;
        }

        if (!this->runtimeReady_) {
            const auto now = NowMs();
            if (now - this->lastInitializeAttempt_ < 1000) {
                Sleep(100);
                continue;
            }

            this->lastInitializeAttempt_ = now;
            if (!this->InitializeRuntime()) {
                Sleep(1000);
                continue;
            }
        }

        this->Tick();
        Sleep(100);
    }
}

bool Worker::InitializeRuntime() {
    if (!this->hooks_.Initialize()) {
        this->store_.WriteLog(LogLevel::Error, LogHookId::None, "MinHook 初始化失败");
        this->store_.State()->LastError = static_cast<int32_t>(NativeError::Internal);
        return false;
    }

    const auto module = QueryMainModule();
    if (!module.Valid()) {
        this->store_.WriteLog(LogLevel::Error, LogHookId::None, "无法读取游戏主模块");
        this->store_.State()->LastError = static_cast<int32_t>(NativeError::UnsupportedGameVersion);
        this->hooks_.Shutdown();
        return false;
    }

    this->signatureMask_ = this->probe_.Resolve(module);
    this->signatureMask_ |= this->hooks_.ResolveTargets(module, this->probe_);

    // 两个过场补丁都位于 ContentDirector 函数体内。
    // 同一段模式在 MassivePC 函数中也会出现，因此优先在 ContentDirector 附近定位；
    // 找不到锚点时再退回全局扫描（与原插件行为一致）。
    uint8_t* const anchor = static_cast<uint8_t*>(this->hooks_.TargetForSlot(SlotContentDirector));

    const auto locatePatch = [&](const char* pattern, MemoryPatch& patch, uint32_t bit) {
        const auto signature = Signature::Parse(pattern);
        if (!signature.Valid())
            return;

        uint8_t* match = nullptr;

        if (anchor != nullptr) {
            const uint8_t* const textStart = module.text;
            const uint8_t* const textEnd = module.text + module.textSize;
            const uint8_t* const windowStart = anchor - 0x400 > textStart ? anchor - 0x400 : textStart;
            const uint8_t* const windowEnd = anchor + 0x400 < textEnd ? anchor + 0x400 : textEnd;

            match = signature.ScanRange(module, const_cast<uint8_t*>(windowStart),
                                        const_cast<uint8_t*>(windowEnd));
        }

        if (match == nullptr)
            match = signature.Scan(module);

        if (match != nullptr && patch.Prepare(match, 2))
            this->signatureMask_ |= bit;
    };

    locatePatch(kCutscenePatch1Signature, this->patch1_, SigCutscenePatch1);
    locatePatch(kCutscenePatch2Signature, this->patch2_, SigCutscenePatch2);

    this->store_.State()->SignatureMask = this->signatureMask_;

    if ((this->signatureMask_ & kRequiredSignatures) != kRequiredSignatures) {
        const uint32_t missing = kRequiredSignatures & ~this->signatureMask_;
        char message[kLogMessageBytes] = {};
        size_t offset = 0;

        for (const auto& label : kSignatureLabels) {
            if ((missing & label.bit) == 0)
                continue;

            const int written = snprintf(message + offset, sizeof(message) - offset, "%s%s",
                                         offset == 0 ? "" : ", ", label.name);
            if (written <= 0)
                break;
            offset += static_cast<size_t>(written);
            if (offset >= sizeof(message))
                break;
        }

        this->store_.WriteLog(LogLevel::Error, LogHookId::None,
                              "关键签名缺失，Skippy 无法工作（游戏版本可能已更新）：%s", message);
        this->store_.State()->LastError = static_cast<int32_t>(NativeError::SignatureScanFailed);
        this->hooks_.Shutdown();
        return false;
    }

    HookManager::BindRuntime(this->store_.State(), &this->store_);
    this->runtimeReady_ = true;
    this->store_.State()->LastError = static_cast<int32_t>(NativeError::None);

    this->store_.WriteLog(LogLevel::Info, LogHookId::None,
                          "初始化完成：签名掩码=0x%X，hooks=%zu", this->signatureMask_, kHookTargetCount);
    return true;
}

void Worker::CleanupRuntime() {
    HookManager::BindRuntime(nullptr, nullptr);
    this->hooks_.Shutdown();
    this->patch1_.Restore();
    this->patch2_.Restore();

    if (this->store_.IsValid()) {
        auto* state = this->store_.State();
        state->PatchApplied = 0;
        state->HookMask = 0;
        state->GameReady = 0;
        state->LastError = static_cast<int32_t>(NativeError::None);
        this->store_.WriteLog(LogLevel::Info, LogHookId::None, "已恢复游戏原始状态");
    }

    this->runtimeReady_ = false;
}

void Worker::Tick() {
    auto* state = this->store_.State();
    if (state == nullptr)
        return;

    this->probe_.Update(this->store_);
    this->hooks_.Sync(*state, this->store_);

    const bool patchActive = state->MemoryPatchActive != 0;

    if (this->patch1_.IsPrepared())
        this->patch1_.Set(patchActive);
    if (this->patch2_.IsPrepared())
        this->patch2_.Set(patchActive);

    state->PatchApplied = (this->patch1_.IsApplied() && this->patch2_.IsApplied()) ? 1u : 0u;
}

} // namespace

DWORD WINAPI WorkerThreadProc(LPVOID parameter) {
    (void)parameter;

    auto* worker = new (std::nothrow) Worker();
    if (worker == nullptr)
        return 0;

    worker->Run();

    // Run() 不会正常返回；这里仅为保持内存安全。
    delete worker;
    return 0;
}

} // namespace skippy


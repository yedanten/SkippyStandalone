// 与托管侧 src/Skippy.Core/Interop/SharedStateLayout.cs 一一对应的共享内存布局。
// 任何字段改动都必须同步修改 C# 侧，并保持 static_assert 通过。
#pragma once

#include <cstddef>
#include <cstdint>

namespace skippy {

constexpr uint32_t kSharedMagic = 0x534B5059; // "SKPY"
constexpr uint32_t kSharedVersion = 1;
constexpr uint32_t kNativeVersion = 1;

constexpr uint32_t kMaxLogEntries = 64;
constexpr uint32_t kLogMessageBytes = 240;
constexpr uint32_t kLogEntrySize = 264;
constexpr uint32_t kLogBufferSize = kMaxLogEntries * kLogEntrySize;

constexpr wchar_t kMappingName[] = L"Local\\SkippyStandalone.SharedState.v1";
constexpr wchar_t kEventName[] = L"Local\\SkippyStandalone.SharedState.Updated.v1";

enum class LogLevel : uint32_t {
    Info = 0,
    Warning = 1,
    Error = 2,
    HookFired = 3,
};

enum class LogHookId : uint32_t {
    None = 0,
    ContentDirector = 1,
    MassivePC = 2,
    CustomTalk = 3,
    NormalCutscenes = 4,
    Inn = 5,
    FeedBuddy = 6,
    CutsceneSeen = 7,
    GrandCompanyRankUp = 8,
    HairMake = 9,
};

enum class NativeError : int32_t {
    None = 0,
    SharedMemoryOpenFailed = 1,
    SignatureScanFailed = 2,
    HookInstallFailed = 3,
    UnsupportedGameVersion = 4,
    Internal = 5,
};

enum SignatureBits : uint32_t {
    SigCutscenePatch1 = 1u << 0,
    SigCutscenePatch2 = 1u << 1,
    SigContentDirector = 1u << 2,
    SigMassivePC = 1u << 3,
    SigCustomTalk = 1u << 4,
    SigNormalCutscenes = 1u << 5,
    SigInn = 1u << 6,
    SigFeedBuddy = 1u << 7,
    SigCutsceneSeen = 1u << 8,
    SigGrandCompanyRankUp = 1u << 9,
    SigHairMake = 1u << 10,
    SigGameMain = 1u << 11,
    SigPlayerState = 1u << 12,
    SigGroupManager = 1u << 13,
    SigHousingManager = 1u << 14,
};

enum HookBits : uint32_t {
    HookContentDirector = 1u << 0,
    HookMassivePC = 1u << 1,
    HookCustomTalk = 1u << 2,
    HookNormalCutscenes = 1u << 3,
    HookInn = 1u << 4,
    HookFeedBuddy = 1u << 5,
    HookCutsceneSeen = 1u << 6,
    HookGrandCompanyRankUp = 1u << 7,
    HookHairMake = 1u << 8,
};

#pragma pack(push, 8)
struct SharedLogEntry {
    uint64_t TimestampMs;
    uint32_t Level;
    uint32_t HookId;
    uint32_t TerritoryId;
    uint32_t IntendedUse;
    char Message[kLogMessageBytes];
};
#pragma pack(pop)

static_assert(sizeof(SharedLogEntry) == kLogEntrySize, "log entry layout mismatch");

#pragma pack(push, 8)
struct SharedState {
    // 头部
    uint32_t Magic;
    uint32_t Version;
    uint32_t Size;
    uint32_t GameSequence;
    uint64_t HeartbeatMs;
    uint32_t Shutdown;
    uint32_t DecisionSequence;

    // 原生 -> 宿主：游戏状态
    uint32_t NativeVersion;
    uint32_t GameReady;
    uint32_t TerritoryId;
    uint32_t IntendedUse;
    uint64_t ContentId;
    uint64_t PartyMembers[8];
    uint32_t PartySize;
    uint32_t IsInWorkshop;
    uint32_t IsLoggedIn;
    uint32_t SignatureMask;
    uint32_t HookMask;
    uint32_t PatchApplied;
    int32_t LastError;
    uint32_t LogWriteSequence;
    uint32_t LastHookId;
    uint32_t LastHookTerritory;
    uint32_t LastHookIntendedUse;

    // 宿主 -> 原生：决策
    uint32_t Enabled;
    uint32_t MemoryPatchActive;
    uint32_t ContentDirectorEnabled;
    uint32_t ContentDirectorSkip;
    uint32_t MassivePCEnabled;
    uint32_t MassivePCSkip;
    uint32_t CustomTalkEnabled;
    uint32_t CustomTalkSkip;
    uint32_t NormalCutscenesEnabled;
    uint32_t NormalCutscenesSkip;
    uint32_t InnEnabled;
    uint32_t InnSkip;
    uint32_t FeedBuddyEnabled;
    uint32_t FeedBuddySkip;
    uint32_t CutsceneSeenEnabled;
    uint32_t CutsceneSeenSkip;
    uint32_t GrandCompanyRankUpEnabled;
    uint32_t GrandCompanyRankUpSkip;
    uint32_t HairMakeEnabled;
    uint32_t HairMakeSkip;
    uint32_t ResearchExtraLogs;
    uint32_t Reserved2;
    uint32_t Reserved3;

    uint8_t Logs[kLogBufferSize];
};
#pragma pack(pop)

static_assert(sizeof(SharedState) == 17152, "shared state size must match the managed side");
static_assert(offsetof(SharedState, ContentId) == 48, "field offset mismatch");
static_assert(offsetof(SharedState, PartyMembers) == 56, "field offset mismatch");
static_assert(offsetof(SharedState, PartySize) == 120, "field offset mismatch");
static_assert(offsetof(SharedState, Enabled) == 164, "field offset mismatch");
static_assert(offsetof(SharedState, Logs) == 256, "field offset mismatch");

} // namespace skippy

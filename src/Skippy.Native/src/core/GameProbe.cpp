#include "GameProbe.h"

#include <windows.h>

#include <cstring>

#include "Signature.h"

namespace skippy {
namespace {

// FFXIVClientStructs 中的静态地址签名（7.57）。
constexpr const char* kGameMainSignature =
    "48 8D 0D ?? ?? ?? ?? 0F 28 F2 48 89 44 24 ?? ??";
constexpr const char* kPlayerStateSignature =
    "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 75 06 F6 43 18 02 ?? ?? ?? ??";
constexpr const char* kGroupManagerSignature =
    "33 D2 48 8D 0D ?? ?? ?? ?? 33 DB ?? ?? ?? ?? ??";
constexpr const char* kHousingManagerSignature =
    "48 89 1D ?? ?? ?? ?? EB 07 ?? ?? ?? ?? ?? ?? ??";
constexpr const char* kCutsceneSeenSignature =
    "E8 ?? ?? ?? ?? 33 D2 0F B6 CB 3A C3 ?? ?? ?? ??";
constexpr const char* kIsInWorkshopSignature =
    "E8 ?? ?? ?? ?? 84 C0 74 0E 48 8B CB ?? ?? ?? ??";

// FFXIVClientStructs 结构体偏移。
constexpr size_t kGameMainCurrentTerritoryTypeId = 16648;
constexpr size_t kGameMainCurrentTerritoryIntendedUse = 16652;

constexpr size_t kPlayerStateIsLoaded = 0;
constexpr size_t kPlayerStateContentId = 104;

constexpr size_t kGroupManagerMainGroup = 32;
constexpr size_t kGroupPartyMembers = 0;
constexpr size_t kGroupMemberCount = 32732;
constexpr size_t kPartyMemberContentId = 1016;
constexpr size_t kPartyMemberSize = 1168;
constexpr size_t kMaxPartyMembers = 8;

// 该函数只包含基本类型，便于在 SEH 中安全包裹对游戏函数的调用。
bool CallIsInWorkshopSafe(HousingWorkshopFn function, void* instance) {
    __try {
        return function(instance);
    } __except (EXCEPTION_EXECUTE_HANDLER) {
        return false;
    }
}

} // namespace

uint32_t GameProbe::Resolve(const ModuleInfo& module) {
    uint32_t mask = 0;

    if (const auto signature = Signature::Parse(kGameMainSignature); signature.Valid()) {
        if (auto* match = signature.Scan(module); match != nullptr) {
            this->gameMain_ = FollowRelativeReference(match, 3);
            if (this->gameMain_ != nullptr)
                mask |= SigGameMain;
        }
    }

    if (const auto signature = Signature::Parse(kPlayerStateSignature); signature.Valid()) {
        if (auto* match = signature.Scan(module); match != nullptr) {
            this->playerState_ = FollowRelativeReference(match, 3);
            if (this->playerState_ != nullptr)
                mask |= SigPlayerState;
        }
    }

    if (const auto signature = Signature::Parse(kGroupManagerSignature); signature.Valid()) {
        if (auto* match = signature.Scan(module); match != nullptr) {
            this->groupManager_ = FollowRelativeReference(match, 5);
            if (this->groupManager_ != nullptr)
                mask |= SigGroupManager;
        }
    }

    if (const auto signature = Signature::Parse(kHousingManagerSignature); signature.Valid()) {
        if (auto* match = signature.Scan(module); match != nullptr) {
            this->housingManager_ = reinterpret_cast<uint8_t**>(FollowRelativeReference(match, 3));
            if (this->housingManager_ != nullptr)
                mask |= SigHousingManager;
        }
    }

    if (const auto signature = Signature::Parse(kCutsceneSeenSignature); signature.Valid()) {
        if (auto* match = signature.Scan(module); match != nullptr) {
            this->cutsceneSeen_ = reinterpret_cast<CutsceneSeenFn>(FollowRelativeReference(match, 1));
            if (this->cutsceneSeen_ != nullptr)
                mask |= SigCutsceneSeen;
        }
    }

    if (const auto signature = Signature::Parse(kIsInWorkshopSignature); signature.Valid()) {
        if (auto* match = signature.Scan(module); match != nullptr) {
            this->isInWorkshop_ = reinterpret_cast<HousingWorkshopFn>(FollowRelativeReference(match, 1));
        }
    }

    return mask;
}

void GameProbe::Update(SharedStateStore& store) {
    auto* state = store.State();
    if (state == nullptr)
        return;

    if (this->gameMain_ == nullptr || this->playerState_ == nullptr) {
        store.BeginGameUpdate();
        state->GameReady = 0;
        state->IsLoggedIn = 0;
        store.EndGameUpdate();
        return;
    }

    uint32_t territory = 0;
    uint32_t intendedUse = 0;
    uint64_t contentId = 0;
    uint32_t isLoaded = 0;
    uint32_t memberCount = 0;
    uint64_t members[kMaxPartyMembers] = {};

    __try {
        std::memcpy(&territory, this->gameMain_ + kGameMainCurrentTerritoryTypeId, sizeof(territory));
        std::memcpy(&intendedUse, this->gameMain_ + kGameMainCurrentTerritoryIntendedUse, sizeof(uint8_t));
        std::memcpy(&contentId, this->playerState_ + kPlayerStateContentId, sizeof(contentId));
        std::memcpy(&isLoaded, this->playerState_ + kPlayerStateIsLoaded, sizeof(uint8_t));

        if (this->groupManager_ != nullptr) {
            std::memcpy(&memberCount, this->groupManager_ + kGroupManagerMainGroup + kGroupMemberCount,
                        sizeof(uint8_t));

            if (memberCount > kMaxPartyMembers)
                memberCount = kMaxPartyMembers;

            for (uint32_t i = 0; i < memberCount; ++i) {
                const auto* member = this->groupManager_ + kGroupManagerMainGroup + kGroupPartyMembers +
                                     (i * kPartyMemberSize);
                std::memcpy(&members[i], member + kPartyMemberContentId, sizeof(uint64_t));
            }
        }
    } __except (EXCEPTION_EXECUTE_HANDLER) {
        store.BeginGameUpdate();
        state->GameReady = 0;
        store.EndGameUpdate();
        return;
    }

    const bool inWorkshop = this->TryReadWorkshop();

    store.BeginGameUpdate();
    state->TerritoryId = territory;
    state->IntendedUse = intendedUse;
    state->ContentId = contentId;
    state->PartySize = memberCount;
    state->IsInWorkshop = inWorkshop ? 1u : 0u;
    state->IsLoggedIn = (isLoaded != 0 && contentId != 0) ? 1u : 0u;
    for (size_t i = 0; i < kMaxPartyMembers; ++i)
        state->PartyMembers[i] = members[i];
    state->GameReady = 1;
    store.EndGameUpdate();
}

bool GameProbe::TryReadWorkshop() {
    if (this->housingManager_ == nullptr || this->isInWorkshop_ == nullptr)
        return false;

    void* instance = nullptr;
    __try {
        instance = *this->housingManager_;
    } __except (EXCEPTION_EXECUTE_HANDLER) {
        return false;
    }

    if (instance == nullptr)
        return false;

    return CallIsInWorkshopSafe(this->isInWorkshop_, instance);
}

} // namespace skippy

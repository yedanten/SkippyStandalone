#pragma once

#include <cstdint>

#include "../ipc/SharedStateStore.h"
#include "ModuleInfo.h"

namespace skippy {

using CutsceneSeenFn = bool (*)(void* uiState, uint32_t cutsceneId);
using HousingWorkshopFn = bool (*)(void* housingManager);

// 解析并轮询游戏内存中的地域/玩家/队伍状态。
// 偏移量取自当前客户端对应的 FFXIVClientStructs（7.57 / 2026.10.06）。
class GameProbe {
public:
    uint32_t Resolve(const ModuleInfo& module);

    bool IsReady() const { return this->gameMain_ != nullptr && this->playerState_ != nullptr; }

    void Update(SharedStateStore& store);

    CutsceneSeenFn CutsceneSeen() const { return this->cutsceneSeen_; }

    bool ResolvedHousing() const { return this->housingManager_ != nullptr && this->isInWorkshop_ != nullptr; }

private:
    bool TryReadWorkshop();

    uint8_t* gameMain_ = nullptr;
    uint8_t* playerState_ = nullptr;
    uint8_t* groupManager_ = nullptr;
    uint8_t** housingManager_ = nullptr;
    CutsceneSeenFn cutsceneSeen_ = nullptr;
    HousingWorkshopFn isInWorkshop_ = nullptr;
};

} // namespace skippy

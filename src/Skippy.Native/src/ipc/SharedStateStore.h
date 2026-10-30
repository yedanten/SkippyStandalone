#pragma once

#include <windows.h>

#include "SharedState.h"

namespace skippy {

// 打开宿主创建的共享内存，并提供状态/日志写入。
class SharedStateStore {
public:
    SharedStateStore() = default;
    ~SharedStateStore();

    SharedStateStore(const SharedStateStore&) = delete;
    SharedStateStore& operator=(const SharedStateStore&) = delete;

    // 宿主先启动并创建映射；这里失败时由调用方重试。
    bool Open();
    void Close();

    SharedState* State() const { return this->state_; }
    bool IsValid() const { return this->state_ != nullptr; }

    void NotifyHost();
    void BeginGameUpdate();
    void EndGameUpdate();
    void WriteLog(LogLevel level, LogHookId hook, const char* format, ...);

private:
    HANDLE mapping_ = nullptr;
    SharedState* state_ = nullptr;
    HANDLE event_ = nullptr;
    SRWLOCK logLock_ = SRWLOCK_INIT;
    uint32_t logSequence_ = 0;
};

} // namespace skippy

#include "SharedStateStore.h"

#include <cstdarg>
#include <cstdio>
#include <cstring>

namespace skippy {

SharedStateStore::~SharedStateStore() {
    this->Close();
}

bool SharedStateStore::Open() {
    if (this->state_ != nullptr)
        return true;

    this->mapping_ = OpenFileMappingW(FILE_MAP_READ | FILE_MAP_WRITE, FALSE, kMappingName);
    if (this->mapping_ == nullptr)
        return false;

    this->state_ = static_cast<SharedState*>(
        MapViewOfFile(this->mapping_, FILE_MAP_READ | FILE_MAP_WRITE, 0, 0, sizeof(SharedState)));

    if (this->state_ == nullptr) {
        CloseHandle(this->mapping_);
        this->mapping_ = nullptr;
        return false;
    }

    if (this->state_->Magic != kSharedMagic || this->state_->Version != kSharedVersion ||
        this->state_->Size != sizeof(SharedState)) {
        UnmapViewOfFile(this->state_);
        this->state_ = nullptr;
        CloseHandle(this->mapping_);
        this->mapping_ = nullptr;
        return false;
    }

    this->event_ = OpenEventW(EVENT_MODIFY_STATE, FALSE, kEventName);
    this->logSequence_ = this->state_->LogWriteSequence;
    this->state_->NativeVersion = kNativeVersion;
    this->state_->LastError = static_cast<int32_t>(NativeError::None);
    return true;
}

void SharedStateStore::Close() {
    if (this->state_ != nullptr) {
        this->state_->NativeVersion = 0;
        this->state_->GameReady = 0;
        UnmapViewOfFile(this->state_);
        this->state_ = nullptr;
    }

    if (this->event_ != nullptr) {
        CloseHandle(this->event_);
        this->event_ = nullptr;
    }

    if (this->mapping_ != nullptr) {
        CloseHandle(this->mapping_);
        this->mapping_ = nullptr;
    }
}

void SharedStateStore::NotifyHost() {
    if (this->event_ != nullptr)
        SetEvent(this->event_);
}

void SharedStateStore::BeginGameUpdate() {
    if (this->state_ != nullptr)
        InterlockedIncrement(reinterpret_cast<volatile LONG*>(&this->state_->GameSequence));
}

void SharedStateStore::EndGameUpdate() {
    if (this->state_ != nullptr)
        InterlockedIncrement(reinterpret_cast<volatile LONG*>(&this->state_->GameSequence));
}

void SharedStateStore::WriteLog(LogLevel level, LogHookId hook, const char* format, ...) {
    if (this->state_ == nullptr)
        return;

    char buffer[kLogMessageBytes] = {};

    va_list args;
    va_start(args, format);
    vsnprintf(buffer, sizeof(buffer), format, args);
    va_end(args);

    AcquireSRWLockExclusive(&this->logLock_);

    const uint32_t sequence = ++this->logSequence_;
    const uint32_t index = (sequence - 1) % kMaxLogEntries;
    auto* entry = reinterpret_cast<SharedLogEntry*>(this->state_->Logs + (index * kLogEntrySize));

    entry->TimestampMs = 0;
    FILETIME fileTime;
    GetSystemTimeAsFileTime(&fileTime);
    ULARGE_INTEGER large;
    large.LowPart = fileTime.dwLowDateTime;
    large.HighPart = fileTime.dwHighDateTime;
    entry->TimestampMs = (large.QuadPart / 10000ULL) - 11644473600000ULL;

    entry->Level = static_cast<uint32_t>(level);
    entry->HookId = static_cast<uint32_t>(hook);
    entry->TerritoryId = this->state_->TerritoryId;
    entry->IntendedUse = this->state_->IntendedUse;
    std::memcpy(entry->Message, buffer, sizeof(entry->Message));
    entry->Message[sizeof(entry->Message) - 1] = '\0';

    if (hook != LogHookId::None) {
        this->state_->LastHookId = static_cast<uint32_t>(hook);
        this->state_->LastHookTerritory = this->state_->TerritoryId;
        this->state_->LastHookIntendedUse = this->state_->IntendedUse;
    }

    this->state_->LogWriteSequence = sequence;

    ReleaseSRWLockExclusive(&this->logLock_);

    this->NotifyHost();
}

} // namespace skippy

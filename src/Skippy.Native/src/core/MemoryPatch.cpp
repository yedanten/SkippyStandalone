#include "MemoryPatch.h"

#include <windows.h>

#include <cstring>

namespace skippy {

bool MemoryPatch::Prepare(uint8_t* address, size_t length) {
    if (address == nullptr || length == 0 || length > sizeof(this->original_))
        return false;

    this->address_ = address;
    this->length_ = length;

    // 越界检查由调用方保证；这里直接拷贝当前指令字节作为恢复值。
    std::memcpy(this->original_, address, length);
    this->prepared_ = true;
    this->applied_ = false;
    return true;
}

bool MemoryPatch::Set(bool active) {
    if (!this->prepared_)
        return false;

    if (active == this->applied_)
        return true;

    if (active) {
        uint8_t nops[2] = {0x90, 0x90};
        if (!this->WriteBytes(nops, this->length_))
            return false;

        this->applied_ = true;
        return true;
    }

    if (!this->WriteBytes(this->original_, this->length_))
        return false;

    this->applied_ = false;
    return true;
}

void MemoryPatch::Restore() {
    if (this->prepared_ && this->applied_)
        this->Set(false);
}

bool MemoryPatch::WriteBytes(const uint8_t* bytes, size_t length) {
    DWORD oldProtect = 0;
    if (!VirtualProtect(this->address_, length, PAGE_EXECUTE_READWRITE, &oldProtect))
        return false;

    std::memcpy(this->address_, bytes, length);
    FlushInstructionCache(GetCurrentProcess(), this->address_, length);

    DWORD ignored = 0;
    VirtualProtect(this->address_, length, oldProtect, &ignored);
    return true;
}

} // namespace skippy

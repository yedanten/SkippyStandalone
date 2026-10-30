#pragma once

#include <cstddef>
#include <cstdint>

namespace skippy {

// 对游戏 .text 中一小段指令做 NOP 补丁，并在关闭时精确恢复原始字节。
// 原插件在启用时写入 0x9090、关闭时写回硬编码值；这里改为保存原始字节，更稳。
class MemoryPatch {
public:
    bool Prepare(uint8_t* address, size_t length = 2);
    bool Set(bool active);
    void Restore();

    bool IsPrepared() const { return this->prepared_; }
    bool IsApplied() const { return this->applied_; }
    uint8_t* Address() const { return this->address_; }

private:
    bool WriteBytes(const uint8_t* bytes, size_t length);

    uint8_t* address_ = nullptr;
    uint8_t original_[2] = {};
    size_t length_ = 0;
    bool prepared_ = false;
    bool applied_ = false;
};

} // namespace skippy

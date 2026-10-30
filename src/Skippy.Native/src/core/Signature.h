#pragma once

#include <cstdint>
#include <string>
#include <string_view>
#include <vector>

#include "ModuleInfo.h"

namespace skippy {

// 支持 "48 89 5C 24 ?? ..." 形式的字节模式。
class Signature {
public:
    static Signature Parse(std::string_view pattern);

    bool Valid() const { return !this->bytes_.empty(); }
    size_t Size() const { return this->bytes_.size(); }

    // 在模块 .text 段中查找；start 用于跳过已命中的位置继续搜索。
    uint8_t* Scan(const ModuleInfo& module, uint8_t* start = nullptr) const;

    // 只在 [begin, end) 范围内查找，用于把补丁定位锁定在已知锚点附近。
    uint8_t* ScanRange(const ModuleInfo& module, uint8_t* begin, uint8_t* end) const;

private:
    bool Matches(const uint8_t* address) const;

    std::vector<uint8_t> bytes_;
    std::vector<uint8_t> wildcards_;
};

// 解析 RIP 相对引用：target = address + offset + 4 + *(int32_t*)(address + offset)。
// 对应 FFXIVClientStructs 的 RelativeFollowOffsets 语义。
uint8_t* FollowRelativeReference(uint8_t* address, uint16_t offset);

} // namespace skippy

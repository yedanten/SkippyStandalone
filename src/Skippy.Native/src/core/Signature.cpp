#include "Signature.h"

#include <charconv>
#include <cstring>

namespace skippy {

Signature Signature::Parse(std::string_view pattern) {
    Signature signature;

    size_t position = 0;
    while (position < pattern.size()) {
        while (position < pattern.size() && pattern[position] == ' ')
            ++position;

        if (position >= pattern.size())
            break;

        const auto tokenEnd = pattern.find(' ', position);
        const auto token = pattern.substr(
            position,
            tokenEnd == std::string_view::npos ? pattern.size() - position : tokenEnd - position);

        if (token.size() == 2 && token[0] == '?' && token[1] == '?') {
            signature.bytes_.push_back(0);
            signature.wildcards_.push_back(1);
        } else if (token.size() == 2) {
            uint8_t value = 0;
            const auto result = std::from_chars(token.data(), token.data() + token.size(), value, 16);
            if (result.ec != std::errc())
                return {};

            signature.bytes_.push_back(value);
            signature.wildcards_.push_back(0);
        } else {
            return {};
        }

        position = tokenEnd == std::string_view::npos ? pattern.size() : tokenEnd;
    }

    return signature;
}

bool Signature::Matches(const uint8_t* address) const {
    for (size_t i = 0; i < this->bytes_.size(); ++i) {
        if (this->wildcards_[i] != 0)
            continue;

        if (address[i] != this->bytes_[i])
            return false;
    }

    return true;
}

uint8_t* Signature::Scan(const ModuleInfo& module, uint8_t* start) const {
    if (!module.Valid() || this->bytes_.empty() || this->bytes_.size() > module.textSize)
        return nullptr;

    uint8_t* begin = module.text;
    if (start != nullptr && start > begin)
        begin = start;

    uint8_t* const end = module.text + module.textSize - this->bytes_.size();

    if (this->wildcards_[0] != 0) {
        for (uint8_t* p = begin; p <= end; ++p) {
            if (this->Matches(p))
                return p;
        }

        return nullptr;
    }

    // 首字节已知时先按首字节过滤，避免对每个位置都做完整比较。
    const uint8_t firstByte = this->bytes_[0];
    for (uint8_t* p = begin; p <= end; ++p) {
        if (*p != firstByte)
            continue;

        if (this->Matches(p))
            return p;
    }

    return nullptr;
}

uint8_t* Signature::ScanRange(const ModuleInfo& module, uint8_t* begin, uint8_t* end) const {
    if (!module.Valid() || this->bytes_.empty())
        return nullptr;

    const uint8_t* textEnd = module.text + module.textSize;
    if (begin == nullptr || begin < module.text)
        begin = module.text;
    if (end == nullptr || end > textEnd)
        end = const_cast<uint8_t*>(textEnd);

    if (begin >= end || this->bytes_.size() > static_cast<size_t>(end - begin))
        return nullptr;

    uint8_t* const last = end - this->bytes_.size();
    for (uint8_t* p = begin; p <= last; ++p) {
        if (this->Matches(p))
            return p;
    }

    return nullptr;
}

uint8_t* FollowRelativeReference(uint8_t* address, uint16_t offset) {
    if (address == nullptr)
        return nullptr;

    int32_t relative = 0;
    std::memcpy(&relative, address + offset, sizeof(relative));

    return address + offset + 4 + relative;
}

} // namespace skippy

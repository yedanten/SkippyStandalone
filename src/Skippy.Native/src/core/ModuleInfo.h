#pragma once

#include <cstddef>
#include <cstdint>

namespace skippy {

struct ModuleInfo {
    uint8_t* base = nullptr;
    size_t size = 0;
    uint8_t* text = nullptr;
    size_t textSize = 0;

    bool Valid() const { return this->base != nullptr && this->text != nullptr && this->textSize != 0; }
};

// 返回游戏主模块（ffxiv_dx11.exe）的基址与 .text 段范围。
ModuleInfo QueryMainModule();

} // namespace skippy

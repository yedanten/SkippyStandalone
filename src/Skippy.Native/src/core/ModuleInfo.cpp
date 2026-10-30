#include "ModuleInfo.h"

#include <windows.h>

#include <cstring>

namespace skippy {

ModuleInfo QueryMainModule() {
    ModuleInfo info;

    const HMODULE module = GetModuleHandleW(nullptr);
    if (module == nullptr)
        return info;

    info.base = reinterpret_cast<uint8_t*>(module);

    const auto* dosHeader = reinterpret_cast<const IMAGE_DOS_HEADER*>(info.base);
    if (dosHeader->e_magic != IMAGE_DOS_SIGNATURE)
        return {};
    if (dosHeader->e_lfanew <= 0)
        return {};

    const auto* ntHeaders = reinterpret_cast<const IMAGE_NT_HEADERS*>(info.base + dosHeader->e_lfanew);
    if (ntHeaders->Signature != IMAGE_NT_SIGNATURE)
        return {};

    info.size = ntHeaders->OptionalHeader.SizeOfImage;

    const IMAGE_SECTION_HEADER* section = IMAGE_FIRST_SECTION(ntHeaders);
    for (WORD i = 0; i < ntHeaders->FileHeader.NumberOfSections; ++i, ++section) {
        if ((section->Characteristics & IMAGE_SCN_MEM_EXECUTE) == 0)
            continue;

        // .text 段优先；如果没有 .text，则退化为第一个可执行段。
        const bool isText = std::memcmp(section->Name, ".text", 5) == 0;
        if (isText || info.text == nullptr) {
            info.text = info.base + section->VirtualAddress;
            info.textSize = section->Misc.VirtualSize;
        }

        if (isText)
            break;
    }

    return info;
}

} // namespace skippy

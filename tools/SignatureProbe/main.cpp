// 离线签名验证工具：在没有运行游戏的情况下检查 Skippy 依赖的所有字节签名
// 是否仍能在当前 ffxiv_dx11.exe 中命中。
#include <windows.h>

#include <cstdio>
#include <cstring>
#include <string>
#include <vector>

#include "core/HookTargets.h"
#include "core/Signature.h"

using namespace skippy;

namespace {

struct Image {
    std::vector<uint8_t> bytes;
    ModuleInfo module;
};

bool LoadImage(const std::wstring& path, Image& image) {
    const HANDLE file = CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, 0, nullptr);
    if (file == INVALID_HANDLE_VALUE) {
        printf("Cannot open file\n");
        return false;
    }

    LARGE_INTEGER size{};
    GetFileSizeEx(file, &size);

    image.bytes.resize(static_cast<size_t>(size.QuadPart));
    DWORD read = 0;
    const BOOL ok = ReadFile(file, image.bytes.data(), static_cast<DWORD>(image.bytes.size()), &read, nullptr);
    CloseHandle(file);

    if (!ok || static_cast<size_t>(read) != image.bytes.size()) {
        printf("Cannot read file\n");
        return false;
    }

    auto* base = image.bytes.data();
    const auto* dos = reinterpret_cast<const IMAGE_DOS_HEADER*>(base);
    if (dos->e_magic != IMAGE_DOS_SIGNATURE) {
        printf("Not a PE file\n");
        return false;
    }

    const auto* nt = reinterpret_cast<const IMAGE_NT_HEADERS*>(base + dos->e_lfanew);
    if (nt->Signature != IMAGE_NT_SIGNATURE) {
        printf("Not a PE file\n");
        return false;
    }

    image.module.base = base;
    image.module.size = nt->OptionalHeader.SizeOfImage;

    const IMAGE_SECTION_HEADER* section = IMAGE_FIRST_SECTION(nt);
    for (WORD i = 0; i < nt->FileHeader.NumberOfSections; ++i, ++section) {
        if (std::memcmp(section->Name, ".text", 5) != 0)
            continue;

        image.module.text = base + section->PointerToRawData;
        image.module.textSize = section->SizeOfRawData;
        break;
    }

    if (!image.module.Valid()) {
        printf("No .text section\n");
        return false;
    }

    return true;
}

void ScanOne(const Image& image, const char* name, const char* pattern, bool verbose) {
    const Signature signature = Signature::Parse(pattern);
    if (!signature.Valid()) {
        printf("[FAIL ] %-32s pattern parse error\n", name);
        return;
    }

    std::vector<size_t> hits;
    uint8_t* cursor = nullptr;

    while (hits.size() < 16) {
        uint8_t* found = signature.Scan(image.module, cursor);
        if (found == nullptr)
            break;

        hits.push_back(static_cast<size_t>(found - image.module.base));
        cursor = found + 1;
    }

    if (hits.empty()) {
        printf("[FAIL ] %-32s not found\n", name);
        return;
    }

    printf("[%-5s] %-32s first=0x%zX hits=%zu", hits.size() == 1 ? "OK" : "MULTI", name, hits[0], hits.size());

    if (hits.size() > 1 || verbose) {
        printf("  [");
        for (size_t i = 0; i < hits.size(); ++i)
            printf("%s0x%zX", i == 0 ? "" : ", ", hits[i]);
        printf("]");
    }

    printf("\n");
}

} // namespace

int wmain(int argc, wchar_t** argv) {
    if (argc < 2) {
        printf("Usage: SignatureProbe <ffxiv_dx11.exe> [--verbose]\n");
        return 2;
    }

    const bool verbose = argc > 2 && std::wcscmp(argv[2], L"--verbose") == 0;

    Image image;
    if (!LoadImage(argv[1], image))
        return 2;

    ScanOne(image, "CutscenePatch1", "75 ?? 48 8B 0D ?? ?? ?? ?? BA ?? 00 00 00 48 83 C1 10 E8 ?? ?? ?? ?? 83 78 ?? ?? 74", verbose);
    ScanOne(image, "CutscenePatch2", "74 18 8B D7 48 8D 0D", verbose);

    ScanOne(image, "GameMain.Instance", "48 8D 0D ?? ?? ?? ?? 0F 28 F2 48 89 44 24 ?? ??", verbose);
    ScanOne(image, "PlayerState.Instance", "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 75 06 F6 43 18 02 ?? ?? ?? ??", verbose);
    ScanOne(image, "GroupManager.Instance", "33 D2 48 8D 0D ?? ?? ?? ?? 33 DB ?? ?? ?? ?? ??", verbose);
    ScanOne(image, "HousingManager.Instance", "48 89 1D ?? ?? ?? ?? EB 07 ?? ?? ?? ?? ?? ?? ??", verbose);
    ScanOne(image, "UIState.IsCutsceneSeen", "E8 ?? ?? ?? ?? 33 D2 0F B6 CB 3A C3 ?? ?? ?? ??", verbose);
    ScanOne(image, "HousingManager.IsInWorkshop", "E8 ?? ?? ?? ?? 84 C0 74 0E 48 8B CB ?? ?? ?? ??", verbose);

    for (size_t i = 0; i < kHookTargetCount; ++i) {
        if (kHookTargets[i].signature[0] == '\0')
            continue;

        ScanOne(image, kHookTargets[i].name, kHookTargets[i].signature, verbose);
    }

    printf("\n.text size = 0x%zX\n", image.module.textSize);
    return 0;
}

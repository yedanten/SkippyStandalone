# Skippy Standalone

**English** | [简体中文](README.zh-CN.md)

[![Release](https://img.shields.io/github/v/release/yedanten/SkippyStandalone)](https://github.com/yedanten/SkippyStandalone/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-informational)

A standalone Windows port of the Dalamud plugin **Skippy 2.2.5.0** — no Dalamud, no plugin loader, no .NET runtime required.

Skippy skips the otherwise unskippable cutscenes in FFXIV (Main Scenario Roulette and many other categories). This port keeps the original plugin's behaviour while moving it into a normal desktop application: the app detects `ffxiv_dx11.exe`, injects a small native component that applies the same two instruction patches and all inline hooks, and restores the original game memory when it exits.

> The port targets the Chinese FFXIV client **7.57 (2026.10.06)**. Other client versions require re-validating the signatures with the bundled `SignatureProbe` tool.

---

## Features

| Category | Description |
| --- | --- |
| **MSQ Roulette** | Castrum Meridianum, The Praetorium, Porta Decumana; per-instance exempt / skip-specific options |
| **Large-Scale Content** | Variant/Deep Dungeons and Cosmic Exploration |
| **Gold Saucer** | Chocobo Racing, Lord of Verminion, Triple Triad, Fall Guys, Air Force One, Mahjong, Keybound Brawler |
| **NPC Dialogue** | Quest NPC, housing and submarine workshop cutscenes |
| **World & Quest Cutscenes** | Overworld story scenes and scripted events |
| **Feed Buddy** | Companion feeding animation |
| **Risky Skips** | Ocean Fishing, Crystalline Conflict, Inn login sequence (disabled by default) |
| **Behaviour** | Opt-out of the first-time cutscene protection |

Additional features:

- **Auto-Party Mode** — enables the MSQ skip when entering an MSQ duty area and disables it when leaving; optional anonymous "all party members have Skippy" check.
- **Developer mode** — unlock the Hook Research page with the author's password to test each signature hook in isolation.
- **Safe defaults** — the first run enables no skip category at all and only reads/writes `%APPDATA%\SkippyStandalone\config.json`. Dalamud plugin configuration is never imported.
- **Automatic recovery** — on host heartbeat timeout (5 s) or normal exit, the native component restores the original bytes and removes every hook.
- **Diagnostics** — one-click log export, anonymous User ID, Tester badge.

---

## Requirements

- Windows 10 / 11 x64
- FINAL FANTASY XIV (Chinese client) **7.57 / 2026.10.06**
- No .NET installation needed — the release is a self-contained single-file executable
- Same privilege level as the game (run as administrator if the game does)

---

## Download & Usage

1. Download `StandaloneApp.exe` from the [latest release](https://github.com/yedanten/SkippyStandalone/releases/latest).
2. Start the game normally (XIVLauncher, etc.).
3. Run `StandaloneApp.exe`.
4. The app injects the native component automatically; the status bar shows `已连接` (connected) and the current territory.
5. Use the character avatar in the top-left corner as the master switch, and the left navigation to enable the categories you need.
6. Close the window to exit. The native component restores the game memory within 5 seconds.

If the app is restarted while the game is still running, it reconnects to the already-loaded native component automatically.

### Configuration and data locations

| Item | Location |
| --- | --- |
| Config | `%APPDATA%\SkippyStandalone\config.json` |
| Logs | `%LOCALAPPDATA%\SkippyStandalone\logs\` |
| Native component cache | `%LOCALAPPDATA%\SkippyStandalone\runtime\<version>\Skippy.Native.dll` |

---

## Building

### Prerequisites

- .NET SDK 10
- Visual Studio 2026 (or 2022) with the **Desktop development with C++** workload (MSVC x64, Windows SDK, CMake)
- No extra NuGet sources: MinHook is vendored under `third_party/minhook`

### One command

```powershell
pwsh -File tools/build.ps1
```

The script builds `src/Skippy.Native` (x64/Release) with the Visual Studio CMake, embeds the resulting DLL into the WPF app, and publishes a self-contained single-file executable to `Release/StandaloneApp.exe`.

### Manual steps

```powershell
# 1. Native component
cmake -S src/Skippy.Native -B artifacts/native-build -G "Visual Studio 18 2026" -A x64
cmake --build artifacts/native-build --config Release
Copy-Item artifacts/native-build/Release/Skippy.Native.dll src/StandaloneApp/Assets/native/

# 2. Managed app
dotnet publish src/StandaloneApp/StandaloneApp.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -o artifacts/publish
```

### Tests and signature validation

```powershell
dotnet test tests/Skippy.Core.Tests
artifacts\native-build\Release\SignatureProbe.exe "E:\最终幻想XIV\game\ffxiv_dx11.exe"
```

`SignatureProbe` checks every signature against the current client executable without launching the game and reports the first match and number of hits.

### Regenerating the territory-name table (optional)

`src/Skippy.Core/Data/TerritoryNames.json` is exported from the game data by `tools/TerritoryDump` (requires a local Dalamud installation that provides Lumina):

```powershell
dotnet run --project tools/TerritoryDump -- --game "E:\最终幻想XIV" `
  --output src/Skippy.Core/Data/TerritoryNames.json
```

---

## Project structure

```
src/
├── Skippy.Core/            Logic and cross-process infrastructure
│   ├── Configuration/      Config model and persistence
│   ├── Skips/              Territory rules and skip decision engine (ported from the original)
│   ├── Interop/            Shared-memory layout and channel
│   ├── Game/               Process discovery, injection, native library extraction
│   ├── Services/           Auto-Party, party check, anonymous IDs
│   ├── Net/                Research / Tester / password endpoints
│   └── Data/               Built-in territory names
├── Skippy.Native/          Native component injected into ffxiv_dx11.exe (C++20)
│   ├── src/core/           Signature scanner, memory patch, game probe, MinHook wrapper
│   ├── src/ipc/            Shared-memory read/write and log ring buffer
│   └── src/Worker.cpp      Background loop: probe → sync decisions → apply patches/hooks
└── StandaloneApp/          WPF UI
    ├── Views/              Settings pages, Logger, Commands, About
    ├── Dialogs/            Warning / Developer mode / Tester dialogs
    ├── Services/           AppHost (composition root), GameMonitor (injection & logs)
    └── Themes/             Dark ImGui-inspired theme

tests/Skippy.Core.Tests/    53 unit tests
tools/
├── build.ps1               One-command build
├── SignatureProbe/         Offline signature validation
└── TerritoryDump/          Territory-name exporter
third_party/minhook/        MinHook (BSD-2-Clause, vendored)
```

### Runtime data flow

```
┌─────────────────────────┐        ┌──────────────────────────────┐
│ StandaloneApp (host)    │        │ ffxiv_dx11.exe                │
│                         │        │                              │
│  WPF UI + config        │        │  Skippy.Native (injected DLL) │
│  Skip decision engine   │◄──────►│  - signature scan / patches   │
│  Auto-Party / checks    │ state  │  - 9 game-function hooks      │
│  GameMonitor / injector │ logs   │  - game state probe           │
└─────────────────────────┘        └──────────────────────────────┘
```

The host re-evaluates the game state every 300 ms and writes the decision table into shared memory; the native component applies patches/hooks every 100 ms and restores everything when the heartbeat disappears. Hook callbacks only read booleans from shared memory — no heavy work on the game thread.

---

## Dependencies and credits

This project is a port of an existing plugin and would not exist without the following projects:

| Project | What it is used for | Link |
| --- | --- | --- |
| **Skippy** (original plugin, by Boxu) | The plugin this port is based on: features, logic, UI text, signatures and research endpoints | https://github.com/BoxuChan/Skippy |
| **Dalamud** | Plugin framework whose injection approach (`CreateRemoteThread` + `LoadLibraryW`), plugin loading model and API semantics were used as the reference | https://github.com/goatcorp/Dalamud |
| **FFXIVClientStructs** | Game structures, static addresses, function signatures and `TerritoryIntendedUse` values. This port uses the Dalamud-maintained fork | https://github.com/Dalamud-DailyRoutines/FFXIVClientStructs (upstream: https://github.com/aers/FFXIVClientStructs) |
| **MinHook** | x64 inline hooking library used by the native component (BSD-2-Clause, vendored) | https://github.com/TsudaKageyu/minhook |
| **Lumina** | Game data reader used only by the optional `tools/TerritoryDump` developer tool (Dalamud-maintained fork) | https://github.com/Dalamud-DailyRoutines/Lumina (upstream: https://github.com/NotAdam/Lumina) |

Special thanks to **Boxu** for the original plugin.

---

## Differences from the original plugin

- **No `/skippy` chat commands** — a standalone process cannot register in-game chat commands; the equivalent actions are available in the UI (see the Commands page).
- **Auto-Party is territory-driven** — the original hooks the queue-pop event (CfPop); this port enables the skip when entering an MSQ territory and disables it on leaving, which covers the same use case.
- **MSQ and Gold Saucer share one ContentDirector hook** — the original creates two hooks on the same signature address (one of which fails); this port merges them so both categories work reliably.
- **Offline About page** — no remote README parsing.
- **Network features keep the original endpoints** — Tester badge, party check, research log and developer password still call the author's Google Apps Script endpoints. If they go away, those features degrade silently and skipping is unaffected.

## Known limitations

- Signatures and struct offsets are tied to the 7.57 / 2026.10.06 client. Run `SignatureProbe` before using a different game version.
- Only validated against the Chinese client; other regions may need new signatures.
- Injection requires the same privilege level as the game; some security software may block remote-thread injection.
- The "all party members have Skippy" check depends on the author's research backend and is disabled by default.

## Disclaimer

Like the original plugin, Skippy modifies game memory. **This may violate the game's terms of service and can lead to an account ban.** Use at your own risk; the author of this port takes no responsibility for any consequences. Test on an account you can afford to lose.

## License

This standalone port is released under the **MIT License** — see [LICENSE](LICENSE).

The original [Skippy](https://github.com/BoxuChan/Skippy) plugin is licensed under **AGPL-3.0-or-later** (Copyright Boxu); its logic, UI text and research endpoints are reused here. [Dalamud](https://github.com/goatcorp/Dalamud) is also AGPL-3.0-or-later. MinHook is BSD-2-Clause. All third-party copyrights remain with their respective authors; see the third-party notices at the end of [LICENSE](LICENSE).

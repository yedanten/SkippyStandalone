# Skippy 独立版

[English](README.md) | **简体中文**

[![Release](https://img.shields.io/github/v/release/yedanten/SkippyStandalone)](https://github.com/yedanten/SkippyStandalone/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-informational)

把 Dalamud（卫月）插件 **Skippy 2.2.5.0** 完整移植为独立 Windows 程序：不依赖卫月本体、不依赖插件加载器、不要求安装 .NET 运行时。

Skippy 用于跳过 FFXIV 中那些“不可跳过”的过场（主线随机任务等众多分类）。本移植保留原插件的全部行为，只是把它搬到普通桌面程序里：程序检测到 `ffxiv_dx11.exe` 后注入一个小型原生组件，复刻原插件的两处指令补丁与全部内联 hook；程序退出时自动恢复游戏原始内存。

> 当前适配国服客户端 **7.57（2026.10.06）**。其他客户端版本需要先用仓库自带的 `SignatureProbe` 重新验证签名。

---

## 功能

| 分类 | 说明 |
| --- | --- |
| **MSQ Roulette** | 帝国南方堡、天幕魔导城、后营门；支持按副本 Exempt / Skip Specific |
| **Large-Scale Content** | 大型内容与 Cosmic Exploration |
| **Gold Saucer** | 赛鸟、萌宠之王、九宫幻卡、糖豆人、天书奇谈、麻将、空军一号、键刃斗士 |
| **NPC Dialogue** | 任务 NPC、住房、潜艇工坊对话过场 |
| **World & Quest Cutscenes** | 野外剧情与脚本事件 |
| **Feed Buddy** | 陆行鸟喂食动画 |
| **Risky Skips** | 海钓、水晶冲突、旅馆登录序列（默认关闭） |
| **Behaviour** | 关闭首次观看保护 |

其他能力：

- **Auto-Party Mode**：进入 MSQ 副本地域时自动启用 MSQ 跳过，离开时自动关闭；可选“全队都装了 Skippy 才启用”的匿名队伍检测。
- **开发者模式**：通过作者提供的密码解锁 Hook Research 页，逐个测试签名 hook。
- **安全默认值**：首次运行不启用任何跳过类别，只读写 `%APPDATA%\SkippyStandalone\config.json`，不会导入或继承 Dalamud 插件的配置。
- **自动恢复**：宿主心跳超时（5 秒）或程序正常退出时，原生组件写回原始字节并卸载所有 hook。
- **诊断**：一键导出日志、匿名 User ID、Tester 徽章。

---

## 系统要求

- Windows 10 / 11 x64
- 最终幻想 XIV 国服客户端 **7.57 / 2026.10.06**
- 无需安装 .NET（发布版为 self-contained 单文件 EXE）
- 与游戏相同的运行权限（游戏以管理员运行时，本程序也需要管理员）

---

## 下载与使用

1. 从 [最新 Release](https://github.com/yedanten/SkippyStandalone/releases/latest) 下载 `StandaloneApp.exe`。
2. 正常启动游戏（XIVLauncher 等）。
3. 运行 `StandaloneApp.exe`。
4. 程序会自动注入原生组件；窗口底部显示 `已连接` 与当前地域。
5. 左上角角色头像 = 总开关；左侧导航切换各分类并勾选需要的项目。
6. 关闭窗口即退出。原生组件会在 5 秒内把游戏内存恢复为原始状态。

如果关闭程序后游戏仍在运行，重新打开程序会自动重连到已加载的原生组件，无需重启游戏。

### 配置文件与数据目录

| 内容 | 位置 |
| --- | --- |
| 配置 | `%APPDATA%\SkippyStandalone\config.json` |
| 日志 | `%LOCALAPPDATA%\SkippyStandalone\logs\` |
| 原生组件缓存 | `%LOCALAPPDATA%\SkippyStandalone\runtime\<版本>\Skippy.Native.dll` |

---

## 构建

### 环境

- .NET SDK 10
- Visual Studio 2026（或 2022），安装 **使用 C++ 的桌面开发** 工作负载（MSVC x64、Windows SDK、CMake）
- 不需要额外 NuGet 源：MinHook 已 vendored 到 `third_party/minhook`

### 一键构建

```powershell
pwsh -File tools/build.ps1
```

脚本会用 Visual Studio 自带 CMake 构建 `src/Skippy.Native`（x64/Release），把生成的 DLL 嵌入 WPF 程序，并发布 self-contained 单文件 EXE 到 `Release/StandaloneApp.exe`。

### 分步构建

```powershell
# 1. 原生组件
cmake -S src/Skippy.Native -B artifacts/native-build -G "Visual Studio 18 2026" -A x64
cmake --build artifacts/native-build --config Release
Copy-Item artifacts/native-build/Release/Skippy.Native.dll src/StandaloneApp/Assets/native/

# 2. 托管程序
dotnet publish src/StandaloneApp/StandaloneApp.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -o artifacts/publish
```

### 测试与签名验证

```powershell
dotnet test tests/Skippy.Core.Tests
artifacts\native-build\Release\SignatureProbe.exe "E:\最终幻想XIV\game\ffxiv_dx11.exe"
```

`SignatureProbe` 无需启动游戏即可检查所有签名是否仍能在当前客户端中命中，并输出首次命中位置与命中次数。

### 重新生成地名表（可选）

`src/Skippy.Core/Data/TerritoryNames.json` 由 `tools/TerritoryDump` 从客户端数据导出（需要本机装过 Dalamud 以提供 Lumina）：

```powershell
dotnet run --project tools/TerritoryDump -- --game "E:\最终幻想XIV" `
  --output src/Skippy.Core/Data/TerritoryNames.json
```

---

## 项目结构

```
src/
├── Skippy.Core/            逻辑与跨进程基础设施
│   ├── Configuration/      配置模型与持久化
│   ├── Skips/              地域规则与跳过决策引擎（移植自原插件）
│   ├── Interop/            共享内存布局与通道
│   ├── Game/               进程发现、注入、原生库释放
│   ├── Services/           Auto-Party、队伍检测、匿名 ID
│   ├── Net/                研究日志 / Tester / 密码端点
│   └── Data/               内置地名表
├── Skippy.Native/          注入 ffxiv_dx11.exe 的原生组件（C++20）
│   ├── src/core/           签名扫描、内存补丁、游戏状态探测、MinHook 封装
│   ├── src/ipc/            共享内存读写与日志环形缓冲
│   └── src/Worker.cpp      后台线程：探测 → 同步决策 → 应用补丁/hook
└── StandaloneApp/          WPF 界面
    ├── Views/              设置页、Logger、Commands、About
    ├── Dialogs/            警告 / 开发者模式 / Tester 弹窗
    ├── Services/           AppHost（组合根）、GameMonitor（注入与日志）
    └── Themes/             深色 ImGui 风格主题

tests/Skippy.Core.Tests/    53 个单元测试
tools/
├── build.ps1               一键构建
├── SignatureProbe/         离线签名验证
└── TerritoryDump/          地名表导出
third_party/minhook/        MinHook（BSD-2-Clause，vendored）
```

### 运行时数据流

```
┌─────────────────────────┐        ┌──────────────────────────────┐
│ StandaloneApp（宿主）    │        │ ffxiv_dx11.exe                │
│                         │        │                              │
│  WPF UI + 配置           │        │  Skippy.Native（注入的 DLL）  │
│  跳过决策引擎             │◄──────►│  - 签名扫描 / 内存补丁        │
│  Auto-Party / 队伍检测   │ 状态   │  - 9 个游戏函数 hook          │
│  GameMonitor / 注入      │ 日志   │  - 游戏状态探测               │
└─────────────────────────┘        └──────────────────────────────┘
```

宿主每 300ms 重新计算决策并写入共享内存；原生组件每 100ms 应用补丁与 hook，心跳消失时恢复一切。hook 回调只读取共享内存中的布尔值，不在游戏线程做重逻辑。

---

## 依赖项目与致谢

本项目是对现有插件的移植，没有以下项目就不会存在：

| 项目 | 用途 | 地址 |
| --- | --- | --- |
| **Skippy**（原始插件，作者 Boxu） | 本移植的基础：功能、逻辑、界面文案、签名与研究端点 | https://github.com/BoxuChan/Skippy |
| **Dalamud**（卫月） | 插件框架；本项目的注入方式（`CreateRemoteThread` + `LoadLibraryW`）、插件加载模型与 API 语义均以其为参考 | https://github.com/goatcorp/Dalamud |
| **FFXIVClientStructs** | 游戏结构体、静态地址、函数签名与 `TerritoryIntendedUse` 枚举；本项目使用 Dalamud 维护的 fork | https://github.com/Dalamud-DailyRoutines/FFXIVClientStructs （上游：https://github.com/aers/FFXIVClientStructs ） |
| **MinHook** | 原生组件使用的 x64 内联 hook 库（BSD-2-Clause，已 vendored） | https://github.com/TsudaKageyu/minhook |
| **Lumina** | 仅开发工具 `tools/TerritoryDump` 使用的游戏数据读取库（Dalamud 维护的 fork） | https://github.com/Dalamud-DailyRoutines/Lumina （上游：https://github.com/NotAdam/Lumina ） |

特别感谢 **Boxu** 制作了原插件。

---

## 与原插件的差异

- **没有 `/skippy` 聊天命令**：独立进程无法注册游戏聊天命令，等效操作都放在界面里（见 Commands 页）。
- **Auto-Party 改为地域触发**：原插件挂在队列弹出事件（CfPop）上；本移植在进入 MSQ 地域时启用、离开时关闭，覆盖同样的场景。
- **MSQ 与 Gold Saucer 合并为一个 ContentDirector hook**：原插件对同一签名地址创建了两个 hook（其中一个会失败），合并后两者都可靠生效。
- **About 页为离线内容**：不再解析远程 README。
- **网络功能保留原端点**：Tester 徽章、队伍检测、研究日志、开发者密码仍请求作者原有的 Google Apps Script 地址；端点失效时相关功能静默降级，不影响跳过。

## 已知限制

- 签名与结构体偏移绑定 7.57 / 2026.10.06 客户端。跨版本使用前请先运行 `SignatureProbe`。
- 仅验证过国服客户端；其他区域可能需要新的签名。
- 注入需要与游戏同权限；部分安全软件可能拦截远程线程注入。
- “全队都装了 Skippy”检测依赖作者的研究后端，默认关闭。

## 免责声明

与原插件一致：Skippy 通过修改游戏内存实现跳过，**可能违反游戏服务条款并带来封号风险**。使用后果由使用者自行承担；请先在小号 / 可承受损失的账号上测试。

## 许可证

本独立移植项目采用 **MIT License**，见 [LICENSE](LICENSE)。

原始插件 [Skippy](https://github.com/BoxuChan/Skippy) 采用 **AGPL-3.0-or-later**（Copyright Boxu），本移植复用了其逻辑、界面文案与研究端点。[Dalamud](https://github.com/goatcorp/Dalamud) 同样为 AGPL-3.0-or-later，MinHook 为 BSD-2-Clause。所有第三方版权归其各自作者所有，详见 [LICENSE](LICENSE) 末尾的第三方声明。

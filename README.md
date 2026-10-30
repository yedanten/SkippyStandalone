# Skippy 独立版（Standalone）

把 Dalamud（卫月）插件 **Skippy 2.2.5.0** 完整移植为不依赖卫月、不依赖插件加载器的 Windows 单文件 EXE。

程序在游戏外运行：检测到 `ffxiv_dx11.exe` 后自动注入一个原生组件，复刻原插件的两处指令补丁与全部内联 hook，并由 WPF 界面负责配置、决策与日志。关闭程序或游戏退出时，原生组件会恢复游戏原始内存并卸载 hook。

---

## 功能

与原插件一致的跳过类别：

| 类别 | 说明 |
| --- | --- |
| **MSQ Roulette** | 帝国南方堡、天幕魔导城、后营门；支持按副本 Exempt / Skip Specific |
| **Large-Scale Content** | 大型内容与 Cosmic Exploration |
| **Gold Saucer** | 赛鸟、萌宠之王、九宫幻卡、糖豆人、天书奇谈、麻将、空军一号、键刃斗士 |
| **NPC Dialogue** | 任务 NPC、住房、潜艇工坊对话过场 |
| **World & Quest Cutscenes** | 野外剧情与脚本事件 |
| **Feed Buddy** | 陆行鸟喂食动画 |
| **Risky Skips** | 海钓、水晶冲突、旅馆登录序列（默认关闭） |
| **Behaviour** | 关闭首次观看保护（`AllowCutsceneSeenGlobally`） |

其他能力：

- **Auto-Party Mode**：进入 MSQ 副本地域时自动启用 MSQ 跳过，离开后自动恢复；可选“全队都装了 Skippy 才启用”的匿名队伍检测。
- **开发者模式**：通过作者提供的密码解锁 Hook Research 页，逐个测试签名 hook。
- **配置导入**：首次启动自动读取原 Dalamud 插件留下的 `Skippy.json`。
- **日志与诊断**：一键把日志导出到桌面；匿名 User ID；Tester 徽章。
- **安全恢复**：宿主心跳超时（游戏侧 5 秒）或程序正常退出时，原生组件都会写回原始字节、移除 hook。

---

## 系统要求

- Windows 10 / 11 x64
- 最终幻想 XIV 国服客户端 **7.57（2026.10.06）**
- 无需安装 .NET 运行时（发布版为 self-contained 单文件）
- 与游戏相同的运行权限；若游戏以管理员身份运行，本程序也需要管理员权限

> 游戏版本更新后，原插件依赖的签名可能失效。届时用 `tools/SignatureProbe` 检查并更新签名即可（见下文）。

---

## 使用

1. 正常启动游戏（XIVLauncher 等）。
2. 运行 `StandaloneApp.exe`。
3. 程序检测到 `ffxiv_dx11.exe` 后自动注入原生组件；底部状态栏显示 `已连接` 与当前地域。
4. 点击左上角角色头像切换总开关；左侧导航进入各分类。
5. 关闭窗口即退出程序。若游戏仍在运行，原生组件会检测到心跳丢失并在 5 秒内恢复原始内存。

**注意**：如果关闭程序后立即重新打开，原生组件会自动重连（无需重启游戏）。

### 配置文件与数据目录

| 内容 | 位置 |
| --- | --- |
| 配置 | `%APPDATA%\SkippyStandalone\config.json` |
| 日志 | `%LOCALAPPDATA%\SkippyStandalone\logs\` |
| 原生组件缓存 | `%LOCALAPPDATA%\SkippyStandalone\runtime\<版本>\Skippy.Native.dll` |

---

## 构建

### 环境

- .NET SDK 10（`dotnet --info` 可见 `10.0.x`）
- Visual Studio 2026（或 2022）并安装 **使用 C++ 的桌面开发**（含 MSVC x64、Windows SDK、CMake）
- 无需额外 NuGet 源；第三方 MinHook 已 vendored 到 `third_party/minhook`

### 一键构建

```powershell
pwsh -File tools/build.ps1
```

脚本会：

1. 用 Visual Studio 自带 CMake 构建 `src/Skippy.Native`（x64 / Release）；
2. 把 `Skippy.Native.dll` 复制到 `src/StandaloneApp/Assets/native/` 作为嵌入资源；
3. `dotnet publish` 生成 self-contained、single-file 的 EXE；
4. 输出 `Release/StandaloneApp.exe`。

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

### 测试

```powershell
dotnet test tests/Skippy.Core.Tests        # 跳过决策引擎 / 配置 / Auto-Party / 共享内存布局
artifacts\native-build\Release\SignatureProbe.exe "E:\最终幻想XIV\game\ffxiv_dx11.exe"
```

`SignatureProbe` 会离线检查所有签名是否仍能在当前客户端中命中（不需要运行游戏），
并输出每个签名的首次命中位置与命中次数。

### 重新生成地名表（可选）

`src/Skippy.Core/Data/TerritoryNames.json` 由 `tools/TerritoryDump` 从客户端数据导出，
用于在 UI / 日志中显示地图名。需要本机装过 Dalamud（提供 Lumina）：

```powershell
dotnet run --project tools/TerritoryDump -- --game "E:\最终幻想XIV" `
  --output src/Skippy.Core/Data/TerritoryNames.json
```

---

## 项目结构

```
src/
├── Skippy.Core/            纯逻辑与跨进程基础设施
│   ├── Configuration/      配置模型、持久化、Dalamud 配置导入
│   ├── Skips/              地域规则与跳过决策引擎（逐条移植自原插件）
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

tests/Skippy.Core.Tests/    52 个单元测试
tools/
├── build.ps1               一键构建
├── SignatureProbe/         离线签名验证
└── TerritoryDump/          地名表导出
third_party/minhook/        MinHook（BSD-2-Clause，vendored）
```

### 运行时数据流

```
┌─────────────────────────┐        ┌──────────────────────────────┐
│ StandaloneApp (宿主)     │        │ ffxiv_dx11.exe                │
│                         │        │                              │
│  WPF UI + 配置           │        │  Skippy.Native (注入的 DLL)   │
│  跳过决策引擎             │◄──共享内存──►│  - 签名扫描 / 内存补丁     │
│  Auto-Party / 队伍检测   │  状态+日志 │  - MinHook 9 个游戏函数 hook │
│  GameMonitor / 注入      │  决策     │  - 游戏状态探测              │
└─────────────────────────┘        └──────────────────────────────┘
```

宿主每 300ms 读取游戏状态并重新计算决策，写入共享内存；原生组件每 100ms 应用补丁与 hook，
并在心跳超时时自动恢复。hook 回调只读取共享内存中的布尔决策，不在游戏线程做重逻辑。

---

## 与原插件的差异

- **没有 `/skippy` 聊天命令**：游戏外程序无法注册游戏聊天命令，功能映射到 GUI（见 Commands 页）。
- **Auto-Party 改为地域触发**：原插件挂在队列弹出事件（CfPop）上；独立版在进入 MSQ 地域时启用、离开时关闭，效果等价。
- **MSQ 与 Gold Saucer 合并为一个 ContentDirector hook**：原插件对同一个签名地址创建了两个 hook（其中一个会失败），独立版合并后两者都可靠生效。
- **About 页为离线静态内容**：不再解析远程 README。
- **网络功能保留原端点**：Tester、队伍检测、研究日志、开发者密码仍请求作者原有的 Google Apps Script 地址；端点失效时相关功能静默降级，不影响跳过。

## 已知限制

- 签名与结构体偏移绑定具体客户端版本（当前为 7.57 / 2026.10.06）。跨版本使用前请先运行 `SignatureProbe`。
- 仅验证过国服客户端；国际服签名可能不同。
- 注入需要与游戏同权限；部分安全软件可能拦截远程线程注入。
- “全队都装了 Skippy”检测依赖网络与研究表，默认关闭。

## 免责声明

与原插件一致：Skippy 通过修改游戏内存实现跳过，**可能违反游戏服务条款并带来封号风险**。
本项目仅供学习与研究，使用后果由使用者自行承担。请先在小号 / 无价值账号上测试。

## 许可

- 原插件 Skippy：AGPL-3.0-or-later（Copyright Boxu）
- 本移植：AGPL-3.0-or-later
- MinHook：BSD-2-Clause（`third_party/minhook/LICENSE.txt`）


# Skippy 独立版

将 FFXIV 卫月（Dalamud）插件 **Skippy 2.2.5.0** 移植为独立运行的 Windows 单文件程序。
不依赖卫月本体、插件加载器或 .NET 运行时。

## 功能

- **MSQ Roulette**：帝国南方堡 / 天幕魔导城 / 后营门过场跳过（可单独豁免或单独跳过）
- **Large-Scale Content**：大型内容与 Cosmic Exploration
- **Gold Saucer**：赛鸟、萌宠之王、九宫幻卡、糖豆人、天书奇谈、麻将、空军一号、键刃斗士
- **NPC Dialogue / World Cutscenes / Feed Buddy**：NPC 对话、野外剧情、陆行鸟喂食动画
- **Risky Skips**（默认关闭）：海钓、水晶冲突、旅馆登录序列
- **Auto-Party Mode**：进入 MSQ 副本时自动启用，离开后自动关闭
- **首次观看保护**：可选择全局关闭（Behaviour 页）
- 配置自动导入、日志一键导出、匿名 User ID、开发者研究模式

## 使用

1. 先正常启动游戏（通过 XIVLauncher 等）。
2. 运行 `StandaloneApp.exe`（无需安装，双击即可）。
3. 程序检测到 `ffxiv_dx11.exe` 后会自动注入；窗口底部显示 `已连接` 与当前地域。
4. 左上角角色头像 = 总开关；左侧导航切换各分类。
5. 关闭窗口即退出。原生组件会自动把游戏内存恢复为原始状态（最长 5 秒）。

配置文件：`%APPDATA%\SkippyStandalone\config.json`
日志目录：`%LOCALAPPDATA%\SkippyStandalone\logs\`

> 首次启动会自动导入原 Dalamud 插件的 `%APPDATA%\XIVLauncherCN\pluginConfigs\Skippy.json`。

## 系统要求

- Windows 10 / 11 x64
- 最终幻想 XIV 国服客户端 7.57（2026.10.06）
- 与游戏相同的运行权限（游戏以管理员运行时，本程序也需管理员）

## 构建方式

需要 .NET SDK 10 + Visual Studio（C++ 桌面开发工作负载，含 CMake）。

```powershell
pwsh -File tools/build.ps1
```

产物输出到 `Release/StandaloneApp.exe`。详细步骤、项目结构与架构说明见仓库根目录 `README.md`。

## 注意事项

- 本程序通过修改游戏内存实现跳过，**可能违反游戏服务条款并存在封号风险**，仅供学习研究，使用后果自负。
- 游戏版本更新后签名可能失效，需用 `tools/SignatureProbe` 重新验证。
- 网络相关功能（Tester 徽章、全队插件检测、研究日志上报）依赖原作者的在线端点，失效时不影响跳过功能。

## 许可

AGPL-3.0-or-later（沿用原插件 Skippy，Copyright Boxu）。

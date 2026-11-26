<h1 align="center">AstralParty-XingShu-Android（星枢手机版）</h1>

<h3 align="center">《吉星派对 / Astral Party》安卓端独立 Compose 纯启动器</h3>

<p align="center">
  <strong>桌面端 / 网页版主仓库：<a href="https://github.com/Breezecaresses/AstralParty-XingShu">AstralParty-XingShu</a> &nbsp;|&nbsp; QQ 群：<code>1080908767</code> &nbsp;|&nbsp; <a href="https://discord.gg/dxVS6ZF393">Discord</a></strong>
</p>

<p align="center">
  <img src="assets/xingshu.svg" width="128" alt="AstralParty-XingShu-Android" />
</p>

<p align="center">
  <a href="https://github.com/Breezecaresses/AstralParty-XingShu-Android/releases"><img src="https://img.shields.io/github/v/release/Breezecaresses/AstralParty-XingShu-Android?include_prereleases&label=android-release&color=3b82f6" alt="Android Release" /></a>
  <img src="https://img.shields.io/badge/Android-Kotlin%20%7C%20Compose%20M3-7f52ff.svg" alt="Kotlin Compose" />
  <img src="https://img.shields.io/badge/Privilege-Shizuku%20%7C%20Wireless%20ADB-10b981.svg" alt="Shizuku Wireless ADB" />
  <img src="https://img.shields.io/badge/Runtime-C%23%20%7C%20Unity%202022.3-purple.svg" alt="C# Unity" />
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-Source%20Available-blue.svg" alt="License" /></a>
</p>

<p align="center">
  <a href="https://astralparty-xingshu.lanxichaxu.cn/xingshu-hub/assets/launcher-runtime/android-releases/v0.2.4/XingShu_Android_0.2.4.apk">⚡ 国内高速直链下载 v0.2.4 APK</a> ·
  <a href="https://astralparty-xingshu.lanxichaxu.cn/xingshu-hub/assets/launcher-runtime/android-releases/v0.2.4/XingShu_Android_0.2.4.zip">国内直链整合包 ZIP</a> ·
  <a href="https://github.com/Breezecaresses/AstralParty-XingShu-Android/releases/latest">GitHub Releases</a> ·
  <a href="https://github.com/Breezecaresses/AstralParty-XingShu">🖥️ 返回电脑端主仓库</a> ·
  <a href="https://astralparty-xingshu.lanxichaxu.cn/xingshu-hub/">🌐 在线网页工坊</a> ·
  <a href="README.md">English</a> · <a href="README_CN.md">简体中文</a>
</p>

> [!IMPORTANT]
> 本仓库专为 **安卓手机版（PE）** 独立维护与高频迭代；**Windows 桌面端启动器（Tauri/Rust）** 与 **在线网页版** 请前往主仓库 **[Breezecaresses/AstralParty-XingShu](https://github.com/Breezecaresses/AstralParty-XingShu)**。

---

## 核心特性

- **纯净启动 + 独立核心框架热更新**：不侵入修改游戏签名；接入无线调试 ADB 或 Shizuku 后一键安装与校验 **`1.0.0-android-preview8.60`**，支持无损还原官方原版。
- **16 项官方功能模块 & 残留清理**：支持手牌监视、对局统计、自动社交、怪物行动可视、防御与卡牌点数、击飞预测、梅加斯伤害计算、角色状态/Buff、自定义大厅背景等 16 项模块独立开关、一键全装/全卸与旧版残留自动清理。
- **桌面端对齐的游戏自定义工作台**：手机端「自定义」页内置 **游戏主页背景**（16:9 缩略图预览、图片/视频/USM 导入、缩放/亮度/倍速/静音与伴音调节）、**卡牌/音效/角色/资源包管理跳转**、**字体与主界面调试**，以及 Material You 色板与启动器壁纸。
- **实时工坊与角色表情回应**：内置星枢模组工坊（适配 `.xspkg` 与 `modimg/` 贴图包）、星枢平台账号登录/注册、以及 36 位游戏角色共 436 枚贴图表情回应。

---

## 目录结构

```text
AstralParty-XingShu-Android/
├── AndroidInstaller/                 # Kotlin + Jetpack Compose M3 安卓启动器工程
│   ├── app/                          # UI、Shizuku/ADB 引擎、工坊与游戏自定义客户端
│   └── localadb/                     # 本机无线 / USB ADB 配对与通信库
├── XingShu-Dev/                      # 游戏内 C# Unity 运行时源码（安卓兼容性校验）
│   ├── src/XingShu.Runtime/
│   └── tools/
├── tools/                            # 安卓运行库打包、APK 构建签名与更新清单工具
├── docs/
│   ├── guides/ANDROID_PHONE_INSTALL.md
│   └── records/RELEASE_0.2.1.md
└── LICENSE
```

---

## 本地构建

- 环境要求：JDK 17+、Android SDK（`compileSdk 35` / `build-tools 35.0.0`）、Gradle 8.9、Python 3.x。

```powershell
python tools/build_android_installer.py --help
```

详细手机端安装与还原说明请参阅 [ANDROID_PHONE_INSTALL.md](docs/guides/ANDROID_PHONE_INSTALL.md)。

---

## 免责声明与许可条款

- 本仓库与主仓库共同遵循 [AstralParty-XingShu 许可与合理使用条款](LICENSE)。
- 仓库内不包含任何游戏专有 DLL、原版商业资源、签名密钥库或账号凭据。

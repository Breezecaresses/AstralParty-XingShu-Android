<h1 align="center">AstralParty-XingShu-Android</h1>

<h3 align="center">Standalone Kotlin / Jetpack Compose Pure Launcher for <em>Astral Party</em> on Android</h3>

<p align="center">
  <strong>Main Desktop &amp; Web Repository: <a href="https://github.com/Breezecaresses/AstralParty-XingShu">AstralParty-XingShu</a> &nbsp;|&nbsp; QQ Group: <code>1080908767</code> &nbsp;|&nbsp; <a href="https://discord.gg/dxVS6ZF393">Discord</a></strong>
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
  <a href="https://astralparty-xingshu.lanxichaxu.cn/xingshu-hub/assets/launcher-runtime/android-releases/v0.2.5/XingShu_Android_0.2.5.apk">⚡ EdgeOne CDN Direct APK (v0.2.5)</a> ·
  <a href="https://astralparty-xingshu.lanxichaxu.cn/xingshu-hub/assets/launcher-runtime/android-releases/v0.2.5/XingShu_Android_0.2.5.zip">Direct Bundle ZIP</a> ·
  <a href="https://github.com/Breezecaresses/AstralParty-XingShu-Android/releases/latest">GitHub Releases</a> ·
  <a href="https://github.com/Breezecaresses/AstralParty-XingShu">🖥️ Main Desktop / Web Repo</a> ·
  <a href="https://astralparty-xingshu.lanxichaxu.cn/xingshu-hub/">🌐 Web Workshop</a> ·
  <a href="README.md">English</a> · <a href="README_CN.md">简体中文</a>
</p>

> [!IMPORTANT]
> This repository is dedicated exclusively to **AstralParty-XingShu on Android** for faster mobile-specific iteration and bug fixes. For **Windows Desktop (Tauri / Rust)** and **Web Workshop**, see the main repository **[Breezecaresses/AstralParty-XingShu](https://github.com/Breezecaresses/AstralParty-XingShu)**.

---

## Highlights

- **Pure Launcher & Independent Runtime Hot-Updates**: Zero APK repackaging; installs verified core runtime (`android-preview8.60`) via Shizuku or Wireless/USB ADB with lossless restore.
- **16 Official Modules & Legacy Residue Cleaner**: Toggle or batch install/uninstall all 16 official in-game modules and clean retired legacy `.module` artifacts in one tap.
- **Game Customize Workbench**: Configure in-game **Lobby Background** (`lobbybg/xingshu_lobby_bg.json`, 16:9 media thumbnail preview, image/video/USM import, scaling/brightness/speed/audio controls), **Fonts & Home UI Debug** (`xingshu_customize_debug.json`), and Material You themes.
- **Live Workshop & Character Emote Reactions**: Built-in Android PE Mod Workshop with XingShu Platform Account login/register and 436 character emote reactions synced with Web and Desktop.

---

## Build

```powershell
python tools/build_android_installer.py --help
```

See [ANDROID_PHONE_INSTALL.md](docs/guides/ANDROID_PHONE_INSTALL.md) for device setup and troubleshooting, and [LICENSE](LICENSE) for usage terms.

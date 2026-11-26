# 星枢手机版 v0.2.1（Android Runtime `1.0.0-android-preview8.60`）

> 自 `v0.2.1` 起，星枢安卓端正式迁入独立仓库 **[AstralParty-XingShu-Android](https://github.com/Breezecaresses/AstralParty-XingShu-Android)** 维护，主仓库（桌面端 / 网页版）请见 **[AstralParty-XingShu](https://github.com/Breezecaresses/AstralParty-XingShu)**。

## 核心更新内容

1. **实时工坊接入角色表情回应与星枢平台账号**
   - 新增 `PlatformAccountClient.java` 与 `GameEmoteCatalog.kt`（36 位角色、436 枚表情），工坊列表卡片底部与详情弹窗支持直接点击表情回应胶囊或弹出角色贴图选择器切换/取消表态。
   - 支持星枢平台账号账密登录与注册，回应数据与网页端/桌面端实时同步。
2. **修复升级后官方功能模组计数异常与「全部卸载」无效**
   - 升级为 16 项合并功能模块后，自动识别并清理磁盘残留的旧版退役 `.module` 文件，修复计数显示 `24 / 16 项` 的问题；新增「发现旧版模块残留」提示条与「清理残留」快捷按钮，并支持 `"all"` 通配全删。
3. **修复安卓端游戏大厅自定义背景不生效（热更新 `android-preview8.60`）**
   - 修复 `CustomLobbyBackground.cs` 初始自动补加载逻辑、在 `VideoPlayer.Prepare()` 前预绑定 `RenderTexture` 防止 Android `MediaCodec` 解码挂起，并新增「原始路径 → `file://` URI → `temporaryCachePath` 纯 ASCII 副本」三段回退及伴音路径同步。
4. **手机版「自定义」页补齐桌面端游戏自定义工作台**
   - 打通 `IInstaller.aidl`、`InstallerEngine.java` 与 `LocalAdbInstaller.java`，支持读写目标渠道 `<filesRoot>/lobbybg/xingshu_lobby_bg.json` 与 `<filesRoot>/xingshu_customize_debug.json`。
   - 集成 **主页背景（含 16:9 缩略图预览、图片/视频/USM 导入、缩放/亮度/倍速/音量控制）**、**卡牌/音效/角色/资源包分类跳转与导入**、**字体（调试）** 与 **主界面调试（调试）**，完整保留 Material You 色板与启动器壁纸外观设置。
5. **独立仓库与自更新升级链路**
   - 安卓应用内自更新器（`ReleaseUpdates.java`）默认指向 `Breezecaresses/AstralParty-XingShu-Android`，并兼容验证旧主仓库签名清单与安卓独立更新清单。

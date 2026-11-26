# 星枢手机版 v0.2.2（国内 EdgeOne CDN + 4 路并发下载提速）

## 更新内容

1. **应用自更新优先走自有 EdgeOne CDN**
   - `ReleaseUpdates.java` 检查更新与下载安装包时优先请求官方 **EdgeOne CDN**（`https://astralparty-xingshu.lanxichaxu.cn/xingshu-hub/assets/launcher-runtime/android-releases/`），大幅降低国内网络检查超时与慢速卡顿问题，同时保留国内 GitHub 镜像与 GitHub 官方源站多级自动兜底，且全部源均严格验证 RSA 公钥签名与全量 SHA-256。
2. **APK、核心运行库与工坊 Mod 全量启用 4 路并发分片下载**
   - `PayloadDownloadManager.java` 与 `ReleaseUpdates.java` 对 ≥ 2 MB 的文件自动探测 `Accept-Ranges: bytes` 并拆分为 **4 路并发分片下载**（每片独立断点续传重试 + 写完后全量 SHA-256 校验）。
3. **国内镜像顺序优化**
   - 当需要从 GitHub 回退下载 APK 时，国内加速镜像（`edgeone.gh-proxy.org`、`gh-proxy.org`、`ghfast.top`）优先于直连 GitHub 源站尝试。
4. **工坊动态表情回应播放与项目来源跳转**
   - Android 9+ 上对动态 WebP 角色表情改用 `ImageDecoder` + `AnimatedImageDrawable` 原生循环播放，解决工坊卡片与选图栏内动态表情呈静态显示的问题；
   - 设置页新增「开源仓库与项目来源」卡片，明确说明源自 `AstralParty-XingShu` 主项目，并提供手机版独立仓库、桌面/主仓库及在线网页工坊快捷跳转按钮。

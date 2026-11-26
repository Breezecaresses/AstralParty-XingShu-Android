# 星枢手机版 v0.2.3（安卓大厅动态视频硬件转码 + 自研流畅引擎）

## 更新内容

1. **彻底突破安卓游戏客户端裁剪 `VideoPlayer` 限制，实现原生流畅动态大厅背景**
   - 经真机实测确认官方安卓端 `com.feimo.astralparty` 裁剪掉了 Unity `VideoPlayer` 全部核心播放接口；现内置 **手机端硬件视频转码器**（`MediaExtractor` + `MediaCodec` + `YuvImage` + `MediaMetadataRetriever` 自动兼容回退）与 **`.xsvid` 动态视频流引擎**（配合热更新 `1.0.0-android-preview8.62`）。
   - 在启动器「自定义 → 主页背景」选择任意 `.mp4` / `.mov` / `.webm` 视频时，自动硬件转码为手机端专享 `.xsvid` 动态背景（含原伴音并自动同步 16:9 封面预览），进游戏大厅流畅循环播放、支持倍速/亮度/音量控制与离厅自动暂停，**无需依赖 Unity `VideoPlayer`**。
2. **自有服务器 EdgeOne CDN 全端满速下载**
   - 应用检查更新、APK 下载、核心运行库与工坊 Mod 全部默认走自有服务器 EdgeOne CDN + 4 路并发分片，大幅提升国内下载体验。

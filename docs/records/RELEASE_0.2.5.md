# 星枢手机版 v0.2.5（修复渠道票据扫描/无线调试掉线重连 & QQ 趴登录渠道直绑）

## 更新内容

1. **修复扫描渠道票据失败（`connect() must be called first` 与 JSON 误解析 Bug）**
   - `LocalAdbInstaller` 与 `AppController` 新增后台连接状态探测及无感自动重连（`ensureLocalAdbReadyBlocking()`）：切去游戏后再回启动器时，若无线调试端口仍在开启状态会自动静默重连并同步，不再出现 `connect() must be called first`。
   - 修复此前 `scanChannelTokens()` 把底层返回的 JSON 结果误当原始 logcat 解析导致小米 / TapTap / B 站等全部扫不到票据的 Bug（新增 `BnSdkTokenExtractor.parseTokensOutput()`）。
   - 对于暂未配置服务端 BnSdk 签名密钥的联运渠道（如 4399 / 华为 / OPPO / vivo），扫描到票据后会明确提示进游戏使用「`趴绑定 8位码`」，避免无头绪报错。
2. **打通 QQ「趴登录」→ 手机助手深链渠道票据一键绑定**
   - 从 QQ 点击「趴登录」网页跳转到星枢启动器时，若当前有无线调试/Shizuku 权限，弹窗会自动扫描并显示已识别的渠道票据，点击即可直接完成 QQ 绑定与星枢账号登录。
3. **补全多渠道包名识别**
   - 新增 4399 / 荣耀及更多 OPPO / vivo / 魅族常见包名探测支持。
4. **服务端 `/auth/assistant.apk` 自动同步最新版**
   - QQ「趴登录」网页中的手机端下载入口现实时联动服务器 `android-releases/latest.json`（优先 302 跳转 EdgeOne CDN 最新直链，确保始终指向最新版本）。

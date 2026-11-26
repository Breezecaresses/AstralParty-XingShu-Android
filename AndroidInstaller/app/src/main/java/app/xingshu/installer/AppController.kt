package app.xingshu.installer

import android.app.Activity
import android.content.ClipData
import android.content.ClipboardManager
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.content.ServiceConnection
import android.content.SharedPreferences
import android.content.pm.PackageManager
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.net.Uri
import android.os.Build
import android.os.IBinder
import android.os.ParcelFileDescriptor
import android.os.SystemClock
import android.provider.OpenableColumns
import android.provider.Settings
import android.util.LruCache
import androidx.compose.runtime.Stable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateMapOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.asImageBitmap
import io.github.muntashirakon.adb.android.AdbMdns
import org.json.JSONObject
import rikka.shizuku.Shizuku
import java.io.ByteArrayOutputStream
import java.io.File
import java.io.FileInputStream
import java.io.FileOutputStream
import java.io.IOException
import java.io.InputStream
import java.net.Inet4Address
import java.net.InetAddress
import java.security.MessageDigest
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.concurrent.CountDownLatch
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean
import java.util.regex.Pattern
import javax.net.ssl.HttpsURLConnection

enum class NavTab(val label: String) {
    LAUNCH("启动"),
    MODS("模组"),
    WORKSHOP("工坊"),
    CUSTOMIZE("自定义"),
    SETTINGS("设置"),
}

data class ChannelEntry(
    val packageName: String,
    val label: String,
    val platform: String,
    val versionName: String,
    val hasCatalog: Boolean,
    val running: Boolean,
    val installState: String,
    val resolvedRel: String,
)

data class InstalledModEntry(
    val id: String,
    val name: String,
    val version: String,
    val sha256: String,
    val installKind: String,
    val declaredCount: Int,
    val presentCount: Int,
) {
    fun isXspkgModule(): Boolean = installKind.equals("xspkg", ignoreCase = true)
}

data class RuntimeProgressState(
    val active: Boolean = false,
    val received: Long = 0L,
    val total: Long = 0L,
    val speed: Long = 0L,
    val stage: String = "",
)

data class ModProgressState(
    val modId: String = "",
    val received: Long = 0L,
    val total: Long = 0L,
    val speed: Long = 0L,
    val stage: String = "",
)

data class ConflictPrompt(
    val packageName: String,
    val hash: String,
    val summary: String,
)

data class LobbyBgMediaEntry(
    val name: String,
    val kind: String,
    val sizeBytes: Long,
    val modifiedAt: Long,
)

data class LobbyBgConfigState(
    val enabled: Boolean = false,
    val selectedFile: String = "",
    val syncSubPanels: Boolean = false,
    val randomOnLobby: Boolean = false,
    val fillMode: String = "cover",
    val brightness: Float = 1.0f,
    val speed: Float = 1.0f,
    val muted: Boolean = true,
    val volume: Float = 0.70f,
    val muteVanillaBgm: Boolean = false,
)

data class CustomFontFileEntry(
    val name: String,
    val sizeBytes: Long,
)

data class HomeUiDebugState(
    val hideVanillaBgAnim: Boolean = false,
    val compactLobbyBadges: Boolean = false,
    val showDebugOutline: Boolean = false,
    val customHudOpacity: Int = 100,
    val hideLobbyAds: Boolean = false,
)

data class CustomizeTuningState(
    val activeCardProfile: String? = null,
    val cardProfiles: List<String> = emptyList(),
    val activeAudioProfile: String? = null,
    val audioProfiles: List<String> = emptyList(),
    val activeCharacterProfile: String? = null,
    val characterProfiles: List<String> = emptyList(),
    val selectedFontFamily: String? = null,
    val selectedFontFile: String? = null,
    val fontCjkOnly: Boolean = false,
    val fontReplaceHeadings: Boolean = true,
    val homeUiDebug: HomeUiDebugState = HomeUiDebugState(),
)

@Stable
internal class AppController(private val activity: Activity) {
    private val appContext: Context = activity.applicationContext
    private val prefs: SharedPreferences = activity.getPreferences(Context.MODE_PRIVATE)

    private val worker = Executors.newSingleThreadExecutor()
    private val workshopWorker = Executors.newCachedThreadPool()
    private val authWorker = Executors.newSingleThreadExecutor()
    private val updateWorker = Executors.newSingleThreadExecutor()
    private val thumbWorker = Executors.newFixedThreadPool(3)
    private val updateTaskActive = AtomicBoolean(false)

    private val thumbCache = object : LruCache<String, ImageBitmap>(24) {}
    private val thumbPending = mutableSetOf<String>()

    var activeTab by mutableStateOf(NavTab.LAUNCH)
    var snackbarMessage by mutableStateOf<String?>(null)

    // Transport & privilege state
    var useShizuku by mutableStateOf(false)
    var bindingShizuku by mutableStateOf(false)
    var remoteBinder by mutableStateOf<IInstaller?>(null)
    private var localAdb: LocalAdbInstaller? = null
    var adbConnected by mutableStateOf(false)
    var connectedHost by mutableStateOf("127.0.0.1")
    var discoveredConnectPort by mutableIntStateOf(-1)
    var discoveredPairingPort by mutableIntStateOf(-1)
    private var discoveredConnectHost: String? = null
    private var lastAutoTriedPort: Int = -1
    @Volatile private var connectDiscoveryLatch: CountDownLatch? = null

    var connectionPortInput by mutableStateOf("")
    var pairingPortInput by mutableStateOf("")
    var pairingCodeInput by mutableStateOf("")
    var showConnectSheet by mutableStateOf(false)

    private var connectMdns: AdbMdns? = null
    private var legacyConnectMdns: AdbMdns? = null
    private var pairingMdns: AdbMdns? = null

    // Busy / log / diagnostics
    var busy by mutableStateOf(false)
    var modTaskBusy by mutableStateOf(false)
    var batchModsBusy by mutableStateOf(false)
    var statusText by mutableStateOf("就绪：可直接启动官方原版游戏；连接授权后可按需安装官方插件或功能模组。")
    var lastResultText by mutableStateOf("")
    var logExpanded by mutableStateOf(false)
    val logs = mutableStateListOf<String>()
    var runtimeProgress by mutableStateOf(RuntimeProgressState())
    var conflictPrompt by mutableStateOf<ConflictPrompt?>(null)
    var cacheBytes by mutableLongStateOf(0L)
    var coreSpec by mutableStateOf(CoreRuntimeSpec.embedded())
    @Volatile private var lastShizukuSpecJson: String? = null

    // Game channels
    val channels = mutableStateListOf<ChannelEntry>()
    var selectedPackageName by mutableStateOf(GamePackageCatalog.DEFAULT_PACKAGE)

    // Official modular switches + installed workshop mods
    val officialModuleStates = mutableStateMapOf<String, String>()
    val legacyModuleIds = mutableStateListOf<String>()
    val installedMods = mutableStateListOf<InstalledModEntry>()
    var activeModuleCount by mutableIntStateOf(0)
    var totalModPngCount by mutableIntStateOf(0)

    // Workshop & Mods sub-tab state
    val workshopMods = mutableStateListOf<AndroidWorkshopClient.WorkshopMod>()
    var workshopLoading by mutableStateOf(false)
    var workshopError by mutableStateOf("")
    var workshopLastSyncTime by mutableStateOf("")
    @Volatile private var lastWorkshopFetchMs: Long = 0L
    var modsSubTab by mutableStateOf("official")
    var modProgress by mutableStateOf(ModProgressState())
    val thumbnails = mutableStateMapOf<String, ImageBitmap>()
    var pendingWorkshopPromptMod by mutableStateOf<AndroidWorkshopClient.WorkshopMod?>(null)
    var workshopDetailMod by mutableStateOf<AndroidWorkshopClient.WorkshopMod?>(null)
    var workshopKindFilter by mutableStateOf("all")

    // XingShu Platform Account & Emote Reactions
    var platformToken by mutableStateOf("")
    var platformUser by mutableStateOf<PlatformAccountClient.PlatformUser?>(null)
    var platformAuthBusy by mutableStateOf(false)
    var platformAuthError by mutableStateOf("")
    var showPlatformLoginDialog by mutableStateOf(false)
    val platformUserReactions = mutableStateMapOf<String, String>()
    var reactionBusyModId by mutableStateOf<String?>(null)
    var emotePickerMod by mutableStateOf<AndroidWorkshopClient.WorkshopMod?>(null)
    var pendingReactionAfterLogin by mutableStateOf<Pair<AndroidWorkshopClient.WorkshopMod, String>?>(null)
    val emoteBitmaps = mutableStateMapOf<String, ImageBitmap>()
    val emoteFiles = mutableStateMapOf<String, File>()
    private val emoteMemCache = object : LruCache<String, ImageBitmap>(120) {}
    private val emotePending = mutableSetOf<String>()

    // Appearance & Game Customize state
    var themeId by mutableStateOf(ThemePalette.DEFAULT_THEME_ID)
    var customThemeHex by mutableStateOf(ThemePalette.DEFAULT_CUSTOM_HEX)
    var brightnessMode by mutableStateOf(ThemeBrightnessMode.SYSTEM)
    var wallpaperBitmap by mutableStateOf<ImageBitmap?>(null)
    var wallpaperAlpha by mutableFloatStateOf(0.28f)
    var customizeSection by mutableStateOf("lobbybg")
    var customizeLoading by mutableStateOf(false)
    var customizeBusy by mutableStateOf(false)
    var customizeProgressText by mutableStateOf("")
    var lobbyBgDir by mutableStateOf("")
    var lobbyBgConfig by mutableStateOf(LobbyBgConfigState())
    val lobbyBgFiles = mutableStateListOf<LobbyBgMediaEntry>()
    val lobbyBgPreviews = mutableStateMapOf<String, ImageBitmap>()
    var customizeTuning by mutableStateOf(CustomizeTuningState())
    val customFontFiles = mutableStateListOf<CustomFontFileEntry>()
    var fontsDir by mutableStateOf("")

    // Auth / Cloud session
    var boundSession by mutableStateOf<MobileAuthHub.BoundAccount?>(null)
    var cloudSummary by mutableStateOf("")
    var authBusy by mutableStateOf(false)
    var webLoginUrl by mutableStateOf("")
    var qqLinkInput by mutableStateOf("")
    var pendingQqHandoff by mutableStateOf("")
    var pendingQqRequester by mutableStateOf("")
    val detectedTokens = mutableStateListOf<BnSdkTokenExtractor.ChannelToken>()
    var selectedTokenIndex by mutableIntStateOf(0)
    var authStatusText by mutableStateOf("")
    @Volatile private var webPollCancelled = false

    // Updates
    var previewUpdates by mutableStateOf(false)
    var availableUpdate by mutableStateOf<ReleaseUpdates.Update?>(null)
    var showUpdateModal by mutableStateOf(false)
    var updateDownloading by mutableStateOf(false)
    var updateVerifying by mutableStateOf(false)
    var updateProgressPct by mutableIntStateOf(0)
    var updateStatusText by mutableStateOf("点击检查是否存在官方版本更新")

    @Volatile private var disposed = false
    var onPickLocalArchiveRequest: (() -> Unit)? = null
    var onPickWallpaperRequest: (() -> Unit)? = null
    var onPickLobbyBgImageRequest: (() -> Unit)? = null
    var onPickLobbyBgVideoRequest: (() -> Unit)? = null
    var onPickCustomFontRequest: (() -> Unit)? = null
    var onExportLogRequest: ((String) -> Unit)? = null

    private val shizukuServiceArgs = Shizuku.UserServiceArgs(
        ComponentName(BuildConfig.APPLICATION_ID, InstallerService::class.java.name)
    )
        .daemon(false)
        .processNameSuffix("installer")
        .debuggable(false)
        .version(BuildConfig.VERSION_CODE)

    private val shizukuReceived = Shizuku.OnBinderReceivedListener {
        activity.runOnUiThread { appendLog("检测到 Shizuku 服务在线。") }
    }

    private val shizukuDead = Shizuku.OnBinderDeadListener {
        activity.runOnUiThread {
            remoteBinder = null
            lastShizukuSpecJson = null
            bindingShizuku = false
            appendLog("Shizuku 服务连接已断开。")
        }
    }

    private val shizukuPermissionListener = Shizuku.OnRequestPermissionResultListener { _, result ->
        activity.runOnUiThread {
            if (result == PackageManager.PERMISSION_GRANTED) {
                connectShizuku()
            } else {
                toast("未授予 Shizuku 权限，请在 Shizuku 应用管理中允许星枢。")
            }
        }
    }

    private val shizukuConnection = object : ServiceConnection {
        override fun onServiceConnected(name: ComponentName?, binder: IBinder?) {
            remoteBinder = IInstaller.Stub.asInterface(binder)
            lastShizukuSpecJson = null
            useShizuku = true
            bindingShizuku = false
            appendLog("Shizuku 授权通道已就绪。")
            scanPackagesAndInspect()
            refreshInstalledModsAsync()
        }

        override fun onServiceDisconnected(name: ComponentName?) {
            remoteBinder = null
            lastShizukuSpecJson = null
            bindingShizuku = false
        }
    }

    fun initialize(initialIntent: Intent?) {
        boundSession = MobileAuthHub.loadSession(prefs)
        val savedPkg = prefs.getString("selectedPackageName", GamePackageCatalog.DEFAULT_PACKAGE)
        if (GamePackageCatalog.isValidPackageName(savedPkg)) {
            selectedPackageName = savedPkg!!
        }
        themeId = prefs.getString("themeId", ThemePalette.DEFAULT_THEME_ID) ?: ThemePalette.DEFAULT_THEME_ID
        customThemeHex = ThemePalette.normalizeHex(prefs.getString("customThemeHex", ThemePalette.DEFAULT_CUSTOM_HEX))
        brightnessMode = ThemeBrightnessMode.fromPref(
            prefs.getString("brightnessMode", if (prefs.getBoolean("customThemeDark", false)) "dark" else "system")
        )
        wallpaperAlpha = prefs.getFloat("wallpaperAlpha", 0.28f).coerceIn(0.08f, 0.72f)
        connectionPortInput = prefs.getString("connectionPort", "") ?: ""
        previewUpdates = prefs.getBoolean("previewUpdates", false)

        loadSavedWallpaperAsync()
        refreshCacheBytes()
        scanOfflinePackages()

        try {
            Shizuku.addBinderReceivedListenerSticky(shizukuReceived)
            Shizuku.addBinderDeadListener(shizukuDead)
            Shizuku.addRequestPermissionResultListener(shizukuPermissionListener)
        } catch (_: Throwable) {
        }

        handleIntent(initialIntent)
        worker.execute {
            try {
                resolveCoreSpecBlocking(forceRefresh = false)
            } catch (_: Exception) {
            }
        }
        refreshWorkshopAsync()
        checkAssistantUpdate(silent = true)

        if (boundSession != null) {
            refreshCloudAccountAsync()
        }
        platformToken = prefs.getString("platformToken", "") ?: ""
        if (platformToken.isNotBlank()) {
            refreshPlatformAccountAsync()
        }
    }

    fun onStart() {
        startMdnsDiscovery()
    }

    fun onStop() {
        stopMdnsDiscovery()
    }

    fun onResume() {
        if (prefs.getBoolean("waitingInstallPermission", false)) {
            prefs.edit().putBoolean("waitingInstallPermission", false).apply()
            requestInstallPendingUpdateApk()
        }
        resumePendingWebAuthIfNeeded()
        if (activeTab == NavTab.WORKSHOP) {
            refreshWorkshopIfStale(10_000L)
        }
    }

    fun dispose() {
        disposed = true
        webPollCancelled = true
        stopMdnsDiscovery()
        try {
            Shizuku.removeBinderReceivedListener(shizukuReceived)
            Shizuku.removeBinderDeadListener(shizukuDead)
            Shizuku.removeRequestPermissionResultListener(shizukuPermissionListener)
        } catch (_: Throwable) {
        }
        val currentLocal = localAdb
        val shouldUnbindShizuku = remoteBinder != null
        try {
            Executors.newSingleThreadExecutor().apply {
                execute {
                    try { currentLocal?.close() } catch (_: Exception) {}
                    try {
                        if (shouldUnbindShizuku && Shizuku.pingBinder()) {
                            Shizuku.unbindUserService(shizukuServiceArgs, shizukuConnection, true)
                        }
                    } catch (_: Exception) {}
                    shutdown()
                }
            }
        } catch (_: Throwable) {
        }
        worker.shutdownNow()
        workshopWorker.shutdownNow()
        authWorker.shutdownNow()
        updateWorker.shutdownNow()
        thumbWorker.shutdownNow()
    }

    fun isPrivilegeReady(): Boolean = (useShizuku && remoteBinder != null) || (!useShizuku && adbConnected)

    fun selectedChannel(): ChannelEntry? =
        channels.firstOrNull { it.packageName == selectedPackageName } ?: channels.firstOrNull()

    fun selectChannel(packageName: String) {
        if (!GamePackageCatalog.isValidPackageName(packageName)) return
        selectedPackageName = packageName
        prefs.edit().putString("selectedPackageName", packageName).apply()
        if (isPrivilegeReady()) {
            inspectSelectedAsync()
            refreshInstalledModsAsync()
            if (activeTab == NavTab.CUSTOMIZE) {
                refreshCustomizeStateAsync()
            }
        }
    }

    fun toast(msg: String) {
        snackbarMessage = msg
        appendLog(msg)
    }

    fun consumeSnackbar() {
        snackbarMessage = null
    }

    fun appendLog(msg: String) {
        if (disposed || msg.isBlank()) return
        val stamp = SimpleDateFormat("HH:mm:ss", Locale.CHINA).format(Date())
        logs.add(0, "[$stamp] $msg")
        if (logs.size > 60) {
            logs.removeRange(60, logs.size)
        }
        statusText = msg.lineSequence().firstOrNull() ?: msg
    }

    private fun requireLocalAdb(): LocalAdbInstaller {
        val current = localAdb
        if (current != null) return current
        val created = LocalAdbInstaller(appContext)
        localAdb = created
        return created
    }

    private fun scanOfflinePackages() {
        val pm = appContext.packageManager
        val discovered = mutableListOf<ChannelEntry>()
        val knownCandidates = listOf(
            "com.feimo.astralparty",
            "com.feimo.jxpd.mi",
            "com.feimo.astralparty.bilibili",
            "com.feimo.jxpd.bili",
            "com.feimo.astralparty.huawei",
            "com.feimo.jxpd.huawei",
            "com.feimo.astralparty.nearme.gamecenter",
            "com.feimo.astralparty.vivo",
            "com.tencent.tmgp.astralparty",
        )
        for (pkg in knownCandidates) {
            try {
                @Suppress("DEPRECATION")
                val info = pm.getPackageInfo(pkg, 0)
                val meta = GamePackageCatalog.classifyPackage(pkg)
                discovered.add(
                    ChannelEntry(
                        packageName = pkg,
                        label = meta.label,
                        platform = meta.platform,
                        versionName = info.versionName ?: "",
                        hasCatalog = false,
                        running = false,
                        installState = "unverified",
                        resolvedRel = "",
                    )
                )
            } catch (_: PackageManager.NameNotFoundException) {
            }
        }
        if (discovered.isNotEmpty()) {
            channels.clear()
            channels.addAll(discovered)
            if (discovered.none { it.packageName == selectedPackageName }) {
                selectedPackageName = discovered.first().packageName
            }
        }
    }

    fun launchSelectedGame() {
        val pkg = selectedChannel()?.packageName ?: selectedPackageName
        val session = boundSession
        try {
            val launch = appContext.packageManager.getLaunchIntentForPackage(pkg)
            if (launch != null) {
                launch.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_RESET_TASK_IF_NEEDED)
                if (session != null && session.token.isNotEmpty()) {
                    authWorker.execute {
                        try {
                            val handoff = MobileAuthHub.prepareGameHandoff(session)
                            val handoffJson = LocalHandoffBridge.buildHandoffJson(handoff.token, handoff.uid)
                            if (isPrivilegeReady()) {
                                val res = if (useShizuku) {
                                    remoteBinder?.writeGameHandoff(pkg, handoffJson)
                                } else {
                                    requireLocalAdb().writeGameHandoff(pkg, handoffJson)
                                }
                                if (res == "OK") {
                                    LocalHandoffBridge.publish(handoff.token, handoff.uid)
                                }
                            } else {
                                LocalHandoffBridge.publish(handoff.token, handoff.uid)
                            }
                        } catch (_: Exception) {
                        }
                    }
                }
                activity.startActivity(launch)
                toast("已纯净启动游戏：${GamePackageCatalog.classifyPackage(pkg).label}")
                return
            }
        } catch (_: Exception) {
        }

        if (isPrivilegeReady()) {
            worker.execute {
                val res = try {
                    if (useShizuku) remoteBinder!!.launchGame(pkg) else requireLocalAdb().launchGame(pkg)
                } catch (e: Exception) {
                    "启动失败：${e.message}"
                }
                activity.runOnUiThread {
                    if (!disposed) toast(res)
                }
            }
            return
        }
        toast("未找到所选渠道（$pkg），请确认游戏已安装后点「刷新渠道」。")
    }

    // ---- Shizuku & Wireless ADB ----

    fun connectShizuku() {
        if (busy || bindingShizuku) return
        useShizuku = true
        try {
            if (!Shizuku.pingBinder()) {
                useShizuku = false
                toast("未检测到 Shizuku 服务运行，可打开 Shizuku 后重试或改用无线调试。")
                return
            }
            if (Shizuku.checkSelfPermission() != PackageManager.PERMISSION_GRANTED) {
                Shizuku.requestPermission(1)
                return
            }
            val current = remoteBinder
            if (current != null) {
                scanPackagesAndInspect()
                refreshInstalledModsAsync()
            } else {
                bindingShizuku = true
                Shizuku.bindUserService(shizukuServiceArgs, shizukuConnection)
                appendLog("正在绑定 Shizuku 特权服务…")
            }
        } catch (e: Exception) {
            bindingShizuku = false
            useShizuku = false
            toast("Shizuku 绑定失败：${e.message}")
        }
    }

    fun startMdnsDiscovery() {
        if (disposed || Build.VERSION.SDK_INT < 30) return
        val sysPort = LocalAdbInstaller.tryDiscoverSystemAdbPort()
        if (sysPort >= 1024) {
            onConnectPortDiscovered(null, sysPort)
        }
        if (connectMdns == null) {
            connectMdns = AdbMdns(appContext, AdbMdns.SERVICE_TYPE_TLS_CONNECT) { addr, port ->
                onConnectPortDiscovered(addr, port)
            }.also { try { it.start() } catch (_: Exception) {} }
        }
        if (legacyConnectMdns == null) {
            legacyConnectMdns = AdbMdns(appContext, AdbMdns.SERVICE_TYPE_ADB) { addr, port ->
                onConnectPortDiscovered(addr, port)
            }.also { try { it.start() } catch (_: Exception) {} }
        }
        if (pairingMdns == null) {
            pairingMdns = AdbMdns(appContext, AdbMdns.SERVICE_TYPE_TLS_PAIRING) { _, port ->
                if (port >= 1024) {
                    activity.runOnUiThread {
                        if (!disposed) {
                            discoveredPairingPort = port
                            if (pairingPortInput.isBlank()) pairingPortInput = port.toString()
                        }
                    }
                }
            }.also { try { it.start() } catch (_: Exception) {} }
        }
    }

    fun stopMdnsDiscovery() {
        try { connectMdns?.stop() } catch (_: Exception) {}
        try { legacyConnectMdns?.stop() } catch (_: Exception) {}
        try { pairingMdns?.stop() } catch (_: Exception) {}
        connectMdns = null
        legacyConnectMdns = null
        pairingMdns = null
    }

    private fun onConnectPortDiscovered(addr: InetAddress?, port: Int) {
        if (port < 1024) return
        val host = (addr as? Inet4Address)?.hostAddress
        discoveredConnectHost = host
        connectDiscoveryLatch?.countDown()
        activity.runOnUiThread {
            if (disposed) return@runOnUiThread
            discoveredConnectPort = port
            if (connectionPortInput.isBlank()) connectionPortInput = port.toString()
            val previouslyPaired = prefs.getBoolean("wirelessPaired", false) ||
                !prefs.getString("connectionPort", "").isNullOrEmpty()
            if (previouslyPaired && discoveredPairingPort < 1024) {
                trySilentAutoConnect(host, port)
            }
        }
    }

    private fun trySilentAutoConnect(host: String?, port: Int) {
        if (disposed || busy || bindingShizuku || useShizuku || adbConnected || port < 1024) return
        if (lastAutoTriedPort == port) return
        lastAutoTriedPort = port
        worker.execute {
            if (disposed || useShizuku) return@execute
            try {
                val adb = requireLocalAdb()
                if (adb.connected()) return@execute
                adb.connect(host, port)
                val scanJson = adb.scanPackages()
                val modJson = adb.listInstalledMods(selectedPackageName)
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    adbConnected = true
                    connectedHost = adb.connectedHost()
                    prefs.edit().putString("connectionPort", port.toString()).putBoolean("wirelessPaired", true).apply()
                    applyScannedPackages(scanJson)
                    applyListedMods(modJson)
                    appendLog("已自动连接无线调试：${adb.connectedHost()}:$port")
                }
            } catch (_: Exception) {
            }
        }
    }

    fun pairAndConnectLocalAdb() {
        if (busy) return
        val pairPort = try { LocalPort.parse(pairingPortInput.trim()) } catch (e: Exception) {
            toast(e.message ?: "配对端口无效")
            return
        }
        val code = pairingCodeInput.trim()
        if (!code.matches(Regex("^\\d{6}$"))) {
            toast("请输入系统弹窗中的 6 位数字配对码。")
            return
        }
        busy = true
        appendLog("正在通过本机无线调试配对端口 $pairPort…")
        worker.execute {
            try {
                val adb = requireLocalAdb()
                adb.pair(null, pairPort, code)
                activity.runOnUiThread {
                    prefs.edit().putBoolean("wirelessPaired", true).apply()
                }
                var connPort = discoveredConnectPort
                var connHost = discoveredConnectHost
                if (connPort < 1024) {
                    val latch = CountDownLatch(1)
                    connectDiscoveryLatch = latch
                    try { latch.await(2400, TimeUnit.MILLISECONDS) } catch (_: InterruptedException) {}
                    connectDiscoveryLatch = null
                    connPort = discoveredConnectPort
                    connHost = discoveredConnectHost
                }
                if (connPort >= 1024) {
                    adb.connect(connHost, connPort)
                    val scanJson = adb.scanPackages()
                    val modJson = adb.listInstalledMods(selectedPackageName)
                    activity.runOnUiThread {
                        if (disposed) return@runOnUiThread
                        busy = false
                        adbConnected = true
                        useShizuku = false
                        connectedHost = adb.connectedHost()
                        connectionPortInput = connPort.toString()
                        prefs.edit().putString("connectionPort", connPort.toString()).apply()
                        applyScannedPackages(scanJson)
                        applyListedMods(modJson)
                        showConnectSheet = false
                        toast("配对并连接无线调试成功（${adb.connectedHost()}:$connPort）")
                    }
                } else {
                    activity.runOnUiThread {
                        busy = false
                        toast("配对成功！请关闭系统配对弹窗并点击「连接无线调试」。")
                    }
                }
            } catch (e: Exception) {
                activity.runOnUiThread {
                    busy = false
                    toast(e.message ?: "无线调试配对失败")
                }
            }
        }
    }

    fun connectLocalAdb() {
        if (busy) return
        val manualPort = if (connectionPortInput.isNotBlank()) {
            try { LocalPort.parse(connectionPortInput.trim()) } catch (e: Exception) {
                toast(e.message ?: "连接端口无效")
                return
            }
        } else null
        busy = true
        appendLog("正在查找并连接本机无线调试…")
        worker.execute {
            try {
                var targetPort = manualPort ?: discoveredConnectPort
                var targetHost = discoveredConnectHost
                if (targetPort < 1024) {
                    val latch = CountDownLatch(1)
                    connectDiscoveryLatch = latch
                    activity.runOnUiThread { startMdnsDiscovery() }
                    try { latch.await(2600, TimeUnit.MILLISECONDS) } catch (_: InterruptedException) {}
                    connectDiscoveryLatch = null
                    targetPort = discoveredConnectPort
                    targetHost = discoveredConnectHost
                }
                if (targetPort < 1024) {
                    targetPort = LocalAdbInstaller.tryDiscoverSystemAdbPort()
                }
                if (targetPort < 1024) {
                    throw IOException("未发现可用无线调试端口，请开启开发者选项「无线调试」并核对端口号。")
                }
                val adb = requireLocalAdb()
                val spec = resolveCoreSpecBlocking(forceRefresh = false)
                adb.connect(targetHost, targetPort)
                adb.syncCoreSpec(spec)
                val scanJson = adb.scanPackages()
                val modJson = adb.listInstalledMods(selectedPackageName)
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    busy = false
                    adbConnected = true
                    useShizuku = false
                    connectedHost = adb.connectedHost()
                    connectionPortInput = targetPort.toString()
                    prefs.edit().putString("connectionPort", targetPort.toString()).putBoolean("wirelessPaired", true).apply()
                    applyScannedPackages(scanJson)
                    applyListedMods(modJson)
                    showConnectSheet = false
                    toast("无线调试已连接（${adb.connectedHost()}:$targetPort）")
                }
            } catch (e: Exception) {
                activity.runOnUiThread {
                    busy = false
                    adbConnected = false
                    toast(e.message ?: "无线调试连接失败")
                }
            }
        }
    }

    fun openWirelessSettings() {
        try {
            activity.startActivity(Intent(Settings.ACTION_APPLICATION_DEVELOPMENT_SETTINGS))
        } catch (_: Exception) {
            try { activity.startActivity(Intent(Settings.ACTION_SETTINGS)) } catch (_: Exception) {}
        }
    }

    // ---- Scanning & Core Plugin Mod ----

    private fun resolveCoreSpecBlocking(forceRefresh: Boolean): CoreRuntimeSpec {
        val resolved = CoreRuntimeSpec.resolve(appContext.filesDir, BuildConfig.VERSION_CODE, forceRefresh)
        activity.runOnUiThread {
            if (!disposed) {
                coreSpec = resolved
            }
        }
        return resolved
    }

    private fun syncCoreSpecToPrivilegeBlocking(spec: CoreRuntimeSpec) {
        if (useShizuku) {
            val binder = remoteBinder ?: throw IOException("Shizuku 服务未就绪")
            val canonical = spec.toCanonicalJson()
            if (canonical != lastShizukuSpecJson) {
                val parsed = MiniJson.parseObject(binder.setCoreSpec(canonical))
                if (parsed["ok"] != true) {
                    throw IOException(MiniJson.stringField(parsed, "error").ifEmpty { "同步核心组件清单失败" })
                }
                lastShizukuSpecJson = canonical
            }
        } else {
            requireLocalAdb().syncCoreSpec(spec)
        }
    }

    fun scanPackagesAndInspect() {
        if (!isPrivilegeReady()) {
            scanOfflinePackages()
            worker.execute {
                try {
                    resolveCoreSpecBlocking(forceRefresh = true)
                } catch (_: Exception) {
                }
            }
            toast("已刷新本地已安装渠道列表；连接无线调试或 Shizuku 后可管理模组。")
            return
        }
        val pkg = selectedPackageName
        busy = true
        worker.execute {
            try {
                val spec = resolveCoreSpecBlocking(forceRefresh = true)
                syncCoreSpecToPrivilegeBlocking(spec)
                val scanJson = if (useShizuku) remoteBinder!!.scanPackages() else requireLocalAdb().scanPackages()
                val inspectOut = if (useShizuku) remoteBinder!!.inspect(pkg) else requireLocalAdb().inspect(pkg)
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    busy = false
                    applyScannedPackages(scanJson)
                    lastResultText = inspectOut
                    appendLog("渠道检测完成：${inspectOut.lineSequence().firstOrNull() ?: ""}")
                }
                refreshInstalledModsAsync()
            } catch (e: Exception) {
                activity.runOnUiThread {
                    busy = false
                    toast("扫描失败：${e.message}")
                }
            }
        }
    }

    fun inspectSelectedAsync() {
        if (!isPrivilegeReady() || busy) return
        val pkg = selectedPackageName
        busy = true
        worker.execute {
            val out = try {
                val spec = resolveCoreSpecBlocking(forceRefresh = true)
                syncCoreSpecToPrivilegeBlocking(spec)
                if (useShizuku) remoteBinder!!.inspect(pkg) else requireLocalAdb().inspect(pkg)
            } catch (e: Exception) {
                "检查未通过：${e.message}"
            }
            activity.runOnUiThread {
                if (disposed) return@runOnUiThread
                busy = false
                lastResultText = out
                appendLog(out.lineSequence().take(2).joinToString(" "))
            }
        }
    }

    fun installOrRestoreCorePlugin(restore: Boolean, conflictHash: String? = null) {
        if (busy) return
        if (!isPrivilegeReady()) {
            showConnectSheet = true
            toast("请先连接无线调试或 Shizuku 服务。")
            return
        }
        val pkg = selectedPackageName
        busy = true
        worker.execute {
            try {
                val spec = resolveCoreSpecBlocking(forceRefresh = true)
                syncCoreSpecToPrivilegeBlocking(spec)
                if (!restore && conflictHash == null) {
                    val preflight = if (useShizuku) remoteBinder!!.inspect(pkg) else requireLocalAdb().inspect(pkg)
                    if (!preflight.startsWith("游戏版本匹配") || preflight.contains("游戏仍在运行")) {
                        activity.runOnUiThread {
                            busy = false
                            lastResultText = preflight
                            toast(preflight)
                        }
                        return@execute
                    }
                    if (preflight.contains("同位置覆盖冲突")) {
                        val m = Pattern.compile("冲突文件SHA256：([a-f0-9]{64})").matcher(preflight)
                        if (m.find()) {
                            val hash = m.group(1)!!
                            activity.runOnUiThread {
                                busy = false
                                lastResultText = preflight
                                conflictPrompt = ConflictPrompt(pkg, hash, preflight)
                            }
                            return@execute
                        }
                    }
                }

                val out = applyCorePluginBlocking(pkg, restore, conflictHash, spec)
                val scanJson = if (useShizuku) remoteBinder!!.scanPackages() else requireLocalAdb().scanPackages()
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    busy = false
                    runtimeProgress = RuntimeProgressState()
                    lastResultText = out
                    applyScannedPackages(scanJson)
                    refreshCacheBytes()
                    toast(out.lineSequence().firstOrNull() ?: out)
                }
                refreshInstalledModsAsync()
            } catch (e: Exception) {
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    busy = false
                    runtimeProgress = RuntimeProgressState()
                    toast(e.message ?: "操作未完成")
                }
            }
        }
    }

    private fun applyCorePluginBlocking(
        pkg: String,
        restore: Boolean,
        conflictHash: String?,
        spec: CoreRuntimeSpec = resolveCoreSpecBlocking(forceRefresh = false),
    ): String {
        val cacheDir = PayloadDownloadManager.cacheDir(appContext)
        val total = if (restore) spec.vanillaSize else spec.candidateSize
        activity.runOnUiThread {
            runtimeProgress = RuntimeProgressState(
                active = true,
                received = 0L,
                total = total,
                speed = 0L,
                stage = if (restore) "正在下载官方原版还原包…" else "正在下载星枢官方插件 Mod ${spec.runtimeVersion}…",
            )
        }
        val listener = PayloadDownloadManager.ProgressListener { rec, tot, spd, st ->
            activity.runOnUiThread {
                runtimeProgress = RuntimeProgressState(true, rec, tot, spd, st)
            }
        }
        val candidateFile = if (restore) null else PayloadDownloadManager.ensureDownloaded(
            cacheDir,
            spec.candidateUrl,
            spec.patchSha256,
            spec.candidateSize,
            "星枢官方插件 Mod ${spec.runtimeVersion}",
            listener,
        )
        val vanillaFile = if (restore) PayloadDownloadManager.ensureDownloaded(
            cacheDir,
            spec.vanillaUrl,
            spec.vanillaSha256,
            spec.vanillaSize,
            "官方原版备份",
            listener,
        ) else null

        activity.runOnUiThread {
            runtimeProgress = RuntimeProgressState(
                active = true,
                received = total,
                total = total,
                speed = 0L,
                stage = if (restore) "正在无损还原官方原版…" else "正在校验并安装星枢官方插件 Mod…",
            )
        }
        if (!useShizuku) {
            return requireLocalAdb().apply(pkg, restore, conflictHash, candidateFile, vanillaFile)
        }
        val binder = remoteBinder!!
        val pfdCandidate = candidateFile?.let { ParcelFileDescriptor.open(it, ParcelFileDescriptor.MODE_READ_ONLY) }
        val pfdVanilla = vanillaFile?.let { ParcelFileDescriptor.open(it, ParcelFileDescriptor.MODE_READ_ONLY) }
        return try {
            binder.apply(pkg, restore, conflictHash, pfdCandidate, pfdVanilla)
        } finally {
            try { pfdCandidate?.close() } catch (_: Exception) {}
            try { pfdVanilla?.close() } catch (_: Exception) {}
        }
    }

    // ---- 18 Official Modules & Mods Management ----

    fun refreshInstalledModsAsync() {
        if (!isPrivilegeReady()) return
        val pkg = selectedPackageName
        worker.execute {
            try {
                val json = if (useShizuku) remoteBinder!!.listInstalledMods(pkg) else requireLocalAdb().listInstalledMods(pkg)
                activity.runOnUiThread {
                    if (!disposed) applyListedMods(json)
                }
            } catch (_: Exception) {
            }
        }
    }

    fun toggleOfficialModule(id: String, enable: Boolean) {
        if (!OfficialModulesCatalog.isSafeId(id) || busy || batchModsBusy) return
        if (!isPrivilegeReady()) {
            showConnectSheet = true
            toast("请先连接无线调试或 Shizuku，再启停功能模组。")
            return
        }
        val pkg = selectedPackageName
        batchModsBusy = true
        worker.execute {
            try {
                val respJson = if (useShizuku) {
                    remoteBinder!!.setOfficialModules(pkg, id, enable, false)
                } else {
                    requireLocalAdb().setOfficialModules(pkg, id, enable, false)
                }
                val parsed = MiniJson.parseObject(respJson)
                activity.runOnUiThread {
                    batchModsBusy = false
                    if (disposed) return@runOnUiThread
                    if (parsed["ok"] == true) {
                        applyOfficialModulesMap(parsed["officialModules"])
                        activeModuleCount = MiniJson.longField(parsed, "activeModuleCount", activeModuleCount.toLong()).toInt()
                        val modName = OfficialModulesCatalog.ALL.firstOrNull { it.id == id }?.name ?: id
                        toast(if (enable) "已开启「$modName」" else "已关闭「$modName」")
                    } else {
                        toast(MiniJson.stringField(parsed, "error").ifEmpty { "切换模组失败" })
                    }
                }
            } catch (e: Exception) {
                activity.runOnUiThread {
                    batchModsBusy = false
                    toast("切换失败：${e.message}")
                }
            }
        }
    }

    fun installAllOfficialModules(includeCore: Boolean = true) {
        if (busy || batchModsBusy) return
        if (!isPrivilegeReady()) {
            showConnectSheet = true
            toast("请先连接无线调试或 Shizuku，即可一键安装全部 ${OfficialModulesCatalog.ALL.size} 项官方模组。")
            return
        }
        val pkg = selectedPackageName
        val currentState = selectedChannel()?.installState ?: ""
        val prevRuntime = coreSpec.runtimeVersion
        batchModsBusy = true
        worker.execute {
            try {
                val spec = resolveCoreSpecBlocking(forceRefresh = true)
                syncCoreSpecToPrivilegeBlocking(spec)
                var coreInstalledNow = false
                if (includeCore && (currentState != "installed" || spec.runtimeVersion != prevRuntime)) {
                    val out = applyCorePluginBlocking(pkg, restore = false, conflictHash = null, spec = spec)
                    if (out.startsWith("未完成") || out.startsWith("检查未通过") || out.contains("同位置覆盖冲突")) {
                        val scanJson = if (useShizuku) remoteBinder!!.scanPackages() else requireLocalAdb().scanPackages()
                        activity.runOnUiThread {
                            batchModsBusy = false
                            runtimeProgress = RuntimeProgressState()
                            applyScannedPackages(scanJson)
                            toast(out)
                        }
                        return@execute
                    }
                    coreInstalledNow = true
                }
                val csv = OfficialModulesCatalog.allIdsCsv()
                val respJson = if (useShizuku) {
                    remoteBinder!!.setOfficialModules(pkg, csv, true, false)
                } else {
                    requireLocalAdb().setOfficialModules(pkg, csv, true, false)
                }
                val parsed = MiniJson.parseObject(respJson)
                val scanJson = if (useShizuku) remoteBinder!!.scanPackages() else requireLocalAdb().scanPackages()
                activity.runOnUiThread {
                    batchModsBusy = false
                    runtimeProgress = RuntimeProgressState()
                    if (disposed) return@runOnUiThread
                    applyScannedPackages(scanJson)
                    if (parsed["ok"] == true) {
                        applyOfficialModulesMap(parsed["officialModules"])
                        activeModuleCount = MiniJson.longField(parsed, "activeModuleCount", 18L).toInt()
                        toast(
                            if (coreInstalledNow) {
                                "已一键安装「星枢官方插件 Mod」并启用全部 ${OfficialModulesCatalog.ALL.size} 项官方模组！"
                            } else {
                                "已一键安装并启用全部 ${OfficialModulesCatalog.ALL.size} 项官方模组！"
                            }
                        )
                    } else {
                        toast(MiniJson.stringField(parsed, "error").ifEmpty { "一键安装未完成" })
                    }
                }
            } catch (e: Exception) {
                activity.runOnUiThread {
                    batchModsBusy = false
                    runtimeProgress = RuntimeProgressState()
                    toast("一键安装失败：${e.message}")
                }
            }
        }
    }

    fun uninstallAllOfficialModules() {
        if (busy || batchModsBusy) return
        if (!isPrivilegeReady()) {
            showConnectSheet = true
            toast("请先连接无线调试或 Shizuku。")
            return
        }
        val pkg = selectedPackageName
        batchModsBusy = true
        worker.execute {
            try {
                val respJson = if (useShizuku) {
                    remoteBinder!!.setOfficialModules(pkg, "all", false, true)
                } else {
                    requireLocalAdb().setOfficialModules(pkg, "all", false, true)
                }
                val parsed = MiniJson.parseObject(respJson)
                activity.runOnUiThread {
                    batchModsBusy = false
                    if (disposed) return@runOnUiThread
                    if (parsed["ok"] == true) {
                        applyOfficialModulesMap(parsed["officialModules"])
                        toast("已卸载全部星枢官方功能模组（含历史残留模块）；如需还原纯净原版可点击上方卸载核心插件。")
                    } else {
                        toast(MiniJson.stringField(parsed, "error").ifEmpty { "卸载未完成" })
                    }
                }
            } catch (e: Exception) {
                activity.runOnUiThread {
                    batchModsBusy = false
                    toast("卸载失败：${e.message}")
                }
            }
        }
    }

    fun cleanLegacyOfficialModules() {
        if (busy || batchModsBusy) return
        if (!isPrivilegeReady()) {
            showConnectSheet = true
            toast("请先连接无线调试或 Shizuku。")
            return
        }
        val pkg = selectedPackageName
        batchModsBusy = true
        worker.execute {
            try {
                val respJson = if (useShizuku) {
                    remoteBinder!!.setOfficialModules(pkg, "legacy", false, true)
                } else {
                    requireLocalAdb().setOfficialModules(pkg, "legacy", false, true)
                }
                val parsed = MiniJson.parseObject(respJson)
                activity.runOnUiThread {
                    batchModsBusy = false
                    if (disposed) return@runOnUiThread
                    if (parsed["ok"] == true) {
                        val cleared = MiniJson.longField(parsed, "changedCount", 0L)
                        applyOfficialModulesMap(parsed["officialModules"])
                        toast(if (cleared > 0L) "已清理 $cleared 个旧版残留功能模块" else "未发现可清理的旧版残留模块")
                    } else {
                        toast(MiniJson.stringField(parsed, "error").ifEmpty { "清理残留未完成" })
                    }
                }
            } catch (e: Exception) {
                activity.runOnUiThread {
                    batchModsBusy = false
                    toast("清理失败：${e.message}")
                }
            }
        }
    }

    fun removeInstalledMod(entry: InstalledModEntry) {
        if (busy || modTaskBusy) return
        if (!isPrivilegeReady()) {
            showConnectSheet = true
            toast("请先连接无线调试或 Shizuku。")
            return
        }
        val pkg = selectedPackageName
        modTaskBusy = true
        worker.execute {
            try {
                val respJson = if (useShizuku) {
                    remoteBinder!!.removeModPack(pkg, entry.id)
                } else {
                    requireLocalAdb().removeModPack(pkg, entry.id)
                }
                val parsed = MiniJson.parseObject(respJson)
                val listJson = if (useShizuku) remoteBinder!!.listInstalledMods(pkg) else requireLocalAdb().listInstalledMods(pkg)
                activity.runOnUiThread {
                    modTaskBusy = false
                    if (disposed) return@runOnUiThread
                    applyListedMods(listJson)
                    if (parsed["ok"] == true) {
                        toast("已卸载 Mod：${entry.name.ifEmpty { entry.id }}")
                    } else {
                        toast(MiniJson.stringField(parsed, "error").ifEmpty { "卸载 Mod 失败" })
                    }
                }
            } catch (e: Exception) {
                activity.runOnUiThread {
                    modTaskBusy = false
                    toast("卸载失败：${e.message}")
                }
            }
        }
    }

    fun requestImportLocalArchive() {
        if (!isPrivilegeReady()) {
            showConnectSheet = true
            toast("导入本地 Mod 包前，请先连接无线调试或 Shizuku。")
            return
        }
        onPickLocalArchiveRequest?.invoke()
    }

    fun installLocalArchiveUri(uri: Uri) {
        if (!isPrivilegeReady()) {
            showConnectSheet = true
            return
        }
        if (modTaskBusy) {
            toast("已有 Mod 操作正在执行，请稍候。")
            return
        }
        modTaskBusy = true
        val pkg = selectedPackageName
        workshopWorker.execute {
            var tempFile: File? = null
            try {
                val displayName = resolveDisplayName(uri) ?: "local-mod.zip"
                val cacheDir = PayloadDownloadManager.cacheDir(appContext).apply { mkdirs() }
                tempFile = File.createTempFile("import-mod-", ".zip", cacheDir)
                val digest = MessageDigest.getInstance("SHA-256")
                var bytes = 0L
                appContext.contentResolver.openInputStream(uri).use { input ->
                    if (input == null) throw IOException("无法读取选中的文件")
                    FileOutputStream(tempFile).use { output ->
                        val buf = ByteArray(65536)
                        while (true) {
                            val n = input.read(buf)
                            if (n == -1) break
                            bytes += n
                            if (bytes > PayloadDownloadManager.MAX_PAYLOAD_BYTES) {
                                throw IOException("导入文件超过 1GB 上限")
                            }
                            digest.update(buf, 0, n)
                            output.write(buf, 0, n)
                        }
                        output.flush()
                    }
                }
                val sha = PayloadDownloadManager.hex(digest.digest())
                val cleanName = displayName.substringBeforeLast('.').ifBlank { "本地模组包" }
                val metaJson = MiniJson.stringify(
                    mapOf(
                        "id" to "local.${sha.take(12)}",
                        "name" to cleanName,
                        "version" to "1.0.0",
                        "sha256" to sha,
                        "installKind" to if (displayName.endsWith(".xspkg", true)) "xspkg" else "",
                    )
                )
                val respJson = if (useShizuku) {
                    ParcelFileDescriptor.open(tempFile, ParcelFileDescriptor.MODE_READ_ONLY).use { pfd ->
                        remoteBinder!!.installModPack(pkg, metaJson, pfd)
                    }
                } else {
                    requireLocalAdb().installModPack(pkg, metaJson, tempFile)
                }
                val parsed = MiniJson.parseObject(respJson)
                val listJson = if (useShizuku) remoteBinder!!.listInstalledMods(pkg) else requireLocalAdb().listInstalledMods(pkg)
                activity.runOnUiThread {
                    modTaskBusy = false
                    if (disposed) return@runOnUiThread
                    applyListedMods(listJson)
                    if (parsed["ok"] == true) {
                        modsSubTab = "local"
                        val fileCount = MiniJson.longField(parsed, "fileCount", 0L)
                        toast("本地 Mod 导入成功：$cleanName（写入 $fileCount 个文件）")
                    } else {
                        toast(MiniJson.stringField(parsed, "error").ifEmpty { "导入失败" })
                    }
                }
            } catch (e: Exception) {
                activity.runOnUiThread {
                    modTaskBusy = false
                    toast("导入本地 Mod 失败：${e.message}")
                }
            } finally {
                tempFile?.delete()
            }
        }
    }

    // ---- Workshop & Lightweight Thumbnail Cache ----

    fun refreshWorkshopAsync(userInitiated: Boolean = false) {
        if (workshopLoading) return
        workshopLoading = true
        workshopError = ""
        workshopWorker.execute {
            try {
                val items = AndroidWorkshopClient.fetchAndroidMods()
                val stamp = SimpleDateFormat("HH:mm:ss", Locale.getDefault()).format(Date())
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    workshopLoading = false
                    workshopMods.clear()
                    workshopMods.addAll(items)
                    workshopLastSyncTime = stamp
                    lastWorkshopFetchMs = SystemClock.elapsedRealtime()
                    if (userInitiated) {
                        val mobileCount = items.count { it.androidInstallable }
                        toast("工坊已实时同步：共 ${items.size} 项（手机可直装 $mobileCount 项）")
                    }
                }
            } catch (e: Exception) {
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    workshopLoading = false
                    workshopError = e.message ?: "无法连接工坊"
                    if (userInitiated) {
                        toast("工坊同步失败：$workshopError")
                    }
                }
            }
        }
    }

    fun refreshWorkshopIfStale(maxAgeMs: Long = 10_000L) {
        if (workshopLoading) return
        if (workshopMods.isEmpty() || SystemClock.elapsedRealtime() - lastWorkshopFetchMs >= maxAgeMs) {
            refreshWorkshopAsync(userInitiated = false)
        }
    }

    fun openWorkshopCategory(filterKey: String) {
        workshopKindFilter = filterKey
        activeTab = NavTab.WORKSHOP
        refreshWorkshopIfStale(5_000L)
    }

    // ---- XingShu Platform Account & Emote Reactions ----

    private fun applyPlatformSession(session: PlatformAccountClient.SessionResult) {
        platformToken = session.token
        prefs.edit().putString("platformToken", session.token).apply()
        platformUser = session.user
        platformUserReactions.clear()
        platformUserReactions.putAll(session.reactions)
    }

    private fun clearPlatformSession() {
        platformToken = ""
        platformUser = null
        platformUserReactions.clear()
        prefs.edit().remove("platformToken").apply()
    }

    fun refreshPlatformAccountAsync() {
        val tok = platformToken.trim()
        if (tok.isEmpty()) return
        authWorker.execute {
            try {
                val res = PlatformAccountClient.fetchMe(tok)
                activity.runOnUiThread {
                    if (!disposed) applyPlatformSession(res)
                }
            } catch (_: PlatformAccountClient.UnauthorizedException) {
                activity.runOnUiThread {
                    if (!disposed) clearPlatformSession()
                }
            } catch (_: Exception) {
            }
        }
    }

    fun platformLogin(username: String, password: String) {
        val cleanUser = username.trim()
        if (cleanUser.isEmpty() || password.isEmpty()) {
            platformAuthError = "请输入用户名和密码"
            return
        }
        if (platformAuthBusy) return
        platformAuthBusy = true
        platformAuthError = ""
        val devLabel = "${Build.MANUFACTURER} ${Build.MODEL}".trim().ifEmpty { "Android" }
        authWorker.execute {
            try {
                val session = PlatformAccountClient.login(cleanUser, password, devLabel)
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    platformAuthBusy = false
                    applyPlatformSession(session)
                    showPlatformLoginDialog = false
                    val name = session.user.displayName.ifEmpty { session.user.username }
                    toast("已登录星枢平台账号：$name (UID ${session.user.uid})")
                    val pending = pendingReactionAfterLogin
                    pendingReactionAfterLogin = null
                    if (pending != null) {
                        toggleModReaction(pending.first, pending.second)
                    }
                }
            } catch (e: Exception) {
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    platformAuthBusy = false
                    platformAuthError = e.message ?: "登录失败"
                }
            }
        }
    }

    fun platformRegister(username: String, password: String, displayName: String) {
        val cleanUser = username.trim()
        if (cleanUser.isEmpty() || password.isEmpty()) {
            platformAuthError = "请输入用户名和密码"
            return
        }
        if (platformAuthBusy) return
        platformAuthBusy = true
        platformAuthError = ""
        val devLabel = "${Build.MANUFACTURER} ${Build.MODEL}".trim().ifEmpty { "Android" }
        authWorker.execute {
            try {
                val session = PlatformAccountClient.register(cleanUser, password, displayName, devLabel)
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    platformAuthBusy = false
                    applyPlatformSession(session)
                    showPlatformLoginDialog = false
                    val name = session.user.displayName.ifEmpty { session.user.username }
                    toast("已注册并登录星枢平台账号：$name (UID ${session.user.uid})")
                    val pending = pendingReactionAfterLogin
                    pendingReactionAfterLogin = null
                    if (pending != null) {
                        toggleModReaction(pending.first, pending.second)
                    }
                }
            } catch (e: Exception) {
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    platformAuthBusy = false
                    platformAuthError = e.message ?: "注册失败"
                }
            }
        }
    }

    fun platformLogout() {
        val tok = platformToken.trim()
        clearPlatformSession()
        toast("已退出星枢平台账号。")
        if (tok.isNotEmpty()) {
            authWorker.execute { PlatformAccountClient.logout(tok) }
        }
    }

    fun toggleModReaction(mod: AndroidWorkshopClient.WorkshopMod, emoteId: String, action: String = "toggle") {
        val cleanEmote = emoteId.trim()
        if (mod.id.isBlank()) return
        if (platformToken.isBlank() || platformUser == null) {
            pendingReactionAfterLogin = mod to cleanEmote
            platformAuthError = ""
            showPlatformLoginDialog = true
            return
        }
        if (reactionBusyModId != null) return
        val tok = platformToken
        reactionBusyModId = mod.id
        workshopWorker.execute {
            try {
                val res = PlatformAccountClient.setModReaction(tok, mod.id, cleanEmote, action)
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    reactionBusyModId = null
                    val idx = workshopMods.indexOfFirst { it.id == mod.id }
                    if (idx >= 0) {
                        val updated = workshopMods[idx].withReactions(res.reactions, res.reactionTotal)
                        workshopMods[idx] = updated
                        if (workshopDetailMod?.id == mod.id) {
                            workshopDetailMod = updated
                        }
                        if (emotePickerMod?.id == mod.id) {
                            emotePickerMod = updated
                        }
                    } else if (workshopDetailMod?.id == mod.id) {
                        workshopDetailMod = workshopDetailMod?.withReactions(res.reactions, res.reactionTotal)
                    }
                    if (res.userReactions.isNotEmpty()) {
                        platformUserReactions.clear()
                        platformUserReactions.putAll(res.userReactions)
                    } else if (res.viewerEmoteId.isEmpty()) {
                        platformUserReactions.remove(mod.id)
                    } else {
                        platformUserReactions[mod.id] = res.viewerEmoteId
                    }
                }
            } catch (e: PlatformAccountClient.UnauthorizedException) {
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    reactionBusyModId = null
                    clearPlatformSession()
                    pendingReactionAfterLogin = mod to cleanEmote
                    platformAuthError = e.message ?: "登录已失效，请重新登录"
                    showPlatformLoginDialog = true
                }
            } catch (e: Exception) {
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    reactionBusyModId = null
                    toast("表情回应失败：${e.message}")
                }
            }
        }
    }

    fun requestEmoteBitmap(emoteId: String) {
        val clean = emoteId.trim()
        if (clean.isEmpty() || !clean.matches(Regex("[A-Za-z0-9_.-]{1,80}"))) return
        val diskDir = File(appContext.cacheDir, "emotes")
        val diskFile = File(diskDir, "$clean.webp")
        if (!emoteFiles.containsKey(clean) && diskFile.isFile && diskFile.length() >= 16L) {
            emoteFiles[clean] = diskFile
        }
        val cached = emoteMemCache.get(clean)
        if (cached != null && diskFile.isFile) {
            if (!emoteBitmaps.containsKey(clean)) emoteBitmaps[clean] = cached
            return
        }
        synchronized(emotePending) {
            if (!emotePending.add(clean)) return
        }
        thumbWorker.execute {
            try {
                diskDir.mkdirs()
                val raw: ByteArray = if (diskFile.isFile && diskFile.length() in 16L..(2 * 1024 * 1024L)) {
                    diskFile.readBytes()
                } else {
                    val url = GameEmoteCatalog.imageUrl(clean)
                    val safeUrl = PayloadDownloadManager.validateOfficialUrl(url)
                    val conn = (safeUrl.openConnection() as HttpsURLConnection).apply {
                        instanceFollowRedirects = false
                        connectTimeout = 8000
                        readTimeout = 10000
                        requestMethod = "GET"
                    }
                    try {
                        if (conn.responseCode != 200) return@execute
                        val downloaded = conn.inputStream.use { input ->
                            val out = ByteArrayOutputStream()
                            val buf = ByteArray(8192)
                            var total = 0
                            while (true) {
                                val n = input.read(buf)
                                if (n == -1) break
                                total += n
                                if (total > 2 * 1024 * 1024) return@execute
                                out.write(buf, 0, n)
                            }
                            out.toByteArray()
                        }
                        try {
                            val tmp = File(diskDir, "$clean.webp.tmp")
                            FileOutputStream(tmp).use { it.write(downloaded) }
                            tmp.renameTo(diskFile)
                        } catch (_: Exception) { }
                        downloaded
                    } finally {
                        conn.disconnect()
                    }
                }
                val opts = BitmapFactory.Options().apply { inJustDecodeBounds = true }
                BitmapFactory.decodeByteArray(raw, 0, raw.size, opts)
                val maxDim = maxOf(opts.outWidth, opts.outHeight).coerceAtLeast(1)
                val sample = if (maxDim > 160) Integer.highestOneBit(maxDim / 80).coerceAtLeast(2) else 1
                val decodeOpts = BitmapFactory.Options().apply {
                    inSampleSize = sample
                    inPreferredConfig = Bitmap.Config.ARGB_8888
                }
                val bmp = BitmapFactory.decodeByteArray(raw, 0, raw.size, decodeOpts) ?: return@execute
                val img = bmp.asImageBitmap()
                emoteMemCache.put(clean, img)
                activity.runOnUiThread {
                    if (!disposed) {
                        emoteBitmaps[clean] = img
                        if (diskFile.isFile) {
                            emoteFiles[clean] = diskFile
                        }
                    }
                }
            } catch (_: Exception) {
            } finally {
                synchronized(emotePending) { emotePending.remove(clean) }
            }
        }
    }

    fun requestWorkshopThumbnail(url: String) {
        if (url.isBlank()) return
        val cached = thumbCache.get(url)
        if (cached != null) {
            if (!thumbnails.containsKey(url)) thumbnails[url] = cached
            return
        }
        synchronized(thumbPending) {
            if (!thumbPending.add(url)) return
        }
        thumbWorker.execute {
            try {
                val safeUrl = PayloadDownloadManager.validateOfficialUrl(url)
                val conn = (safeUrl.openConnection() as HttpsURLConnection).apply {
                    instanceFollowRedirects = false
                    connectTimeout = 8000
                    readTimeout = 10000
                    requestMethod = "GET"
                }
                try {
                    if (conn.responseCode != 200) return@execute
                    val raw = conn.inputStream.use { input ->
                        val out = ByteArrayOutputStream()
                        val buf = ByteArray(8192)
                        var total = 0
                        while (true) {
                            val n = input.read(buf)
                            if (n == -1) break
                            total += n
                            if (total > 2 * 1024 * 1024) return@execute
                            out.write(buf, 0, n)
                        }
                        out.toByteArray()
                    }
                    val opts = BitmapFactory.Options().apply { inJustDecodeBounds = true }
                    BitmapFactory.decodeByteArray(raw, 0, raw.size, opts)
                    val maxDim = maxOf(opts.outWidth, opts.outHeight).coerceAtLeast(1)
                    val sample = if (maxDim > 384) Integer.highestOneBit(maxDim / 192).coerceAtLeast(2) else 1
                    val decodeOpts = BitmapFactory.Options().apply {
                        inSampleSize = sample
                        inPreferredConfig = Bitmap.Config.RGB_565
                    }
                    val bmp = BitmapFactory.decodeByteArray(raw, 0, raw.size, decodeOpts) ?: return@execute
                    val img = bmp.asImageBitmap()
                    thumbCache.put(url, img)
                    activity.runOnUiThread {
                        if (!disposed) thumbnails[url] = img
                    }
                } finally {
                    conn.disconnect()
                }
            } catch (_: Exception) {
            } finally {
                synchronized(thumbPending) { thumbPending.remove(url) }
            }
        }
    }

    fun downloadAndInstallWorkshopMod(mod: AndroidWorkshopClient.WorkshopMod) {
        if (!mod.androidInstallable) {
            toast("该模组当前仅提供 PC 端资源包，可在网页版查看详情或等待作者发布手机版。")
            return
        }
        if (modTaskBusy) {
            toast("已有 Mod 下载安装任务正在进行中。")
            return
        }
        modTaskBusy = true
        val cacheDir = PayloadDownloadManager.cacheDir(appContext)
        val pkg = selectedPackageName
        modProgress = ModProgressState(mod.id, 0L, mod.sizeBytes, 0L, "正在准备下载 ${mod.name}…")

        workshopWorker.execute {
            try {
                val zipFile = PayloadDownloadManager.ensureDownloaded(
                    cacheDir,
                    mod.downloadUrl,
                    mod.sha256,
                    mod.sizeBytes,
                    mod.name,
                ) { received, total, speed, stage ->
                    activity.runOnUiThread {
                        modProgress = ModProgressState(mod.id, received, total, speed, stage)
                    }
                }

                if (!isPrivilegeReady()) {
                    activity.runOnUiThread {
                        modTaskBusy = false
                        modProgress = ModProgressState()
                        refreshCacheBytes()
                        toast("已下载并校验「${mod.name}」。连接无线调试或 Shizuku 后点击即可装入游戏。")
                    }
                    return@execute
                }

                activity.runOnUiThread {
                    modProgress = ModProgressState(
                        mod.id, mod.sizeBytes, mod.sizeBytes, 0L,
                        if (mod.isXspkgModule()) "正在写入并启用官方模块…" else "正在解压写入游戏 modimg/…",
                    )
                }

                val metaJson = MiniJson.stringify(
                    mapOf(
                        "id" to mod.id,
                        "name" to mod.name,
                        "version" to mod.version,
                        "sha256" to mod.sha256,
                        "installKind" to mod.installKind,
                    )
                )

                val respJson = if (useShizuku) {
                    ParcelFileDescriptor.open(zipFile, ParcelFileDescriptor.MODE_READ_ONLY).use { pfd ->
                        remoteBinder!!.installModPack(pkg, metaJson, pfd)
                    }
                } else {
                    requireLocalAdb().installModPack(pkg, metaJson, zipFile)
                }

                val parsed = MiniJson.parseObject(respJson)
                val listJson = if (useShizuku) remoteBinder!!.listInstalledMods(pkg) else requireLocalAdb().listInstalledMods(pkg)
                activity.runOnUiThread {
                    modTaskBusy = false
                    modProgress = ModProgressState()
                    refreshCacheBytes()
                    if (disposed) return@runOnUiThread
                    applyListedMods(listJson)
                    if (parsed["ok"] == true) {
                        val fileCount = MiniJson.longField(parsed, "fileCount", 0L)
                        toast("安装成功：${mod.name}（共写入 $fileCount 项）")
                    } else {
                        toast("安装未完成：${MiniJson.stringField(parsed, "error")}")
                    }
                }
            } catch (e: Exception) {
                activity.runOnUiThread {
                    modTaskBusy = false
                    modProgress = ModProgressState()
                    toast("安装「${mod.name}」失败：${e.message}")
                }
            }
        }
    }

    // ---- Appearance / Wallpaper Customization ----

    fun selectThemePreset(id: String) {
        themeId = id
        prefs.edit().putString("themeId", id).apply()
    }

    fun applyCustomHexColor(rawHex: String) {
        val norm = ThemePalette.normalizeHex(rawHex)
        customThemeHex = norm
        themeId = "custom"
        prefs.edit().putString("themeId", "custom").putString("customThemeHex", norm).apply()
        toast("已切换自定义主题色：$norm")
    }

    fun selectBrightnessMode(mode: ThemeBrightnessMode) {
        brightnessMode = mode
        prefs.edit()
            .putString("brightnessMode", mode.prefValue)
            .putBoolean("customThemeDark", mode == ThemeBrightnessMode.DARK)
            .apply()
    }

    fun updateWallpaperAlpha(alpha: Float) {
        val clamped = alpha.coerceIn(0.08f, 0.72f)
        wallpaperAlpha = clamped
        prefs.edit().putFloat("wallpaperAlpha", clamped).apply()
    }

    fun requestPickWallpaper() {
        onPickWallpaperRequest?.invoke()
    }

    fun importWallpaperUri(uri: Uri) {
        worker.execute {
            try {
                val dir = File(appContext.filesDir, "wallpaper").apply { mkdirs() }
                val dest = File(dir, "custom_wallpaper.bin")
                val tmp = File(dir, "custom_wallpaper.tmp")
                appContext.contentResolver.openInputStream(uri).use { input ->
                    if (input == null) throw IOException("无法读取所选图片")
                    FileOutputStream(tmp).use { output ->
                        val buf = ByteArray(32768)
                        var total = 0
                        while (true) {
                            val n = input.read(buf)
                            if (n == -1) break
                            total += n
                            if (total > 24 * 1024 * 1024) throw IOException("壁纸图片大小超出 24 MB 上限")
                            output.write(buf, 0, n)
                        }
                        output.flush()
                    }
                }
                if (dest.exists()) dest.delete()
                if (!tmp.renameTo(dest)) {
                    FileInputStream(tmp).use { i -> FileOutputStream(dest).use { o -> i.copyTo(o) } }
                    tmp.delete()
                }
                val img = decodeBoundedBitmap(dest, 1920)
                    ?: throw IOException("无法解码该图片格式")
                activity.runOnUiThread {
                    if (!disposed) {
                        wallpaperBitmap = img
                        prefs.edit().putBoolean("wallpaperEnabled", true).apply()
                        toast("已设置自定义启动器壁纸。")
                    }
                }
            } catch (e: Exception) {
                activity.runOnUiThread { toast("设置壁纸失败：${e.message}") }
            }
        }
    }

    fun clearWallpaper() {
        wallpaperBitmap = null
        prefs.edit().putBoolean("wallpaperEnabled", false).apply()
        File(appContext.filesDir, "wallpaper/custom_wallpaper.bin").delete()
        toast("已恢复默认纯净背景。")
    }

    // ---- Game Customize (Lobby Background, Fonts & Home UI Debug) ----

    fun requestPickLobbyBgImage() {
        if (!isPrivilegeReady()) {
            showConnectSheet = true
            toast("请先连接无线调试或 Shizuku，即可向游戏目录导入大厅背景。")
            return
        }
        onPickLobbyBgImageRequest?.invoke()
    }

    fun requestPickLobbyBgVideo() {
        if (!isPrivilegeReady()) {
            showConnectSheet = true
            toast("请先连接无线调试或 Shizuku，即可向游戏目录导入视频背景。")
            return
        }
        onPickLobbyBgVideoRequest?.invoke()
    }

    fun requestPickCustomFont() {
        if (!isPrivilegeReady()) {
            showConnectSheet = true
            toast("请先连接无线调试或 Shizuku，即可导入游戏自定义字体。")
            return
        }
        onPickCustomFontRequest?.invoke()
    }

    fun refreshCustomizeStateAsync(userInitiated: Boolean = false) {
        if (!isPrivilegeReady()) {
            if (userInitiated) {
                showConnectSheet = true
                toast("请先连接无线调试或 Shizuku，再同步游戏自定义配置。")
            }
            return
        }
        if (customizeLoading) return
        val pkg = selectedPackageName
        customizeLoading = true
        worker.execute {
            try {
                val bgJson = executeCustomizeOpBlocking(pkg, "lobbybg_state", "{}")
                val tuningJson = executeCustomizeOpBlocking(pkg, "tuning_get", "{}")
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    customizeLoading = false
                    applyLobbyBgStateJson(bgJson)
                    applyCustomizeTuningJson(tuningJson)
                    if (userInitiated) toast("已刷新游戏自定义配置与素材列表。")
                }
            } catch (e: Exception) {
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    customizeLoading = false
                    if (userInitiated) toast("读取游戏自定义配置失败：${e.message}")
                }
            }
        }
    }

    fun saveLobbyBgConfig(next: LobbyBgConfigState, successToast: String? = null) {
        lobbyBgConfig = next
        if (!isPrivilegeReady()) {
            showConnectSheet = true
            toast("请先连接无线调试或 Shizuku，才能写入游戏大厅背景配置。")
            return
        }
        if (customizeBusy) return
        val pkg = selectedPackageName
        val payload = lobbyBgConfigToJson(next)
        customizeBusy = true
        worker.execute {
            try {
                val resp = executeCustomizeOpBlocking(pkg, "lobbybg_save", payload)
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    customizeBusy = false
                    val err = applyLobbyBgStateJson(resp)
                    if (err != null) toast(err) else if (successToast != null) toast(successToast)
                }
            } catch (e: Exception) {
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    customizeBusy = false
                    toast("保存大厅背景配置失败：${e.message}")
                }
            }
        }
    }

    fun selectLobbyBgFile(fileName: String) {
        val next = lobbyBgConfig.copy(enabled = true, selectedFile = fileName)
        saveLobbyBgConfig(next, "已切换大厅背景：$fileName")
    }

    fun deleteLobbyBgFile(fileName: String) {
        if (!isPrivilegeReady()) {
            showConnectSheet = true
            toast("请先连接无线调试或 Shizuku，再删除背景素材。")
            return
        }
        if (customizeBusy) return
        val pkg = selectedPackageName
        val payload = JSONObject().put("fileName", fileName).toString()
        customizeBusy = true
        worker.execute {
            try {
                val resp = executeCustomizeOpBlocking(pkg, "lobbybg_delete", payload)
                deleteCachedLobbyBgPreview(fileName)
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    customizeBusy = false
                    val err = applyLobbyBgStateJson(resp)
                    if (err != null) toast(err) else toast("已删除背景素材：$fileName")
                }
            } catch (e: Exception) {
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    customizeBusy = false
                    toast("删除背景素材失败：${e.message}")
                }
            }
        }
    }

    fun resetLobbyBgToDefault() {
        val reset = LobbyBgConfigState(
            enabled = false,
            selectedFile = "",
            syncSubPanels = false,
            randomOnLobby = false,
            fillMode = "cover",
            brightness = 1.0f,
            speed = 1.0f,
            muted = true,
            volume = 0.70f,
            muteVanillaBgm = false,
        )
        saveLobbyBgConfig(reset, "已恢复游戏原版大厅背景设置。")
    }

    fun importLobbyBgMediaUri(uri: Uri, isVideo: Boolean) {
        if (!isPrivilegeReady()) {
            showConnectSheet = true
            toast("请先连接无线调试或 Shizuku，即可导入大厅背景。")
            return
        }
        if (customizeBusy) {
            toast("正在处理其他自定义任务，请稍候。")
            return
        }
        val pkg = selectedPackageName
        customizeBusy = true
        customizeProgressText = if (isVideo) "正在读取视频文件…" else "正在读取图片文件…"
        worker.execute {
            var tempFile: File? = null
            var xsvidTempFile: File? = null
            try {
                val rawName = resolveDisplayName(uri) ?: if (isVideo) "custom_lobby_bg.mp4" else "custom_lobby_bg.png"
                val safeName = sanitizeLocalImportFileName(rawName, if (isVideo) "custom_lobby_bg.mp4" else "custom_lobby_bg.png")
                val cacheRoot = File(appContext.cacheDir, "customize_stage").apply { mkdirs() }
                tempFile = File(cacheRoot, "import_lobby_${System.currentTimeMillis()}.bin")
                copyUriToFile(uri, tempFile, 300L * 1024L * 1024L)

                val needXsvidTranscode = AndroidLobbyVideoTranscoder.isTranscodableVideoFile(safeName)
                val targetFileName = if (needXsvidTranscode) {
                    replaceFileExtension(safeName, ".xsvid")
                } else {
                    safeName
                }

                val previewBmp = generateAndSaveMediaPreview(tempFile, safeName, targetFileName)

                var transcodeResult: AndroidLobbyVideoTranscoder.TranscodeResult? = null
                val uploadFile: File = if (needXsvidTranscode) {
                    xsvidTempFile = File(cacheRoot, "transcode_lobby_${System.currentTimeMillis()}.xsvid")
                    transcodeResult = AndroidLobbyVideoTranscoder.transcodeVideoToXsvid(tempFile, xsvidTempFile) { pct, msg ->
                        activity.runOnUiThread {
                            if (!disposed) customizeProgressText = "$msg ($pct%)"
                        }
                    }
                    xsvidTempFile
                } else {
                    tempFile
                }

                activity.runOnUiThread {
                    if (!disposed) customizeProgressText = "正在写入游戏目录 lobbybg/…"
                }
                val importJson = executeCustomizeImportBlocking(pkg, "lobbybg", targetFileName, uploadFile)
                val parsedImport = MiniJson.parseObject(importJson)
                val err = MiniJson.stringField(parsedImport, "error")
                if (err.isNotEmpty()) throw IOException(err)

                val importedName = MiniJson.stringField(parsedImport, "fileName").ifEmpty { targetFileName }
                val nextCfg = lobbyBgConfig.copy(enabled = true, selectedFile = importedName)
                val saveResp = executeCustomizeOpBlocking(pkg, "lobbybg_save", lobbyBgConfigToJson(nextCfg))

                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    customizeBusy = false
                    customizeProgressText = ""
                    if (previewBmp != null) lobbyBgPreviews[importedName] = previewBmp
                    val saveErr = applyLobbyBgStateJson(saveResp)
                    if (saveErr != null) {
                        toast(saveErr)
                    } else {
                        val suffix = if (transcodeResult != null) {
                            String.format(Locale.US, "（已转码完整 %.1f 秒 · %d 帧）", transcodeResult.durationSeconds, transcodeResult.frameCount)
                        } else if (needXsvidTranscode) {
                            "（已硬件转码为手机动态包）"
                        } else {
                            ""
                        }
                        toast("已导入并启用大厅背景：$importedName$suffix")
                    }
                }
            } catch (e: Exception) {
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    customizeBusy = false
                    customizeProgressText = ""
                    toast("导入大厅背景失败：${e.message}")
                }
            } finally {
                tempFile?.delete()
                xsvidTempFile?.delete()
            }
        }
    }

    private fun replaceFileExtension(name: String, newExt: String): String {
        val dot = name.lastIndexOf('.')
        val stem = if (dot > 0) name.substring(0, dot) else name
        return (stem.take(150).ifEmpty { "custom_lobby_bg" }) + newExt
    }

    fun saveCustomizeTuning(next: CustomizeTuningState, successToast: String? = null) {
        customizeTuning = next
        if (!isPrivilegeReady()) {
            showConnectSheet = true
            toast("请先连接无线调试或 Shizuku，再写入游戏自定义调试配置。")
            return
        }
        if (customizeBusy) return
        val pkg = selectedPackageName
        val payload = customizeTuningToJson(next)
        customizeBusy = true
        worker.execute {
            try {
                val resp = executeCustomizeOpBlocking(pkg, "tuning_save", payload)
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    customizeBusy = false
                    val err = applyCustomizeTuningJson(resp)
                    if (err != null) toast(err) else if (successToast != null) toast(successToast)
                }
            } catch (e: Exception) {
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    customizeBusy = false
                    toast("保存调试配置失败：${e.message}")
                }
            }
        }
    }

    fun resetHomeUiDebug() {
        val next = customizeTuning.copy(homeUiDebug = HomeUiDebugState())
        saveCustomizeTuning(next, "已恢复主界面默认调试参数。")
    }

    fun importCustomFontUri(uri: Uri) {
        if (!isPrivilegeReady()) {
            showConnectSheet = true
            toast("请先连接无线调试或 Shizuku，即可导入游戏字体。")
            return
        }
        if (customizeBusy) {
            toast("正在处理其他自定义任务，请稍候。")
            return
        }
        val pkg = selectedPackageName
        customizeBusy = true
        worker.execute {
            var tempFile: File? = null
            try {
                val rawName = resolveDisplayName(uri) ?: "custom_font.ttf"
                val safeName = sanitizeLocalImportFileName(rawName, "custom_font.ttf")
                val cacheRoot = File(appContext.cacheDir, "customize_stage").apply { mkdirs() }
                tempFile = File(cacheRoot, "import_font_${System.currentTimeMillis()}.bin")
                copyUriToFile(uri, tempFile, 32L * 1024L * 1024L)

                val importJson = executeCustomizeImportBlocking(pkg, "fonts", safeName, tempFile)
                val parsed = MiniJson.parseObject(importJson)
                val err = MiniJson.stringField(parsed, "error")
                if (err.isNotEmpty()) throw IOException(err)

                val importedName = MiniJson.stringField(parsed, "fileName").ifEmpty { safeName }
                val next = customizeTuning.copy(selectedFontFile = importedName)
                val saveResp = executeCustomizeOpBlocking(pkg, "tuning_save", customizeTuningToJson(next))

                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    customizeBusy = false
                    val saveErr = applyCustomizeTuningJson(saveResp)
                    if (saveErr != null) toast(saveErr) else toast("已导入并选中字体：$importedName")
                }
            } catch (e: Exception) {
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    customizeBusy = false
                    toast("导入字体失败：${e.message}")
                }
            } finally {
                tempFile?.delete()
            }
        }
    }

    private fun executeCustomizeOpBlocking(pkg: String, op: String, payloadJson: String): String {
        return if (useShizuku) {
            val binder = remoteBinder ?: throw IOException("Shizuku 服务未连接")
            binder.manageCustomize(pkg, op, payloadJson)
        } else {
            requireLocalAdb().manageCustomize(pkg, op, payloadJson)
        }
    }

    private fun executeCustomizeImportBlocking(pkg: String, targetKind: String, fileName: String, staged: File): String {
        return if (useShizuku) {
            val binder = remoteBinder ?: throw IOException("Shizuku 服务未连接")
            val pfd = ParcelFileDescriptor.open(staged, ParcelFileDescriptor.MODE_READ_ONLY)
            try {
                binder.importCustomizeFile(pkg, targetKind, fileName, pfd)
            } finally {
                try { pfd.close() } catch (_: Exception) {}
            }
        } else {
            requireLocalAdb().importCustomizeFile(pkg, targetKind, fileName, staged)
        }
    }

    private fun applyLobbyBgStateJson(json: String): String? {
        return try {
            val root = MiniJson.parseObject(json)
            val err = MiniJson.stringField(root, "error")
            if (err.isNotEmpty()) return err

            lobbyBgDir = MiniJson.stringField(root, "lobbyBgDir")
            val cfg = MiniJson.objectField(root, "config")
            if (cfg != null) {
                lobbyBgConfig = LobbyBgConfigState(
                    enabled = MiniJson.boolField(cfg, "enabled", false),
                    selectedFile = MiniJson.stringField(cfg, "selectedFile"),
                    syncSubPanels = MiniJson.boolField(cfg, "syncSubPanels", false),
                    randomOnLobby = MiniJson.boolField(cfg, "randomOnLobby", false),
                    fillMode = MiniJson.stringField(cfg, "fillMode").ifEmpty { "cover" },
                    brightness = ((cfg["brightness"] as? Number)?.toFloat() ?: 1.0f).coerceIn(0.30f, 1.0f),
                    speed = ((cfg["speed"] as? Number)?.toFloat() ?: 1.0f).coerceIn(0.50f, 2.0f),
                    muted = MiniJson.boolField(cfg, "muted", true),
                    volume = ((cfg["volume"] as? Number)?.toFloat() ?: 0.70f).coerceIn(0.0f, 1.0f),
                    muteVanillaBgm = MiniJson.boolField(cfg, "muteVanillaBgm", false),
                )
            }
            val parsedFiles = mutableListOf<LobbyBgMediaEntry>()
            val rawFiles = root["files"] as? List<*>
            rawFiles?.forEach { item ->
                @Suppress("UNCHECKED_CAST")
                val m = item as? Map<String, Any?> ?: return@forEach
                val name = MiniJson.stringField(m, "name")
                if (name.isNotEmpty()) {
                    parsedFiles.add(
                        LobbyBgMediaEntry(
                            name = name,
                            kind = MiniJson.stringField(m, "kind").ifEmpty { "image" },
                            sizeBytes = MiniJson.longField(m, "sizeBytes", 0L),
                            modifiedAt = MiniJson.longField(m, "modifiedAt", 0L),
                        )
                    )
                }
            }
            lobbyBgFiles.clear()
            lobbyBgFiles.addAll(parsedFiles)
            loadCachedLobbyBgPreviewsAsync(parsedFiles.map { it.name })
            null
        } catch (e: Exception) {
            e.message ?: "解析大厅背景配置失败"
        }
    }

    private fun applyCustomizeTuningJson(json: String): String? {
        return try {
            val root = MiniJson.parseObject(json)
            val err = MiniJson.stringField(root, "error")
            if (err.isNotEmpty()) return err

            fontsDir = MiniJson.stringField(root, "fontsDir")
            val t = MiniJson.objectField(root, "tuning")
            if (t != null) {
                val hd = MiniJson.objectField(t, "homeUiDebug")
                val homeDebug = HomeUiDebugState(
                    hideVanillaBgAnim = MiniJson.boolField(hd, "hideVanillaBgAnim", false),
                    compactLobbyBadges = MiniJson.boolField(hd, "compactLobbyBadges", false),
                    showDebugOutline = MiniJson.boolField(hd, "showDebugOutline", false),
                    customHudOpacity = MiniJson.longField(hd, "customHudOpacity", 100L).toInt().coerceIn(40, 100),
                    hideLobbyAds = MiniJson.boolField(hd, "hideLobbyAds", false),
                )
                customizeTuning = CustomizeTuningState(
                    activeCardProfile = MiniJson.stringField(t, "activeCardProfile").ifEmpty { null },
                    cardProfiles = MiniJson.listField(t, "cardProfiles").mapNotNull { it?.toString()?.takeIf(String::isNotBlank) },
                    activeAudioProfile = MiniJson.stringField(t, "activeAudioProfile").ifEmpty { null },
                    audioProfiles = MiniJson.listField(t, "audioProfiles").mapNotNull { it?.toString()?.takeIf(String::isNotBlank) },
                    activeCharacterProfile = MiniJson.stringField(t, "activeCharacterProfile").ifEmpty { null },
                    characterProfiles = MiniJson.listField(t, "characterProfiles").mapNotNull { it?.toString()?.takeIf(String::isNotBlank) },
                    selectedFontFamily = MiniJson.stringField(t, "selectedFontFamily").ifEmpty { null },
                    selectedFontFile = MiniJson.stringField(t, "selectedFontFile").ifEmpty { null },
                    fontCjkOnly = MiniJson.boolField(t, "fontCjkOnly", false),
                    fontReplaceHeadings = MiniJson.boolField(t, "fontReplaceHeadings", true),
                    homeUiDebug = homeDebug,
                )
            }
            val parsedFonts = mutableListOf<CustomFontFileEntry>()
            val rawFonts = root["fontFiles"] as? List<*>
            rawFonts?.forEach { item ->
                @Suppress("UNCHECKED_CAST")
                val m = item as? Map<String, Any?> ?: return@forEach
                val name = MiniJson.stringField(m, "name")
                if (name.isNotEmpty()) {
                    parsedFonts.add(CustomFontFileEntry(name, MiniJson.longField(m, "sizeBytes", 0L)))
                }
            }
            customFontFiles.clear()
            customFontFiles.addAll(parsedFonts)
            null
        } catch (e: Exception) {
            e.message ?: "解析游戏自定义调试配置失败"
        }
    }

    private fun lobbyBgConfigToJson(c: LobbyBgConfigState): String {
        return JSONObject().apply {
            put("enabled", c.enabled)
            put("selectedFile", c.selectedFile)
            put("syncSubPanels", c.syncSubPanels)
            put("randomOnLobby", c.randomOnLobby)
            put("fillMode", c.fillMode)
            put("brightness", c.brightness.toDouble())
            put("speed", c.speed.toDouble())
            put("muted", c.muted)
            put("volume", c.volume.toDouble())
            put("muteVanillaBgm", c.muteVanillaBgm)
        }.toString()
    }

    private fun customizeTuningToJson(t: CustomizeTuningState): String {
        val debugObj = JSONObject().apply {
            put("hideVanillaBgAnim", t.homeUiDebug.hideVanillaBgAnim)
            put("compactLobbyBadges", t.homeUiDebug.compactLobbyBadges)
            put("showDebugOutline", t.homeUiDebug.showDebugOutline)
            put("customHudOpacity", t.homeUiDebug.customHudOpacity)
            put("hideLobbyAds", t.homeUiDebug.hideLobbyAds)
        }
        return JSONObject().apply {
            put("activeCardProfile", t.activeCardProfile ?: JSONObject.NULL)
            put("cardProfiles", org.json.JSONArray(t.cardProfiles))
            put("activeAudioProfile", t.activeAudioProfile ?: JSONObject.NULL)
            put("audioProfiles", org.json.JSONArray(t.audioProfiles))
            put("activeCharacterProfile", t.activeCharacterProfile ?: JSONObject.NULL)
            put("characterProfiles", org.json.JSONArray(t.characterProfiles))
            put("selectedFontFamily", t.selectedFontFamily ?: JSONObject.NULL)
            put("selectedFontFile", t.selectedFontFile ?: JSONObject.NULL)
            put("fontCjkOnly", t.fontCjkOnly)
            put("fontReplaceHeadings", t.fontReplaceHeadings)
            put("homeUiDebug", debugObj)
        }.toString()
    }

    private fun copyUriToFile(uri: Uri, dest: File, maxBytes: Long) {
        appContext.contentResolver.openInputStream(uri).use { input ->
            if (input == null) throw IOException("无法打开所选文件")
            FileOutputStream(dest).use { output ->
                val buf = ByteArray(65536)
                var total = 0L
                while (true) {
                    val n = input.read(buf)
                    if (n == -1) break
                    total += n
                    if (total > maxBytes) {
                        throw IOException("文件大小超出 ${maxBytes / (1024L * 1024L)} MB 上限")
                    }
                    output.write(buf, 0, n)
                }
                output.flush()
            }
        }
    }

    private fun sanitizeLocalImportFileName(raw: String, fallback: String): String {
        val cleaned = raw.replace('/', '_').replace('\\', '_').replace(':', '_').replace("..", "_").trim()
        return if (cleaned.isEmpty()) fallback else cleaned.take(160)
    }

    private fun lobbyBgPreviewFile(fileName: String): File {
        val digest = MessageDigest.getInstance("SHA-256").digest(fileName.toByteArray(Charsets.UTF_8))
        val hex = digest.joinToString("") { "%02x".format(it) }.take(24)
        val dir = File(appContext.filesDir, "lobbybg_previews").apply { mkdirs() }
        return File(dir, "$hex.jpg")
    }

    private fun deleteCachedLobbyBgPreview(fileName: String) {
        try {
            lobbyBgPreviewFile(fileName).delete()
            lobbyBgPreviews.remove(fileName)
        } catch (_: Exception) { }
    }

    private fun generateAndSaveMediaPreview(
        mediaFile: File,
        sourceNameForDetect: String,
        previewKeyName: String = sourceNameForDetect,
    ): ImageBitmap? {
        return try {
            val lower = sourceNameForDetect.lowercase(Locale.ROOT)
            val rawBmp: Bitmap? = if (lower.endsWith(".mp4") || lower.endsWith(".mov") || lower.endsWith(".webm") || lower.endsWith(".mkv") || lower.endsWith(".avi") || lower.endsWith(".m4v")) {
                val mmr = android.media.MediaMetadataRetriever()
                try {
                    mmr.setDataSource(mediaFile.absolutePath)
                    mmr.getFrameAtTime(1_000_000L, android.media.MediaMetadataRetriever.OPTION_CLOSEST_SYNC)
                        ?: mmr.getFrameAtTime(0L)
                } finally {
                    try { mmr.release() } catch (_: Exception) { }
                }
            } else if (lower.endsWith(".usm")) {
                null
            } else {
                val bounds = BitmapFactory.Options().apply { inJustDecodeBounds = true }
                BitmapFactory.decodeFile(mediaFile.absolutePath, bounds)
                val largest = maxOf(bounds.outWidth, bounds.outHeight).coerceAtLeast(1)
                var sample = 1
                while (largest / sample > 960) sample *= 2
                BitmapFactory.decodeFile(
                    mediaFile.absolutePath,
                    BitmapFactory.Options().apply {
                        inSampleSize = sample
                        inPreferredConfig = Bitmap.Config.ARGB_8888
                    }
                )
            }
            if (rawBmp == null) return null
            val scale = if (rawBmp.width > 960) 960f / rawBmp.width.toFloat() else 1f
            val finalBmp = if (scale < 0.99f) {
                Bitmap.createScaledBitmap(rawBmp, (rawBmp.width * scale).toInt().coerceAtLeast(1), (rawBmp.height * scale).toInt().coerceAtLeast(1), true)
            } else {
                rawBmp
            }
            val dest = lobbyBgPreviewFile(previewKeyName)
            FileOutputStream(dest).use { out ->
                finalBmp.compress(Bitmap.CompressFormat.JPEG, 85, out)
                out.flush()
            }
            finalBmp.asImageBitmap()
        } catch (_: Exception) {
            null
        }
    }

    private fun loadCachedLobbyBgPreviewsAsync(names: List<String>) {
        if (names.isEmpty()) return
        thumbWorker.execute {
            for (name in names) {
                if (lobbyBgPreviews.containsKey(name)) continue
                val file = lobbyBgPreviewFile(name)
                if (!file.isFile) continue
                val bmp = decodeBoundedBitmap(file, 960) ?: continue
                activity.runOnUiThread {
                    if (!disposed) lobbyBgPreviews[name] = bmp
                }
            }
        }
    }

    private fun loadSavedWallpaperAsync() {
        if (!prefs.getBoolean("wallpaperEnabled", false)) return
        val file = File(appContext.filesDir, "wallpaper/custom_wallpaper.bin")
        if (!file.isFile) return
        worker.execute {
            val img = decodeBoundedBitmap(file, 1920) ?: return@execute
            activity.runOnUiThread {
                if (!disposed) wallpaperBitmap = img
            }
        }
    }

    private fun decodeBoundedBitmap(file: File, maxSidePx: Int): ImageBitmap? {
        return try {
            val bounds = BitmapFactory.Options().apply { inJustDecodeBounds = true }
            BitmapFactory.decodeFile(file.absolutePath, bounds)
            val largest = maxOf(bounds.outWidth, bounds.outHeight).coerceAtLeast(1)
            var sample = 1
            while (largest / sample > maxSidePx) {
                sample *= 2
            }
            val opts = BitmapFactory.Options().apply {
                inSampleSize = sample
                inPreferredConfig = Bitmap.Config.ARGB_8888
            }
            BitmapFactory.decodeFile(file.absolutePath, opts)?.asImageBitmap()
        } catch (_: Exception) {
            null
        }
    }

    // ---- Account & Token Auth ----

    fun handleIntent(intent: Intent?) {
        if (intent == null) return
        if (Intent.ACTION_VIEW != intent.action || intent.data == null) return
        val uri = intent.data!!
        if ("xingshu-installer" == uri.scheme && "sync-game" == uri.host) {
            val pkgParam = uri.getQueryParameter("pkg")
            if (pkgParam != null && GamePackageCatalog.isValidPackageName(pkgParam)) {
                selectChannel(pkgParam)
            }
            val uidParam = uri.getQueryParameter("uid")
            if (!uidParam.isNullOrBlank()) {
                toast("已从游戏内跳转（UID $uidParam），可完成账号云端同步。")
            }
            activeTab = NavTab.SETTINGS
            return
        }
        try {
            val handoff = AssistantLoginLink.parse(uri.toString(), intent.getStringExtra("handoff"))
            pendingQqHandoff = handoff
            pendingQqRequester = "正在读取 QQ 请求信息…"
            activeTab = NavTab.SETTINGS
            authWorker.execute {
                val requester = try {
                    MobileAuthHub.qqHandoffRequester(handoff)
                } catch (_: Exception) {
                    "QQ 快速登录请求"
                }
                activity.runOnUiThread {
                    if (!disposed) pendingQqRequester = requester
                }
            }
        } catch (_: Exception) {
        }
    }

    fun startWebLogin() {
        if (authBusy) return
        authBusy = true
        webPollCancelled = false
        authStatusText = "正在向星枢服务器申请官网登录页…"
        val devId = MobileAuthHub.getOrCreateDeviceId(prefs)
        authWorker.execute {
            try {
                val start = MobileAuthHub.startWebDeviceLogin(devId)
                MobileAuthHub.savePendingWebLogin(prefs, start, System.currentTimeMillis() + 5 * 60_000L)
                activity.runOnUiThread {
                    if (disposed) return@runOnUiThread
                    webLoginUrl = start.loginUrl
                    authStatusText = "已打开官方授权页面，请在浏览器中完成登录并回到本应用。"
                    openExternalBrowser(start.loginUrl)
                }
                pollWebLoginLoop(start.requestId, start.verifier)
            } catch (e: Exception) {
                activity.runOnUiThread {
                    authBusy = false
                    authStatusText = "发起登录失败：${e.message}"
                }
            }
        }
    }

    private fun resumePendingWebAuthIfNeeded() {
        if (authBusy) return
        val pending = MobileAuthHub.loadPendingWebLogin(prefs, System.currentTimeMillis()) ?: return
        authBusy = true
        webPollCancelled = false
        authStatusText = "正在恢复浏览器授权状态查询…"
        authWorker.execute {
            pollWebLoginLoop(pending.requestId, pending.verifier)
        }
    }

    private fun pollWebLoginLoop(requestId: String, verifier: String) {
        try {
            val deadline = SystemClock.elapsedRealtime() + 180_000L
            while (SystemClock.elapsedRealtime() < deadline && !disposed && !webPollCancelled) {
                val poll = try {
                    MobileAuthHub.pollDeviceLogin(requestId, verifier)
                } catch (_: IOException) {
                    Thread.sleep(1600)
                    continue
                }
                if ("succeeded" == poll.status && poll.account != null) {
                    MobileAuthHub.clearPendingWebLogin(prefs)
                    MobileAuthHub.saveSession(prefs, poll.account)
                    val sum = try { MobileAuthHub.fetchAccountSummary(poll.account.token) } catch (_: Exception) { "" }
                    activity.runOnUiThread {
                        if (disposed) return@runOnUiThread
                        authBusy = false
                        webLoginUrl = ""
                        boundSession = poll.account
                        cloudSummary = sum
                        authStatusText = "已登录星枢账号：${poll.account.nick}（UID ${poll.account.uid}）"
                    }
                    return
                }
                if ("failed" == poll.status || "expired" == poll.status || "superseded" == poll.status) {
                    MobileAuthHub.clearPendingWebLogin(prefs)
                    throw IOException(poll.error.ifEmpty { "登录未完成或已过期" })
                }
                Thread.sleep(1600)
            }
            activity.runOnUiThread {
                authBusy = false
                if (webPollCancelled) authStatusText = "已取消网页登录。"
            }
        } catch (e: Exception) {
            activity.runOnUiThread {
                authBusy = false
                authStatusText = "网页登录未完成：${e.message}"
            }
        }
    }

    fun cancelWebLogin() {
        webPollCancelled = true
        authBusy = false
        webLoginUrl = ""
        MobileAuthHub.clearPendingWebLogin(prefs)
        authStatusText = "已取消网页登录。"
    }

    fun refreshCloudAccountAsync() {
        val current = boundSession ?: return
        authWorker.execute {
            try {
                val sum = MobileAuthHub.fetchAccountSummary(current.token)
                activity.runOnUiThread {
                    if (!disposed) cloudSummary = sum
                }
            } catch (e: Exception) {
                if ("UNAUTHORIZED" == e.message) {
                    MobileAuthHub.clearSession(prefs)
                    activity.runOnUiThread {
                        if (!disposed) {
                            boundSession = null
                            cloudSummary = ""
                            authStatusText = "星枢登录状态已过期，请重新登录。"
                        }
                    }
                }
            }
        }
    }

    fun scanChannelTokens() {
        if (authBusy) return
        if (!isPrivilegeReady()) {
            showConnectSheet = true
            toast("读取渠道登录日志需要先连接无线调试或 Shizuku。")
            return
        }
        authBusy = true
        authStatusText = "正在扫描手机游戏渠道登录票据…"
        authWorker.execute {
            try {
                val raw = if (useShizuku) remoteBinder!!.readChannelLogcat() else requireLocalAdb().readChannelLogcat()
                val tokens = BnSdkTokenExtractor.extractFromLogcat(raw, BnSdkTokenExtractor.appIdForPackage(selectedPackageName))
                activity.runOnUiThread {
                    authBusy = false
                    detectedTokens.clear()
                    detectedTokens.addAll(tokens)
                    selectedTokenIndex = 0
                    authStatusText = if (tokens.isEmpty()) {
                        "暂未发现有效的 BN_DEBUG 登录票据；请先启动渠道游戏登录进大厅一次后再扫描。"
                    } else {
                        "已发现 ${tokens.size} 条渠道票据，可选中后登录星枢或绑定 QQ。"
                    }
                }
            } catch (e: Exception) {
                activity.runOnUiThread {
                    authBusy = false
                    authStatusText = "扫描票据失败：${e.message}"
                }
            }
        }
    }

    fun startChannelHubAuth() {
        val tok = detectedTokens.getOrNull(selectedTokenIndex) ?: return
        if (authBusy) return
        authBusy = true
        webPollCancelled = false
        authStatusText = "正在通过「${tok.label}」票据登录星枢…"
        val devId = MobileAuthHub.getOrCreateDeviceId(prefs)
        authWorker.execute {
            try {
                val start = MobileAuthHub.startChannelHubLogin(devId, tok)
                pollWebLoginLoop(start.requestId, start.verifier)
            } catch (e: Exception) {
                activity.runOnUiThread {
                    authBusy = false
                    authStatusText = "渠道授权失败：${e.message}"
                }
            }
        }
    }

    fun bindQqWithCurrentSession() {
        val acct = boundSession ?: return
        val handoff = pendingQqHandoff
        if (authBusy || handoff.isBlank()) return
        authBusy = true
        authWorker.execute {
            try {
                val updated = MobileAuthHub.bindCurrentSessionToQq(handoff, acct)
                MobileAuthHub.saveSession(prefs, updated)
                activity.runOnUiThread {
                    authBusy = false
                    boundSession = updated
                    pendingQqHandoff = ""
                    pendingQqRequester = ""
                    authStatusText = "已将账号 ${updated.nick}（UID ${updated.uid}）绑定至 QQ。"
                    toast("QQ 绑定成功！")
                }
            } catch (e: Exception) {
                activity.runOnUiThread {
                    authBusy = false
                    toast("绑定未完成：${e.message}")
                }
            }
        }
    }

    fun dismissQqHandoff() {
        pendingQqHandoff = ""
        pendingQqRequester = ""
    }

    fun bindQqLinkWithSelectedToken() {
        val tok = detectedTokens.getOrNull(selectedTokenIndex) ?: return
        val link = qqLinkInput.trim()
        if (authBusy || link.isBlank()) return
        try {
            BnSdkTokenExtractor.parseLoginLink(link)
        } catch (e: Exception) {
            authStatusText = e.message ?: "QQ 登录链接格式无效"
            return
        }
        authBusy = true
        authStatusText = "正在验证 ${tok.label} 票据并绑定 QQ…"
        authWorker.execute {
            try {
                val res = MobileAuthHub.completeQqLinkWithChannelToken(link, tok)
                activity.runOnUiThread {
                    authBusy = false
                    authStatusText = "QQ 绑定成功：${res.nick}（UID ${res.uid}）"
                    toast("QQ Bot 绑定成功！")
                }
            } catch (e: Exception) {
                activity.runOnUiThread {
                    authBusy = false
                    authStatusText = "QQ 绑定失败：${e.message}"
                }
            }
        }
    }

    fun logoutAccount() {
        MobileAuthHub.clearSession(prefs)
        boundSession = null
        cloudSummary = ""
        authStatusText = "已退出星枢账号。"
    }

    // ---- Self-Update & Cache / Diagnostics ----

    fun setPreviewUpdatesEnabled(enabled: Boolean) {
        previewUpdates = enabled
        prefs.edit().putBoolean("previewUpdates", enabled).apply()
    }

    fun checkAssistantUpdate(silent: Boolean) {
        if (!updateTaskActive.compareAndSet(false, true)) return
        if (!silent) updateStatusText = "正在检查官方最新发行版…"
        updateWorker.execute {
            try {
                val found = ReleaseUpdates.check(
                    previewUpdates,
                    { appContext.assets.open("update-public-cert.txt") },
                    { stage -> if (!silent) activity.runOnUiThread { updateStatusText = stage } },
                )
                activity.runOnUiThread {
                    updateTaskActive.set(false)
                    if (disposed) return@runOnUiThread
                    if (found != null && found.code() > BuildConfig.VERSION_CODE) {
                        availableUpdate = found
                        updateStatusText = "发现新版 v${found.version()}（当前 ${BuildConfig.VERSION_NAME}）"
                        showUpdateModal = true
                    } else if (!silent) {
                        updateStatusText = "当前已是最新版本 v${BuildConfig.VERSION_NAME}"
                        toast("当前已是最新版本")
                    }
                }
            } catch (e: Exception) {
                activity.runOnUiThread {
                    updateTaskActive.set(false)
                    if (!silent && !disposed) {
                        updateStatusText = ReleaseUpdates.failureMessage(e)
                    }
                }
            }
        }
    }

    fun downloadAvailableUpdate() {
        val upd = availableUpdate ?: return
        if (!updateTaskActive.compareAndSet(false, true)) return
        updateDownloading = true
        updateVerifying = false
        updateProgressPct = 0
        updateWorker.execute {
            try {
                val apk = ReleaseUpdates.download(
                    upd,
                    appContext.cacheDir,
                    object : ReleaseUpdates.DownloadListener {
                        override fun progress(read: Long, total: Long) {
                            val pct = if (total > 0L) ((read * 100L) / total).toInt().coerceIn(0, 100) else 0
                            activity.runOnUiThread {
                                updateProgressPct = pct
                                updateStatusText = "正在下载新版 APK… $pct%"
                            }
                        }

                        override fun verifying() {
                            activity.runOnUiThread {
                                updateVerifying = true
                                updateStatusText = "正在校验 APK SHA-256 与签名…"
                            }
                        }
                    },
                )
                verifyDownloadedApk(apk, upd)
                activity.runOnUiThread {
                    updateTaskActive.set(false)
                    updateDownloading = false
                    updateVerifying = false
                    updateProgressPct = 100
                    showUpdateModal = false
                    requestInstallPendingUpdateApk()
                }
            } catch (e: Exception) {
                activity.runOnUiThread {
                    updateTaskActive.set(false)
                    updateDownloading = false
                    updateVerifying = false
                    updateStatusText = "更新失败：${ReleaseUpdates.failureMessage(e)}"
                }
            }
        }
    }

    private fun verifyDownloadedApk(apk: File, update: ReleaseUpdates.Update) {
        val digest = MessageDigest.getInstance("SHA-256")
        FileInputStream(apk).use { input ->
            val buf = ByteArray(65536)
            while (true) {
                val n = input.read(buf)
                if (n == -1) break
                digest.update(buf, 0, n)
            }
        }
        val expected = update.manifest.getJSONObject("android").getString("sha256")
        if (ReleaseUpdates.hex(digest.digest()) != expected) {
            throw IOException("APK 文件哈希已变化")
        }
    }

    private fun requestInstallPendingUpdateApk() {
        val apk = File(appContext.cacheDir, "update.apk")
        val upd = availableUpdate ?: return
        if (!apk.isFile) return
        try {
            verifyDownloadedApk(apk, upd)
            if (Build.VERSION.SDK_INT >= 26 && !appContext.packageManager.canRequestPackageInstalls()) {
                prefs.edit().putBoolean("waitingInstallPermission", true).apply()
                activity.startActivity(
                    Intent(
                        Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,
                        Uri.parse("package:${appContext.packageName}"),
                    )
                )
                toast("请先在系统设置中授予安装应用权限。")
                return
            }
            val uri = Uri.parse("content://${BuildConfig.APPLICATION_ID}.update/update.apk")
            activity.startActivity(
                Intent(Intent.ACTION_VIEW)
                    .setDataAndType(uri, "application/vnd.android.package-archive")
                    .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_ACTIVITY_NEW_TASK)
            )
        } catch (e: Exception) {
            toast("未能拉起系统安装界面：${e.message}")
        }
    }

    fun refreshCacheBytes() {
        cacheBytes = PayloadDownloadManager.getCacheTotalBytes(PayloadDownloadManager.cacheDir(appContext))
    }

    fun clearDownloadCache() {
        worker.execute {
            val freed = PayloadDownloadManager.clearCache(PayloadDownloadManager.cacheDir(appContext))
            activity.runOnUiThread {
                refreshCacheBytes()
                toast("已清理缓存 ${PayloadDownloadManager.formatBytes(freed)}。")
            }
        }
    }

    fun exportDiagnosticLog() {
        val ts = SimpleDateFormat("yyyyMMdd-HHmmss", Locale.CHINA).format(Date())
        onExportLogRequest?.invoke("XingShu-Android-$ts.log")
    }

    fun writeDiagnosticLogToUri(uri: Uri) {
        try {
            appContext.contentResolver.openOutputStream(uri)?.use { out ->
                out.write(buildDiagnosticReport().toByteArray(Charsets.UTF_8))
                out.flush()
            }
            toast("已保存诊断报告文件。")
        } catch (e: Exception) {
            toast("保存诊断日志失败：${e.message}")
        }
    }

    fun copyDiagnosticLogToClipboard() {
        val cm = appContext.getSystemService(Context.CLIPBOARD_SERVICE) as? ClipboardManager
        cm?.setPrimaryClip(ClipData.newPlainText("XingShu Diagnostics", buildDiagnosticReport()))
        toast("已复制诊断信息到剪贴板。")
    }

    fun buildDiagnosticReport(): String = buildString {
        appendLine("=== 星枢安卓启动器 · 诊断报告 ===")
        appendLine("版本：${BuildConfig.VERSION_NAME} (code=${BuildConfig.VERSION_CODE})")
        appendLine("官方插件目标组件：${coreSpec.runtimeVersion}（适配游戏 ${coreSpec.gameVersion} · 来源 ${coreSpec.source}）")
        appendLine("设备：${Build.MODEL} / Android ${Build.VERSION.RELEASE} (API ${Build.VERSION.SDK_INT})")
        appendLine("选中渠道包名：$selectedPackageName")
        appendLine("已发现渠道数：${channels.size}")
        appendLine("授权通道：${if (useShizuku) "Shizuku(${remoteBinder != null})" else "LocalADB($adbConnected)"}")
        appendLine("已开启官方模组数：${OfficialModulesCatalog.ALL.count { officialModuleStates[it.id] == "enabled" }} / ${OfficialModulesCatalog.ALL.size}" +
            if (legacyModuleIds.isNotEmpty()) "（另有旧版残留：${legacyModuleIds.joinToString(",")}）" else "")
        if (lastResultText.isNotBlank()) {
            appendLine()
            appendLine("--- 最近检查/执行结果 ---")
            appendLine(lastResultText)
        }
        appendLine()
        appendLine("--- 运行日志 ---")
        logs.forEach { appendLine(it) }
    }

    fun openExternalBrowser(url: String) {
        try {
            activity.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url)))
        } catch (_: Exception) {
            toast("未能打开系统浏览器")
        }
    }

    private fun resolveDisplayName(uri: Uri): String? {
        return try {
            appContext.contentResolver.query(uri, arrayOf(OpenableColumns.DISPLAY_NAME), null, null, null)?.use { c ->
                if (c.moveToFirst()) c.getString(0) else null
            }
        } catch (_: Exception) {
            null
        }
    }

    // ---- JSON parsers ----

    private fun applyScannedPackages(scanJson: String) {
        try {
            val root = MiniJson.parseObject(scanJson)
            val err = MiniJson.stringField(root, "error")
            if (err.isNotEmpty()) {
                appendLog("渠道扫描异常：$err")
                return
            }
            val rawList = root["packages"] as? List<*> ?: return
            val parsed = mutableListOf<ChannelEntry>()
            for (obj in rawList) {
                val map = obj as? Map<*, *> ?: continue
                @Suppress("UNCHECKED_CAST")
                val m = map as Map<String, Any?>
                val pkg = MiniJson.stringField(m, "packageName")
                if (!GamePackageCatalog.isValidPackageName(pkg)) continue
                parsed.add(
                    ChannelEntry(
                        packageName = pkg,
                        label = MiniJson.stringField(m, "label").ifEmpty { GamePackageCatalog.classifyPackage(pkg).label },
                        platform = MiniJson.stringField(m, "platform"),
                        versionName = MiniJson.stringField(m, "versionName"),
                        hasCatalog = MiniJson.boolField(m, "hasCatalog", false),
                        running = MiniJson.boolField(m, "running", false),
                        installState = MiniJson.stringField(m, "installState"),
                        resolvedRel = MiniJson.stringField(m, "resolvedRel"),
                    )
                )
            }
            if (parsed.isNotEmpty()) {
                channels.clear()
                channels.addAll(parsed)
                if (parsed.none { it.packageName == selectedPackageName }) {
                    selectedPackageName = parsed.first().packageName
                }
            }
        } catch (_: Exception) {
        }
    }

    private fun applyListedMods(listJson: String) {
        try {
            val root = MiniJson.parseObject(listJson)
            if (root["ok"] != true) return
            applyOfficialModulesMap(root["officialModules"])
            activeModuleCount = MiniJson.longField(root, "activeModuleCount", 0L).toInt()
            totalModPngCount = MiniJson.longField(root, "totalPngCount", 0L).toInt()

            val rawMods = root["mods"] as? List<*>
            val parsed = mutableListOf<InstalledModEntry>()
            if (rawMods != null) {
                for (obj in rawMods) {
                    val map = obj as? Map<*, *> ?: continue
                    @Suppress("UNCHECKED_CAST")
                    val m = map as Map<String, Any?>
                    val id = MiniJson.stringField(m, "id")
                    if (id.isEmpty()) continue
                    parsed.add(
                        InstalledModEntry(
                            id = id,
                            name = MiniJson.stringField(m, "name"),
                            version = MiniJson.stringField(m, "version"),
                            sha256 = MiniJson.stringField(m, "sha256"),
                            installKind = MiniJson.stringField(m, "installKind"),
                            declaredCount = MiniJson.longField(m, "declaredCount", 0L).toInt(),
                            presentCount = MiniJson.longField(m, "presentCount", 0L).toInt(),
                        )
                    )
                }
            }
            installedMods.clear()
            installedMods.addAll(parsed)
        } catch (_: Exception) {
        }
    }

    private fun applyOfficialModulesMap(raw: Any?) {
        officialModuleStates.clear()
        legacyModuleIds.clear()
        val map = raw as? Map<*, *> ?: run {
            activeModuleCount = 0
            return
        }
        val legacySeen = linkedSetOf<String>()
        for ((k, v) in map) {
            val id = k?.toString()?.trim()?.lowercase(Locale.ROOT) ?: continue
            val state = v?.toString()?.trim()?.lowercase(Locale.ROOT) ?: continue
            if (!OfficialModulesCatalog.isSafeId(id)) continue
            if (OfficialModulesCatalog.CANONICAL_IDS.contains(id)) {
                officialModuleStates[id] = state
            } else {
                legacySeen.add(id)
            }
        }
        legacyModuleIds.addAll(legacySeen.sorted())
        activeModuleCount = OfficialModulesCatalog.ALL.count { officialModuleStates[it.id] == "enabled" }
    }
}

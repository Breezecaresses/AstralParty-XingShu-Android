package app.xingshu.installer

import androidx.compose.animation.core.Spring
import androidx.compose.animation.core.animateDpAsState
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.spring
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.AutoAwesome
import androidx.compose.material.icons.outlined.Explore
import androidx.compose.material.icons.outlined.GridView
import androidx.compose.material.icons.outlined.PlayCircle
import androidx.compose.material.icons.outlined.Tune
import androidx.compose.material.icons.rounded.AdminPanelSettings
import androidx.compose.material.icons.rounded.AutoAwesome
import androidx.compose.material.icons.rounded.Close
import androidx.compose.material.icons.rounded.Explore
import androidx.compose.material.icons.rounded.GridView
import androidx.compose.material.icons.rounded.PlayCircle
import androidx.compose.material.icons.rounded.Security
import androidx.compose.material.icons.rounded.Tune
import androidx.compose.material.icons.rounded.Usb
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.AssistChip
import androidx.compose.material3.Button
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilledTonalButton
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.material3.VerticalDivider
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.scale
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties

@OptIn(ExperimentalMaterial3Api::class)
@Composable
internal fun XingShuMobileApp(controller: AppController) {
    XingShuTheme(
        themeId = controller.themeId,
        customHex = controller.customThemeHex,
        brightnessMode = controller.brightnessMode,
    ) {
        val snackbarHostState = remember { SnackbarHostState() }
        val currentSnack = controller.snackbarMessage
        LaunchedEffect(currentSnack) {
            if (currentSnack != null) {
                snackbarHostState.showSnackbar(currentSnack)
                controller.consumeSnackbar()
            }
        }

        val hasWallpaper = controller.wallpaperBitmap != null
        val config = LocalConfiguration.current
        val isTablet = config.screenWidthDp >= 600
        val isExpandedRail = config.screenWidthDp >= 780

        val onSelectTab: (NavTab) -> Unit = { tab ->
            controller.activeTab = tab
            if (tab == NavTab.WORKSHOP) {
                controller.refreshWorkshopIfStale(5_000L)
            } else if (tab == NavTab.CUSTOMIZE) {
                controller.refreshCustomizeStateAsync()
            }
        }

        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(MaterialTheme.colorScheme.background),
        ) {
            controller.wallpaperBitmap?.let { wall ->
                Image(
                    bitmap = wall,
                    contentDescription = null,
                    contentScale = ContentScale.Crop,
                    modifier = Modifier
                        .fillMaxSize()
                        .alpha(controller.wallpaperAlpha),
                )
                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .background(MaterialTheme.colorScheme.background.copy(alpha = 0.55f)),
                )
            }

            if (isTablet) {
                Scaffold(
                    containerColor = Color.Transparent,
                    contentColor = MaterialTheme.colorScheme.onBackground,
                    snackbarHost = { SnackbarHost(hostState = snackbarHostState) },
                ) { innerPadding ->
                    Row(
                        modifier = Modifier
                            .fillMaxSize()
                            .padding(innerPadding),
                    ) {
                        XingShuTabletSideRail(
                            controller = controller,
                            expanded = isExpandedRail,
                            hasWallpaper = hasWallpaper,
                            onSelect = onSelectTab,
                        )
                        Box(
                            modifier = Modifier
                                .weight(1f)
                                .fillMaxHeight(),
                            contentAlignment = Alignment.TopCenter,
                        ) {
                            Box(
                                modifier = Modifier
                                    .widthIn(max = 1160.dp)
                                    .fillMaxSize(),
                            ) {
                                ActiveScreenHost(controller = controller, onSelectTab = onSelectTab)
                            }
                        }
                    }
                }
            } else {
                Scaffold(
                    containerColor = if (hasWallpaper) Color.Transparent else MaterialTheme.colorScheme.background,
                    contentColor = MaterialTheme.colorScheme.onBackground,
                    snackbarHost = { SnackbarHost(hostState = snackbarHostState) },
                    topBar = {
                        TopAppBar(
                            colors = TopAppBarDefaults.topAppBarColors(
                                containerColor = MaterialTheme.colorScheme.surface.copy(alpha = if (hasWallpaper) 0.78f else 0.96f),
                            ),
                            title = {
                                Row(
                                    verticalAlignment = Alignment.CenterVertically,
                                    horizontalArrangement = Arrangement.spacedBy(10.dp),
                                ) {
                                    Image(
                                        painter = painterResource(id = R.drawable.ic_xingshu_brand),
                                        contentDescription = null,
                                        modifier = Modifier.size(28.dp),
                                    )
                                    Column {
                                        Text(
                                            text = "星枢启动器",
                                            style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                                        )
                                        Text(
                                            text = controller.selectedChannel()?.label ?: "吉星派对",
                                            style = MaterialTheme.typography.labelMedium,
                                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                                        )
                                    }
                                }
                            },
                            actions = {
                                AssistChip(
                                    onClick = { controller.showConnectSheet = true },
                                    label = { Text(controller.privilegeChipLabel()) },
                                    leadingIcon = {
                                        Icon(
                                            if (controller.useRoot && controller.rootConnected) Icons.Rounded.AdminPanelSettings else Icons.Rounded.Usb,
                                            contentDescription = null,
                                            modifier = Modifier.size(16.dp),
                                        )
                                    },
                                    modifier = Modifier.padding(end = 10.dp),
                                )
                            },
                        )
                    },
                    bottomBar = {
                        XingShuBottomNavBar(
                            activeTab = controller.activeTab,
                            hasWallpaper = hasWallpaper,
                            onSelect = onSelectTab,
                        )
                    },
                ) { innerPadding ->
                    Box(
                        modifier = Modifier
                            .fillMaxSize()
                            .padding(innerPadding),
                    ) {
                        ActiveScreenHost(controller = controller, onSelectTab = onSelectTab)
                    }
                }
            }
        }

        // Authorization Sheet / Dialog: Root / Wireless Debugging / Shizuku
        if (controller.showConnectSheet) {
            if (isTablet) {
                Dialog(
                    onDismissRequest = { controller.showConnectSheet = false },
                    properties = DialogProperties(usePlatformDefaultWidth = false),
                ) {
                    Surface(
                        shape = RoundedCornerShape(24.dp),
                        color = MaterialTheme.colorScheme.surface,
                        tonalElevation = 6.dp,
                        modifier = Modifier
                            .padding(horizontal = 24.dp, vertical = 16.dp)
                            .widthIn(max = 820.dp)
                            .heightIn(max = (config.screenHeightDp * 0.92f).dp),
                    ) {
                        Column(
                            modifier = Modifier
                                .fillMaxWidth()
                                .verticalScroll(rememberScrollState())
                                .padding(horizontal = 20.dp, vertical = 16.dp),
                            verticalArrangement = Arrangement.spacedBy(12.dp),
                        ) {
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween,
                                verticalAlignment = Alignment.CenterVertically,
                            ) {
                                Text("配置本地文件访问授权", style = MaterialTheme.typography.titleLarge)
                                IconButton(onClick = { controller.showConnectSheet = false }) {
                                    Icon(Icons.Rounded.Close, contentDescription = "关闭")
                                }
                            }
                            Text(
                                text = "纯启动游戏无需授权；管理 ${OfficialModulesCatalog.ALL.size} 项官方模组、安装核心插件或使用自定义功能时，可任选 Root / 无线调试 / Shizuku：",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.spacedBy(12.dp),
                                verticalAlignment = Alignment.Top,
                            ) {
                                Column(
                                    modifier = Modifier.weight(1f),
                                    verticalArrangement = Arrangement.spacedBy(12.dp),
                                ) {
                                    RootAuthCard(controller)
                                    ShizukuAuthCard(controller)
                                }
                                Column(
                                    modifier = Modifier.weight(1f),
                                    verticalArrangement = Arrangement.spacedBy(12.dp),
                                ) {
                                    WirelessAdbAuthCard(controller)
                                }
                            }
                        }
                    }
                }
            } else {
                val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true)
                ModalBottomSheet(
                    onDismissRequest = { controller.showConnectSheet = false },
                    sheetState = sheetState,
                ) {
                    Column(
                        modifier = Modifier
                            .fillMaxWidth()
                            .verticalScroll(rememberScrollState())
                            .padding(horizontal = 20.dp, vertical = 8.dp),
                        verticalArrangement = Arrangement.spacedBy(12.dp),
                    ) {
                        Text("配置本地文件访问授权", style = MaterialTheme.typography.titleLarge)
                        Text(
                            text = "纯启动游戏无需授权；当你需要安装/卸载「星枢官方插件 Mod」、管理 ${OfficialModulesCatalog.ALL.size} 项官方模组或使用自定义功能时，请选择以下任一授权方式：",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                        RootAuthCard(controller)
                        WirelessAdbAuthCard(controller)
                        ShizukuAuthCard(controller)
                        Spacer(modifier = Modifier.height(16.dp))
                    }
                }
            }
        }

        // Conflict Backup Confirm Dialog
        val conflict = controller.conflictPrompt
        if (conflict != null) {
            AlertDialog(
                onDismissRequest = { controller.conflictPrompt = null },
                title = { Text("检测到同定位资源冲突") },
                text = {
                    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Text("目标渠道目录存在其他同定位插件（如旧版 APX）或非本版缓存。继续后将自动按 SHA-256 备份原文件，清理冲突并安装「星枢官方插件 Mod」。")
                        Text(
                            text = conflict.summary,
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                },
                dismissButton = {
                    TextButton(onClick = { controller.conflictPrompt = null }) {
                        Text("暂不安装")
                    }
                },
                confirmButton = {
                    Button(
                        onClick = {
                            controller.conflictPrompt = null
                            controller.installOrRestoreCorePlugin(restore = false, conflictHash = conflict.hash)
                        },
                    ) {
                        Text("备份冲突并安装")
                    }
                },
            )
        }

        // App Self-Update Dialog
        val upd = controller.availableUpdate
        if (controller.showUpdateModal && upd != null) {
            val verText = runCatching { upd.version() }.getOrDefault("")
            val notesText = runCatching { upd.notes() }.getOrDefault("")
            AlertDialog(
                onDismissRequest = { if (!controller.updateDownloading) controller.showUpdateModal = false },
                title = { Text("发现新版星枢启动器 v$verText") },
                text = {
                    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Text("当前版本：v${BuildConfig.VERSION_NAME} → 目标版本：v$verText")
                        if (notesText.isNotBlank()) {
                            Text(
                                text = notesText.take(600),
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        }
                        if (controller.updateDownloading || controller.updateVerifying) {
                            Text(controller.updateStatusText, style = MaterialTheme.typography.bodySmall)
                            if (controller.updateVerifying) {
                                LinearProgressIndicator(modifier = Modifier.fillMaxWidth())
                            } else {
                                LinearProgressIndicator(
                                    progress = { (controller.updateProgressPct / 100f).coerceIn(0f, 1f) },
                                    modifier = Modifier.fillMaxWidth(),
                                )
                            }
                        }
                    }
                },
                dismissButton = {
                    TextButton(
                        onClick = { controller.showUpdateModal = false },
                        enabled = !controller.updateDownloading,
                    ) {
                        Text("稍后再说")
                    }
                },
                confirmButton = {
                    Button(
                        onClick = { controller.downloadAvailableUpdate() },
                        enabled = !controller.updateDownloading && !controller.updateVerifying,
                    ) {
                        Text(if (controller.updateDownloading) "正在更新…" else "下载并安装")
                    }
                },
            )
        }

        // QQ Quick Login Dialog
        if (controller.pendingQqHandoff.isNotEmpty()) {
            val session = controller.boundSession
            val selectedTok = controller.detectedTokens.getOrNull(controller.selectedTokenIndex)
            val nativeTok = selectedTok?.takeIf { it.nativeSupported }
                ?: controller.detectedTokens.firstOrNull { it.nativeSupported }
            AlertDialog(
                onDismissRequest = { controller.dismissQqHandoff() },
                title = { Text("QQ 快速绑定授权") },
                text = {
                    Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                        Text("请求来源：${controller.pendingQqRequester}")
                        if (session != null) {
                            Text("当前星枢账号：${session.nick} (UID ${session.uid})")
                            Text("确认后即可将当前账号绑定至 QQ。", style = MaterialTheme.typography.bodySmall)
                        } else if (nativeTok != null) {
                            Text("已识别渠道票据：${nativeTok.displaySummary()}", style = MaterialTheme.typography.bodySmall)
                            Text("点击下方按钮可直接用该渠道账号绑定 QQ 并同步登录星枢。", style = MaterialTheme.typography.bodySmall)
                        } else {
                            if (controller.authStatusText.isNotBlank()) {
                                Text(controller.authStatusText, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.primary)
                            }
                            Text("请先扫描已登录的官方/小米渠道票据，或点击「去登录星枢」完成网页授权。", style = MaterialTheme.typography.bodySmall)
                        }
                    }
                },
                dismissButton = {
                    if (session == null && nativeTok == null && controller.isPrivilegeReady()) {
                        TextButton(
                            onClick = { controller.scanChannelTokens() },
                            enabled = !controller.authBusy,
                        ) {
                            Text(if (controller.authBusy) "扫描中…" else "扫描渠道票据")
                        }
                    } else {
                        TextButton(onClick = { controller.dismissQqHandoff() }) {
                            Text("取消")
                        }
                    }
                },
                confirmButton = {
                    if (session != null) {
                        Button(
                            onClick = { controller.bindQqWithCurrentSession() },
                            enabled = !controller.authBusy,
                        ) {
                            Text("确认绑定 QQ")
                        }
                    } else if (nativeTok != null) {
                        Button(
                            onClick = {
                                val idx = controller.detectedTokens.indexOf(nativeTok)
                                if (idx >= 0) controller.selectedTokenIndex = idx
                                controller.bindQqHandoffWithSelectedToken()
                            },
                            enabled = !controller.authBusy,
                        ) {
                            Text("使用渠道票据绑定 QQ")
                        }
                    } else {
                        Button(
                            onClick = {
                                controller.activeTab = NavTab.SETTINGS
                                controller.startWebLogin()
                            },
                        ) {
                            Text("去登录星枢")
                        }
                    }
                },
            )
        }
    }
}

@Composable
private fun ActiveScreenHost(
    controller: AppController,
    onSelectTab: (NavTab) -> Unit,
) {
    when (controller.activeTab) {
        NavTab.LAUNCH -> LoaderScreen(
            controller = controller,
            onOpenMods = { onSelectTab(NavTab.MODS) },
            onOpenCustomize = { onSelectTab(NavTab.CUSTOMIZE) },
        )
        NavTab.MODS -> ModsScreen(
            controller = controller,
            onOpenWorkshop = { onSelectTab(NavTab.WORKSHOP) },
        )
        NavTab.WORKSHOP -> WorkshopScreen(controller = controller)
        NavTab.CUSTOMIZE -> CustomizeScreen(controller = controller)
        NavTab.SETTINGS -> SettingsScreen(controller = controller)
    }
}

@Composable
private fun RootAuthCard(controller: AppController) {
    Surface(
        color = MaterialTheme.colorScheme.surfaceContainerLow,
        shape = RoundedCornerShape(16.dp),
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                Icon(
                    Icons.Rounded.AdminPanelSettings,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(20.dp),
                )
                Text("Root 超级用户授权（模拟器 / 已 Root 设备）", style = MaterialTheme.typography.titleMedium)
            }
            Text(
                text = when {
                    controller.useRoot && controller.rootConnected ->
                        "当前已获取 Root (su) 超级用户授权，可直接高速读写游戏目录。"
                    controller.rootManagerLabel != null && !controller.rootBinaryDetected ->
                        "检测到已安装 ${controller.rootManagerLabel}（KernelSU/APatch 会在授权前隐藏 su）。点下方请求授权，或先在 ${controller.rootManagerLabel} →「超级用户」开启「星枢启动器」开关。"
                    controller.rootBinaryDetected ->
                        "已检测到系统 su，点击下方按钮并允许超级用户授权即可完成连接。"
                    else ->
                        "未探测到公开 su：若使用 KernelSU/Magisk 可点击尝试拉起授权；电脑模拟器也可在「设置 → 开启手机 Root 权限」后使用。"
                },
                style = MaterialTheme.typography.bodySmall,
                color = if (controller.rootBinaryDetected || controller.rootManagerLabel != null) {
                    MaterialTheme.colorScheme.primary
                } else {
                    MaterialTheme.colorScheme.onSurfaceVariant
                },
            )
            if (controller.rootStatusMessage.isNotBlank()) {
                Surface(
                    color = if (controller.rootStatusError) {
                        MaterialTheme.colorScheme.errorContainer
                    } else {
                        MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.72f)
                    },
                    shape = RoundedCornerShape(10.dp),
                    modifier = Modifier.fillMaxWidth(),
                ) {
                    Text(
                        text = controller.rootStatusMessage,
                        style = MaterialTheme.typography.bodySmall,
                        color = if (controller.rootStatusError) {
                            MaterialTheme.colorScheme.onErrorContainer
                        } else {
                            MaterialTheme.colorScheme.onPrimaryContainer
                        },
                        modifier = Modifier.padding(horizontal = 10.dp, vertical = 8.dp),
                    )
                }
            }
            if (controller.rootManagerPackage != null && !(controller.useRoot && controller.rootConnected)) {
                FilledTonalButton(
                    onClick = { controller.openRootManager() },
                    enabled = !controller.busy,
                    modifier = Modifier.fillMaxWidth(),
                ) {
                    Text("打开 ${controller.rootManagerLabel ?: "Root 管理器"} 授权")
                }
            }
            Button(
                onClick = { controller.connectRoot() },
                enabled = !controller.busy && !controller.rootConnecting,
                modifier = Modifier.fillMaxWidth(),
            ) {
                if (controller.rootConnecting) {
                    androidx.compose.material3.CircularProgressIndicator(
                        modifier = Modifier.size(16.dp),
                        strokeWidth = 2.dp,
                        color = MaterialTheme.colorScheme.onPrimary,
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Text("正在请求 Root (su) 授权…")
                } else {
                    Text(if (controller.useRoot && controller.rootConnected) "重新验证 Root 授权" else "请求 Root (su) 权限")
                }
            }
        }
    }
}

@Composable
private fun WirelessAdbAuthCard(controller: AppController) {
    Surface(
        color = MaterialTheme.colorScheme.surfaceContainerLow,
        shape = RoundedCornerShape(16.dp),
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(10.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text(
                    text = "本机无线调试（免 Root / 推荐手机端）",
                    style = MaterialTheme.typography.titleMedium,
                    modifier = Modifier.weight(1f),
                )
                OutlinedButton(
                    onClick = { controller.openWirelessSettings() },
                    contentPadding = PaddingValues(horizontal = 12.dp, vertical = 6.dp),
                    modifier = Modifier.heightIn(min = 36.dp),
                ) {
                    Text("开发者选项", maxLines = 1, softWrap = false)
                }
            }
            Text(
                text = when {
                    !controller.useRoot && !controller.useShizuku && controller.adbConnected ->
                        "当前已连接本机无线调试 (${controller.connectedHost})。"
                    controller.discoveredConnectPort >= 1024 ->
                        "已自动探测到无线调试端口：${controller.discoveredConnectPort}"
                    else ->
                        "打开系统「开发者选项 → 无线调试」开关；首次配对请先点系统「使用配对码配对设备」。"
                },
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.primary,
            )
            OutlinedTextField(
                value = controller.connectionPortInput,
                onValueChange = { controller.connectionPortInput = it.filter(Char::isDigit).take(5) },
                label = { Text("无线调试连接端口（自动发现时可留空）") },
                singleLine = true,
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                modifier = Modifier.fillMaxWidth(),
            )
            Button(
                onClick = { controller.connectLocalAdb() },
                enabled = !controller.busy,
                modifier = Modifier.fillMaxWidth(),
            ) {
                Text("连接本机无线调试")
            }
            HorizontalDivider()
            Text("首次配对（已配对过可跳过）", style = MaterialTheme.typography.labelMedium)
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(
                    value = controller.pairingPortInput,
                    onValueChange = { controller.pairingPortInput = it.filter(Char::isDigit).take(5) },
                    label = { Text("配对端口") },
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                    modifier = Modifier.weight(1f),
                )
                OutlinedTextField(
                    value = controller.pairingCodeInput,
                    onValueChange = { controller.pairingCodeInput = it.filter(Char::isDigit).take(6) },
                    label = { Text("6 位配对码") },
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                    modifier = Modifier.weight(1f),
                )
            }
            FilledTonalButton(
                onClick = { controller.pairAndConnectLocalAdb() },
                enabled = !controller.busy,
                modifier = Modifier.fillMaxWidth(),
            ) {
                Text("配对并自动连接")
            }
        }
    }
}

@Composable
private fun ShizukuAuthCard(controller: AppController) {
    Surface(
        color = MaterialTheme.colorScheme.surfaceContainerLow,
        shape = RoundedCornerShape(16.dp),
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            Text("Shizuku 服务授权", style = MaterialTheme.typography.titleMedium)
            Text(
                text = if (controller.useShizuku && controller.remoteBinder != null) {
                    "Shizuku 特权服务已连接。"
                } else {
                    "如果你已经在系统后台启动了 Shizuku，可直接一键绑定。"
                },
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            FilledTonalButton(
                onClick = { controller.connectShizuku() },
                enabled = !controller.busy && !controller.bindingShizuku,
                modifier = Modifier.fillMaxWidth(),
            ) {
                Text(if (controller.bindingShizuku) "正在绑定 Shizuku…" else "连接 Shizuku 服务")
            }
        }
    }
}

@Composable
private fun XingShuTabletSideRail(
    controller: AppController,
    expanded: Boolean,
    hasWallpaper: Boolean,
    onSelect: (NavTab) -> Unit,
) {
    val haptic = LocalHapticFeedback.current
    val railWidth = if (expanded) 196.dp else 84.dp
    val privReady = controller.isPrivilegeReady()

    Row(modifier = Modifier.fillMaxHeight()) {
        Surface(
            color = MaterialTheme.colorScheme.surface.copy(alpha = if (hasWallpaper) 0.84f else 0.96f),
            modifier = Modifier
                .width(railWidth)
                .fillMaxHeight(),
        ) {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .statusBarsPadding()
                    .navigationBarsPadding()
                    .padding(horizontal = 10.dp, vertical = 14.dp),
                verticalArrangement = Arrangement.spacedBy(10.dp),
            ) {
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = if (expanded) Arrangement.spacedBy(10.dp) else Arrangement.Center,
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = if (expanded) 6.dp else 0.dp, vertical = 4.dp),
                ) {
                    Image(
                        painter = painterResource(id = R.drawable.ic_xingshu_brand),
                        contentDescription = null,
                        modifier = Modifier
                            .size(34.dp)
                            .clip(RoundedCornerShape(10.dp)),
                    )
                    if (expanded) {
                        Column(modifier = Modifier.weight(1f)) {
                            Text(
                                text = "星枢启动器",
                                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                                maxLines = 1,
                                overflow = TextOverflow.Ellipsis,
                            )
                            Text(
                                text = controller.selectedChannel()?.label ?: "吉星派对",
                                style = MaterialTheme.typography.labelSmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                maxLines = 1,
                                overflow = TextOverflow.Ellipsis,
                            )
                        }
                    }
                }

                HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.45f))

                Column(
                    modifier = Modifier
                        .weight(1f)
                        .verticalScroll(rememberScrollState()),
                    verticalArrangement = Arrangement.spacedBy(6.dp),
                ) {
                    NavTab.entries.forEach { tab ->
                        val selected = tab == controller.activeTab
                        val icon = when (tab) {
                            NavTab.LAUNCH -> if (selected) Icons.Rounded.PlayCircle else Icons.Outlined.PlayCircle
                            NavTab.MODS -> if (selected) Icons.Rounded.GridView else Icons.Outlined.GridView
                            NavTab.WORKSHOP -> if (selected) Icons.Rounded.Explore else Icons.Outlined.Explore
                            NavTab.CUSTOMIZE -> if (selected) Icons.Rounded.AutoAwesome else Icons.Outlined.AutoAwesome
                            NavTab.SETTINGS -> if (selected) Icons.Rounded.Tune else Icons.Outlined.Tune
                        }
                        val bg = if (selected) MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.85f) else Color.Transparent
                        val fg = if (selected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurfaceVariant

                        Surface(
                            onClick = {
                                haptic.performHapticFeedback(HapticFeedbackType.TextHandleMove)
                                onSelect(tab)
                            },
                            color = bg,
                            shape = RoundedCornerShape(14.dp),
                            modifier = Modifier.fillMaxWidth(),
                        ) {
                            Row(
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = if (expanded) Arrangement.spacedBy(10.dp) else Arrangement.Center,
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(horizontal = 12.dp, vertical = 11.dp),
                            ) {
                                Icon(icon, contentDescription = tab.label, tint = fg, modifier = Modifier.size(22.dp))
                                if (expanded) {
                                    Text(
                                        text = tab.label,
                                        color = fg,
                                        style = MaterialTheme.typography.labelLarge.copy(
                                            fontWeight = if (selected) FontWeight.Bold else FontWeight.Medium,
                                        ),
                                        maxLines = 1,
                                    )
                                }
                            }
                        }
                    }
                }

                Surface(
                    onClick = { controller.showConnectSheet = true },
                    color = if (privReady) MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.surfaceContainerHigh,
                    shape = RoundedCornerShape(14.dp),
                    border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.6f)),
                    modifier = Modifier.fillMaxWidth(),
                ) {
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = if (expanded) Arrangement.spacedBy(8.dp) else Arrangement.Center,
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 10.dp, vertical = 10.dp),
                    ) {
                        Icon(
                            if (controller.useRoot && controller.rootConnected) Icons.Rounded.AdminPanelSettings else Icons.Rounded.Security,
                            contentDescription = "授权状态",
                            tint = if (privReady) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.size(18.dp),
                        )
                        if (expanded) {
                            Text(
                                text = controller.privilegeChipLabel(),
                                style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
                                color = if (privReady) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurface,
                                maxLines = 1,
                                overflow = TextOverflow.Ellipsis,
                            )
                        }
                    }
                }
            }
        }
        VerticalDivider(color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.45f))
    }
}

@Composable
private fun XingShuBottomNavBar(
    activeTab: NavTab,
    hasWallpaper: Boolean,
    onSelect: (NavTab) -> Unit,
) {
    val haptic = LocalHapticFeedback.current
    Surface(
        color = MaterialTheme.colorScheme.surface.copy(alpha = if (hasWallpaper) 0.90f else 0.99f),
        tonalElevation = 0.dp,
        shadowElevation = 0.dp,
        modifier = Modifier.fillMaxWidth(),
    ) {
        Column(modifier = Modifier.fillMaxWidth().navigationBarsPadding()) {
            HorizontalDivider(
                thickness = 1.dp,
                color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.55f),
            )
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(62.dp)
                    .padding(horizontal = 6.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                NavTab.entries.forEach { tab ->
                    val selected = activeTab == tab
                    val interactionSource = remember { MutableInteractionSource() }
                    val pressed by interactionSource.collectIsPressedAsState()
                    val pressScale by animateFloatAsState(
                        targetValue = if (pressed) 0.88f else 1f,
                        animationSpec = spring(
                            dampingRatio = Spring.DampingRatioMediumBouncy,
                            stiffness = Spring.StiffnessMediumLow,
                        ),
                        label = "tabScale",
                    )
                    val indicatorWidth by animateDpAsState(
                        targetValue = if (selected) 28.dp else 0.dp,
                        animationSpec = spring(
                            dampingRatio = Spring.DampingRatioNoBouncy,
                            stiffness = Spring.StiffnessMedium,
                        ),
                        label = "tabIndicator",
                    )
                    val iconOffsetY by animateDpAsState(
                        targetValue = if (selected) (-2).dp else 0.dp,
                        animationSpec = spring(
                            dampingRatio = Spring.DampingRatioMediumBouncy,
                            stiffness = Spring.StiffnessMedium,
                        ),
                        label = "tabLift",
                    )
                    val icon = when (tab) {
                        NavTab.LAUNCH -> if (selected) Icons.Rounded.PlayCircle else Icons.Outlined.PlayCircle
                        NavTab.MODS -> if (selected) Icons.Rounded.GridView else Icons.Outlined.GridView
                        NavTab.WORKSHOP -> if (selected) Icons.Rounded.Explore else Icons.Outlined.Explore
                        NavTab.CUSTOMIZE -> if (selected) Icons.Rounded.AutoAwesome else Icons.Outlined.AutoAwesome
                        NavTab.SETTINGS -> if (selected) Icons.Rounded.Tune else Icons.Outlined.Tune
                    }
                    val contentColor = if (selected) {
                        MaterialTheme.colorScheme.primary
                    } else {
                        MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.72f)
                    }
                    val pressWash = if (pressed) {
                        MaterialTheme.colorScheme.primary.copy(alpha = 0.09f)
                    } else {
                        Color.Transparent
                    }

                    Box(
                        modifier = Modifier
                            .weight(1f)
                            .height(62.dp)
                            .padding(horizontal = 2.dp, vertical = 2.dp)
                            .clip(RoundedCornerShape(12.dp))
                            .background(pressWash)
                            .clickable(
                                interactionSource = interactionSource,
                                indication = null,
                            ) {
                                haptic.performHapticFeedback(HapticFeedbackType.TextHandleMove)
                                onSelect(tab)
                            },
                        contentAlignment = Alignment.Center,
                    ) {
                        if (indicatorWidth > 0.dp) {
                            Box(
                                modifier = Modifier
                                    .align(Alignment.TopCenter)
                                    .width(indicatorWidth)
                                    .height(3.dp)
                                    .clip(RoundedCornerShape(bottomStart = 3.dp, bottomEnd = 3.dp))
                                    .background(MaterialTheme.colorScheme.primary),
                            )
                        }
                        Column(
                            horizontalAlignment = Alignment.CenterHorizontally,
                            verticalArrangement = Arrangement.Center,
                            modifier = Modifier.scale(pressScale),
                        ) {
                            Icon(
                                imageVector = icon,
                                contentDescription = tab.label,
                                tint = contentColor,
                                modifier = Modifier
                                    .size(22.dp)
                                    .offset(y = iconOffsetY),
                            )
                            Spacer(modifier = Modifier.height(3.dp))
                            Text(
                                text = tab.label,
                                color = contentColor,
                                fontSize = 11.sp,
                                lineHeight = 13.sp,
                                fontWeight = if (selected) FontWeight.Bold else FontWeight.Medium,
                                maxLines = 1,
                                overflow = TextOverflow.Ellipsis,
                            )
                        }
                    }
                }
            }
        }
    }
}

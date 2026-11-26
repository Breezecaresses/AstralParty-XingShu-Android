package app.xingshu.installer

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.AudioFile
import androidx.compose.material.icons.rounded.BugReport
import androidx.compose.material.icons.rounded.Check
import androidx.compose.material.icons.rounded.ColorLens
import androidx.compose.material.icons.rounded.DeleteOutline
import androidx.compose.material.icons.rounded.Face
import androidx.compose.material.icons.rounded.FontDownload
import androidx.compose.material.icons.rounded.Home
import androidx.compose.material.icons.rounded.Image
import androidx.compose.material.icons.rounded.Inventory2
import androidx.compose.material.icons.rounded.Movie
import androidx.compose.material.icons.rounded.Refresh
import androidx.compose.material.icons.rounded.Restore
import androidx.compose.material.icons.rounded.Storefront
import androidx.compose.material.icons.rounded.Style
import androidx.compose.material.icons.rounded.UploadFile
import androidx.compose.material3.Button
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.FilledTonalButton
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedCard
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.SegmentedButton
import androidx.compose.material3.SegmentedButtonDefaults
import androidx.compose.material3.SingleChoiceSegmentedButtonRow
import androidx.compose.material3.Slider
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import kotlin.math.roundToInt

private data class CustomizeNavItem(
    val key: String,
    val label: String,
    val icon: ImageVector,
    val badge: String? = null,
)

private val CUSTOMIZE_SECTIONS = listOf(
    CustomizeNavItem("lobbybg", "主页背景", Icons.Rounded.Home, "已内置"),
    CustomizeNavItem("cards", "卡牌", Icons.Rounded.Style),
    CustomizeNavItem("audio", "音效", Icons.Rounded.AudioFile),
    CustomizeNavItem("character", "角色", Icons.Rounded.Face),
    CustomizeNavItem("resource", "资源包", Icons.Rounded.Inventory2),
    CustomizeNavItem("fonts", "字体", Icons.Rounded.FontDownload, "调试"),
    CustomizeNavItem("home_debug", "主界面调试", Icons.Rounded.BugReport, "调试"),
    CustomizeNavItem("appearance", "星枢外观", Icons.Rounded.ColorLens),
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
internal fun CustomizeScreen(controller: AppController) {
    LaunchedEffect(controller.selectedPackageName, controller.useShizuku, controller.adbConnected) {
        if (controller.isPrivilegeReady()) {
            controller.refreshCustomizeStateAsync(userInitiated = false)
        }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 16.dp, vertical = 12.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        // Horizontal section selector matching Desktop Customize Workbench
        LazyRow(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            items(CUSTOMIZE_SECTIONS, key = { it.key }) { sec ->
                FilterChip(
                    selected = controller.customizeSection == sec.key,
                    onClick = { controller.customizeSection = sec.key },
                    leadingIcon = {
                        Icon(sec.icon, contentDescription = null, modifier = Modifier.size(16.dp))
                    },
                    label = {
                        val txt = if (sec.badge != null) "${sec.label} · ${sec.badge}" else sec.label
                        Text(txt, maxLines = 1, softWrap = false)
                    },
                )
            }
        }

        if (controller.customizeLoading || controller.customizeBusy) {
            LinearProgressIndicator(modifier = Modifier.fillMaxWidth())
            if (controller.customizeProgressText.isNotBlank()) {
                Text(
                    text = controller.customizeProgressText,
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.primary,
                )
            }
        }

        when (controller.customizeSection) {
            "lobbybg" -> LobbyBgSection(controller)
            "cards" -> ModPackBridgeSection(
                controller = controller,
                title = "游戏自定义 · 卡牌卡面",
                subtitle = "通过工坊卡牌替换包或本地 .zip/.modimg 贴图包替换对局卡牌立绘；装入游戏 modimg/ 后随游戏自动生效。",
                workshopFilterKey = "card",
                matchKeywords = listOf("卡牌", "卡面", "card"),
            )
            "audio" -> ModPackBridgeSection(
                controller = controller,
                title = "游戏自定义 · 语音与音效",
                subtitle = "管理社区语音包与音效包，可前往工坊挑选或从手机本地导入压缩包并统一在「模组」页卸载。",
                workshopFilterKey = "audio",
                matchKeywords = listOf("音效", "语音", "音乐", "audio"),
            )
            "character" -> ModPackBridgeSection(
                controller = controller,
                title = "游戏自定义 · 角色立绘与皮肤",
                subtitle = "替换角色立绘与服装皮肤，支持 PE 手机端专用皮肤整合包一键校验装入。",
                workshopFilterKey = "character",
                matchKeywords = listOf("角色", "皮肤", "立绘", "character"),
            )
            "resource" -> ModPackBridgeSection(
                controller = controller,
                title = "游戏自定义 · 外部资源包汇总",
                subtitle = "管理已装入当前渠道 modimg/ 与 xingshu/plugins/ 的全部外部资源包，随时查看贴图数量与卸载还原。",
                workshopFilterKey = "community",
                matchKeywords = emptyList(),
            )
            "fonts" -> FontsDebugSection(controller)
            "home_debug" -> HomeUiDebugSection(controller)
            else -> AppearanceSection(controller)
        }

        Spacer(modifier = Modifier.height(16.dp))
    }
}

@Composable
private fun PrivilegeRequiredBanner(controller: AppController, actionText: String) {
    if (controller.isPrivilegeReady()) return
    Surface(
        color = MaterialTheme.colorScheme.secondaryContainer.copy(alpha = 0.92f),
        shape = RoundedCornerShape(16.dp),
        modifier = Modifier.fillMaxWidth(),
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 14.dp, vertical = 10.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            Text(
                text = "当前未连接无线调试 / Shizuku，$actionText 需要游戏目录读写权限。",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSecondaryContainer,
                modifier = Modifier.weight(1f),
            )
            FilledTonalButton(
                onClick = { controller.showConnectSheet = true },
                contentPadding = PaddingValues(horizontal = 12.dp, vertical = 6.dp),
            ) {
                Text("连接授权")
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class, ExperimentalLayoutApi::class)
@Composable
private fun LobbyBgSection(controller: AppController) {
    val cfg = controller.lobbyBgConfig
    val selectedName = cfg.selectedFile
    val selectedEntry = controller.lobbyBgFiles.firstOrNull { it.name == selectedName }
    val previewBmp = if (selectedName.isNotEmpty()) controller.lobbyBgPreviews[selectedName] else null

    var brightnessDraft by remember(cfg.brightness) { mutableFloatStateOf(cfg.brightness) }
    var speedDraft by remember(cfg.speed) { mutableFloatStateOf(cfg.speed) }
    var volumeDraft by remember(cfg.volume) { mutableFloatStateOf(cfg.volume) }

    PrivilegeRequiredBanner(controller, "同步与修改游戏主页背景")

    OutlinedCard(
        colors = CardDefaults.outlinedCardColors(
            containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.94f),
        ),
        shape = MaterialTheme.shapes.large,
    ) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    Text("自定义 · 主页背景（已内置）", style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold))
                    Text(
                        text = "直接读写游戏目录 lobbybg/；手机端选择 MP4/MOV/WebM 时将自动硬件转码为手机专享 .xsvid 动态背景（无需 Unity VideoPlayer，进大厅流畅播放）。",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
                IconButton(
                    onClick = { controller.refreshCustomizeStateAsync(userInitiated = true) },
                    enabled = !controller.customizeLoading,
                ) {
                    Icon(Icons.Rounded.Refresh, contentDescription = "刷新大厅背景列表")
                }
            }

            // 16:9 Preview Frame
            Surface(
                shape = RoundedCornerShape(14.dp),
                color = MaterialTheme.colorScheme.surfaceContainerHighest,
                border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
                modifier = Modifier
                    .fillMaxWidth()
                    .aspectRatio(16f / 9f)
                    .clip(RoundedCornerShape(14.dp)),
            ) {
                Box(modifier = Modifier.fillMaxSize()) {
                    if (previewBmp != null) {
                        Image(
                            bitmap = previewBmp,
                            contentDescription = selectedName,
                            contentScale = when (cfg.fillMode) {
                                "contain" -> ContentScale.Fit
                                "stretch" -> ContentScale.FillBounds
                                else -> ContentScale.Crop
                            },
                            modifier = Modifier.fillMaxSize(),
                        )
                    } else {
                        Column(
                            modifier = Modifier
                                .fillMaxSize()
                                .padding(16.dp),
                            verticalArrangement = Arrangement.Center,
                            horizontalAlignment = Alignment.CenterHorizontally,
                        ) {
                            Icon(
                                imageVector = if (selectedEntry?.kind == "video" || selectedEntry?.kind == "usm") Icons.Rounded.Movie else Icons.Rounded.Home,
                                contentDescription = null,
                                tint = MaterialTheme.colorScheme.primary,
                                modifier = Modifier.size(34.dp),
                            )
                            Spacer(modifier = Modifier.height(6.dp))
                            Text(
                                text = when {
                                    selectedName.isBlank() -> "当前使用游戏原版默认背景"
                                    selectedEntry?.kind == "usm" -> "$selectedName（CRI USM 视频）"
                                    else -> "$selectedName（进游戏大厅后实时播放）"
                                },
                                style = MaterialTheme.typography.labelLarge,
                                maxLines = 1,
                                overflow = TextOverflow.Ellipsis,
                            )
                            Text(
                                text = if (selectedName.isBlank()) "点击下方导入图片或视频作为大厅背景"
                                else "手机端导入新素材后将自动生成缩略图预览",
                                style = MaterialTheme.typography.labelSmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        }
                    }

                    // Bottom status overlay inside preview
                    Surface(
                        color = Color.Black.copy(alpha = 0.56f),
                        shape = RoundedCornerShape(999.dp),
                        modifier = Modifier
                            .align(Alignment.BottomStart)
                            .padding(10.dp),
                    ) {
                        Text(
                            text = if (cfg.enabled && selectedName.isNotBlank()) "● 已启用 · $selectedName"
                            else if (selectedName.isNotBlank()) "○ 已暂停 · $selectedName"
                            else "默认原版大厅",
                            style = MaterialTheme.typography.labelSmall,
                            color = Color.White,
                            maxLines = 1,
                            overflow = TextOverflow.Ellipsis,
                            modifier = Modifier.padding(horizontal = 10.dp, vertical = 4.dp),
                        )
                    }
                }
            }

            // Import & Reset actions
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                FilledTonalButton(
                    onClick = { controller.requestPickLobbyBgVideo() },
                    enabled = !controller.customizeBusy,
                    modifier = Modifier.weight(1f),
                ) {
                    Icon(Icons.Rounded.Movie, contentDescription = null, modifier = Modifier.size(18.dp))
                    Spacer(modifier = Modifier.width(4.dp))
                    Text("选择视频/USM", maxLines = 1)
                }
                Button(
                    onClick = { controller.requestPickLobbyBgImage() },
                    enabled = !controller.customizeBusy,
                    modifier = Modifier.weight(1f),
                ) {
                    Icon(Icons.Rounded.Image, contentDescription = null, modifier = Modifier.size(18.dp))
                    Spacer(modifier = Modifier.width(4.dp))
                    Text("选择图片", maxLines = 1)
                }
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    Text("启用自定义大厅背景", style = MaterialTheme.typography.titleSmall)
                    Text(
                        text = if (selectedName.isNotBlank()) "当前素材：$selectedName" else "尚未选中素材（若开启且列表非空将自动加载首个）",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                    )
                }
                Switch(
                    checked = cfg.enabled,
                    enabled = !controller.customizeBusy,
                    onCheckedChange = { enabled ->
                        val nextSelected = if (enabled && cfg.selectedFile.isBlank() && controller.lobbyBgFiles.isNotEmpty()) {
                            controller.lobbyBgFiles.first().name
                        } else cfg.selectedFile
                        controller.saveLobbyBgConfig(
                            cfg.copy(enabled = enabled, selectedFile = nextSelected),
                            if (enabled) "已启用自定义大厅背景" else "已暂停自定义大厅背景",
                        )
                    },
                )
            }

            OutlinedButton(
                onClick = { controller.resetLobbyBgToDefault() },
                enabled = !controller.customizeBusy && (cfg.enabled || cfg.selectedFile.isNotBlank()),
                modifier = Modifier.fillMaxWidth(),
            ) {
                Icon(Icons.Rounded.Restore, contentDescription = null, modifier = Modifier.size(16.dp))
                Spacer(modifier = Modifier.width(6.dp))
                Text("恢复游戏默认大厅背景")
            }
        }
    }

    // Saved media library list
    OutlinedCard(
        colors = CardDefaults.outlinedCardColors(
            containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.94f),
        ),
        shape = MaterialTheme.shapes.large,
    ) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Text("游戏目录已有背景素材 (${controller.lobbyBgFiles.size})", style = MaterialTheme.typography.titleMedium)
            if (controller.lobbyBgFiles.isEmpty()) {
                Text(
                    text = "lobbybg 目录中暂无背景图片或视频，可点击上方按钮从手机导入。",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            } else {
                controller.lobbyBgFiles.forEach { entry ->
                    val isSelected = cfg.selectedFile == entry.name
                    Surface(
                        shape = RoundedCornerShape(12.dp),
                        color = if (isSelected) MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.72f)
                        else MaterialTheme.colorScheme.surfaceContainerLow,
                        border = BorderStroke(
                            width = 1.dp,
                            color = if (isSelected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outlineVariant,
                        ),
                        modifier = Modifier
                            .fillMaxWidth()
                            .clip(RoundedCornerShape(12.dp))
                            .clickable(enabled = !controller.customizeBusy) {
                                controller.selectLobbyBgFile(entry.name)
                            },
                    ) {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(horizontal = 12.dp, vertical = 10.dp),
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(10.dp),
                        ) {
                            Icon(
                                imageVector = if (entry.kind == "image") Icons.Rounded.Image else Icons.Rounded.Movie,
                                contentDescription = null,
                                tint = MaterialTheme.colorScheme.primary,
                            )
                            Column(modifier = Modifier.weight(1f)) {
                                Text(
                                    text = entry.name,
                                    style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.Medium),
                                    maxLines = 1,
                                    overflow = TextOverflow.Ellipsis,
                                )
                                Text(
                                    text = "${entry.kind.uppercase()} · ${PayloadDownloadManager.formatBytes(entry.sizeBytes)}${if (isSelected) " · 当前选用" else ""}",
                                    style = MaterialTheme.typography.labelSmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                )
                            }
                            if (isSelected) {
                                Icon(Icons.Rounded.Check, contentDescription = null, tint = MaterialTheme.colorScheme.primary)
                            }
                            IconButton(
                                enabled = !controller.customizeBusy,
                                onClick = { controller.deleteLobbyBgFile(entry.name) },
                            ) {
                                Icon(Icons.Rounded.DeleteOutline, contentDescription = "删除素材")
                            }
                        }
                    }
                }
            }
        }
    }

    // Playback & Layout Parameters
    OutlinedCard(
        colors = CardDefaults.outlinedCardColors(
            containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.94f),
        ),
        shape = MaterialTheme.shapes.large,
    ) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Text("显示与播放参数", style = MaterialTheme.typography.titleMedium)

            Text("画面缩放模式", style = MaterialTheme.typography.labelLarge)
            val fillModes = listOf("cover" to "等比填满", "contain" to "完整显示", "stretch" to "拉伸填充")
            SingleChoiceSegmentedButtonRow(modifier = Modifier.fillMaxWidth()) {
                fillModes.forEachIndexed { idx, (key, label) ->
                    SegmentedButton(
                        selected = cfg.fillMode == key,
                        onClick = { controller.saveLobbyBgConfig(cfg.copy(fillMode = key)) },
                        shape = SegmentedButtonDefaults.itemShape(index = idx, count = fillModes.size),
                    ) {
                        Text(label, maxLines = 1)
                    }
                }
            }

            LobbySwitchRow(
                title = "同步替换二级界面背景（选角/房间/商店）",
                checked = cfg.syncSubPanels,
                enabled = !controller.customizeBusy,
                onCheckedChange = { controller.saveLobbyBgConfig(cfg.copy(syncSubPanels = it)) },
            )
            LobbySwitchRow(
                title = "每次返回大厅随机切换",
                checked = cfg.randomOnLobby,
                enabled = !controller.customizeBusy,
                onCheckedChange = { controller.saveLobbyBgConfig(cfg.copy(randomOnLobby = it)) },
            )
            LobbySwitchRow(
                title = "关闭原版大厅背景音乐（进对局自动恢复）",
                checked = cfg.muteVanillaBgm,
                enabled = !controller.customizeBusy,
                onCheckedChange = { controller.saveLobbyBgConfig(cfg.copy(muteVanillaBgm = it)) },
            )
            LobbySwitchRow(
                title = "自定义视频静音（关闭静音即播放伴音）",
                checked = cfg.muted,
                enabled = !controller.customizeBusy,
                onCheckedChange = { controller.saveLobbyBgConfig(cfg.copy(muted = it)) },
            )

            HorizontalDivider()

            SliderSettingRow(
                label = "画面亮度",
                valueText = "${(brightnessDraft * 100).roundToInt()}%",
                value = brightnessDraft,
                range = 0.30f..1.00f,
                onValueChange = { brightnessDraft = it },
                onValueChangeFinished = {
                    val v = (brightnessDraft * 100f).roundToInt() / 100f
                    controller.saveLobbyBgConfig(cfg.copy(brightness = v))
                },
            )
            SliderSettingRow(
                label = "视频播放倍速",
                valueText = String.format("%.2fx", speedDraft),
                value = speedDraft,
                range = 0.50f..2.00f,
                onValueChange = { speedDraft = it },
                onValueChangeFinished = {
                    val v = (speedDraft * 20f).roundToInt() / 20f
                    controller.saveLobbyBgConfig(cfg.copy(speed = v))
                },
            )
            SliderSettingRow(
                label = "自定义视频音量",
                valueText = "${(volumeDraft * 100).roundToInt()}%",
                value = volumeDraft,
                range = 0.00f..1.00f,
                onValueChange = { volumeDraft = it },
                onValueChangeFinished = {
                    val v = (volumeDraft * 100f).roundToInt() / 100f
                    controller.saveLobbyBgConfig(cfg.copy(volume = v))
                },
            )
        }
    }
}

@Composable
private fun LobbySwitchRow(
    title: String,
    checked: Boolean,
    enabled: Boolean,
    onCheckedChange: (Boolean) -> Unit,
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            text = title,
            style = MaterialTheme.typography.bodyMedium,
            modifier = Modifier
                .weight(1f)
                .padding(end = 12.dp),
        )
        Switch(checked = checked, enabled = enabled, onCheckedChange = onCheckedChange)
    }
}

@Composable
private fun SliderSettingRow(
    label: String,
    valueText: String,
    value: Float,
    range: ClosedFloatingPointRange<Float>,
    onValueChange: (Float) -> Unit,
    onValueChangeFinished: () -> Unit,
) {
    Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Text(label, style = MaterialTheme.typography.labelLarge)
            Text(valueText, style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.primary)
        }
        Slider(
            value = value.coerceIn(range.start, range.endInclusive),
            onValueChange = onValueChange,
            onValueChangeFinished = onValueChangeFinished,
            valueRange = range,
        )
    }
}

@Composable
private fun ModPackBridgeSection(
    controller: AppController,
    title: String,
    subtitle: String,
    workshopFilterKey: String,
    matchKeywords: List<String>,
) {
    val externalMods = controller.installedMods.filter { !it.isXspkgModule() }
    val matchedMods = if (matchKeywords.isEmpty()) {
        externalMods
    } else {
        externalMods.filter { m ->
            val lower = m.name.lowercase()
            matchKeywords.any { kw -> lower.contains(kw.lowercase()) }
        }
    }

    OutlinedCard(
        colors = CardDefaults.outlinedCardColors(
            containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.94f),
        ),
        shape = MaterialTheme.shapes.large,
    ) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Text(title, style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold))
            Text(subtitle, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)

            Surface(
                color = MaterialTheme.colorScheme.surfaceContainerLow,
                shape = RoundedCornerShape(12.dp),
                modifier = Modifier.fillMaxWidth(),
            ) {
                Column(modifier = Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    Text(
                        text = "当前渠道外部包：${externalMods.size} 项（已装入 modimg 贴图 ${controller.totalModPngCount} 张）",
                        style = MaterialTheme.typography.labelLarge,
                    )
                    if (matchedMods.isNotEmpty()) {
                        Text(
                            text = "命中分类包：${matchedMods.joinToString("、") { it.name }}",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.primary,
                        )
                    } else {
                        Text(
                            text = "尚未识别到对应分类资源包，可前往实时工坊下载或从手机本地导入。",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                }
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                Button(
                    onClick = { controller.openWorkshopCategory(workshopFilterKey) },
                    modifier = Modifier.weight(1f),
                ) {
                    Icon(Icons.Rounded.Storefront, contentDescription = null, modifier = Modifier.size(16.dp))
                    Spacer(modifier = Modifier.width(4.dp))
                    Text("工坊挑包", maxLines = 1)
                }
                FilledTonalButton(
                    onClick = { controller.onPickLocalArchiveRequest?.invoke() },
                    modifier = Modifier.weight(1f),
                ) {
                    Icon(Icons.Rounded.UploadFile, contentDescription = null, modifier = Modifier.size(16.dp))
                    Spacer(modifier = Modifier.width(4.dp))
                    Text("本地导入", maxLines = 1)
                }
            }
            OutlinedButton(
                onClick = {
                    controller.modsSubTab = "external"
                    controller.activeTab = NavTab.MODS
                },
                modifier = Modifier.fillMaxWidth(),
            ) {
                Text("前往「模组 · 本地与外部 Mod」管理与卸载")
            }
        }
    }
}

@Composable
private fun FontsDebugSection(controller: AppController) {
    val tuning = controller.customizeTuning
    var fontFamilyInput by remember(tuning.selectedFontFamily) { mutableStateOf(tuning.selectedFontFamily.orEmpty()) }
    var fontCjkOnly by remember(tuning.fontCjkOnly) { mutableStateOf(tuning.fontCjkOnly) }
    var fontReplaceHeadings by remember(tuning.fontReplaceHeadings) { mutableStateOf(tuning.fontReplaceHeadings) }

    PrivilegeRequiredBanner(controller, "导入自定义字体与保存字体配置")

    OutlinedCard(
        colors = CardDefaults.outlinedCardColors(
            containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.94f),
        ),
        shape = MaterialTheme.shapes.large,
    ) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Text("游戏自定义 · 字体（调试）", style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold))
            Text(
                text = "支持导入 .ttf / .otf / .ttc 字体文件至游戏 xingshu/fonts/ 目录，并持久化到 xingshu_customize_debug.json。",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )

            FilledTonalButton(
                onClick = { controller.requestPickCustomFont() },
                enabled = !controller.customizeBusy,
                modifier = Modifier.fillMaxWidth(),
            ) {
                Icon(Icons.Rounded.UploadFile, contentDescription = null, modifier = Modifier.size(18.dp))
                Spacer(modifier = Modifier.width(6.dp))
                Text("导入自定义字体 (.ttf / .otf / .ttc)")
            }

            if (controller.customFontFiles.isNotEmpty()) {
                Text("已导入字体文件 (${controller.customFontFiles.size})", style = MaterialTheme.typography.labelLarge)
                controller.customFontFiles.forEach { f ->
                    val selected = tuning.selectedFontFile == f.name
                    Surface(
                        shape = RoundedCornerShape(10.dp),
                        color = if (selected) MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.surfaceContainerLow,
                        border = BorderStroke(
                            1.dp,
                            if (selected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outlineVariant,
                        ),
                        modifier = Modifier
                            .fillMaxWidth()
                            .clip(RoundedCornerShape(10.dp))
                            .clickable(enabled = !controller.customizeBusy) {
                                controller.saveCustomizeTuning(
                                    tuning.copy(selectedFontFile = f.name),
                                    "已选用字体文件：${f.name}",
                                )
                            },
                    ) {
                        Row(
                            modifier = Modifier.padding(horizontal = 12.dp, vertical = 8.dp),
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            Text(
                                text = "${f.name} (${PayloadDownloadManager.formatBytes(f.sizeBytes)})",
                                style = MaterialTheme.typography.bodySmall,
                                modifier = Modifier.weight(1f),
                            )
                            if (selected) Icon(Icons.Rounded.Check, contentDescription = null, tint = MaterialTheme.colorScheme.primary)
                        }
                    }
                }
            }

            OutlinedTextField(
                value = fontFamilyInput,
                onValueChange = { fontFamilyInput = it },
                label = { Text("自定义字体族名称（选填）") },
                placeholder = { Text("例如 Noto Sans SC / Microsoft YaHei") },
                singleLine = true,
                modifier = Modifier.fillMaxWidth(),
            )

            LobbySwitchRow(
                title = "仅替换中文/CJK 字符",
                checked = fontCjkOnly,
                enabled = !controller.customizeBusy,
                onCheckedChange = { fontCjkOnly = it },
            )
            LobbySwitchRow(
                title = "同时替换界面标题文字",
                checked = fontReplaceHeadings,
                enabled = !controller.customizeBusy,
                onCheckedChange = { fontReplaceHeadings = it },
            )

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                OutlinedButton(
                    enabled = !controller.customizeBusy,
                    onClick = {
                        fontFamilyInput = ""
                        fontCjkOnly = false
                        fontReplaceHeadings = true
                        controller.saveCustomizeTuning(
                            tuning.copy(selectedFontFamily = null, selectedFontFile = null, fontCjkOnly = false, fontReplaceHeadings = true),
                            "已恢复默认游戏字体。",
                        )
                    },
                ) {
                    Text("恢复默认")
                }
                Button(
                    enabled = !controller.customizeBusy,
                    modifier = Modifier.weight(1f),
                    onClick = {
                        controller.saveCustomizeTuning(
                            tuning.copy(
                                selectedFontFamily = fontFamilyInput.trim().ifEmpty { null },
                                fontCjkOnly = fontCjkOnly,
                                fontReplaceHeadings = fontReplaceHeadings,
                            ),
                            "已保存字体调试配置。",
                        )
                    },
                ) {
                    Text("保存字体配置")
                }
            }
        }
    }
}

@Composable
private fun HomeUiDebugSection(controller: AppController) {
    val tuning = controller.customizeTuning
    val hd = tuning.homeUiDebug
    var hideVanillaBgAnim by remember(hd.hideVanillaBgAnim) { mutableStateOf(hd.hideVanillaBgAnim) }
    var compactLobbyBadges by remember(hd.compactLobbyBadges) { mutableStateOf(hd.compactLobbyBadges) }
    var showDebugOutline by remember(hd.showDebugOutline) { mutableStateOf(hd.showDebugOutline) }
    var hideLobbyAds by remember(hd.hideLobbyAds) { mutableStateOf(hd.hideLobbyAds) }
    var customHudOpacity by remember(hd.customHudOpacity) { mutableIntStateOf(hd.customHudOpacity) }

    PrivilegeRequiredBanner(controller, "读写主界面调试配置")

    OutlinedCard(
        colors = CardDefaults.outlinedCardColors(
            containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.94f),
        ),
        shape = MaterialTheme.shapes.large,
    ) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Text("游戏自定义 · 主界面调试（调试）", style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold))
            Text(
                text = "与桌面端共用 xingshu_customize_debug.json 结构，可精简大厅视觉元素或调节 HUD 透明度。",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )

            LobbySwitchRow(
                title = "隐藏原版大厅背景动画",
                checked = hideVanillaBgAnim,
                enabled = !controller.customizeBusy,
                onCheckedChange = { hideVanillaBgAnim = it },
            )
            LobbySwitchRow(
                title = "精简大厅角标与徽章",
                checked = compactLobbyBadges,
                enabled = !controller.customizeBusy,
                onCheckedChange = { compactLobbyBadges = it },
            )
            LobbySwitchRow(
                title = "显示界面调试线框",
                checked = showDebugOutline,
                enabled = !controller.customizeBusy,
                onCheckedChange = { showDebugOutline = it },
            )
            LobbySwitchRow(
                title = "隐藏大厅活动推广横幅",
                checked = hideLobbyAds,
                enabled = !controller.customizeBusy,
                onCheckedChange = { hideLobbyAds = it },
            )

            SliderSettingRow(
                label = "自定义 HUD 不透明度",
                valueText = "${customHudOpacity}%",
                value = customHudOpacity.toFloat(),
                range = 40f..100f,
                onValueChange = { customHudOpacity = it.roundToInt().coerceIn(40, 100) },
                onValueChangeFinished = { },
            )

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                OutlinedButton(
                    enabled = !controller.customizeBusy,
                    onClick = { controller.resetHomeUiDebug() },
                ) {
                    Text("恢复默认")
                }
                Button(
                    enabled = !controller.customizeBusy,
                    modifier = Modifier.weight(1f),
                    onClick = {
                        controller.saveCustomizeTuning(
                            tuning.copy(
                                homeUiDebug = HomeUiDebugState(
                                    hideVanillaBgAnim = hideVanillaBgAnim,
                                    compactLobbyBadges = compactLobbyBadges,
                                    showDebugOutline = showDebugOutline,
                                    customHudOpacity = customHudOpacity,
                                    hideLobbyAds = hideLobbyAds,
                                )
                            ),
                            "已保存主界面调试配置。",
                        )
                    },
                ) {
                    Text("保存主界面配置")
                }
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun AppearanceSection(controller: AppController) {
    var hexInput by remember(controller.customThemeHex) { mutableStateOf(controller.customThemeHex) }

    // Live Theme Preview Card (flat Surface prevents ElevatedCard translucent tonal artifact)
    Surface(
        color = MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.90f),
        shape = MaterialTheme.shapes.large,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.primary.copy(alpha = 0.24f)),
        modifier = Modifier.fillMaxWidth(),
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            Surface(
                color = MaterialTheme.colorScheme.primary,
                shape = CircleShape,
                modifier = Modifier.size(42.dp),
            ) {
                Box(contentAlignment = Alignment.Center) {
                    Icon(Icons.Rounded.ColorLens, contentDescription = null, tint = MaterialTheme.colorScheme.onPrimary)
                }
            }
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = "星枢 Material You 外观引擎",
                    style = MaterialTheme.typography.titleMedium,
                    color = MaterialTheme.colorScheme.onPrimaryContainer,
                )
                Text(
                    text = "明暗：${controller.brightnessMode.labelZh} · 主题：${
                        if (controller.themeId == "custom") "自定义 ${controller.customThemeHex}"
                        else ThemePalette.byId(controller.themeId).nameZh
                    }",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onPrimaryContainer,
                )
            }
        }
    }

    // Brightness Mode Card
    OutlinedCard(
        colors = CardDefaults.outlinedCardColors(
            containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.92f),
        ),
        shape = MaterialTheme.shapes.large,
    ) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text("界面明暗模式", style = MaterialTheme.typography.titleMedium)
            val modes = ThemeBrightnessMode.entries
            SingleChoiceSegmentedButtonRow(modifier = Modifier.fillMaxWidth()) {
                modes.forEachIndexed { idx, mode ->
                    SegmentedButton(
                        selected = controller.brightnessMode == mode,
                        onClick = { controller.selectBrightnessMode(mode) },
                        shape = SegmentedButtonDefaults.itemShape(index = idx, count = modes.size),
                    ) {
                        Text(mode.labelZh)
                    }
                }
            }
        }
    }

    // Theme Preset Swatches & Custom Hex
    OutlinedCard(
        colors = CardDefaults.outlinedCardColors(
            containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.92f),
        ),
        shape = MaterialTheme.shapes.large,
    ) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Text("星枢官方色板（与桌面端同步）", style = MaterialTheme.typography.titleMedium)
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                ThemePalette.PRESETS.toList().chunked(3).forEach { rowPresets ->
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        rowPresets.forEach { preset ->
                            val selected = controller.themeId == preset.id
                            Column(
                                horizontalAlignment = Alignment.CenterHorizontally,
                                modifier = Modifier
                                    .weight(1f)
                                    .clip(RoundedCornerShape(12.dp))
                                    .clickable { controller.selectThemePreset(preset.id) }
                                    .padding(vertical = 8.dp, horizontal = 4.dp),
                            ) {
                                Box(
                                    modifier = Modifier
                                        .size(44.dp)
                                        .clip(CircleShape)
                                        .background(Color(preset.primary))
                                        .border(
                                            width = if (selected) 3.dp else 1.dp,
                                            color = if (selected) MaterialTheme.colorScheme.onSurface else MaterialTheme.colorScheme.outlineVariant,
                                            shape = CircleShape,
                                        ),
                                    contentAlignment = Alignment.Center,
                                ) {
                                    if (selected) {
                                        Icon(Icons.Rounded.Check, contentDescription = null, tint = Color.White)
                                    }
                                }
                                Spacer(modifier = Modifier.height(6.dp))
                                Text(preset.nameZh, style = MaterialTheme.typography.labelMedium, maxLines = 1)
                            }
                        }
                        repeat(3 - rowPresets.size) {
                            Spacer(modifier = Modifier.weight(1f))
                        }
                    }
                }
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.Bottom,
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                OutlinedTextField(
                    value = hexInput,
                    onValueChange = { hexInput = it.take(7) },
                    label = { Text("自定义 Hex 主色") },
                    placeholder = { Text("#1370F3") },
                    singleLine = true,
                    shape = RoundedCornerShape(14.dp),
                    modifier = Modifier.weight(1f),
                )
                Button(
                    onClick = { controller.applyCustomHexColor(hexInput) },
                    shape = RoundedCornerShape(14.dp),
                    contentPadding = PaddingValues(horizontal = 20.dp),
                    modifier = Modifier.height(54.dp),
                ) {
                    Text("应用")
                }
            }
        }
    }

    // Custom Background Wallpaper
    OutlinedCard(
        colors = CardDefaults.outlinedCardColors(
            containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.92f),
        ),
        shape = MaterialTheme.shapes.large,
    ) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Text("自定义启动器背景壁纸", style = MaterialTheme.typography.titleMedium)
            Text(
                text = if (controller.wallpaperBitmap != null) "已启用相册自定义背景；下方滑块可调节磨砂遮罩强度保证文字清晰度。" else "支持从手机相册选择任意图片作为背景，自动叠加坡度遮罩。",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )

            if (controller.wallpaperBitmap != null) {
                Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        Text(
                            text = "背景可视透明度",
                            style = MaterialTheme.typography.labelMedium,
                        )
                        Surface(
                            color = MaterialTheme.colorScheme.primaryContainer,
                            shape = RoundedCornerShape(999.dp),
                        ) {
                            Text(
                                text = "${(controller.wallpaperAlpha * 100).toInt()}%",
                                style = MaterialTheme.typography.labelMedium,
                                color = MaterialTheme.colorScheme.onPrimaryContainer,
                                modifier = Modifier.padding(horizontal = 10.dp, vertical = 2.dp),
                            )
                        }
                    }
                    Slider(
                        value = controller.wallpaperAlpha,
                        onValueChange = { controller.updateWallpaperAlpha(it) },
                        valueRange = 0.08f..0.72f,
                    )
                }
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                FilledTonalButton(
                    onClick = { controller.requestPickWallpaper() },
                    modifier = Modifier.weight(1f),
                ) {
                    Icon(Icons.Rounded.Image, contentDescription = null, modifier = Modifier.size(18.dp))
                    Spacer(modifier = Modifier.width(6.dp))
                    Text("选择相册图片")
                }
                OutlinedButton(
                    onClick = { controller.clearWallpaper() },
                    enabled = controller.wallpaperBitmap != null,
                ) {
                    Icon(Icons.Rounded.Restore, contentDescription = null, modifier = Modifier.size(18.dp))
                    Spacer(modifier = Modifier.width(4.dp))
                    Text("恢复默认")
                }
            }
        }
    }
}

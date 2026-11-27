package app.xingshu.installer

import android.graphics.ImageDecoder
import android.graphics.drawable.AnimatedImageDrawable
import android.graphics.drawable.Drawable
import android.os.Build
import android.widget.ImageView
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items as gridItems
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.AccountCircle
import androidx.compose.material.icons.rounded.Add
import androidx.compose.material.icons.rounded.AutoFixHigh
import androidx.compose.material.icons.rounded.Clear
import androidx.compose.material.icons.rounded.DeleteOutline
import androidx.compose.material.icons.rounded.Download
import androidx.compose.material.icons.rounded.Image
import androidx.compose.material.icons.rounded.Login
import androidx.compose.material.icons.rounded.Logout
import androidx.compose.material.icons.rounded.Mood
import androidx.compose.material.icons.rounded.OpenInNew
import androidx.compose.material.icons.rounded.Refresh
import androidx.compose.material.icons.rounded.Search
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.FilledTonalButton
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedCard
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import java.io.File
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

@OptIn(ExperimentalLayoutApi::class, ExperimentalMaterial3Api::class)
@Composable
internal fun WorkshopScreen(controller: AppController) {
    val context = LocalContext.current
    val cacheDir = remember(context) { PayloadDownloadManager.cacheDir(context) }
    var searchQuery by remember { mutableStateOf("") }
    val kindFilter = controller.workshopKindFilter
    val isTablet = LocalConfiguration.current.screenWidthDp >= 600

    LaunchedEffect(Unit) {
        controller.refreshWorkshopAsync(userInitiated = false)
        if (controller.platformToken.isNotBlank() && controller.platformUser == null) {
            controller.refreshPlatformAccountAsync()
        }
    }

    val allMods = controller.workshopMods.toList()
    val installableCount = remember(allMods) { allMods.count { it.androidInstallable } }
    val communityCount = remember(allMods) { allMods.count { !it.isXspkgModule() } }
    val xspkgCount = remember(allMods) { allMods.count { it.isXspkgModule() } }

    val filteredMods = remember(allMods, searchQuery, kindFilter) {
        val q = searchQuery.trim().lowercase()
        allMods.filter { mod ->
            val kindOk = when (kindFilter) {
                "android" -> mod.androidInstallable
                "community" -> !mod.isXspkgModule()
                "xspkg" -> mod.isXspkgModule()
                "card" -> mod.category.equals("card", ignoreCase = true) || mod.categoryLabel.contains("卡牌")
                "character" -> mod.category.equals("character", ignoreCase = true) || mod.categoryLabel.contains("角色")
                "audio" -> mod.category.equals("audio", ignoreCase = true) || mod.categoryLabel.contains("音效") || mod.categoryLabel.contains("语音")
                else -> true
            }
            val queryOk = q.isEmpty() ||
                mod.name.lowercase().contains(q) ||
                mod.author.lowercase().contains(q) ||
                mod.description.lowercase().contains(q) ||
                mod.categoryLabel.lowercase().contains(q) ||
                mod.platformLabel.lowercase().contains(q)
            kindOk && queryOk
        }
    }

    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .padding(horizontal = if (isTablet) 24.dp else 16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        // Platform Account Status Bar for Emote Reactions
        item {
            Spacer(modifier = Modifier.height(4.dp))
            OutlinedCard(
                colors = CardDefaults.outlinedCardColors(
                    containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.90f),
                ),
                shape = RoundedCornerShape(16.dp),
            ) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 12.dp, vertical = 8.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(10.dp),
                ) {
                    Icon(
                        imageVector = Icons.Rounded.AccountCircle,
                        contentDescription = null,
                        tint = if (controller.platformUser != null) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.size(28.dp),
                    )
                    val pUser = controller.platformUser
                    Column(modifier = Modifier.weight(1f)) {
                        Text(
                            text = if (pUser != null) {
                                "${pUser.displayName.ifEmpty { pUser.username }} · UID ${pUser.uid}"
                            } else {
                                "星枢平台账号 · 未登录"
                            },
                            style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
                            maxLines = 1,
                            overflow = TextOverflow.Ellipsis,
                        )
                        Text(
                            text = if (pUser != null) "已登录，点击工坊卡片底部角色表情即可表态回应" else "登录后可为模组发送游戏角色表情回应",
                            style = MaterialTheme.typography.labelSmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            maxLines = 1,
                            overflow = TextOverflow.Ellipsis,
                        )
                    }
                    if (pUser != null) {
                        TextButton(onClick = { controller.platformLogout() }) {
                            Icon(Icons.Rounded.Logout, contentDescription = null, modifier = Modifier.size(16.dp))
                            Spacer(modifier = Modifier.width(4.dp))
                            Text("退出")
                        }
                    } else {
                        FilledTonalButton(
                            onClick = {
                                controller.pendingReactionAfterLogin = null
                                controller.platformAuthError = ""
                                controller.showPlatformLoginDialog = true
                            },
                            contentPadding = PaddingValues(horizontal = 12.dp, vertical = 6.dp),
                        ) {
                            Icon(Icons.Rounded.Login, contentDescription = null, modifier = Modifier.size(16.dp))
                            Spacer(modifier = Modifier.width(4.dp))
                            Text("登录 / 注册")
                        }
                    }
                }
            }
        }

        item {
            OutlinedTextField(
                value = searchQuery,
                onValueChange = { searchQuery = it },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true,
                placeholder = { Text("搜索工坊 Mod 名称、作者、平台或说明…") },
                leadingIcon = { Icon(Icons.Rounded.Search, contentDescription = null) },
                trailingIcon = {
                    if (searchQuery.isNotEmpty()) {
                        IconButton(onClick = { searchQuery = "" }) {
                            Icon(Icons.Rounded.Clear, contentDescription = "清空搜索")
                        }
                    }
                },
                shape = RoundedCornerShape(16.dp),
            )
        }

        item {
            Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    LazyRow(
                        modifier = Modifier.weight(1f),
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        val filters = listOf(
                            "all" to "全部 (${allMods.size})",
                            "android" to "手机可装 ($installableCount)",
                            "community" to "社区模组 ($communityCount)",
                            "xspkg" to "官方优化 ($xspkgCount)",
                            "card" to "卡牌替换",
                            "character" to "角色皮肤",
                            "audio" to "语音/音效",
                        )
                        items(filters, key = { it.first }) { (key, text) ->
                            FilterChip(
                                selected = kindFilter == key,
                                onClick = { controller.workshopKindFilter = key },
                                label = { Text(text, maxLines = 1, softWrap = false) },
                            )
                        }
                    }
                    IconButton(
                        onClick = { controller.refreshWorkshopAsync(userInitiated = true) },
                        enabled = !controller.workshopLoading,
                    ) {
                        Icon(Icons.Rounded.Refresh, contentDescription = "刷新工坊")
                    }
                }

                if (controller.workshopLoading) {
                    LinearProgressIndicator(modifier = Modifier.fillMaxWidth())
                }

                Text(
                    text = buildString {
                        append("实时工坊 · 共 ${allMods.size} 项（手机可直装 $installableCount 项）")
                        if (controller.workshopLastSyncTime.isNotEmpty()) {
                            append(" · 同步于 ${controller.workshopLastSyncTime}")
                        }
                        if (controller.workshopLoading) {
                            append(" · 同步中…")
                        }
                    },
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )

                if (controller.workshopError.isNotEmpty() && allMods.isNotEmpty()) {
                    Text(
                        text = "同步提示：${controller.workshopError}",
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.error,
                    )
                }
            }
        }

        if (controller.workshopLoading && controller.workshopMods.isEmpty()) {
            item {
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(vertical = 48.dp),
                    contentAlignment = Alignment.Center,
                ) {
                    Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(12.dp)) {
                        CircularProgressIndicator()
                        Text("正在实时同步星枢工坊目录…", style = MaterialTheme.typography.bodySmall)
                    }
                }
            }
        } else if (controller.workshopError.isNotEmpty() && controller.workshopMods.isEmpty()) {
            item {
                OutlinedCard {
                    Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Text("工坊加载失败", style = MaterialTheme.typography.titleMedium)
                        Text(controller.workshopError, style = MaterialTheme.typography.bodySmall)
                        FilledTonalButton(onClick = { controller.refreshWorkshopAsync(userInitiated = true) }) {
                            Text("重新加载")
                        }
                    }
                }
            }
        } else if (filteredMods.isEmpty()) {
            item {
                OutlinedCard {
                    Text(
                        text = "没有找到匹配的工坊模组条目。",
                        style = MaterialTheme.typography.bodyMedium,
                        modifier = Modifier.padding(18.dp),
                    )
                }
            }
        } else if (isTablet) {
            val pairedMods = filteredMods.chunked(2)
            items(pairedMods, key = { pair -> pair.joinToString("_") { "${it.id}_${it.versionId}" } }) { pair ->
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(12.dp),
                    verticalAlignment = Alignment.Top,
                ) {
                    pair.forEach { mod ->
                        WorkshopModCard(
                            mod = mod,
                            controller = controller,
                            cacheDir = cacheDir,
                            modifier = Modifier.weight(1f),
                        )
                    }
                    if (pair.size < 2) {
                        Spacer(modifier = Modifier.weight(1f))
                    }
                }
            }
        } else {
            items(filteredMods, key = { "${it.id}_${it.versionId}" }) { mod ->
                WorkshopModCard(
                    mod = mod,
                    controller = controller,
                    cacheDir = cacheDir,
                    modifier = Modifier.fillMaxWidth(),
                )
            }
        }

        item { Spacer(modifier = Modifier.height(14.dp)) }
    }

    // Detail dialog
    val detailMod = controller.workshopDetailMod
    if (detailMod != null) {
        AlertDialog(
            onDismissRequest = { controller.workshopDetailMod = null },
            modifier = Modifier.widthIn(max = 680.dp),
            title = { Text(detailMod.name) },
            text = {
                Column(
                    modifier = Modifier.verticalScroll(rememberScrollState()),
                    verticalArrangement = Arrangement.spacedBy(8.dp),
                ) {
                    Text("作者：${detailMod.author} · 版本：v${detailMod.version} · ${detailMod.downloads} 次下载")
                    Text(
                        buildString {
                            append("适用：")
                            append(
                                if (detailMod.androidInstallable) detailMod.platformLabel.ifEmpty { "手机端 (PE)" }
                                else "仅 PC 端（请在电脑端星枢或网页工坊下载）"
                            )
                            if (detailMod.sizeBytes > 0L) {
                                append(" · 体积：${PayloadDownloadManager.formatBytes(detailMod.sizeBytes)}")
                            }
                        }
                    )
                    if (detailMod.targetInfo.isNotBlank()) {
                        Text("目标路径：${detailMod.targetInfo}", style = MaterialTheme.typography.bodySmall)
                    }
                    if (detailMod.description.isNotBlank()) {
                        Text(detailMod.description, style = MaterialTheme.typography.bodySmall)
                    }
                    if (detailMod.notes.isNotBlank()) {
                        Text("更新说明：${detailMod.notes}", style = MaterialTheme.typography.bodySmall)
                    }
                    HorizontalDivider(modifier = Modifier.padding(vertical = 4.dp))
                    Text(
                        text = "角色表情回应 (${detailMod.reactionTotal})",
                        style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
                    )
                    EmoteReactionBar(mod = detailMod, controller = controller)
                }
            },
            dismissButton = {
                TextButton(onClick = { controller.workshopDetailMod = null }) {
                    Text("关闭")
                }
            },
            confirmButton = {
                if (detailMod.androidInstallable) {
                    Button(
                        onClick = {
                            controller.workshopDetailMod = null
                            controller.pendingWorkshopPromptMod = detailMod
                        },
                        enabled = !controller.modTaskBusy && !controller.busy,
                    ) {
                        Text("下载并安装")
                    }
                } else {
                    Button(
                        onClick = {
                            val url = detailMod.webDetailUrl()
                            controller.workshopDetailMod = null
                            controller.openExternalBrowser(url)
                        },
                    ) {
                        Icon(Icons.Rounded.OpenInNew, contentDescription = null, modifier = Modifier.size(16.dp))
                        Spacer(modifier = Modifier.width(4.dp))
                        Text("网页查看")
                    }
                }
            },
        )
    }

    // Install confirmation dialog
    val promptMod = controller.pendingWorkshopPromptMod
    if (promptMod != null) {
        AlertDialog(
            onDismissRequest = { controller.pendingWorkshopPromptMod = null },
            title = { Text("确认安装「${promptMod.name}」") },
            text = {
                Text(
                    if (promptMod.isXspkgModule()) {
                        "将下载并校验 SHA-256 后写入目标渠道 xingshu/plugins/ 目录（需在「模组」页安装星枢官方插件 Mod 后生效）。"
                    } else {
                        "将下载并解压 PNG 贴图资源写入目标渠道 modimg/ 目录；可随时在「模组」页一键卸载。"
                    }
                )
            },
            dismissButton = {
                TextButton(onClick = { controller.pendingWorkshopPromptMod = null }) {
                    Text("取消")
                }
            },
            confirmButton = {
                Button(
                    onClick = {
                        controller.pendingWorkshopPromptMod = null
                        controller.downloadAndInstallWorkshopMod(promptMod)
                    },
                ) {
                    Text("确认安装")
                }
            },
        )
    }

    // Emote Picker Bottom Sheet
    val pickerMod = controller.emotePickerMod
    if (pickerMod != null) {
        val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true)
        ModalBottomSheet(
            onDismissRequest = { controller.emotePickerMod = null },
            sheetState = sheetState,
        ) {
            EmotePickerSheetContent(
                mod = pickerMod,
                controller = controller,
                onPick = { emoteId, action ->
                    controller.emotePickerMod = null
                    controller.toggleModReaction(pickerMod, emoteId, action)
                },
            )
        }
    }

    // XingShu Platform Account Login / Register Dialog
    if (controller.showPlatformLoginDialog) {
        PlatformLoginDialog(controller = controller)
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun EmoteReactionBar(
    mod: AndroidWorkshopClient.WorkshopMod,
    controller: AppController,
) {
    val viewerEmoteId = controller.platformUserReactions[mod.id] ?: ""
    val busy = controller.reactionBusyModId == mod.id
    val items = mod.reactions.take(8)

    FlowRow(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(6.dp),
        verticalArrangement = Arrangement.spacedBy(6.dp),
    ) {
        items.forEach { entry ->
            val selected = viewerEmoteId == entry.emoteId
            Surface(
                shape = RoundedCornerShape(999.dp),
                color = if (selected) MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.surfaceContainerLow,
                border = BorderStroke(
                    width = 1.dp,
                    color = if (selected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outlineVariant,
                ),
                modifier = Modifier
                    .clip(RoundedCornerShape(999.dp))
                    .clickable(enabled = !busy) {
                        controller.toggleModReaction(mod, entry.emoteId)
                    },
            ) {
                Row(
                    modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(5.dp),
                ) {
                    EmoteStickerView(
                        emoteId = entry.emoteId,
                        contentDescription = GameEmoteCatalog.displayName(entry.emoteId),
                        size = 24.dp,
                        cornerRadius = 6.dp,
                        controller = controller,
                    )
                    Text(
                        text = entry.count.toString(),
                        style = MaterialTheme.typography.labelMedium.copy(
                            fontWeight = if (selected) FontWeight.Bold else FontWeight.Medium,
                        ),
                        color = if (selected) MaterialTheme.colorScheme.onPrimaryContainer else MaterialTheme.colorScheme.onSurface,
                    )
                }
            }
        }

        Surface(
            shape = RoundedCornerShape(999.dp),
            color = MaterialTheme.colorScheme.surfaceContainerHighest.copy(alpha = 0.72f),
            border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
            modifier = Modifier
                .clip(RoundedCornerShape(999.dp))
                .clickable(enabled = !busy) {
                    controller.emotePickerMod = mod
                },
        ) {
            Row(
                modifier = Modifier.padding(horizontal = 9.dp, vertical = 5.dp),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(4.dp),
            ) {
                if (busy) {
                    CircularProgressIndicator(modifier = Modifier.size(14.dp), strokeWidth = 2.dp)
                } else {
                    Icon(
                        imageVector = Icons.Rounded.Add,
                        contentDescription = null,
                        modifier = Modifier.size(15.dp),
                        tint = MaterialTheme.colorScheme.primary,
                    )
                }
                Text(
                    text = if (viewerEmoteId.isNotEmpty()) "更换回应" else "表情回应",
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
    }
}

@Composable
private fun EmotePickerSheetContent(
    mod: AndroidWorkshopClient.WorkshopMod,
    controller: AppController,
    onPick: (String, String) -> Unit,
) {
    var selectedGroupId by remember { mutableStateOf("featured") }
    val viewerEmoteId = controller.platformUserReactions[mod.id] ?: ""
    val displayEmotes = remember(selectedGroupId) {
        if (selectedGroupId == "featured") {
            GameEmoteCatalog.FEATURED
        } else {
            GameEmoteCatalog.GROUPS.firstOrNull { it.id == selectedGroupId }?.emotes ?: emptyList()
        }
    }

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp, vertical = 6.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween,
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Text("选择角色表情回应", style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold))
                Text(
                    text = mod.name,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                )
            }
            if (viewerEmoteId.isNotEmpty()) {
                TextButton(onClick = { onPick(viewerEmoteId, "remove") }) {
                    Icon(Icons.Rounded.DeleteOutline, contentDescription = null, modifier = Modifier.size(16.dp))
                    Spacer(modifier = Modifier.width(4.dp))
                    Text("清除我的回应")
                }
            }
        }

        LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            item(key = "featured") {
                FilterChip(
                    selected = selectedGroupId == "featured",
                    onClick = { selectedGroupId = "featured" },
                    label = { Text("常用推荐", maxLines = 1, softWrap = false) },
                )
            }
            items(GameEmoteCatalog.GROUPS, key = { it.id }) { group ->
                FilterChip(
                    selected = selectedGroupId == group.id,
                    onClick = { selectedGroupId = group.id },
                    label = { Text(group.name, maxLines = 1, softWrap = false) },
                )
            }
        }

        LazyVerticalGrid(
            columns = GridCells.Fixed(4),
            modifier = Modifier
                .fillMaxWidth()
                .heightIn(min = 180.dp, max = 360.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            gridItems(displayEmotes, key = { it.id }) { emote ->
                val isSelected = viewerEmoteId == emote.id
                Surface(
                    shape = RoundedCornerShape(14.dp),
                    color = if (isSelected) MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.surfaceContainerLow,
                    border = BorderStroke(
                        width = if (isSelected) 1.5.dp else 1.dp,
                        color = if (isSelected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outlineVariant,
                    ),
                    modifier = Modifier
                        .clip(RoundedCornerShape(14.dp))
                        .clickable { onPick(emote.id, "toggle") },
                ) {
                    Column(
                        modifier = Modifier.padding(8.dp),
                        horizontalAlignment = Alignment.CenterHorizontally,
                        verticalArrangement = Arrangement.spacedBy(4.dp),
                    ) {
                        EmoteStickerView(
                            emoteId = emote.id,
                            contentDescription = emote.name,
                            size = 46.dp,
                            cornerRadius = 8.dp,
                            controller = controller,
                        )
                        Text(
                            text = if (emote.animated) "${emote.name.replace("（动态）", "")}·动" else emote.name,
                            style = MaterialTheme.typography.labelSmall,
                            maxLines = 1,
                            overflow = TextOverflow.Ellipsis,
                            textAlign = TextAlign.Center,
                        )
                    }
                }
            }
        }
        Spacer(modifier = Modifier.height(12.dp))
    }
}

@Composable
private fun PlatformLoginDialog(controller: AppController) {
    var modeTab by remember { mutableStateOf(0) } // 0=login, 1=register
    var username by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }
    var displayName by remember { mutableStateOf("") }

    AlertDialog(
        onDismissRequest = {
            if (!controller.platformAuthBusy) {
                controller.showPlatformLoginDialog = false
                controller.pendingReactionAfterLogin = null
            }
        },
        title = { Text("星枢平台账号") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Text(
                    text = "登录或注册独立的星枢平台账号后，即可在手机端工坊发送游戏角色表情回应，并与网页端/电脑端同步。",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                TabRow(selectedTabIndex = modeTab) {
                    Tab(selected = modeTab == 0, onClick = { modeTab = 0 }, text = { Text("登录") })
                    Tab(selected = modeTab == 1, onClick = { modeTab = 1 }, text = { Text("注册") })
                }
                OutlinedTextField(
                    value = username,
                    onValueChange = { username = it },
                    label = { Text("用户名") },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth(),
                )
                if (modeTab == 1) {
                    OutlinedTextField(
                        value = displayName,
                        onValueChange = { displayName = it },
                        label = { Text("昵称（选填）") },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth(),
                    )
                }
                OutlinedTextField(
                    value = password,
                    onValueChange = { password = it },
                    label = { Text("密码") },
                    singleLine = true,
                    visualTransformation = PasswordVisualTransformation(),
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
                    modifier = Modifier.fillMaxWidth(),
                )
                if (controller.platformAuthError.isNotBlank()) {
                    Text(
                        text = controller.platformAuthError,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.error,
                    )
                }
            }
        },
        dismissButton = {
            TextButton(
                enabled = !controller.platformAuthBusy,
                onClick = {
                    controller.showPlatformLoginDialog = false
                    controller.pendingReactionAfterLogin = null
                },
            ) {
                Text("取消")
            }
        },
        confirmButton = {
            Button(
                enabled = !controller.platformAuthBusy,
                onClick = {
                    if (modeTab == 0) {
                        controller.platformLogin(username, password)
                    } else {
                        controller.platformRegister(username, password, displayName)
                    }
                },
            ) {
                if (controller.platformAuthBusy) {
                    CircularProgressIndicator(modifier = Modifier.size(16.dp), strokeWidth = 2.dp)
                    Spacer(modifier = Modifier.width(6.dp))
                }
                Text(if (modeTab == 0) "登录" else "注册并登录")
            }
        },
    )
}

@Composable
private fun EmoteStickerView(
    emoteId: String,
    contentDescription: String,
    size: Dp,
    cornerRadius: Dp,
    controller: AppController,
) {
    LaunchedEffect(emoteId) {
        controller.requestEmoteBitmap(emoteId)
    }
    val meta = remember(emoteId) { GameEmoteCatalog.findById(emoteId) }
    val emoteFile = controller.emoteFiles[emoteId]
    val fallbackBmp = controller.emoteBitmaps[emoteId]
    val shouldAnimate = Build.VERSION.SDK_INT >= 28 && (meta?.animated == true) && emoteFile != null

    val animDrawable by produceState<Drawable?>(initialValue = null, key1 = emoteId, key2 = emoteFile, key3 = shouldAnimate) {
        value = if (!shouldAnimate || emoteFile == null) {
            null
        } else {
            withContext(Dispatchers.IO) {
                try {
                    val src = ImageDecoder.createSource(emoteFile)
                    ImageDecoder.decodeDrawable(src) { decoder, _, _ ->
                        decoder.allocator = ImageDecoder.ALLOCATOR_SOFTWARE
                    }
                } catch (_: Exception) {
                    null
                }
            }
        }
    }

    if (Build.VERSION.SDK_INT >= 28 && animDrawable is AnimatedImageDrawable) {
        val anim = animDrawable as AnimatedImageDrawable
        DisposableEffect(anim) {
            anim.repeatCount = AnimatedImageDrawable.REPEAT_INFINITE
            anim.start()
            onDispose {
                try { anim.stop() } catch (_: Exception) { }
            }
        }
        AndroidView(
            factory = { ctx ->
                ImageView(ctx).apply {
                    scaleType = ImageView.ScaleType.FIT_CENTER
                    adjustViewBounds = true
                    setImageDrawable(anim)
                }
            },
            update = { iv ->
                if (iv.drawable !== anim) {
                    iv.setImageDrawable(anim)
                }
                if (!anim.isRunning) {
                    anim.start()
                }
            },
            modifier = Modifier
                .size(size)
                .clip(RoundedCornerShape(cornerRadius)),
        )
    } else if (fallbackBmp != null) {
        Image(
            bitmap = fallbackBmp,
            contentDescription = contentDescription,
            contentScale = ContentScale.Fit,
            modifier = Modifier
                .size(size)
                .clip(RoundedCornerShape(cornerRadius)),
        )
    } else {
        Box(
            modifier = Modifier
                .size(size)
                .background(MaterialTheme.colorScheme.surfaceContainerHigh, RoundedCornerShape(cornerRadius)),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                imageVector = Icons.Rounded.Mood,
                contentDescription = contentDescription,
                modifier = Modifier.size((size.value * 0.72f).dp),
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

@Composable
private fun BadgeChip(label: String) {
    Surface(
        color = MaterialTheme.colorScheme.secondaryContainer,
        shape = RoundedCornerShape(999.dp),
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.labelMedium,
            color = MaterialTheme.colorScheme.onSecondaryContainer,
            modifier = Modifier.padding(horizontal = 8.dp, vertical = 2.dp),
        )
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun WorkshopModCard(
    mod: AndroidWorkshopClient.WorkshopMod,
    controller: AppController,
    cacheDir: File,
    modifier: Modifier = Modifier,
) {
    val previewUrl = mod.previewUrl
    LaunchedEffect(previewUrl) {
        if (previewUrl.isNotBlank()) {
            controller.requestWorkshopThumbnail(previewUrl)
        }
    }
    val thumb = if (previewUrl.isNotBlank()) controller.thumbnails[previewUrl] else null
    val isInstalled = remember(
        mod.id,
        mod.fileName,
        controller.installedMods.size,
        controller.officialModuleStates.size,
    ) {
        controller.installedMods.any {
            it.id == mod.id ||
                (mod.fileName.isNotBlank() && it.name.equals(mod.fileName, ignoreCase = true))
        } || (mod.isXspkgModule() && controller.officialModuleStates.containsKey(mod.id))
    }
    val isCached = remember(mod.sha256, mod.sizeBytes, controller.cacheBytes) {
        mod.androidInstallable &&
            mod.sha256.isNotBlank() &&
            PayloadDownloadManager.getCachedIfValid(cacheDir, mod.sha256, mod.sizeBytes) != null
    }
    val progress = controller.modProgress
    val isDownloading = progress.modId == mod.id

    OutlinedCard(
        onClick = { controller.workshopDetailMod = mod },
        modifier = modifier,
        colors = CardDefaults.outlinedCardColors(
            containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.94f),
        ),
        shape = MaterialTheme.shapes.large,
    ) {
        Column(modifier = Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(12.dp),
                verticalAlignment = Alignment.Top,
            ) {
                Surface(
                    shape = RoundedCornerShape(12.dp),
                    color = MaterialTheme.colorScheme.surfaceContainerHigh,
                    modifier = Modifier.size(64.dp),
                ) {
                    if (thumb != null) {
                        Image(
                            bitmap = thumb,
                            contentDescription = mod.name,
                            contentScale = ContentScale.Crop,
                            modifier = Modifier.fillMaxSize(),
                        )
                    } else {
                        Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                            Icon(
                                imageVector = if (mod.isXspkgModule()) Icons.Rounded.AutoFixHigh else Icons.Rounded.Image,
                                contentDescription = null,
                                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                                modifier = Modifier.size(28.dp),
                            )
                        }
                    }
                }
                Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    Text(
                        text = mod.name,
                        style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis,
                    )
                    Text(
                        text = buildString {
                            append(mod.author.ifEmpty { "星枢社区" })
                            if (mod.version.isNotBlank()) append(" · v${mod.version}")
                            append(" · ${mod.downloads} 次下载")
                            if (mod.sizeBytes > 0L) append(" · ${PayloadDownloadManager.formatBytes(mod.sizeBytes)}")
                        },
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis,
                    )
                }
            }

            FlowRow(
                horizontalArrangement = Arrangement.spacedBy(6.dp),
                verticalArrangement = Arrangement.spacedBy(6.dp),
            ) {
                if (mod.categoryLabel.isNotBlank()) {
                    BadgeChip(mod.categoryLabel)
                }
                BadgeChip(
                    if (mod.androidInstallable) mod.platformLabel.ifEmpty { "手机端 (PE)" }
                    else "仅 PC 端"
                )
                if (isInstalled) {
                    BadgeChip("已安装")
                } else if (isCached) {
                    BadgeChip("已缓存")
                }
            }

            if (mod.description.isNotBlank()) {
                Text(
                    text = mod.description,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 2,
                    overflow = TextOverflow.Ellipsis,
                )
            }

            EmoteReactionBar(mod = mod, controller = controller)

            if (isDownloading) {
                val frac = if (progress.total > 0L) (progress.received.toFloat() / progress.total.toFloat()).coerceIn(0f, 1f) else 0f
                Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    Text(
                        text = "${progress.stage} (${PayloadDownloadManager.formatBytes(progress.received)} / ${PayloadDownloadManager.formatBytes(progress.total)})",
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.primary,
                    )
                    if (progress.total > 0L) {
                        LinearProgressIndicator(progress = { frac }, modifier = Modifier.fillMaxWidth())
                    } else {
                        LinearProgressIndicator(modifier = Modifier.fillMaxWidth())
                    }
                }
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                OutlinedButton(
                    onClick = { controller.workshopDetailMod = mod },
                    contentPadding = PaddingValues(horizontal = 12.dp, vertical = 6.dp),
                ) {
                    Text("详情")
                }
                if (mod.androidInstallable) {
                    Button(
                        onClick = { controller.pendingWorkshopPromptMod = mod },
                        enabled = !controller.modTaskBusy && !controller.busy,
                        modifier = Modifier.weight(1f),
                        contentPadding = PaddingValues(horizontal = 12.dp, vertical = 6.dp),
                    ) {
                        Icon(Icons.Rounded.Download, contentDescription = null, modifier = Modifier.size(16.dp))
                        Spacer(modifier = Modifier.width(4.dp))
                        Text(if (isInstalled) "重新安装" else "下载并安装")
                    }
                } else {
                    FilledTonalButton(
                        onClick = { controller.openExternalBrowser(mod.webDetailUrl()) },
                        modifier = Modifier.weight(1f),
                        contentPadding = PaddingValues(horizontal = 12.dp, vertical = 6.dp),
                    ) {
                        Icon(Icons.Rounded.OpenInNew, contentDescription = null, modifier = Modifier.size(16.dp))
                        Spacer(modifier = Modifier.width(4.dp))
                        Text("网页查看")
                    }
                }
            }
        }
    }
}

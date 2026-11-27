package app.xingshu.installer

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.AutoAwesome
import androidx.compose.material.icons.rounded.ExpandLess
import androidx.compose.material.icons.rounded.ExpandMore
import androidx.compose.material.icons.rounded.Extension
import androidx.compose.material.icons.rounded.PlayArrow
import androidx.compose.material.icons.rounded.Refresh
import androidx.compose.material.icons.rounded.Security
import androidx.compose.material.icons.rounded.SportsEsports
import androidx.compose.material.icons.rounded.Terminal
import androidx.compose.material.icons.rounded.Usb
import androidx.compose.material3.AssistChip
import androidx.compose.material3.Button
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ElevatedCard
import androidx.compose.material3.FilterChip
import androidx.compose.material3.FilledTonalButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedCard
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp

@Composable
internal fun LoaderScreen(
    controller: AppController,
    onOpenMods: () -> Unit,
    onOpenCustomize: () -> Unit,
) {
    val selected = controller.selectedChannel()
    val privReady = controller.isPrivilegeReady()
    val enabledModulesCount = controller.officialModuleStates.count { it.value == "enabled" }
    val coreState = selected?.installState ?: "unverified"
    val isTablet = LocalConfiguration.current.screenWidthDp >= 600

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = if (isTablet) 24.dp else 16.dp, vertical = 14.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        if (isTablet) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(14.dp),
                verticalAlignment = Alignment.Top,
            ) {
                Column(
                    modifier = Modifier.weight(1.05f),
                    verticalArrangement = Arrangement.spacedBy(12.dp),
                ) {
                    LoaderHeroCard(controller, selected, onOpenMods)
                    LoaderCustomizeShortcutCard(onOpenCustomize)
                }
                Column(
                    modifier = Modifier.weight(0.95f),
                    verticalArrangement = Arrangement.spacedBy(12.dp),
                ) {
                    LoaderRuntimeEnvCard(controller, privReady, coreState, enabledModulesCount)
                    LoaderRuntimeLogCard(controller)
                }
            }
        } else {
            LoaderHeroCard(controller, selected, onOpenMods)
            LoaderRuntimeEnvCard(controller, privReady, coreState, enabledModulesCount)
            LoaderCustomizeShortcutCard(onOpenCustomize)
            LoaderRuntimeLogCard(controller)
        }
        Spacer(modifier = Modifier.height(6.dp))
    }
}

@Composable
private fun LoaderHeroCard(
    controller: AppController,
    selected: ChannelEntry?,
    onOpenMods: () -> Unit,
) {
    // Pure Vanilla Launcher Hero Card
    OutlinedCard(
        colors = CardDefaults.outlinedCardColors(
            containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.92f),
        ),
        shape = MaterialTheme.shapes.large,
    ) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(12.dp),
            ) {
                Image(
                    painter = painterResource(id = R.drawable.ic_xingshu_brand),
                    contentDescription = "星枢启动器",
                    modifier = Modifier
                        .size(52.dp)
                        .clip(RoundedCornerShape(14.dp)),
                )
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        text = "吉星派对 · 纯净启动台",
                        style = MaterialTheme.typography.titleLarge,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                    )
                    Spacer(modifier = Modifier.height(2.dp))
                    Text(
                        text = selected?.let { "${it.label} (${it.packageName})" } ?: "尚未选择游戏渠道",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                    )
                }
            }

            if (controller.channels.isNotEmpty()) {
                LazyRow(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                ) {
                    items(controller.channels, key = { it.packageName }) { ch ->
                        val isSelected = ch.packageName == controller.selectedPackageName
                        FilterChip(
                            selected = isSelected,
                            onClick = { controller.selectChannel(ch.packageName) },
                            label = {
                                Text(
                                    text = if (ch.versionName.isNotEmpty()) "${ch.label} ${ch.versionName}" else ch.label,
                                    maxLines = 1,
                                    softWrap = false,
                                )
                            },
                            leadingIcon = if (isSelected) {
                                { Icon(Icons.Rounded.SportsEsports, contentDescription = null, modifier = Modifier.size(16.dp)) }
                            } else null,
                        )
                    }
                }
            }

            Button(
                onClick = { controller.launchSelectedGame() },
                modifier = Modifier
                    .fillMaxWidth()
                    .height(50.dp),
                shape = RoundedCornerShape(15.dp),
            ) {
                Icon(Icons.Rounded.PlayArrow, contentDescription = null, modifier = Modifier.size(22.dp))
                Spacer(modifier = Modifier.width(8.dp))
                Text(
                    text = "启动游戏",
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                )
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                FilledTonalButton(
                    onClick = onOpenMods,
                    contentPadding = PaddingValues(horizontal = 12.dp, vertical = 8.dp),
                    modifier = Modifier.weight(1f),
                ) {
                    Icon(Icons.Rounded.Extension, contentDescription = null, modifier = Modifier.size(18.dp))
                    Spacer(modifier = Modifier.width(6.dp))
                    Text("星枢官方插件 / 模组", maxLines = 1, softWrap = false)
                }
                FilledTonalButton(
                    onClick = { controller.scanPackagesAndInspect() },
                    enabled = !controller.busy,
                    contentPadding = PaddingValues(horizontal = 14.dp, vertical = 8.dp),
                ) {
                    Icon(Icons.Rounded.Refresh, contentDescription = null, modifier = Modifier.size(18.dp))
                    Spacer(modifier = Modifier.width(6.dp))
                    Text("刷新", maxLines = 1, softWrap = false)
                }
            }
        }
    }
}

@Composable
private fun LoaderRuntimeEnvCard(
    controller: AppController,
    privReady: Boolean,
    coreState: String,
    enabledModulesCount: Int,
) {
    // Compact Status Overview Card
    OutlinedCard(
        colors = CardDefaults.outlinedCardColors(
            containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.90f),
        ),
        shape = MaterialTheme.shapes.large,
    ) {
        Column(modifier = Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text(
                    text = "当前运行环境",
                    style = MaterialTheme.typography.titleMedium,
                )
                AssistChip(
                    onClick = { controller.showConnectSheet = true },
                    label = { Text(if (privReady) controller.privilegeChipLabel() else "配置授权") },
                    leadingIcon = {
                        Icon(
                            Icons.Rounded.Usb,
                            contentDescription = null,
                            modifier = Modifier.size(16.dp),
                        )
                    },
                )
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                StatusMetricTile(
                    title = "星枢官方插件",
                    value = when (coreState) {
                        "installed" -> "已安装"
                        "upgrade" -> "有新版可更新"
                        "conflict" -> "存在冲突"
                        "vanilla" -> "未安装 · 纯净"
                        "unverified" -> if (privReady) "待检测" else "纯净可用"
                        else -> coreState
                    },
                    modifier = Modifier.weight(1f),
                )
                StatusMetricTile(
                    title = "官方功能模组",
                    value = "$enabledModulesCount / ${OfficialModulesCatalog.ALL.size} 项",
                    modifier = Modifier.weight(1f),
                )
                StatusMetricTile(
                    title = "游戏账号",
                    value = controller.boundSession?.let { "UID ${it.uid}" } ?: "未登录",
                    modifier = Modifier.weight(1f),
                )
            }

            if (controller.runtimeProgress.active) {
                Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    val prog = controller.runtimeProgress
                    val frac = if (prog.total > 0L) (prog.received.toFloat() / prog.total.toFloat()).coerceIn(0f, 1f) else 0f
                    Text(
                        text = prog.stage,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.primary,
                    )
                    if (prog.total > 0L) {
                        LinearProgressIndicator(progress = { frac }, modifier = Modifier.fillMaxWidth())
                    } else {
                        LinearProgressIndicator(modifier = Modifier.fillMaxWidth())
                    }
                }
            }

            Surface(
                color = MaterialTheme.colorScheme.surfaceContainerHigh,
                shape = RoundedCornerShape(12.dp),
            ) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 12.dp, vertical = 10.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(10.dp),
                ) {
                    Icon(
                        Icons.Rounded.Security,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.size(18.dp),
                    )
                    Text(
                        text = "默认纯净启动原版；需开启悬浮球、${OfficialModulesCatalog.ALL.size} 项星枢功能或局内开房时，前往「模组」页按需安装核心插件。",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.weight(1f),
                    )
                }
            }
        }
    }
}

@Composable
private fun LoaderCustomizeShortcutCard(onOpenCustomize: () -> Unit) {
    // Quick Customize Banner
    OutlinedCard(
        onClick = onOpenCustomize,
        colors = CardDefaults.outlinedCardColors(
            containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.88f),
        ),
        shape = MaterialTheme.shapes.large,
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 16.dp, vertical = 12.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween,
        ) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(10.dp),
                modifier = Modifier.weight(1f),
            ) {
                Icon(Icons.Rounded.AutoAwesome, contentDescription = null, tint = MaterialTheme.colorScheme.primary)
                Column {
                    Text("个性化主题与壁纸", style = MaterialTheme.typography.titleMedium)
                    Text(
                        text = "支持动态深浅色、6 套星枢色板、自定义 Hex 与相册背景",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
            TextButton(onClick = onOpenCustomize) {
                Text("设置")
            }
        }
    }
}

@Composable
private fun LoaderRuntimeLogCard(controller: AppController) {
    // Collapsible Runtime Log Card
    OutlinedCard(
        colors = CardDefaults.outlinedCardColors(
            containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.88f),
        ),
        shape = MaterialTheme.shapes.medium,
    ) {
        Column(modifier = Modifier.padding(14.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween,
            ) {
                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Icon(Icons.Rounded.Terminal, contentDescription = null, modifier = Modifier.size(18.dp))
                    Text("运行状态", style = MaterialTheme.typography.titleMedium)
                }
                IconButton(onClick = { controller.logExpanded = !controller.logExpanded }) {
                    Icon(
                        if (controller.logExpanded) Icons.Rounded.ExpandLess else Icons.Rounded.ExpandMore,
                        contentDescription = "展开或收起日志",
                    )
                }
            }
            Text(
                text = controller.statusText,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            AnimatedVisibility(visible = controller.logExpanded) {
                Column(
                    modifier = Modifier.padding(top = 10.dp),
                    verticalArrangement = Arrangement.spacedBy(4.dp),
                ) {
                    if (controller.logs.isEmpty()) {
                        Text("暂无操作记录。", style = MaterialTheme.typography.bodySmall)
                    } else {
                        controller.logs.take(15).forEach { line ->
                            Text(
                                text = line,
                                style = MaterialTheme.typography.bodySmall.copy(fontFamily = FontFamily.Monospace),
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun StatusMetricTile(
    title: String,
    value: String,
    modifier: Modifier = Modifier,
) {
    Surface(
        modifier = modifier,
        color = MaterialTheme.colorScheme.surfaceContainerLow,
        shape = RoundedCornerShape(14.dp),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.55f)),
    ) {
        Column(modifier = Modifier.padding(horizontal = 10.dp, vertical = 10.dp)) {
            Text(
                text = title,
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
            Spacer(modifier = Modifier.height(3.dp))
            Text(
                text = value,
                style = MaterialTheme.typography.titleMedium,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
        }
    }
}

package app.xingshu.installer

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
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.AccountCircle
import androidx.compose.material.icons.rounded.CleaningServices
import androidx.compose.material.icons.rounded.Code
import androidx.compose.material.icons.rounded.Gamepad
import androidx.compose.material.icons.rounded.Info
import androidx.compose.material.icons.rounded.OpenInNew
import androidx.compose.material.icons.rounded.Public
import androidx.compose.material.icons.rounded.SaveAlt
import androidx.compose.material.icons.rounded.SystemUpdate
import androidx.compose.material.icons.rounded.Usb
import androidx.compose.material3.Button
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.FilledTonalButton
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedCard
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp

@Composable
internal fun SettingsScreen(controller: AppController) {
    val privReady = controller.isPrivilegeReady()
    val isTablet = LocalConfiguration.current.screenWidthDp >= 600

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = if (isTablet) 24.dp else 16.dp, vertical = 14.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp),
    ) {
        if (isTablet) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(14.dp),
                verticalAlignment = Alignment.Top,
            ) {
                Column(
                    modifier = Modifier.weight(1f),
                    verticalArrangement = Arrangement.spacedBy(14.dp),
                ) {
                    SettingsPrivilegeCard(controller, privReady)
                    SettingsAccountSyncCard(controller)
                    SettingsRoomGuidanceCard()
                }
                Column(
                    modifier = Modifier.weight(1f),
                    verticalArrangement = Arrangement.spacedBy(14.dp),
                ) {
                    SettingsUpdateAndDiagCard(controller)
                    SettingsRepositoryCard(controller)
                }
            }
        } else {
            SettingsPrivilegeCard(controller, privReady)
            SettingsAccountSyncCard(controller)
            SettingsRoomGuidanceCard()
            SettingsUpdateAndDiagCard(controller)
            SettingsRepositoryCard(controller)
        }
        Spacer(modifier = Modifier.height(8.dp))
    }
}

@Composable
private fun SettingsPrivilegeCard(controller: AppController, privReady: Boolean) {
    // 1. Authorization Transport Card (Root / Wireless Debugging / Shizuku)
    OutlinedCard(
        colors = CardDefaults.outlinedCardColors(
            containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.92f),
        ),
        shape = MaterialTheme.shapes.large,
    ) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(10.dp),
            ) {
                Icon(Icons.Rounded.Usb, contentDescription = null, tint = MaterialTheme.colorScheme.primary)
                Column(modifier = Modifier.weight(1f)) {
                    Text("Root / 无线调试 / Shizuku 授权", style = MaterialTheme.typography.titleMedium)
                    Text(
                        text = controller.privilegeStatusDetail(),
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                Button(
                    onClick = { controller.showConnectSheet = true },
                    modifier = Modifier.weight(1f),
                ) {
                    Text(if (privReady) "管理授权连接" else "配置本地授权")
                }
                OutlinedButton(
                    onClick = { controller.openWirelessSettings() },
                    contentPadding = PaddingValues(horizontal = 12.dp, vertical = 8.dp),
                ) {
                    Text("开发者选项", maxLines = 1, softWrap = false)
                }
            }
        }
    }
}

@Composable
private fun SettingsAccountSyncCard(controller: AppController) {
    // 2. Account & QQ Binding Card
    OutlinedCard(
        colors = CardDefaults.outlinedCardColors(
            containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.92f),
        ),
        shape = MaterialTheme.shapes.large,
    ) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(10.dp),
            ) {
                Icon(Icons.Rounded.AccountCircle, contentDescription = null, tint = MaterialTheme.colorScheme.primary)
                Column(modifier = Modifier.weight(1f)) {
                    Text("星枢账号与渠道同步", style = MaterialTheme.typography.titleMedium)
                    val session = controller.boundSession
                    Text(
                        text = if (session != null) {
                            "${session.nick.ifEmpty { "星枢用户" }} · UID ${session.uid}${
                                if (controller.cloudSummary.isNotBlank()) "\n${controller.cloudSummary}" else ""
                            }"
                        } else {
                            "可选登录：支持官方网页授权、渠道 BN_DEBUG 票据扫描与 QQ Bot 绑定。"
                        },
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }

            if (controller.authStatusText.isNotBlank()) {
                Text(
                    text = controller.authStatusText,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.primary,
                )
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                FilledTonalButton(
                    onClick = { controller.startWebLogin() },
                    enabled = !controller.authBusy,
                    modifier = Modifier.weight(1f),
                ) {
                    Text(if (controller.boundSession == null) "官网网页登录" else "重新网页授权")
                }
                FilledTonalButton(
                    onClick = { controller.scanChannelTokens() },
                    enabled = !controller.authBusy,
                    modifier = Modifier.weight(1f),
                ) {
                    Text("扫描渠道票据")
                }
            }

            if (controller.authBusy && controller.webLoginUrl.isNotEmpty()) {
                OutlinedButton(
                    onClick = { controller.cancelWebLogin() },
                    modifier = Modifier.fillMaxWidth(),
                ) {
                    Text("取消等待网页登录")
                }
            }

            if (controller.detectedTokens.isNotEmpty()) {
                Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    controller.detectedTokens.forEachIndexed { idx, tok ->
                        val isSel = idx == controller.selectedTokenIndex
                        Surface(
                            onClick = { controller.selectedTokenIndex = idx },
                            color = if (isSel) MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.surfaceContainerLow,
                            shape = RoundedCornerShape(12.dp),
                        ) {
                            Text(
                                text = tok.displaySummary(),
                                style = MaterialTheme.typography.bodySmall,
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(horizontal = 12.dp, vertical = 8.dp),
                            )
                        }
                    }
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        Button(
                            onClick = { controller.startChannelHubAuth() },
                            enabled = !controller.authBusy,
                            modifier = Modifier.weight(1f),
                        ) {
                            Text("使用选中票据登录星枢")
                        }
                    }
                    OutlinedTextField(
                        value = controller.qqLinkInput,
                        onValueChange = { controller.qqLinkInput = it },
                        label = { Text("QQ Bot 登录链接（可选）") },
                        placeholder = { Text("粘贴 QQ 中收到的 https://124.221.176.134/login#...") },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth(),
                    )
                    OutlinedButton(
                        onClick = { controller.bindQqLinkWithSelectedToken() },
                        enabled = !controller.authBusy && controller.qqLinkInput.isNotBlank(),
                        modifier = Modifier.fillMaxWidth(),
                    ) {
                        Text("将选中票据绑定到 QQ")
                    }
                }
            }

            if (controller.boundSession != null) {
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    OutlinedButton(
                        onClick = { controller.refreshCloudAccountAsync() },
                        modifier = Modifier.weight(1f),
                    ) {
                        Text("刷新云端档案")
                    }
                    OutlinedButton(
                        onClick = { controller.logoutAccount() },
                        modifier = Modifier.weight(1f),
                    ) {
                        Text("退出账号")
                    }
                }
            }
        }
    }
}

@Composable
private fun SettingsRoomGuidanceCard() {
    // 3. In-Game Room Guidance Card
    OutlinedCard(
        colors = CardDefaults.outlinedCardColors(
            containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.90f),
        ),
        shape = MaterialTheme.shapes.large,
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            horizontalArrangement = Arrangement.spacedBy(12.dp),
            verticalAlignment = Alignment.Top,
        ) {
            Icon(Icons.Rounded.Gamepad, contentDescription = null, tint = MaterialTheme.colorScheme.primary)
            Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                Text("关于游戏内开房与房间控制", style = MaterialTheme.typography.titleMedium)
                Text(
                    text = "为避免进游戏前创建云端房间被官服回收，现已取消云端预开房，统一改为进入游戏大厅后在游戏内直接建房、改房与开局。",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
    }
}

@Composable
private fun SettingsUpdateAndDiagCard(controller: AppController) {
    // 4. Storage, Updates & Diagnostics Card
    OutlinedCard(
        colors = CardDefaults.outlinedCardColors(
            containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.92f),
        ),
        shape = MaterialTheme.shapes.large,
    ) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Icon(Icons.Rounded.SystemUpdate, contentDescription = null, tint = MaterialTheme.colorScheme.primary)
                Column(modifier = Modifier.weight(1f)) {
                    Text("应用更新、缓存与诊断", style = MaterialTheme.typography.titleMedium)
                    Text(
                        text = "星枢启动器 v${BuildConfig.VERSION_NAME} (${BuildConfig.VERSION_CODE}) · ${controller.updateStatusText}",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis,
                    )
                }
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text("接收测试预览版更新", style = MaterialTheme.typography.bodyMedium)
                Switch(
                    checked = controller.previewUpdates,
                    onCheckedChange = { controller.setPreviewUpdatesEnabled(it) },
                )
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                FilledTonalButton(
                    onClick = { controller.checkAssistantUpdate(silent = false) },
                    modifier = Modifier.weight(1f),
                ) {
                    Text("检查新版更新")
                }
                OutlinedButton(
                    onClick = { controller.clearDownloadCache() },
                    modifier = Modifier.weight(1f),
                ) {
                    Icon(Icons.Rounded.CleaningServices, contentDescription = null, modifier = Modifier.size(16.dp))
                    Spacer(modifier = Modifier.width(4.dp))
                    Text("清理 ${PayloadDownloadManager.formatBytes(controller.cacheBytes)}")
                }
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                OutlinedButton(
                    onClick = { controller.exportDiagnosticLog() },
                    modifier = Modifier.weight(1f),
                ) {
                    Icon(Icons.Rounded.SaveAlt, contentDescription = null, modifier = Modifier.size(16.dp))
                    Spacer(modifier = Modifier.width(4.dp))
                    Text("保存诊断日志")
                }
                OutlinedButton(
                    onClick = { controller.copyDiagnosticLogToClipboard() },
                    modifier = Modifier.weight(1f),
                ) {
                    Icon(Icons.Rounded.Info, contentDescription = null, modifier = Modifier.size(16.dp))
                    Spacer(modifier = Modifier.width(4.dp))
                    Text("复制诊断文本")
                }
            }
        }
    }
}

@Composable
private fun SettingsRepositoryCard(controller: AppController) {
    // 5. Open-Source Repository & Project Origin Card
    OutlinedCard(
        colors = CardDefaults.outlinedCardColors(
            containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.92f),
        ),
        shape = MaterialTheme.shapes.large,
    ) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Icon(Icons.Rounded.Code, contentDescription = null, tint = MaterialTheme.colorScheme.primary)
                Column(modifier = Modifier.weight(1f)) {
                    Text("开源仓库与项目来源", style = MaterialTheme.typography.titleMedium)
                    Text(
                        text = "星枢手机版（AstralParty-XingShu-Android）源自开源项目 AstralParty-XingShu（星枢），现作为独立手机版仓库持续维护，遵循《AstralParty-XingShu 许可与合理使用条款》。",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                FilledTonalButton(
                    onClick = { controller.openExternalBrowser("https://github.com/Breezecaresses/AstralParty-XingShu-Android") },
                    modifier = Modifier.weight(1f),
                ) {
                    Icon(Icons.Rounded.OpenInNew, contentDescription = null, modifier = Modifier.size(16.dp))
                    Spacer(modifier = Modifier.width(4.dp))
                    Text("手机版仓库", maxLines = 1)
                }
                OutlinedButton(
                    onClick = { controller.openExternalBrowser("https://github.com/Breezecaresses/AstralParty-XingShu") },
                    modifier = Modifier.weight(1f),
                ) {
                    Icon(Icons.Rounded.OpenInNew, contentDescription = null, modifier = Modifier.size(16.dp))
                    Spacer(modifier = Modifier.width(4.dp))
                    Text("桌面/主仓库", maxLines = 1)
                }
            }

            OutlinedButton(
                onClick = { controller.openExternalBrowser("https://astralparty-xingshu.lanxichaxu.cn/xingshu-hub/") },
                modifier = Modifier.fillMaxWidth(),
            ) {
                Icon(Icons.Rounded.Public, contentDescription = null, modifier = Modifier.size(16.dp))
                Spacer(modifier = Modifier.width(6.dp))
                Text("访问星枢在线网页工坊 · 社区反馈 QQ 群 1080908767")
            }
        }
    }
}

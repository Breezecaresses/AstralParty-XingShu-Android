using UnityEngine;

namespace XingShuRuntime.Core;

/// <summary>
/// XingShu 配置键名、默认值与边界集中常量表 (Phase 1B.3D Wave 3)
/// 
/// 契约说明:
/// 1. 集中定义键名与单一真相源默认值常量，成对出现；数值型配置提供明确上下界 (Min/Max)。
/// 2. Wave 3 包含既有配置以及 HandCardDisplayManager 与 AssistBallGUI 迁移配置项。
/// 3. HandCard 与 AssistBall 解耦出牌按钮尺寸 (XINGSHU_HAND_CARD_BTN_SIZE)，确立 HandCard 为单一所有者。
/// </summary>
internal static class XingShuConfigKeys
{
	// Wave 1: 自动重连与对局统计
	internal const string AutoReconnectEnabled = "XINGSHU_CONFIG_AUTO_RECONNECT_ENABLED";

	internal const bool AutoReconnectEnabledDefault = true;

	internal const string MatchObserveEnabled = "XINGSHU_MATCH_OBSERVE_ENABLED";

	internal const bool MatchObserveEnabledDefault = true;

	// Wave 2: 游戏全局变速控制器 (GameSpeedControllerGUI)
	internal const string SpeedEnabled = "XINGSHU_CONFIG_SPEED_ENABLED";

	internal const bool SpeedEnabledDefault = false;

	internal const string TimeScale = "XINGSHU_CONFIG_TIMESCALE";

	internal const float TimeScaleDefault = 1.0f;

	internal const float TimeScaleMin = 0.0f;

	internal const float TimeScaleMax = 10.0f;

	internal const string SpeedHotkey = "XINGSHU_CONFIG_SPEED_HOTKEY";

	internal const KeyCode SpeedHotkeyDefault = KeyCode.F7;

	// Wave 4: 双变速引擎与原生加速 (Phase 2D-2B2)
	internal const string GameSpeedEngineMode = "XINGSHU_GAME_SPEED_ENGINE_MODE";

	internal const int GameSpeedEngineModeDefault = 0;

	// 加速模式下右侧标点/表情延长时长 (秒)
	internal const string PingAccelExtendSeconds = "XINGSHU_PING_ACCEL_EXTEND_SECONDS";

	internal const float PingAccelExtendSecondsDefault = 5.0f;

	internal const float PingAccelExtendSecondsMin = 0.0f;

	internal const float PingAccelExtendSecondsMax = 15.0f;

	internal const string NativeSpeedEnabled = "XINGSHU_NATIVE_SPEED_ENABLED";

	internal const bool NativeSpeedEnabledDefault = false;

	internal const string NativeSpeedValue = "XINGSHU_NATIVE_SPEED_VALUE";

	internal const float NativeSpeedValueDefault = 2.0f;

	internal const float NativeSpeedValueMin = 0.25f;

	internal const float NativeSpeedValueMax = 10.0f;

	internal const string NativeSmartPauseEnabled = "XINGSHU_NATIVE_SMART_PAUSE_ENABLED";

	internal const bool NativeSmartPauseEnabledDefault = false;

	// Wave 2: 地图自由镜头 (MapCameraGUI)
	internal const string MapFreeCameraEnabled = "XINGSHU_MAP_FREE_CAMERA_ENABLED";

	internal const bool MapFreeCameraEnabledDefault = false;

	internal const string MapScrollSpeed = "XINGSHU_MAP_SCROLL_SPEED";

	internal const float MapScrollSpeedDefault = 0.14f;

	internal const float MapScrollSpeedMin = 0.02f;

	internal const float MapScrollSpeedMax = 0.5f;

	// Wave 2: 移动端/通用 UI 缩放 (XingShuMobileUIHelper)
	internal const string MobileUserScale = "XINGSHU_UI_USER_SCALE";

	// Phone feature-center typography is independent of map widgets and the ball.
	internal const string MobileTextScale = "XINGSHU_MOBILE_TEXT_SCALE";
	internal const float MobileTextScaleDefault = .80f;
	internal const float MobileTextScaleMin = .80f;
	internal const float MobileTextScaleMax = 1.35f;
	internal const string MobilePanelScale = "XINGSHU_MOBILE_PANEL_SCALE";
	internal const float MobilePanelScaleMin = .70f;
	internal const float MobilePanelScaleMax = 1.30f;
	internal const string MobilePanelX = "XINGSHU_MOBILE_PANEL_X";
	internal const string MobilePanelY = "XINGSHU_MOBILE_PANEL_Y";

	internal const float MobileUserScaleDefault = 1.0f;

	internal const float MobileUserScaleMin = 1.0f;

	internal const float MobileUserScaleMax = 10.0f;

	// Track C (Phase 2D-7D): 桌面现代功能中心安全显示比例
	internal const float FunctionCenterDesktopScaleMin = 1.00f;

	internal const float FunctionCenterDesktopScaleDefault = 1.00f;

	internal const float FunctionCenterDesktopScaleMax = 1.25f;

	// Wave 3: 手牌监视管理器 (HandCardDisplayManager)
	internal const string HandCardEnabled = "XINGSHU_HAND_CARD_ENABLED";

	internal const bool HandCardEnabledDefault = true;

	internal const string HandRosterEnabled = "XINGSHU_HAND_ROSTER_ENABLED";

	internal const string HandRosterScale = "XINGSHU_HAND_ROSTER_SCALE";

	internal const string HandRosterCompactDefaultApplied = "XINGSHU_HAND_ROSTER_COMPACT_DEFAULT_APPLIED";

	internal const string HandRosterToggleOffsetX = "XINGSHU_HAND_ROSTER_TOGGLE_OFFSET_X";
	internal const float HandRosterToggleOffsetXDefault = 20f;
	internal const string HandRosterToggleOffsetY = "XINGSHU_HAND_ROSTER_TOGGLE_OFFSET_Y";
	internal const float HandRosterToggleOffsetYDefault = -25f;
	internal const string HandRosterGridOffsetX = "XINGSHU_HAND_ROSTER_GRID_OFFSET_X";
	internal const float HandRosterGridOffsetXDefault = 15f;
	internal const string HandRosterGridOffsetY = "XINGSHU_HAND_ROSTER_GRID_OFFSET_Y";
	internal const float HandRosterGridOffsetYDefault = -25f;
	internal const float HandRosterOffsetMinX = -80f;
	internal const float HandRosterToggleOffsetMaxX = 280f;
	internal const float HandRosterGridOffsetMinX = -120f;
	internal const float HandRosterGridOffsetMaxX = 320f;
	internal const float HandRosterOffsetMinY = -80f;
	internal const float HandRosterOffsetMaxY = 80f;

	// Phase HS4-P1.6: 实时手牌悬浮窗进入观战自动开启偏好
	internal const string HandCardSpectatorAutoOpen = "XINGSHU_HAND_CARD_SPECTATOR_AUTO_OPEN";

	internal const bool HandCardSpectatorAutoOpenDefault = true;

	// Phase HS4-P1.6: 实时手牌悬浮窗整体 UI 缩放偏好
	internal const string HandCardUIScale = "XINGSHU_HAND_CARD_UI_SCALE";

	internal const float HandCardUIScaleDefault = 1.0f;

	internal const float HandCardUIScaleMin = 0.7f;

	internal const float HandCardUIScaleMax = 1.5f;

	internal const string HandCardScale = "XINGSHU_HAND_CARD_SCALE";

	internal const float HandCardScaleDesktopDefault = 1.0f;

	internal const float HandCardScaleMobileDefault = 1.35f;

	internal const float HandCardScaleMin = 0.6f;

	internal const float HandCardScaleMax = 3.0f;

	internal const string HandCardBtnSize = "XINGSHU_HAND_CARD_BTN_SIZE";

	internal const float HandCardBtnSizeDesktopDefault = 24.0f;

	internal const float HandCardBtnSizeMobileDefault = 38.0f;

	internal const float HandCardBtnSizeMin = 16.0f;

	internal const float HandCardBtnSizeMax = 200.0f;

	// Wave 3: 悬浮球设置与样式 (AssistBallGUI)
	internal const string AssistAutoHide = "XINGSHU_ASSIST_AUTOHIDE";

	internal const bool AssistAutoHideDefault = false;

	internal const string AssistSize = "XINGSHU_ASSIST_SIZE";

	internal const float AssistSizeDesktopDefault = 64.0f;

	internal const float AssistSizeMobileDefault = 64.0f;

	internal const float AssistSizeMobileMin = 48.0f;

	internal const string AssistMobileSizeV2Applied = "XINGSHU_ASSIST_MOBILE_SIZE_V2_APPLIED";

	internal const string AssistMobileCompactDefaultApplied = "XINGSHU_ASSIST_MOBILE_COMPACT_DEFAULT_APPLIED";

	internal const float AssistSizeMin = 36.0f;

	internal const float AssistSizeMax = 200.0f;

	internal const string AssistAlpha = "XINGSHU_ASSIST_ALPHA";

	internal const float AssistAlphaDefault = 0.9f;

	internal const float AssistAlphaMin = 0.2f;

	internal const float AssistAlphaMax = 1.0f;

	internal const string AssistX = "XINGSHU_ASSIST_X";

	internal const float AssistXDefault = -40.0f;

	internal const string AssistY = "XINGSHU_ASSIST_Y";

	internal const float AssistYDefault = -180.0f;

	internal const string AssistXImgui = "XINGSHU_ASSIST_X_IMGUI";

	internal const string AssistYImgui = "XINGSHU_ASSIST_Y_IMGUI";

	internal const string AssistHotkey = "XINGSHU_ASSIST_HOTKEY";

	internal const KeyCode AssistHotkeyDefault = KeyCode.BackQuote;

	internal const string AssistDesktopScale = "XINGSHU_ASSIST_DESKTOP_SCALE";

	internal const float AssistDesktopScaleDefault = 1.0f;

	internal const float AssistDesktopScaleMin = 1.0f;

	internal const float AssistDesktopScaleMax = 3.0f;

	// Wave 3: 对局增强 · 击飞概率 (MonsterKillProbability)
	internal const string MatchKillEnabled = "XINGSHU_MATCH_KILL_ENABLED_V1";

	internal const bool MatchKillEnabledDefault = true;

	internal const string MonsterTargetEnabled = "XINGSHU_MONSTER_TARGET_ENABLED_V1";

	internal const bool MonsterTargetEnabledDefault = true;

	internal const string MonsterActionRollMoveEnabled = "XINGSHU_MONSTER_ROLL_MOVE_ENABLED_V1";

	internal const bool MonsterActionRollMoveEnabledDefault = true;

	internal const string MonsterActionPathMarkersEnabled = "XINGSHU_MONSTER_PATH_MARKERS_ENABLED_V1";

	internal const bool MonsterActionPathMarkersEnabledDefault = true;

	internal const string MonsterArrowBoostEnabled = "XINGSHU_MONSTER_ARROW_BOOST_ENABLED_V1";

	internal const bool MonsterArrowBoostEnabledDefault = true;

	internal const string MatchKillFightSize = "XINGSHU_MATCH_KILL_FIGHT_SIZE";

	internal const int MatchKillFightSizeDefault = 26;

	internal const int MatchKillFightSizeMin = 12;

	internal const int MatchKillFightSizeMax = 72;

	internal const string MatchKillFightX = "XINGSHU_MATCH_KILL_FIGHT_X";

	internal const float MatchKillFightXDefault = 50.0f;

	internal const float MatchKillFightXMin = -1000.0f;

	internal const float MatchKillFightXMax = 1000.0f;

	internal const string MatchKillFightY = "XINGSHU_MATCH_KILL_FIGHT_Y";

	internal const float MatchKillFightYDefault = -32.0f;

	internal const float MatchKillFightYMin = -1000.0f;

	internal const float MatchKillFightYMax = 1000.0f;

	// Visual V3: 击飞预测文字颜色模式与炫彩高亮 (Phase 2D-2B4H)
	internal const string MatchKillAutoColor = "XINGSHU_MATCH_KILL_AUTO_COLOR";

	internal const bool MatchKillAutoColorDefault = true;

	internal const string MatchKillRainbow100 = "XINGSHU_MATCH_KILL_RAINBOW_100";

	internal const bool MatchKillRainbow100Default = false;

	internal const string MatchKillProbabilityColor = "XINGSHU_MATCH_KILL_PROBABILITY_COLOR";

	internal const string MatchKillProbabilityColorDefault = "#FFD85A";

	internal const string MatchKillTargetColor = "XINGSHU_MATCH_KILL_TARGET_COLOR";

	internal const string MatchKillTargetColorDefault = "#F4F7FF";

	// Wave 4: 立绘替换 (StaticPortraitGUI)
	internal const string OriginalArtworkEnabled = "XINGSHU_ORIGINAL_ARTWORK_ENABLED";

	internal const bool OriginalArtworkEnabledDefault = true;

	internal const string StaticPortraitEnableLoad = "XINGSHU_STATIC_ENABLE_LOAD";

	internal const bool StaticPortraitEnableLoadDefault = true;

	internal const string StaticPortraitEnableDump = "XINGSHU_STATIC_ENABLE_DUMP";

	internal const bool StaticPortraitEnableDumpDefault = true;

	internal const string StaticPortraitBasePath = "XINGSHU_STATIC_BASE_PATH";

	internal const string StaticPortraitBasePathDefault = "";

	internal const string StaticPortraitModImg = "XINGSHU_STATIC_MODIMG";

	internal const string StaticPortraitModImgDefault = "";

	internal const string StaticPortraitModOut = "XINGSHU_STATIC_MODOUT";

	internal const string StaticPortraitModOutDefault = "";

	internal const string StaticPortraitHotkey = "XINGSHU_STATIC_HOTKEY";

	internal const KeyCode StaticPortraitHotkeyDefault = KeyCode.F8;

	// Wave 4: 统计悬浮窗 (StatsWidgetView) - PC
	internal const string StatsWidgetPcBackground = "XINGSHU_STATS_WIDGET_PC_BACKGROUND";

	internal const string StatsWidgetPcText = "XINGSHU_STATS_WIDGET_PC_TEXT";

	internal const string StatsWidgetPcScale = "XINGSHU_STATS_WIDGET_PC_SCALE";

	internal const string StatsWidgetPcX = "XINGSHU_STATS_WIDGET_PC_X";

	internal const string StatsWidgetPcY = "XINGSHU_STATS_WIDGET_PC_Y";

	internal const string StatsWidgetPcLock = "XINGSHU_STATS_WIDGET_PC_LOCK";

	// Wave 4: 统计悬浮窗 (StatsWidgetView) - Android/Mobile
	internal const string StatsWidgetAndroidBackground = "XINGSHU_STATS_WIDGET_ANDROID_BACKGROUND";

	internal const string StatsWidgetAndroidText = "XINGSHU_STATS_WIDGET_ANDROID_TEXT";

	internal const string StatsWidgetAndroidScale = "XINGSHU_STATS_WIDGET_ANDROID_SCALE";

	internal const string StatsWidgetAndroidX = "XINGSHU_STATS_WIDGET_ANDROID_X";

	internal const string StatsWidgetAndroidY = "XINGSHU_STATS_WIDGET_ANDROID_Y";

	internal const string StatsWidgetAndroidLock = "XINGSHU_STATS_WIDGET_ANDROID_LOCK";

	// Wave 4: 统计悬浮窗默认值
	internal const float StatsWidgetBackgroundDefault = 0.66f;

	internal const float StatsWidgetTextAlphaDefault = 0.96f;

	internal const float StatsWidgetScaleDefault = 1.0f;
	internal const float StatsWidgetAndroidScaleDefault = .80f;

	internal const float StatsWidgetXDefault = 1.0f;

	internal const float StatsWidgetYDefault = 0.12f;

	internal const bool StatsWidgetLockDefault = false;

	// Phase 2D-7B-R2: 统计悬浮挂件进入对局自动显示偏好 (独立于底层数据采集开关)
	internal const string StatsWidgetAutoShow = "XINGSHU_STATS_WIDGET_AUTO_SHOW";

	internal const bool StatsWidgetAutoShowDefault = true;

	// Phase 2D-5B: 原生快捷社交增强 (Track A)
	internal const string AutoThanksEnabled = "XINGSHU_AUTO_THANKS_ENABLED";

	internal const bool AutoThanksEnabledDefault = true;

	internal const string AutoPraiseEnabled = "XINGSHU_AUTO_PRAISE_ENABLED";

	internal const bool AutoPraiseEnabledDefault = true;

	internal const string AutoPraiseDefault = "XINGSHU_AUTO_PRAISE_DEFAULT";

	internal const bool AutoPraiseDefaultDefault = true;

	internal const string AutoPraiseScale = "XINGSHU_AUTO_PRAISE_SCALE";

#if XINGSHU_ANDROID_PREVIEW
	internal const float AutoPraiseScaleDefault = 1.0f;
#else
	internal const float AutoPraiseScaleDefault = 2.0f;
#endif

	internal const float AutoPraiseScaleMin = 0.6f;

	internal const float AutoPraiseScaleMax = 2.0f;

	internal const string AutoPraiseOffsetX = "XINGSHU_AUTO_PRAISE_OFFSET_X";
	internal const float AutoPraiseOffsetXDefault = 0f;
	internal const string AutoPraiseOffsetY = "XINGSHU_AUTO_PRAISE_OFFSET_Y";
	internal const float AutoPraiseOffsetYDefault = 73f;
	internal const float AutoPraiseOffsetMin = -120f;
	internal const float AutoPraiseOffsetMax = 120f;

	// 结算战绩自动截图 (SettlementScreenshot)
	internal const string AutoSettlementScreenshotEnabled = "XINGSHU_AUTO_SETTLEMENT_SCREENSHOT_ENABLED";

	internal const bool AutoSettlementScreenshotEnabledDefault = true;

	internal const string AutoSettlementScreenshotLastPath = "XINGSHU_AUTO_SETTLEMENT_SCREENSHOT_LAST_PATH";

	// 选人界面右侧地图名称提示 (SelectRoleMapNameHint)
	internal const string SelectRoleMapNameEnabled = "XINGSHU_SELECT_ROLE_MAP_NAME";

	internal const bool SelectRoleMapNameEnabledDefault = true;

	internal const string SelectRoleMapNamePosX = "XINGSHU_SELECT_ROLE_MAP_POS_X";
	internal const float SelectRoleMapNamePosXDefault = 0.98f;
	internal const string SelectRoleMapNamePosY = "XINGSHU_SELECT_ROLE_MAP_POS_Y";
	internal const float SelectRoleMapNamePosYDefault = 0.56f;
	internal const string SelectRoleMapNameScale = "XINGSHU_SELECT_ROLE_MAP_SCALE";
	internal const float SelectRoleMapNameScaleDefault = 1.0f;
	internal const float SelectRoleMapNameScaleMin = 0.60f;
	internal const float SelectRoleMapNameScaleMax = 1.80f;
	internal const string SelectRoleMapNameLocked = "XINGSHU_SELECT_ROLE_MAP_LOCKED";
	internal const bool SelectRoleMapNameLockedDefault = false;

	// 战斗卡牌打出点数提示 (BattleCardPointHint)
	internal const string BattleCardPointHintEnabled = "XINGSHU_BATTLE_CARD_POINT_HINT";

	internal const bool BattleCardPointHintEnabledDefault = true;

	internal const string BattleCardPointAtkX = "XINGSHU_BATTLE_CARD_ATK_X";
	internal const float BattleCardPointAtkXDefault = 0.03f;
	internal const string BattleCardPointAtkY = "XINGSHU_BATTLE_CARD_ATK_Y";
	internal const float BattleCardPointAtkYDefault = 0.16f;
	internal const string BattleCardPointDefX = "XINGSHU_BATTLE_CARD_DEF_X";
	internal const float BattleCardPointDefXDefault = 0.97f;
	internal const string BattleCardPointDefY = "XINGSHU_BATTLE_CARD_DEF_Y";
	internal const float BattleCardPointDefYDefault = 0.16f;
	internal const string BattleCardPointScale = "XINGSHU_BATTLE_CARD_SCALE";
	internal const float BattleCardPointScaleDefault = 1.0f;
	internal const float BattleCardPointScaleMin = 0.60f;
	internal const float BattleCardPointScaleMax = 1.80f;
	internal const string BattleCardPointLocked = "XINGSHU_BATTLE_CARD_LOCKED";
	internal const bool BattleCardPointLockedDefault = false;

	// 自定义显示统计项掩码 (9项: 击杀/死亡/输出/实际伤害/承伤/治疗/累计金币/转账/移动步数，默认全选 511)
	internal const string StatsWidgetMetricsLegacy = "XINGSHU_STATS_WIDGET_METRICS";

	internal const string StatsWidgetMetrics = "XINGSHU_STATS_WIDGET_METRICS_V2";

	internal const int StatsWidgetMetricsDefault = 511;

	// 自定义大厅背景 (CustomLobbyBackground)
	internal const string CustomLobbyBgEnabled = "XINGSHU_CUSTOM_LOBBY_BG_ENABLED";
	internal const bool CustomLobbyBgEnabledDefault = true;

	internal const string CustomLobbyBgSyncSubPanels = "XINGSHU_CUSTOM_LOBBY_BG_SYNC_SUB";
	internal const bool CustomLobbyBgSyncSubPanelsDefault = false;

	internal const string CustomLobbyBgRandomOnLobby = "XINGSHU_CUSTOM_LOBBY_BG_RANDOM";
	internal const bool CustomLobbyBgRandomOnLobbyDefault = false;

	internal const string CustomLobbyBgSelectedFile = "XINGSHU_CUSTOM_LOBBY_BG_FILE";
	internal const string CustomLobbyBgSelectedFileDefault = "";

	internal const string CustomLobbyBgFolderPath = "XINGSHU_CUSTOM_LOBBY_BG_DIR";
	internal const string CustomLobbyBgFolderPathDefault = "";

	internal const string CustomLobbyBgFillMode = "XINGSHU_CUSTOM_LOBBY_BG_FILL";
	internal const int CustomLobbyBgFillModeDefault = 0;

	internal const string CustomLobbyBgBrightness = "XINGSHU_CUSTOM_LOBBY_BG_BRIGHTNESS";
	internal const float CustomLobbyBgBrightnessMin = 0.30f;
	internal const float CustomLobbyBgBrightnessMax = 1.00f;
	internal const float CustomLobbyBgBrightnessDefault = 1.00f;

	internal const string CustomLobbyBgSpeed = "XINGSHU_CUSTOM_LOBBY_BG_SPEED";
	internal const float CustomLobbyBgSpeedMin = 0.50f;
	internal const float CustomLobbyBgSpeedMax = 2.00f;
	internal const float CustomLobbyBgSpeedDefault = 1.00f;

	internal const string CustomLobbyBgMuted = "XINGSHU_CUSTOM_LOBBY_BG_MUTED";
	internal const bool CustomLobbyBgMutedDefault = true;

	internal const string CustomLobbyBgVolume = "XINGSHU_CUSTOM_LOBBY_BG_VOLUME";
	internal const float CustomLobbyBgVolumeMin = 0.00f;
	internal const float CustomLobbyBgVolumeMax = 1.00f;
	internal const float CustomLobbyBgVolumeDefault = 0.35f;

	internal const string CustomLobbyBgMuteVanillaBgm = "XINGSHU_CUSTOM_LOBBY_BG_MUTE_VANILLA_BGM";
	internal const bool CustomLobbyBgMuteVanillaBgmDefault = false;

	// 梅加斯机械超人开技能理论最大伤害面板 (MegathSkillDamageHint)
	internal const string MegathDamageHintEnabled = "XINGSHU_MEGATH_DMG_HINT";
	internal const bool MegathDamageHintEnabledDefault = true;
	internal const string MegathDamageHintPosX = "XINGSHU_MEGATH_DMG_X";
	internal const float MegathDamageHintPosXDefault = 0.78f;
	internal const string MegathDamageHintPosY = "XINGSHU_MEGATH_DMG_Y";
	internal const float MegathDamageHintPosYDefault = 0.78f;
	internal const string MegathDamageHintScale = "XINGSHU_MEGATH_DMG_SCALE";
	internal const float MegathDamageHintScaleMin = 0.60f;
	internal const float MegathDamageHintScaleMax = 1.80f;
	internal const float MegathDamageHintScaleDefault = 1.0f;
	internal const string MegathDamageHintLocked = "XINGSHU_MEGATH_DMG_LOCKED";
	internal const bool MegathDamageHintLockedDefault = false;

	// 角色状态显示 (PlayerCombatStatusHud：棋盘角色头顶与怪物推演同层锁死 + 左侧头像下方可调位置)
	internal const string PlayerCombatHudEnabled = "XINGSHU_PLAYER_COMBAT_HUD";
	internal const bool PlayerCombatHudEnabledDefault = true;
	internal const string PlayerCombatHudShowHead = "XINGSHU_PLAYER_HUD_HEAD";
	internal const bool PlayerCombatHudShowHeadDefault = true;
	internal const string PlayerCombatHudShowAvatar = "XINGSHU_PLAYER_HUD_AVATAR";
	internal const bool PlayerCombatHudShowAvatarDefault = true;
	internal const string PlayerCombatHudOffsetX = "XINGSHU_PLAYER_AVATAR_OFF_X";
	internal const float PlayerCombatHudOffsetXMin = -160f;
	internal const float PlayerCombatHudOffsetXMax = 160f;
	internal const float PlayerCombatHudOffsetXDefault = 0f;
	internal const string PlayerCombatHudOffsetY = "XINGSHU_PLAYER_AVATAR_OFF_Y";
	internal const float PlayerCombatHudOffsetYMin = -160f;
	internal const float PlayerCombatHudOffsetYMax = 160f;
	internal const float PlayerCombatHudOffsetYDefault = 2f;
	internal const string PlayerCombatHudHeadOffsetY = "XINGSHU_PLAYER_HEAD_OFF_Y";
	internal const float PlayerCombatHudHeadOffsetYMin = -120f;
	internal const float PlayerCombatHudHeadOffsetYMax = 120f;
	internal const float PlayerCombatHudHeadOffsetYDefault = 0f;
	internal const string PlayerCombatHudScale = "XINGSHU_PLAYER_HUD_SCALE";
	internal const float PlayerCombatHudScaleMin = 0.60f;
	internal const float PlayerCombatHudScaleMax = 1.80f;
	internal const float PlayerCombatHudScaleDefault = 1.0f;
	internal const string PlayerCombatHudLocked = "XINGSHU_PLAYER_HUD_LOCKED";
	internal const bool PlayerCombatHudLockedDefault = false;
	internal const string PlayerCombatHudShowCd = "XINGSHU_PLAYER_HUD_CD";
	internal const bool PlayerCombatHudShowCdDefault = true;
	internal const string PlayerCombatHudShowHp = "XINGSHU_PLAYER_HUD_HP";
	internal const bool PlayerCombatHudShowHpDefault = true;
	internal const string PlayerCombatHudShowAtkDef = "XINGSHU_PLAYER_HUD_ATKDEF";
	internal const bool PlayerCombatHudShowAtkDefDefault = true;
	internal const string PlayerCombatHudShowMove = "XINGSHU_PLAYER_HUD_MOVE";
	internal const bool PlayerCombatHudShowMoveDefault = true;

	// 角色星级区域 Buff 显示 (PlayerStarBuffHud：独立模块 player-star-buffs)
	internal const string PlayerStarBuffsEnabled = "XINGSHU_STAR_BUFFS_ENABLED";
	internal const bool PlayerStarBuffsEnabledDefault = true;
	internal const string PlayerStarBuffsAnchorMode = "XINGSHU_STAR_BUFFS_ANCHOR";
	internal const int PlayerStarBuffsAnchorModeDefault = 0; // 0=星级右侧, 1=星级左侧, 2=星级下方
	internal const string PlayerStarBuffsOffsetX = "XINGSHU_STAR_BUFFS_OFF_X";
	internal const float PlayerStarBuffsOffsetXMin = -220f;
	internal const float PlayerStarBuffsOffsetXMax = 260f;
	internal const float PlayerStarBuffsOffsetXDefault = 4f;
	internal const string PlayerStarBuffsOffsetY = "XINGSHU_STAR_BUFFS_OFF_Y";
	internal const float PlayerStarBuffsOffsetYMin = -160f;
	internal const float PlayerStarBuffsOffsetYMax = 160f;
	internal const float PlayerStarBuffsOffsetYDefault = 0f;
	internal const string PlayerStarBuffsScale = "XINGSHU_STAR_BUFFS_SCALE";
	internal const float PlayerStarBuffsScaleMin = 0.50f;
	internal const float PlayerStarBuffsScaleMax = 1.80f;
	internal const float PlayerStarBuffsScaleDefault = 1.0f;
	internal const string PlayerStarBuffsMaxIcons = "XINGSHU_STAR_BUFFS_MAX";
	internal const int PlayerStarBuffsMaxIconsMin = 3;
	internal const int PlayerStarBuffsMaxIconsMax = 16;
	internal const int PlayerStarBuffsMaxIconsDefault = 8;
	internal const string PlayerStarBuffsLocked = "XINGSHU_STAR_BUFFS_LOCKED";
	internal const bool PlayerStarBuffsLockedDefault = false;
	internal const string PlayerStarBuffsCatMove = "XINGSHU_STAR_BUFFS_CAT_MOVE";
	internal const bool PlayerStarBuffsCatMoveDefault = true;
	internal const string PlayerStarBuffsCatHero = "XINGSHU_STAR_BUFFS_CAT_HERO";
	internal const bool PlayerStarBuffsCatHeroDefault = true;
	internal const string PlayerStarBuffsCatCombat = "XINGSHU_STAR_BUFFS_CAT_COMBAT";
	internal const bool PlayerStarBuffsCatCombatDefault = true;
	internal const string PlayerStarBuffsCatDebuff = "XINGSHU_STAR_BUFFS_CAT_DEBUFF";
	internal const bool PlayerStarBuffsCatDebuffDefault = true;
	internal const string PlayerStarBuffsCatEvent = "XINGSHU_STAR_BUFFS_CAT_EVENT";
	internal const bool PlayerStarBuffsCatEventDefault = true;
	internal const string PlayerStarBuffsCatOther = "XINGSHU_STAR_BUFFS_CAT_OTHER";
	internal const bool PlayerStarBuffsCatOtherDefault = true;
	internal const string PlayerStarBuffsDisabledIds = "XINGSHU_STAR_BUFFS_DISABLED_IDS";
	internal const string PlayerStarBuffsDisabledIdsDefault = "";
	internal const string PlayerStarBuffsEnabledIds = "XINGSHU_STAR_BUFFS_ENABLED_IDS";
	internal const string PlayerStarBuffsEnabledIdsDefault = "";

	// 功能中心界面风格 (0=原版 UI 皮肤, 1=毛玻璃圆角通透框架)
	internal const string UiCenterStyle = "XINGSHU_UI_CENTER_STYLE";
	internal const int UiCenterStyleDefault = 0;

	// 毛玻璃 UI 自定义配色与透明度
	internal const string UiGlassThemePreset = "XINGSHU_UI_GLASS_PRESET";
	internal const int UiGlassThemePresetDefault = 0;

	internal const string UiGlassCustomHue = "XINGSHU_UI_GLASS_HUE";
	internal const float UiGlassCustomHueMin = 0f;
	internal const float UiGlassCustomHueMax = 360f;
	internal const float UiGlassCustomHueDefault = 212f;

	internal const string UiGlassShellAlpha = "XINGSHU_UI_GLASS_SHELL_ALPHA";
	internal const float UiGlassShellAlphaMin = 0.45f;
	internal const float UiGlassShellAlphaMax = 0.96f;
	internal const float UiGlassShellAlphaDefault = 0.86f;

	internal const string UiGlassCardAlpha = "XINGSHU_UI_GLASS_CARD_ALPHA";
	internal const float UiGlassCardAlphaMin = 0.35f;
	internal const float UiGlassCardAlphaMax = 0.95f;
	internal const float UiGlassCardAlphaDefault = 0.78f;
}

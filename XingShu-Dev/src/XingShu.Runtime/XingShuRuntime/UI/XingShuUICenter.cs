using System;
using System.Collections.Generic;
using XingShuRuntime.Core;
using XingShuRuntime.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace XingShuRuntime.UI
{
    /// <summary>
    /// 功能中心主控制器 (Phase 2B-R4 生产整合版)
    /// 参照游戏设置页的独立导航、深色主框和白色设置卡片构建。
    /// </summary>
    [DefaultExecutionOrder(-9900)]
    internal sealed class XingShuUICenter : MonoBehaviour
    {
        internal static XingShuUICenter Instance { get; private set; }

        internal static bool IsOpen => Instance != null && Instance._isOpen;

        private const string CenterScalePrefKey = "XINGSHU_UI_CENTER_SCALE";
        internal const float CenterScaleMin = 0.65f;
        internal const float CenterScaleMax = 1.00f;
        internal const float CenterScaleDefault = 1.00f;

        internal static float PanelScale => XingShuMobileUIHelper.FiniteClamp(
            XingShuConfigService.GetFloat(CenterScalePrefKey, CenterScaleDefault),
            CenterScaleMin,
            CenterScaleMax,
            CenterScaleDefault);

        internal static void SetPanelScale(float scale)
        {
            float clamped = XingShuMobileUIHelper.FiniteClamp(scale, CenterScaleMin, CenterScaleMax, CenterScaleDefault);
            XingShuConfigService.SetFloat(CenterScalePrefKey, clamped);
            XingShuConfigService.Save();
            if (Instance != null)
            {
                Instance.FitPanelToScreen();
                Instance.UpdateSizeCycleLabel();
            }
        }

        internal static void ResetWindowCenter()
        {
            XingShuConfigService.SetFloat(CenterScalePrefKey, CenterScaleDefault);
            XingShuConfigService.Save();
            if (Instance != null)
            {
                if (Instance._mainFrameRt != null) Instance._mainFrameRt.anchoredPosition = Vector2.zero;
                Instance.FitPanelToScreen();
                Instance.UpdateSizeCycleLabel();
            }
        }

        internal static int CenterStyle
        {
            get
            {
                int v = XingShuConfigService.GetInt(XingShuConfigKeys.UiCenterStyle, XingShuConfigKeys.UiCenterStyleDefault);
                return v == 1 ? 1 : 0;
            }
        }

        internal static void SetCenterStyle(int style)
        {
            int next = style == 1 ? 1 : 0;
            if (CenterStyle == next) return;
            XingShuConfigService.SetInt(XingShuConfigKeys.UiCenterStyle, next);
            XingShuConfigService.Save();
            if (Instance != null)
            {
                Instance.RebuildCenterUi();
            }
        }

        internal static bool IsGlassActive => Instance != null ? Instance._activeBuiltStyle == 1 : CenterStyle == 1;

        internal static void ApplyGlassAppearanceLive()
        {
            XingShuGlassSkin.RefreshPaletteFromConfig();
            if (Instance == null || Instance._activeBuiltStyle != 1) return;
            if (Instance._glassBackdropImage != null) Instance._glassBackdropImage.color = XingShuGlassSkin.BackdropDim;
            if (Instance._glassShellImage != null) Instance._glassShellImage.sprite = XingShuGlassSkin.ShellSprite;
            if (Instance._glassRailImage != null) Instance._glassRailImage.sprite = XingShuGlassSkin.RailSprite;
            if (Instance._glassHeaderImage != null)
            {
                Instance._glassHeaderImage.sprite = XingShuGlassSkin.GetRoundedPanelSprite(12, XingShuGlassSkin.HeaderFill, XingShuGlassSkin.DividerLine, 1f);
            }
            Sprite btnSprite = XingShuGlassSkin.ButtonSprite;
            if (Instance._glassBottomBackImg != null) Instance._glassBottomBackImg.sprite = btnSprite;
            if (Instance._glassOrigBtnImg != null) Instance._glassOrigBtnImg.sprite = btnSprite;
            if (Instance._glassSizeBtnImg != null) Instance._glassSizeBtnImg.sprite = btnSprite;
            if (Instance._glassCloseBtnImg != null) Instance._glassCloseBtnImg.sprite = btnSprite;
            if (Instance._glassVerText != null) Instance._glassVerText.color = XingShuGlassSkin.AccentBlue;
            if (Instance._glassBadgeText != null) Instance._glassBadgeText.color = XingShuGlassSkin.AccentMint;
            XingShuGlassSkin.InvalidateRestyleCache();
            Instance.SelectTab(Instance._currentTabIndex);
            if (Instance._contentArea != null)
            {
                foreach (Transform page in Instance._contentArea.transform)
                {
                    if (page != null && page.gameObject.activeSelf && page.gameObject != Instance._comingSoonView)
                    {
                        XingShuGlassSkin.RestylePageForGlass(page.gameObject);
                    }
                }
            }
        }

        private bool _isOpen;
        private int _activeBuiltStyle;
        private Canvas _canvas;
        private CanvasScaler _canvasScaler;
        private CanvasGroup _canvasGroup;
        private RectTransform _logicalRootRt;
        private RectTransform _mainFrameRt;
        private RectTransform _leftRailRt;
        private RectTransform _returnWrapRt;
        private Text _pageTitle;
        private Text _hotkeyHint;
        private Text _sizeCycleLabel;
        private int _hotkeyRevision = -1;
        private float _preferredScale = 1f;
        private GameObject _contentArea;
        private Image _glassBackdropImage;
        private Image _glassShellImage;
        private Image _glassRailImage;
        private Image _glassHeaderImage;
        private Image _glassBottomBackImg;
        private Image _glassOrigBtnImg;
        private Image _glassSizeBtnImg;
        private Image _glassCloseBtnImg;
        private Text _glassVerText;
        private Text _glassBadgeText;
        private XingShuGameSpeedView _gameSpeedView;
        private XingShuHandCardView _handCardView;
        private XingShuKnockbackView _knockbackView;
        private XingShuMonsterActionView _monsterActionView;
        private XingShuCombatPointsView _combatPointsView;
        private XingShuMegathDamageView _megathDamageView;
        private XingShuPlayerCombatHudView _playerCombatHudView;
        private XingShuPlayerStarBuffView _playerStarBuffView;
        private XingShuStaticPortraitView _staticPortraitView;
        private XingShuReplayView _replayView;
        private XingShuFreeCameraView _freeCameraView;
        private XingShuActionGuardView _actionGuardView;
        private XingShuAutoSocialView _autoSocialView;
        private XingShuSelectRoleMapView _selectRoleMapView;
        private XingShuBattleStatsView _battleStatsView;
        private XingShuAdvancedSettingsView _advancedSettingsView;
        private XingShuHotkeySettingsView _hotkeySettingsView;
        private XingShuLobbyBgView _lobbyBgView;
        private XingShuPluginHubView _pluginHubView;
        private readonly Dictionary<int, IXingShuExternalPage> _externalPagesByTabId = new Dictionary<int, IXingShuExternalPage>();
        private readonly Dictionary<int, (GameObject Root, Action Sync)> _tabViews = new Dictionary<int, (GameObject, Action)>();
        private GameObject _comingSoonView;
        private Text _comingSoonText;

        private XingShuUIDragHandle _dragHandle;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private Rect _lastSafeScreenRect;

        private struct TabNavEntry
        {
            internal int TabId;
            internal Button Button;
            internal Image Bg;
            internal Text Label;
            internal bool IsPill;
        }

        private readonly List<TabNavEntry> _tabEntries = new List<TabNavEntry>();
        private int _currentTabIndex = 0;

        private static readonly int[] DISPLAY_TAB_ORDER = new int[]
        {
            0,  // 游戏变速
            1,  // 手牌监视
            2,  // 击飞预测
            11, // 怪物行动可视 (合并索敌/投点选路/冷却)
            13, // 防御与卡牌点数 (合并防御提示/卡牌点数)
            15, // 梅加斯伤害
            16, // 角色状态
            17, // 角色Buff显示
            6,  // 战局守护
            18, // 自动化社交 (合并快捷致谢/头像点赞/星币商店/结算截图)
            22, // 选人地图
            7,  // 对局数据
            5,  // 自由镜头
            4,  // 录像回放
            10, // 大厅背景 (仅手机端展示)
            3,  // 模组加载 (仅手机端展示)
            8,  // 高级设置
            9   // 快捷键
        };

        private static readonly string[] TAB_NAMES = new string[]
        {
            "游戏变速",       // 0
            "手牌监视",       // 1
            "击飞预测",       // 2
            "模组加载",       // 3
            "录像回放",       // 4
            "自由镜头",       // 5
            "战局守护",       // 6
            "对局数据",       // 7
            "高级设置",       // 8
            "快捷键",         // 9
            "大厅背景",       // 10
            "怪物行动可视",   // 11
            "",               // 12 (已并入 11)
            "防御与卡牌点数", // 13
            "",               // 14 (已并入 13)
            "梅加斯伤害",     // 15
            "角色状态",       // 16
            "角色Buff显示",   // 17
            "自动化社交",     // 18
            "",               // 19 (已并入 18)
            "",               // 20 (已并入 18)
            "",               // 21 (已并入 18)
            "选人地图"        // 22
        };

        internal static void Install(GameObject host)
        {
            if (host != null && host.GetComponent<XingShuUICenter>() == null)
            {
                host.AddComponent<XingShuUICenter>();
                Debug.Log("[XingShu Plugin] 功能中心 (XingShuUICenter) 已成功初始化");
            }
            try
            {
                AssistBall.RegisterAction("xingshu_center", "星枢功能中心", Toggle, 10, "打开星枢玩家实用功能中心", "🛠", () => IsOpen);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[XingShu AssistBall] 注册功能中心动作异常: " + ex.Message);
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            XingShuNativeGameUI.EnsureLoaded(ApplyNativeBindingsOnReady);
            XingShuCenterPluginHost.PagesChanged += OnExternalPagesChanged;
            _currentTabIndex = FirstAvailableTab();
            BuildCenterCanvas();
            SetOpenState(false);
            XingShuOverlayInput.Register(this, () => _isOpen, () => new Rect(0f, 0f, Screen.width, Screen.height));
        }

        private void OnExternalPagesChanged()
        {
            if (this == null || _activeBuiltStyle != 1) return;
            RebuildCenterUi();
        }

        private void RebuildCenterUi()
        {
            bool wasOpen = _isOpen;
            if (_canvas != null)
            {
                Destroy(_canvas.gameObject);
                _canvas = null;
            }
            _tabEntries.Clear();
            _tabViews.Clear();
            _externalPagesByTabId.Clear();
            XingShuGlassSkin.InvalidateRestyleCache();
            _canvasScaler = null;
            _logicalRootRt = null;
            _leftRailRt = null;
            _returnWrapRt = null;
            _mainFrameRt = null;
            _dragHandle = null;
            _pageTitle = null;
            _hotkeyHint = null;
            _sizeCycleLabel = null;
            _pluginHubView = null;

            BuildCenterCanvas();
            SetOpenState(wasOpen);
        }

        private void ApplyNativeBindingsOnReady()
        {
            if (this == null || _canvas == null || _activeBuiltStyle == 1) return;
            SelectTab(_currentTabIndex);
        }

        private void OnDestroy()
        {
            XingShuCenterPluginHost.PagesChanged -= OnExternalPagesChanged;
            if (Instance == this)
            {
                XingShuHotkeys.CancelCapture();
                XingShuUIAssetService.Cleanup();
                Instance = null;
            }
        }

        private void Update()
        {
            XingShuNativeGameUI.TickEnsureLoaded();
            XingShuHotkeys.PollCapture();
            if (XingShuHotkeys.TryConsume(XingShuShortcut.Center, "XingShu.UICenter"))
            {
                Toggle();
            }

            if (_isOpen)
            {
                if (_hotkeyHint != null && _hotkeyRevision != XingShuHotkeys.Revision)
                {
                    _hotkeyRevision = XingShuHotkeys.Revision;
                    _hotkeyHint.text = XingShuMobileUIHelper.IsMobile ? "拖动顶部移动面板" : XingShuHotkeys.Display(XingShuShortcut.Center) + " 打开 / 收起";
                }
                // 分辨率变化自适应 Clamp (0 GC / 仅变化时触发)
                if (_lastScreenWidth != Screen.width || _lastScreenHeight != Screen.height || _lastSafeScreenRect != XingShuMobileUIHelper.SafeScreenRect)
                {
                    _lastScreenWidth = Screen.width;
                    _lastScreenHeight = Screen.height;
                    _lastSafeScreenRect = XingShuMobileUIHelper.SafeScreenRect;
                    FitPanelToScreen();
                    if (_dragHandle != null)
                    {
                        _dragHandle.ClampPosition();
                    }
                }

                // 页面数据同步 (无变更 0 GC / 0 UI 写入)
                if (_tabViews.TryGetValue(_currentTabIndex, out var activeView))
                {
                    if (activeView.Sync != null) activeView.Sync();
                }
            }
        }

        private void LateUpdate()
        {
            if (!_isOpen || _activeBuiltStyle != 1) return;
            if (_tabViews.TryGetValue(_currentTabIndex, out var activeView) && activeView.Root != null)
            {
                XingShuGlassSkin.RestylePageForGlassFast(activeView.Root);
            }
        }

        internal static void Open()
        {
            if (Instance != null)
            {
                Instance.SetOpenState(true);
            }
        }

        internal static void OpenHotkeySettingsPage()
        {
            if (Instance == null) return;
            Instance.SetOpenState(true);
            Instance.SelectTab(9);
        }

        internal static void OpenGameSpeedPage()
        {
            if (Instance != null)
            {
                if (!Instance._isOpen)
                {
                    Instance.SetOpenState(true);
                }
                Instance.SelectTab(0);
            }
        }

        internal static void ToggleGameSpeedPage()
        {
            if (Instance == null) return;

            if (!Instance._isOpen)
            {
                // CASE A: Center CLOSED -> OPEN and SELECT GameSpeed
                Instance.SetOpenState(true);
                Instance.SelectTab(0);
            }
            else if (Instance._currentTabIndex == 0)
            {
                // CASE B: Center OPEN && Current Page == GameSpeed -> CLOSE Center
                Instance.SetOpenState(false);
            }
            else
            {
                // CASE C: Center OPEN && Current Page != GameSpeed -> KEEP OPEN, SELECT GameSpeed
                Instance.SelectTab(0);
            }
        }

        internal static void OpenHandCardPage()
        {
            if (Instance != null)
            {
                if (!Instance._isOpen)
                {
                    Instance.SetOpenState(true);
                }
                Instance.SelectTab(1);
            }
        }

        internal static void ToggleHandCardPage()
        {
            if (Instance == null) return;

            if (!Instance._isOpen)
            {
                // CASE A: Center CLOSED -> OPEN and SELECT HandCard
                Instance.SetOpenState(true);
                Instance.SelectTab(1);
            }
            else if (Instance._currentTabIndex == 1)
            {
                // CASE B: Center OPEN && Current Page == HandCard -> CLOSE Center
                Instance.SetOpenState(false);
            }
            else
            {
                // CASE C: Center OPEN && Current Page != HandCard -> KEEP OPEN, SELECT HandCard
                Instance.SelectTab(1);
            }
        }

        internal static void OpenKnockbackPage()
        {
            if (Instance != null)
            {
                if (!Instance._isOpen)
                {
                    Instance.SetOpenState(true);
                }
                Instance.SelectTab(2);
            }
        }

        internal static void OpenStaticPortraitPage()
        {
            if (!TabAvailable(3)) return;
            if (Instance != null)
            {
                if (!Instance._isOpen)
                {
                    Instance.SetOpenState(true);
                }
                Instance.SelectTab(3);
            }
        }

        internal static void ToggleStaticPortraitPage()
        {
            if (!TabAvailable(3)) return;
            if (Instance == null) return;

            if (!Instance._isOpen)
            {
                // CASE A: Center CLOSED -> OPEN and SELECT StaticPortrait
                Instance.SetOpenState(true);
                Instance.SelectTab(3);
            }
            else if (Instance._currentTabIndex == 3)
            {
                // CASE B: Center OPEN && Current Page == StaticPortrait -> CLOSE Center
                Instance.SetOpenState(false);
            }
            else
            {
                // CASE C: Center OPEN && Current Page != StaticPortrait -> KEEP OPEN, SELECT StaticPortrait
                Instance.SelectTab(3);
            }
        }

        internal static void OpenReplayPage()
        {
            if (Instance != null)
            {
                if (!Instance._isOpen)
                {
                    Instance.SetOpenState(true);
                }
                Instance.SelectTab(4);
            }
        }

        internal static void ToggleReplayPage()
        {
            if (Instance == null) return;

            if (!Instance._isOpen)
            {
                // CASE A: Center CLOSED -> OPEN and SELECT Replay
                Instance.SetOpenState(true);
                Instance.SelectTab(4);
            }
            else if (Instance._currentTabIndex == 4)
            {
                // CASE B: Center OPEN && Current Page == Replay -> CLOSE Center
                Instance.SetOpenState(false);
            }
            else
            {
                // CASE C: Center OPEN && Current Page != Replay -> KEEP OPEN, SELECT Replay
                Instance.SelectTab(4);
            }
        }

        internal static void OpenFreeCameraPage()
        {
            if (Instance != null)
            {
                if (!Instance._isOpen)
                {
                    Instance.SetOpenState(true);
                }
                Instance.SelectTab(5);
            }
        }

        internal static void ToggleFreeCameraPage()
        {
            if (Instance == null) return;

            if (!Instance._isOpen)
            {
                // CASE A: Center CLOSED -> OPEN and SELECT FreeCamera
                Instance.SetOpenState(true);
                Instance.SelectTab(5);
            }
            else if (Instance._currentTabIndex == 5)
            {
                // CASE B: Center OPEN && Current Page == FreeCamera -> CLOSE Center
                Instance.SetOpenState(false);
            }
            else
            {
                // CASE C: Center OPEN && Current Page != FreeCamera -> KEEP OPEN, SELECT FreeCamera
                Instance.SelectTab(5);
            }
        }

        internal static void OpenActionGuardPage()
        {
            if (Instance != null)
            {
                if (!Instance._isOpen)
                {
                    Instance.SetOpenState(true);
                }
                Instance.SelectTab(6);
            }
        }

        internal static void ToggleActionGuardPage()
        {
            if (Instance == null) return;

            if (!Instance._isOpen)
            {
                // CASE A: Center CLOSED -> OPEN and SELECT ActionGuard
                Instance.SetOpenState(true);
                Instance.SelectTab(6);
            }
            else if (Instance._currentTabIndex == 6)
            {
                // CASE B: Center OPEN && Current Page == ActionGuard -> CLOSE Center
                Instance.SetOpenState(false);
            }
            else
            {
                // CASE C: Center OPEN && Current Page != ActionGuard -> KEEP OPEN, SELECT ActionGuard
                Instance.SelectTab(6);
            }
        }

        internal static void OpenBattleStatsPage()
        {
            if (Instance != null)
            {
                if (!Instance._isOpen)
                {
                    Instance.SetOpenState(true);
                }
                Instance.SelectTab(7);
            }
        }

        internal static void ToggleBattleStatsPage()
        {
            if (Instance == null) return;

            if (!Instance._isOpen)
            {
                Instance.SetOpenState(true);
                Instance.SelectTab(7);
            }
            else if (Instance._currentTabIndex == 7)
            {
                Instance.SetOpenState(false);
            }
            else
            {
                Instance.SelectTab(7);
            }
        }

        internal static void OpenLobbyBgPage()
        {
            if (Instance == null || !TabAvailable(10)) return;
            if (!Instance._isOpen)
            {
                Instance.SetOpenState(true);
            }
            Instance.SelectTab(10);
        }

        internal static void ToggleLobbyBgPage()
        {
            if (Instance == null || !TabAvailable(10)) return;
            if (!Instance._isOpen)
            {
                Instance.SetOpenState(true);
                Instance.SelectTab(10);
            }
            else if (Instance._currentTabIndex == 10)
            {
                Instance.SetOpenState(false);
            }
            else
            {
                Instance.SelectTab(10);
            }
        }

        internal static void OpenAdvancedSettings(string focusSection = null)
        {
            if (Instance != null)
            {
                if (!Instance._isOpen)
                {
                    Instance.SetOpenState(true);
                }
                Instance.SelectTab(8);
                if (Instance._advancedSettingsView != null && !string.IsNullOrEmpty(focusSection))
                {
                    Instance._advancedSettingsView.FocusSection(focusSection);
                }
            }
        }

        internal static void OpenAdvancedSettingsPage()
        {
            OpenAdvancedSettings(null);
        }

        internal static void ApplyUserScale(float scale)
        {
            if (Instance != null && Instance._mainFrameRt != null)
            {
                if (!XingShuMobileUIHelper.IsMobile)
                {
                    scale = XingShuMobileUIHelper.FiniteClamp(scale, XingShuConfigKeys.FunctionCenterDesktopScaleMin, XingShuConfigKeys.FunctionCenterDesktopScaleMax, XingShuConfigKeys.FunctionCenterDesktopScaleDefault);
                }
                else
                {
                    scale = Mathf.Clamp(scale, XingShuConfigKeys.MobileUserScaleMin, XingShuConfigKeys.MobileUserScaleMax);
                }
                Instance._preferredScale = scale;
                Instance.FitPanelToScreen();
                if (Instance._dragHandle != null)
                {
                    Instance._dragHandle.ClampPosition();
                }
            }
        }

        internal static void ToggleAdvancedSettingsPage()
        {
            if (Instance == null) return;

            if (!Instance._isOpen)
            {
                Instance.SetOpenState(true);
                Instance.SelectTab(8);
            }
            else if (Instance._currentTabIndex == 8)
            {
                Instance.SetOpenState(false);
            }
            else
            {
                Instance.SelectTab(8);
            }
        }

        internal static void Close()
        {
            if (Instance != null)
            {
                Instance.SetOpenState(false);
            }
        }

        internal static void Toggle()
        {
            if (Instance != null)
            {
                Instance.SetOpenState(!Instance._isOpen);
            }
        }

        private void SetOpenState(bool open)
        {
            if (!open) XingShuHotkeys.CancelCapture();
            _isOpen = open;
            if (_canvas != null)
            {
                _canvas.enabled = open;
            }

            if (open)
            {
                XingShuUITheme.RefreshFonts(_canvas != null ? _canvas.gameObject : null, _activeBuiltStyle == 1);
                XingShuCentralInputManager.EnsureEventSystemAvailable("XingShu.UICenter");
                SelectTab(_currentTabIndex);
                FitPanelToScreen();
                if (PanelScale < 0.99f && _dragHandle != null)
                {
                    _dragHandle.ClampPosition();
                }

                // 互斥门禁: 打开新功能中心时确保关闭旧功能助手菜单 (Simultaneous Visible: 0)
                try
                {
                    XingShuRuntime.Utilities.AssistBallGUI assistBall = UnityEngine.Object.FindObjectOfType<XingShuRuntime.Utilities.AssistBallGUI>();
                    if (assistBall != null)
                    {
                        assistBall.SetMenuOpen(false);
                    }
                }
                catch { }
            }
        }

        private void BuildCenterCanvas()
        {
            _activeBuiltStyle = CenterStyle;
            if (_activeBuiltStyle == 1)
            {
                BuildGlassCenterCanvas();
                return;
            }

            // 0. 核心生产资产运行时确定性自检
            XingShuUIAssetService.ValidateCoreAssets();

            // 1. Root Canvas (1920x1080 双端统一适配)
            GameObject canvasGo = new GameObject("XINGSHU_UICenter_Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 31000;
            _canvas.pixelPerfect = false;

            float ss = ComputeCanvasSupersample(Screen.height);
            CanvasScaler scaler = _canvasScaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f / ss, 1080f / ss);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1.0f;

            canvasGo.AddComponent<GraphicRaycaster>();
            _canvasGroup = canvasGo.AddComponent<CanvasGroup>();

            GameObject logicalRoot = XingShuUIFactory.CreateUIObject("LogicalRoot_1080p", canvasGo);
            _logicalRootRt = Place(logicalRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f));

            // 2. 轻量遮罩，点击空白处收起
            GameObject backdropGo = XingShuUIFactory.CreateUIObject("Backdrop", logicalRoot);
            RectTransform backdropRt = backdropGo.GetComponent<RectTransform>();
            backdropRt.anchorMin = Vector2.zero;
            backdropRt.anchorMax = Vector2.one;
            backdropRt.offsetMin = Vector2.zero;
            backdropRt.offsetMax = Vector2.zero;

            Image backdropImg = backdropGo.AddComponent<Image>();
            backdropImg.sprite = XingShuUIAssetService.WhiteSprite;
            backdropImg.color = new Color(0.08f, 0.06f, 0.14f, 0.54f);
            backdropImg.type = Image.Type.Simple;

            Button backdropBtn = backdropGo.AddComponent<Button>();
            backdropBtn.transition = Selectable.Transition.None;
            backdropBtn.onClick.AddListener(Close);

            // 2B. 贴左独立导航组 (返回按钮 + 白底星点导航栏，与游戏 Setting 界面比例 1:1 对齐)
            GameObject leftRailGo = XingShuUIFactory.CreateUIObject("LeftRailGroup", logicalRoot);
            _leftRailRt = Place(leftRailGo, Vector2.zero, Vector2.up, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(328f, 0f));

            // 3. 居中偏右的深色主设置框架（尺寸与位置对齐原版游戏 Setting 界面，右侧预留悬浮球通道）
            GameObject mainFrame = XingShuUIFactory.CreateUIObject("MainFrame", logicalRoot);
            _mainFrameRt = mainFrame.GetComponent<RectTransform>();
            _mainFrameRt.anchorMin = new Vector2(0.5f, 0.5f);
            _mainFrameRt.anchorMax = new Vector2(0.5f, 0.5f);
            _mainFrameRt.pivot = new Vector2(0.5f, 0.5f);
            _mainFrameRt.anchoredPosition = Vector2.zero;
            _mainFrameRt.sizeDelta = new Vector2(1460f, 960f);

            float userScale = XingShuConfigService.GetFloat(XingShuConfigKeys.MobileUserScale, XingShuConfigKeys.MobileUserScaleDefault);
            if (!XingShuMobileUIHelper.IsMobile)
            {
                float safeScale = XingShuMobileUIHelper.FiniteClamp(userScale, XingShuConfigKeys.FunctionCenterDesktopScaleMin, XingShuConfigKeys.FunctionCenterDesktopScaleMax, XingShuConfigKeys.FunctionCenterDesktopScaleDefault);
                if (safeScale != userScale)
                {
                    XingShuConfigService.SetFloat(XingShuConfigKeys.MobileUserScale, safeScale);
                    XingShuConfigService.Save();
                    userScale = safeScale;
                }
            }
            else
            {
                userScale = Mathf.Clamp(userScale, XingShuConfigKeys.MobileUserScaleMin, XingShuConfigKeys.MobileUserScaleMax);
            }
            _preferredScale = userScale;
            FitPanelToScreen();

            // 深炭灰圆角主体（底部预留 68px 给外置 UID / 版权信息 / 收起按钮，与原版 Setting 一致）
            Sprite frameSprite = XingShuNativeGameUI.GetIfReady(XingShuNativeSprite.RoundedDarkFrame) ?? XingShuUIAssetService.GetSprite("window_frame.png");
            GameObject body = XingShuUIFactory.CreateUIObject("GameSettingsBody", mainFrame);
            RectTransform bodyRt = body.GetComponent<RectTransform>();
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(0f, 68f);
            bodyRt.offsetMax = Vector2.zero;
            Image baseBg = body.AddComponent<Image>();
            baseBg.sprite = frameSprite;
            baseBg.type = Image.Type.Sliced;
            baseBg.color = XingShuNativeGameUI.GetIfReady(XingShuNativeSprite.RoundedDarkFrame) != null
                ? Color.white
                : XingShuUITheme.PanelBase;
            XingShuNativeGameUI.Bind(baseBg, XingShuNativeSprite.RoundedDarkFrame, Image.Type.Sliced, false, Color.white);
            XingShuUIFactory.AddShadow(body, new Color(0f, 0f, 0f, 0.28f), new Vector2(0f, -8f));

            GameObject bodyClip = XingShuUIFactory.CreateUIObject("NativeBodyClip", body);
            RectTransform clipRt = bodyClip.GetComponent<RectTransform>();
            clipRt.anchorMin = Vector2.zero;
            clipRt.anchorMax = Vector2.one;
            clipRt.offsetMin = new Vector2(6f, 6f);
            clipRt.offsetMax = new Vector2(-6f, -6f);
            bodyClip.AddComponent<RectMask2D>();

            Image darkPattern = XingShuUIFactory.CreateImage(bodyClip, "DarkPatternLayer", XingShuUIAssetService.WhiteSprite, new Color(1f, 1f, 1f, 0.01f));
            XingShuDetailLayout.Fill(darkPattern.rectTransform);
            darkPattern.raycastTarget = false;
            XingShuNativeGameUI.Bind(darkPattern, XingShuNativeSprite.DarkPattern, Image.Type.Tiled, false, new Color(1f, 1f, 1f, 0.85f));

            XingShuUIFactory.CreateNativeDiagonalStripes(bodyClip, 0.72f, 220f, 105f);

            // 4. 顶部标题栏与外置底栏 + 左上角独立返回键
            BuildTitleBar(mainFrame, leftRailGo);

            // 5. 左侧贴边星点白底可滚动导航栏
            BuildSidebar(leftRailGo);

            // 6. 纵向分割线
            BuildVerticalDivider(mainFrame);

            // 7. 右侧主内容展示区
            BuildContentArea(mainFrame);
            if (XingShuMobileUIHelper.IsMobile)
            {
                foreach (Transform page in _contentArea.transform)
                {
                    if (page.gameObject != _comingSoonView)
                    {
                        XingShuMobilePageLayout.Adapt(page.gameObject);
                    }
                }
            }
        }

        public static float ComputeCanvasSupersample(int screenHeight)
        {
            if (screenHeight <= 1200) return 2.0f;
            if (screenHeight <= 1800) return 1.5f;
            return 1.0f;
        }

        private void FitPanelToScreen()
        {
            if (_mainFrameRt == null || Screen.width <= 0 || Screen.height <= 0) return;
            float panelScale = PanelScale;
            float ss = ComputeCanvasSupersample(Screen.height);
            float canvasScale = Mathf.Max(0.1f, Screen.height / 1080f);
            float screenW = Screen.width / canvasScale;
            float screenH = 1080f;

            if (_canvasScaler != null)
            {
                _canvasScaler.referenceResolution = new Vector2(1920f / ss, 1080f / ss);
            }
            if (_logicalRootRt != null)
            {
                _logicalRootRt.sizeDelta = new Vector2(screenW, screenH);
                _logicalRootRt.localScale = Vector3.one / ss;
            }

            if (_activeBuiltStyle == 1)
            {
                float safeL = Mathf.Max(0f, XingShuMobileUIHelper.SafeScreenRect.x / canvasScale);
                float baseGW = Mathf.Min(1480f, Mathf.Max(920f, screenW - safeL - 88f));
                float baseGH = Mathf.Min(920f, Mathf.Max(640f, screenH - 72f));
                _mainFrameRt.sizeDelta = new Vector2(baseGW, baseGH);
                _mainFrameRt.localScale = Vector3.one * panelScale;
                if (panelScale >= 0.99f)
                {
                    _mainFrameRt.anchoredPosition = Vector2.zero;
                }
                else if (_dragHandle != null)
                {
                    _dragHandle.ClampPosition();
                }
                return;
            }

            float safeLeft = Mathf.Max(0f, XingShuMobileUIHelper.SafeScreenRect.x / canvasScale);
            float railOffset = safeLeft > 90f ? safeLeft * 0.45f : 0f;
            if (_leftRailRt != null)
            {
                _leftRailRt.anchoredPosition = new Vector2(railOffset, 0f);
            }

            float leftBound = railOffset + (screenW >= 2100f ? 380f : 326f);
            float rightClear = screenW >= 2100f ? 220f : 112f;
            float rightBound = Mathf.Max(leftBound + 880f, screenW - rightClear);
            float availW = Mathf.Max(880f, rightBound - leftBound);
            float availH = Mathf.Max(640f, screenH - 72f);

            float maxPanelW = screenW >= 2100f ? 1540f : 1460f;
            float baseW = Mathf.Min(maxPanelW, availW);
            float baseH = Mathf.Min(960f, availH);

            _mainFrameRt.sizeDelta = new Vector2(baseW, baseH);
            _mainFrameRt.localScale = Vector3.one * panelScale;
            if (panelScale >= 0.99f)
            {
                float centerX = (leftBound + rightBound) * 0.5f - screenW * 0.5f;
                _mainFrameRt.anchoredPosition = new Vector2(centerX, 0f);
            }
            else if (_dragHandle != null)
            {
                _dragHandle.ClampPosition();
            }
        }

        private void UpdateSizeCycleLabel()
        {
            if (_sizeCycleLabel == null) return;
            float scale = PanelScale;
            _sizeCycleLabel.text = scale >= 0.985f ? "标准" : $"{Mathf.RoundToInt(scale * 100f)}%";
        }

        private void CyclePanelSize()
        {
            float cur = PanelScale;
            float next = cur >= 0.94f ? 0.85f : (cur >= 0.78f ? 0.70f : 1.00f);
            SetPanelScale(next);
            if (_advancedSettingsView != null && _currentTabIndex == 8)
            {
                _advancedSettingsView.UpdateSync();
            }
        }

        private static RectTransform Place(GameObject go, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        internal static void RefreshMobileText()
        {
            if (Instance != null && Instance._canvas != null)
                XingShuMobilePageLayout.RefreshAll(Instance._canvas.gameObject);
        }

        internal static void RefreshMobileWindow()
        {
            if (Instance == null || !XingShuMobileUIHelper.IsMobile) return;
            Instance.FitPanelToScreen();
            Instance._dragHandle?.ClampPosition();
        }

        private void BuildTitleBar(GameObject parent, GameObject leftRailParent)
        {
            // 左上角分层立体蓝色「返回」键（贴靠 LeftRailGroup 顶部，与游戏 Common 包 1:1 对齐）
            GameObject backWrap = XingShuUIFactory.CreateUIObject("ReturnWrap", leftRailParent);
            _returnWrapRt = Place(backWrap, Vector2.up, Vector2.up, Vector2.up, new Vector2(56f, -24f), new Vector2(196f, 70f));
            Image returnShadow = XingShuUIFactory.CreateImage(backWrap, "ReturnBgShadow", XingShuUIAssetService.GetRoundedFallbackSprite(14), new Color(0.14f, 0.25f, 0.62f, 0.85f));
            Place(returnShadow.gameObject, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-4f, -4f), new Vector2(-6f, -6f));
            returnShadow.raycastTarget = false;
            XingShuNativeGameUI.Bind(returnShadow, XingShuNativeSprite.ReturnBg, Image.Type.Sliced, false, Color.white);

            Button back = XingShuUIFactory.CreateButton(backWrap, "ReturnToGame", "‹  返回", XingShuUIAssetService.GetRoundedFallbackSprite(14),
                new Color(0.24f, 0.42f, 0.92f, 1f), Color.white, Close, 26);
            Place(back.gameObject, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(4f, 4f), new Vector2(-10f, -10f));
            XingShuNativeGameUI.Bind(back.GetComponent<Image>(), XingShuNativeSprite.ReturnBtn, Image.Type.Sliced, false, Color.white);
            XingShuUIFactory.AddOutline(back.GetComponentInChildren<Text>().gameObject, new Color(0.10f, 0.14f, 0.34f, 1f), new Vector2(2f, -2f));

            GameObject titleBar = XingShuUIFactory.CreateUIObject("TitleBar", parent);
            Place(titleBar, Vector2.up, Vector2.one, Vector2.up, new Vector2(28f, -14f), new Vector2(-56f, 62f));
            Image hitArea = titleBar.AddComponent<Image>();
            hitArea.sprite = XingShuUIAssetService.WhiteSprite;
            // A zero-alpha Graphic is culled by Unity and cannot receive the drag gesture.
            hitArea.color = new Color(0f, 0f, 0f, 0.01f);
            hitArea.raycastTarget = true;
            _dragHandle = titleBar.AddComponent<XingShuUIDragHandle>();
            _dragHandle.targetRect = _mainFrameRt;
            _dragHandle.parentRect = _logicalRootRt ?? (_canvas.transform as RectTransform);

            Image chip = XingShuUIFactory.CreateNativeTitleTag(titleBar, "PageTitleChip", TAB_NAMES[0], out _pageTitle, 25);
            Place(chip.gameObject, Vector2.zero, Vector2.up, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(256f, -6f));

            Text brand = XingShuUIFactory.CreateText(titleBar, "Brand", "星枢 · 原版 UI 皮肤", 18, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(brand.gameObject, Vector2.zero, Vector2.up, new Vector2(0f, 0.5f), new Vector2(276f, 0f), new Vector2(240f, 0f));
            brand.raycastTarget = false;
            XingShuUIFactory.AddOutline(brand.gameObject, new Color(0.08f, 0.08f, 0.12f, 0.9f), new Vector2(1.4f, -1.4f));

            Text hotkey = _hotkeyHint = XingShuUIFactory.CreateText(titleBar, "Hotkey", XingShuMobileUIHelper.IsMobile ? "缩小窗口后可拖动顶部" : XingShuHotkeys.Display(XingShuShortcut.Center) + " 打开 / 收起", 14, new Color(0.86f, 0.85f, 0.92f), TextAnchor.MiddleRight);
            Place(hotkey.gameObject, Vector2.right, Vector2.one, new Vector2(1f, 0.5f), new Vector2(-264f, 0f), new Vector2(200f, 0f));
            hotkey.raycastTarget = false;

            Button styleSwitchBtn = XingShuUIFactory.CreateButton(titleBar, "StyleSwitchBtn", "毛玻璃 UI",
                XingShuUIAssetService.GetSprite("button_secondary.png"), new Color(0.88f, 0.94f, 1f, 0.95f), XingShuUITheme.PrimaryText,
                () => SetCenterStyle(1), 13);
            RectTransform styleBtnRt = Place(styleSwitchBtn.gameObject, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-146f, 0f), new Vector2(106f, 34f));

            Button sizeCycleBtn = XingShuUIFactory.CreateButton(titleBar, "SizeCycleBtn", PanelScale >= 0.985f ? "标准" : $"{Mathf.RoundToInt(PanelScale * 100f)}%",
                XingShuUIAssetService.GetSprite("button_secondary.png"), new Color(0.95f, 0.96f, 0.99f, 0.92f), XingShuUITheme.PrimaryText, CyclePanelSize, 14);
            _sizeCycleLabel = sizeCycleBtn.GetComponentInChildren<Text>();
            RectTransform sizeBtnRt = Place(sizeCycleBtn.gameObject, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-60f, 0f), new Vector2(76f, 34f));

            Button close = XingShuUIFactory.CreateButton(titleBar, "Close", "×", XingShuUIAssetService.GetSprite("close_normal.png"), Color.white, Color.white, Close, 28);
            RectTransform closeRt = Place(close.gameObject, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(48f, 48f));
            _dragHandle.SetExcludedRects(closeRt, sizeBtnRt, styleBtnRt);
            _dragHandle.BindEvents();

            // 底部左侧外置原生 UID 胶囊（与深色主框下方外缘对齐：左白底粉字「星枢」，右粉底白字「v0.1.7」）
            GameObject uidGo = XingShuUIFactory.CreateUIObject("UidPill", parent);
            Place(uidGo, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(24f, 8f), new Vector2(252f, 52f));
            Image uidBg = uidGo.AddComponent<Image>();
            uidBg.sprite = XingShuUIAssetService.GetSprite("hotkey_badge.png");
            uidBg.type = Image.Type.Sliced;
            uidBg.color = XingShuUIAssetService.IsFallbackSprite(uidBg.sprite) ? new Color(0.11f, 0.12f, 0.16f, 0.92f) : Color.white;
            uidBg.raycastTarget = false;
            XingShuNativeGameUI.Bind(uidBg, XingShuNativeSprite.UidPill, Image.Type.Sliced, false, Color.white);

            Text uidPrefix = XingShuUIFactory.CreateText(uidGo, "UidPrefix", "星枢", 18, XingShuUITheme.Pink, TextAnchor.MiddleCenter, FontStyle.Bold);
            RectTransform upRt = uidPrefix.rectTransform;
            upRt.anchorMin = new Vector2(0f, 0f);
            upRt.anchorMax = new Vector2(0.32f, 1f);
            upRt.offsetMin = new Vector2(4f, 0f);
            upRt.offsetMax = Vector2.zero;
            uidPrefix.raycastTarget = false;

            Text uidVer = XingShuUIFactory.CreateText(uidGo, "UidVersion", "v0.2.0", 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            RectTransform uvRt = uidVer.rectTransform;
            uvRt.anchorMin = new Vector2(0.33f, 0f);
            uvRt.anchorMax = new Vector2(0.95f, 1f);
            uvRt.offsetMin = Vector2.zero;
            uvRt.offsetMax = Vector2.zero;
            uidVer.raycastTarget = false;

            Text footer = XingShuUIFactory.CreateText(parent, "ProjectSource", $"{XingShu.Runtime.XingShuRuntimeHost.RuntimeVersion} · github.com/Breezecaresses/AstralParty-XingShu", 13, new Color(0.90f, 0.91f, 0.96f), TextAnchor.MiddleCenter);
            Place(footer.gameObject, Vector2.zero, Vector2.right, Vector2.zero, new Vector2(290f, 18f), new Vector2(-548f, 28f));
            footer.raycastTarget = false;
            XingShuUIFactory.AddOutline(footer.gameObject, new Color(0.06f, 0.06f, 0.12f, 0.9f), new Vector2(1.2f, -1.2f));

            // 底栏右侧外置原生「收起悬浮球」粉红大按钮
            Button hidePanelBtn = XingShuUIFactory.CreateButton(parent, "CollapsePanelBtn", "收起悬浮球", XingShuUIAssetService.GetSprite("button_primary.png"),
                Color.white, Color.white, Close, 18);
            Place(hidePanelBtn.gameObject, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 8f), new Vector2(216f, 52f));
            XingShuNativeGameUI.Bind(hidePanelBtn.GetComponent<Image>(), XingShuNativeSprite.BtnLarge, Image.Type.Sliced, false, Color.white);
            Text hideTxt = hidePanelBtn.GetComponentInChildren<Text>();
            if (hideTxt != null) XingShuUIFactory.AddOutline(hideTxt.gameObject, new Color(0.32f, 0.04f, 0.16f, 1f), new Vector2(1.5f, -1.5f));
        }

        private void BuildSidebar(GameObject parent)
        {
            Image rail = XingShuUIFactory.CreateImage(parent, "NavigationRail", XingShuUIAssetService.GetSprite("card_base.png"), new Color(0.98f, 0.98f, 0.99f, 0.96f), Image.Type.Sliced);
            Place(rail.gameObject, Vector2.zero, Vector2.up, Vector2.zero, new Vector2(0f, 72f), new Vector2(270f, -196f));
            rail.raycastTarget = false;
            XingShuNativeGameUI.Bind(rail, XingShuNativeSprite.TabBg, Image.Type.Sliced, false, Color.white);

            Image line = XingShuUIFactory.CreateImage(rail.gameObject, "YellowRail", null, new Color(0.96f, 0.91f, 0.24f, 0.65f));
            RectTransform lineRt = line.rectTransform;
            lineRt.anchorMin = new Vector2(0.72f, 0f); lineRt.anchorMax = new Vector2(0.72f, 1f);
            lineRt.offsetMin = new Vector2(-16f, 0f); lineRt.offsetMax = new Vector2(16f, 0f);
            line.raycastTarget = false;

            GameObject sidebarWrap = XingShuUIFactory.CreateUIObject("Sidebar", parent);
            Place(sidebarWrap, Vector2.zero, Vector2.up, Vector2.zero, new Vector2(18f, 82f), new Vector2(280f, -216f));
            ScrollRect sideScroll = XingShuUIFactory.CreateVerticalScrollView(sidebarWrap, "SidebarScroll", out GameObject sidebar);
            sideScroll.scrollSensitivity = 36f;
            Transform sbBar = sideScroll.transform.Find("VerticalScrollbar");
            if (sbBar != null) sbBar.gameObject.SetActive(false);
            VerticalLayoutGroup layout = sidebar.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(4, 6, 8, 8);
            layout.spacing = 6f; layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;

            Sprite normal = XingShuNativeGameUI.GetIfReady(XingShuNativeSprite.TabPink) ?? XingShuUIAssetService.GetSprite("sidebar_tab_normal.png");
            Sprite selected = XingShuNativeGameUI.GetIfReady(XingShuNativeSprite.TabGold) ?? XingShuUIAssetService.GetSprite("sidebar_tab_selected.png");
            Sprite hover = XingShuNativeGameUI.GetIfReady(XingShuNativeSprite.TabSelected) ?? XingShuUIAssetService.GetSprite("sidebar_tab_hover.png");
            Sprite dotFb = XingShuUIAssetService.GetRoundedFallbackSprite(8);

            for (int k = 0; k < DISPLAY_TAB_ORDER.Length; k++)
            {
                int tabId = DISPLAY_TAB_ORDER[k];
                if (!TabAvailable(tabId)) continue;

                if (tabId == 8)
                {
                    GameObject sep = XingShuUIFactory.CreateUIObject("SidebarGroupSpacer", sidebar);
                    LayoutElement sepLe = sep.AddComponent<LayoutElement>();
                    sepLe.minHeight = 6f; sepLe.preferredHeight = 6f; sepLe.flexibleHeight = 0f;
                    Image sepLine = sep.AddComponent<Image>();
                    sepLine.color = new Color(1f, 0.75f, 0f, 0.45f);
                    sepLine.raycastTarget = false;
                }

                GameObject tab = XingShuUIFactory.CreateUIObject("Tab_" + tabId, sidebar);
                LayoutElement le = tab.AddComponent<LayoutElement>();
                le.minHeight = 44f; le.preferredHeight = 44f; le.flexibleHeight = 0f;
                Image image = tab.AddComponent<Image>();
                image.sprite = tabId == _currentTabIndex ? selected : normal; image.type = Image.Type.Sliced; image.color = Color.white;

                Image dotImg = XingShuUIFactory.CreateImage(tab, "Dot", XingShuNativeGameUI.GetIfReady(XingShuNativeSprite.TabDot) ?? dotFb, Color.white, Image.Type.Simple);
                dotImg.preserveAspect = true;
                dotImg.raycastTarget = false;
                XingShuNativeGameUI.Bind(dotImg, XingShuNativeSprite.TabDot, Image.Type.Simple, true, Color.white);
                Place(dotImg.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(14f, 14f));

                Text text = XingShuUIFactory.CreateText(tab, "Text", TAB_NAMES[tabId], 18, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
                text.rectTransform.offsetMin = new Vector2(44f, 0f); text.rectTransform.offsetMax = new Vector2(-8f, 0f);
                text.raycastTarget = false;
                XingShuUIFactory.AddOutline(text.gameObject, new Color(0.10f, 0.08f, 0.13f, 1f), new Vector2(1.8f, -1.8f));
                Button button = tab.AddComponent<Button>(); button.targetGraphic = image;
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState { highlightedSprite=hover, pressedSprite=selected };
                button.onClick.AddListener(delegate { SelectTab(tabId); });
                _tabEntries.Add(new TabNavEntry
                {
                    TabId = tabId,
                    Button = button,
                    Bg = image,
                    Label = text,
                    IsPill = false,
                });
            }
        }

        private void BuildVerticalDivider(GameObject parent)
        {
            // The detached navigation rail replaces the old glass divider.
        }

        private void BuildContentArea(GameObject parent)
        {
            _contentArea = XingShuUIFactory.CreateUIObject("ContentArea", parent);
            RectTransform caRt = _contentArea.GetComponent<RectTransform>();
            caRt.anchorMin = Vector2.zero;
            caRt.anchorMax = Vector2.one;
            caRt.offsetMin = new Vector2(28f, 84f);
            caRt.offsetMax = new Vector2(-28f, -84f);

            _tabViews.Clear();

            RegisterTabView(0, TabAvailable(0) ? (_gameSpeedView = new XingShuGameSpeedView(_contentArea.transform)).Root : null,
                () => _gameSpeedView?.UpdateSync());
            RegisterTabView(1, TabAvailable(1) ? (_handCardView = new XingShuHandCardView(_contentArea.transform)).Root : null,
                () => _handCardView?.UpdateSync());
            RegisterTabView(2, TabAvailable(2) ? (_knockbackView = new XingShuKnockbackView(_contentArea.transform)).Root : null,
                () => _knockbackView?.UpdateSync());
            RegisterTabView(11, TabAvailable(11) ? (_monsterActionView = new XingShuMonsterActionView(_contentArea.transform)).Root : null,
                () => _monsterActionView?.UpdateSync());
            RegisterTabView(13, TabAvailable(13) ? (_combatPointsView = new XingShuCombatPointsView(_contentArea.transform)).Root : null,
                () => _combatPointsView?.UpdateSync());
            RegisterTabView(15, TabAvailable(15) ? (_megathDamageView = new XingShuMegathDamageView(_contentArea.transform)).Root : null,
                () => _megathDamageView?.UpdateSync());
            RegisterTabView(16, TabAvailable(16) ? (_playerCombatHudView = new XingShuPlayerCombatHudView(_contentArea.transform)).Root : null,
                () => _playerCombatHudView?.UpdateSync());
            RegisterTabView(17, TabAvailable(17) ? (_playerStarBuffView = new XingShuPlayerStarBuffView(_contentArea.transform)).Root : null,
                () => _playerStarBuffView?.UpdateSync());
            RegisterTabView(6, TabAvailable(6) ? (_actionGuardView = new XingShuActionGuardView(_contentArea.transform)).Root : null,
                () => _actionGuardView?.UpdateSync());
            RegisterTabView(18, TabAvailable(18) ? (_autoSocialView = new XingShuAutoSocialView(_contentArea.transform)).Root : null,
                () => _autoSocialView?.UpdateSync());
            RegisterTabView(22, TabAvailable(22) ? (_selectRoleMapView = new XingShuSelectRoleMapView(_contentArea.transform)).Root : null,
                () => _selectRoleMapView?.UpdateSync());
            RegisterTabView(7, TabAvailable(7) ? (_battleStatsView = new XingShuBattleStatsView(_contentArea.transform)).Root : null,
                () => _battleStatsView?.UpdateSync());
            RegisterTabView(5, TabAvailable(5) ? (_freeCameraView = new XingShuFreeCameraView(_contentArea.transform)).Root : null,
                () => _freeCameraView?.UpdateSync());
            RegisterTabView(4, TabAvailable(4) ? (_replayView = new XingShuReplayView(_contentArea.transform)).Root : null,
                () => _replayView?.UpdateSync());
            RegisterTabView(10, TabAvailable(10) ? (_lobbyBgView = new XingShuLobbyBgView(_contentArea.transform)).Root : null,
                () => _lobbyBgView?.UpdateSync());
            RegisterTabView(3, TabAvailable(3) ? (_staticPortraitView = new XingShuStaticPortraitView(_contentArea.transform)).Root : null,
                () => _staticPortraitView?.UpdateSync());

            _advancedSettingsView = new XingShuAdvancedSettingsView(_contentArea.transform);
            RegisterTabView(8, _advancedSettingsView.Root, () => _advancedSettingsView?.UpdateSync());

            _hotkeySettingsView = new XingShuHotkeySettingsView(_contentArea.transform);
            RegisterTabView(9, _hotkeySettingsView.Root, () => _hotkeySettingsView?.UpdateSync());

            if (_activeBuiltStyle == 1)
            {
                _pluginHubView = new XingShuPluginHubView(_contentArea.transform, () => SetCenterStyle(0));
                RegisterTabView(23, _pluginHubView.Root, () => _pluginHubView?.UpdateSync());

                foreach (var kv in _externalPagesByTabId)
                {
                    int extTabId = kv.Key;
                    IXingShuExternalPage page = kv.Value;
                    GameObject extRoot = XingShuUIFactory.CreateUIObject("Page_Ext_" + page.Id, _contentArea);
                    RectTransform ert = extRoot.GetComponent<RectTransform>();
                    ert.anchorMin = Vector2.zero; ert.anchorMax = Vector2.one;
                    ert.offsetMin = Vector2.zero; ert.offsetMax = Vector2.zero;
                    try
                    {
                        page.BuildContent(extRoot);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[XingShuCenterPluginHost] 渲染外部插件页面异常 (" + page.Id + "): " + ex.Message);
                    }
                    RegisterTabView(extTabId, extRoot, () =>
                    {
                        try { page.OnSync(); } catch { }
                    });
                }
            }

            // 占位提示页面 (Coming Soon)
            _comingSoonView = XingShuUIFactory.CreateUIObject("Page_ComingSoon", _contentArea);
            RectTransform csRt = _comingSoonView.GetComponent<RectTransform>();
            csRt.anchorMin = Vector2.zero;
            csRt.anchorMax = Vector2.one;
            csRt.offsetMin = Vector2.zero;
            csRt.offsetMax = Vector2.zero;

            // 拟态磨砂玻璃卡片切片 (card_base.png, 圆角 12px)
            Sprite csSprite = XingShuUIAssetService.GetSprite("card_base.png");
            Image csBase = _comingSoonView.AddComponent<Image>();
            csBase.sprite = csSprite;
            csBase.type = Image.Type.Sliced;
            csBase.color = XingShuUIAssetService.IsFallbackSprite(csSprite) ? XingShuUITheme.Card : Color.white;

            _comingSoonText = XingShuUIFactory.CreateText(_comingSoonView, "ComingSoonText", "功能正在开发与适配中", 16, XingShuUITheme.SecondaryText, TextAnchor.MiddleCenter);
            RectTransform cstRt = _comingSoonText.rectTransform;
            cstRt.anchorMin = Vector2.zero;
            cstRt.anchorMax = Vector2.one;
            cstRt.offsetMin = new Vector2(30f, 30f);
            cstRt.offsetMax = new Vector2(-30f, -30f);

            _comingSoonView.SetActive(false);
        }

        private void RegisterTabView(int tabId, GameObject root, Action sync)
        {
            if (root == null) return;
            root.SetActive(tabId == _currentTabIndex);
            _tabViews[tabId] = (root, sync);
        }

        private static bool TabAvailable(int index)
        {
            switch (index)
            {
                case 0: return LauncherModules.CanLoad("GameSpeedControllerGUI");
                case 1: return LauncherModules.CanLoad("HandCardDisplayManager");
                case 2: return LauncherModules.CanLoad("MonsterKillProbability");
                case 3: return XingShuMobileUIHelper.IsMobile && LauncherModules.CanLoad("StaticPortraitGUI");
                case 4: return LauncherModules.CanLoad("ReplayPlayerGUI");
                case 5: return LauncherModules.CanLoad("MapCameraGUI");
                case 6: return LauncherModules.CanLoad("AutoResetActionTimeGUI");
                case 7: return LauncherModules.CanLoad("BattleStatsOverlay");
                case 10: return XingShuMobileUIHelper.IsMobile;
                case 11: return LauncherModules.CanLoad("MonsterTargetRuntime") || LauncherModules.CanLoad("MonsterCooldownRuntime");
                case 12: return false;
                case 13: return LauncherModules.CanLoad("DefensePointHint") || LauncherModules.CanLoad("BattleCardPointHint");
                case 14: return false;
                case 15: return LauncherModules.CanLoad("MegathSkillDamageHint");
                case 16: return LauncherModules.CanLoad("PlayerCombatStatusHud");
                case 17: return LauncherModules.CanLoad("PlayerStarBuffHud");
                case 18: return LauncherModules.CanLoad("XingShuAutoThanks") || LauncherModules.CanLoad("XingShuAutoPraise") || LauncherModules.CanLoad("DailyStarCoinShop") || LauncherModules.CanLoad("SettlementScreenshot");
                case 19:
                case 20:
                case 21: return false;
                case 22: return LauncherModules.CanLoad("SelectRoleMapNameHint");
                default: return true;
            }
        }

        private static int FirstAvailableTab()
        {
            for (int k = 0; k < DISPLAY_TAB_ORDER.Length; k++)
            {
                if (TabAvailable(DISPLAY_TAB_ORDER[k])) return DISPLAY_TAB_ORDER[k];
            }
            return 8;
        }

        private bool IsTabAvailableCurrentStyle(int index)
        {
            if (index == 23) return _activeBuiltStyle == 1;
            if (_externalPagesByTabId.TryGetValue(index, out IXingShuExternalPage extPage))
                return _activeBuiltStyle == 1 && extPage != null && extPage.IsAvailable();
            return TabAvailable(index);
        }

        private string ResolveTabTitle(int index)
        {
            if (index == 23) return "扩展框架";
            if (_externalPagesByTabId.TryGetValue(index, out IXingShuExternalPage extPage) && extPage != null)
                return extPage.Title;
            if (index >= 0 && index < TAB_NAMES.Length) return TAB_NAMES[index];
            return "星枢";
        }

        private void SelectTab(int index)
        {
            if (!IsTabAvailableCurrentStyle(index))
            {
                index = FirstAvailableTab();
            }
            if (index != 9) XingShuHotkeys.CancelCapture();
            _currentTabIndex = index;
            if (_pageTitle != null) _pageTitle.text = ResolveTabTitle(index);
            bool glass = _activeBuiltStyle == 1;
            Sprite normalTabSprite = glass ? XingShuGlassSkin.TabNormalSprite : (XingShuNativeGameUI.GetIfReady(XingShuNativeSprite.TabPink) ?? XingShuUIAssetService.GetSprite("sidebar_tab_normal.png"));
            Sprite selectedTabSprite = glass ? XingShuGlassSkin.TabSelectedSprite : (XingShuNativeGameUI.GetIfReady(XingShuNativeSprite.TabGold) ?? XingShuUIAssetService.GetSprite("sidebar_tab_selected.png"));
            Sprite hoverTabSprite = glass ? XingShuGlassSkin.TabHoverSprite : (XingShuNativeGameUI.GetIfReady(XingShuNativeSprite.TabSelected) ?? XingShuUIAssetService.GetSprite("sidebar_tab_hover.png"));
            Sprite pillNormal = XingShuUIAssetService.GetSprite("button_secondary.png");
            Sprite pillSelected = XingShuUIAssetService.GetSprite("button_selected.png");

            for (int i = 0; i < _tabEntries.Count; i++)
            {
                TabNavEntry entry = _tabEntries[i];
                bool isSelected = (entry.TabId == index);
                if (entry.IsPill)
                {
                    if (entry.Bg != null)
                    {
                        entry.Bg.sprite = isSelected ? pillSelected : pillNormal;
                        entry.Bg.color = XingShuUIAssetService.IsFallbackSprite(entry.Bg.sprite)
                            ? (isSelected ? XingShuUITheme.Yellow : XingShuUITheme.ButtonSecondaryBase)
                            : (isSelected ? Color.white : new Color(0.95f, 0.94f, 0.98f, 0.92f));
                    }
                    if (entry.Label != null)
                    {
                        entry.Label.color = XingShuUITheme.PrimaryText;
                        entry.Label.fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal;
                    }
                    continue;
                }

                if (entry.Bg != null)
                {
                    entry.Bg.sprite = isSelected ? selectedTabSprite : normalTabSprite;
                    entry.Bg.color = glass
                        ? Color.white
                        : (XingShuUIAssetService.IsFallbackSprite(entry.Bg.sprite) ? (isSelected ? XingShuUITheme.Yellow : XingShuUITheme.Pink) : Color.white);
                }
                if (entry.Button != null)
                {
                    SpriteState tabSs = new SpriteState();
                    tabSs.highlightedSprite = isSelected ? selectedTabSprite : hoverTabSprite;
                    tabSs.pressedSprite = isSelected ? selectedTabSprite : hoverTabSprite;
                    entry.Button.spriteState = tabSs;
                }
                if (entry.Label != null)
                {
                    if (glass)
                    {
                        entry.Label.color = isSelected ? XingShuGlassSkin.TextPrimary : XingShuGlassSkin.TextSecondary;
                        entry.Label.fontSize = XingShuMobileUIHelper.IsMobile ? 20 : 16;
                        entry.Label.font = XingShuUITheme.GetFontForText(entry.Label.text);
                        entry.Label.fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal;
                    }
                    else
                    {
                        entry.Label.color = Color.white;
                        entry.Label.fontSize = XingShuMobileUIHelper.IsMobile ? 20 : 18;
                        entry.Label.fontStyle = FontStyle.Bold;
                    }
                }
            }

            foreach (var kv in _tabViews)
            {
                bool active = kv.Key == index;
                if (kv.Value.Root != null && kv.Value.Root.activeSelf != active)
                {
                    kv.Value.Root.SetActive(active);
                }
            }
            if (_tabViews.TryGetValue(index, out var currentView))
            {
                if (currentView.Sync != null) currentView.Sync();
                if (glass && currentView.Root != null) XingShuGlassSkin.RestylePageForGlass(currentView.Root);
            }

            if (_comingSoonView != null)
            {
                bool isComingSoon = !_tabViews.ContainsKey(index);
                _comingSoonView.SetActive(isComingSoon);
                if (isComingSoon && _comingSoonText != null)
                {
                    _comingSoonText.text = $"「{ResolveTabTitle(index)}」正在进行适配，将在后续阶段上线\n(当前底层功能正常，可通过快捷键独立运行)";
                }
            }
        }

        private void BuildGlassCenterCanvas()
        {
            GameObject canvasGo = new GameObject("XINGSHU_UICenter_Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 31000;
            _canvas.pixelPerfect = false;

            float ss = ComputeCanvasSupersample(Screen.height);
            CanvasScaler scaler = _canvasScaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f / ss, 1080f / ss);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1.0f;

            canvasGo.AddComponent<GraphicRaycaster>();
            _canvasGroup = canvasGo.AddComponent<CanvasGroup>();

            GameObject logicalRoot = XingShuUIFactory.CreateUIObject("LogicalRoot_1080p", canvasGo);
            _logicalRootRt = Place(logicalRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f));

            GameObject backdropGo = XingShuUIFactory.CreateUIObject("Backdrop", logicalRoot);
            RectTransform backdropRt = backdropGo.GetComponent<RectTransform>();
            backdropRt.anchorMin = Vector2.zero; backdropRt.anchorMax = Vector2.one;
            backdropRt.offsetMin = Vector2.zero; backdropRt.offsetMax = Vector2.zero;
            XingShuGlassSkin.RefreshPaletteFromConfig();
            Image backdropImg = _glassBackdropImage = backdropGo.AddComponent<Image>();
            backdropImg.sprite = XingShuUIAssetService.WhiteSprite;
            backdropImg.color = XingShuGlassSkin.BackdropDim;
            Button backdropBtn = backdropGo.AddComponent<Button>();
            backdropBtn.transition = Selectable.Transition.None;
            backdropBtn.onClick.AddListener(Close);

            GameObject mainFrame = XingShuUIFactory.CreateUIObject("MainFrame_Glass", logicalRoot);
            _mainFrameRt = mainFrame.GetComponent<RectTransform>();
            _mainFrameRt.anchorMin = new Vector2(0.5f, 0.5f);
            _mainFrameRt.anchorMax = new Vector2(0.5f, 0.5f);
            _mainFrameRt.pivot = new Vector2(0.5f, 0.5f);
            _mainFrameRt.anchoredPosition = Vector2.zero;
            _mainFrameRt.sizeDelta = new Vector2(1480f, 920f);

            float userScale = XingShuConfigService.GetFloat(XingShuConfigKeys.MobileUserScale, XingShuConfigKeys.MobileUserScaleDefault);
            if (!XingShuMobileUIHelper.IsMobile)
            {
                userScale = XingShuMobileUIHelper.FiniteClamp(userScale, XingShuConfigKeys.FunctionCenterDesktopScaleMin, XingShuConfigKeys.FunctionCenterDesktopScaleMax, XingShuConfigKeys.FunctionCenterDesktopScaleDefault);
            }
            else
            {
                userScale = Mathf.Clamp(userScale, XingShuConfigKeys.MobileUserScaleMin, XingShuConfigKeys.MobileUserScaleMax);
            }
            _preferredScale = userScale;
            FitPanelToScreen();

            Image shellImg = _glassShellImage = mainFrame.AddComponent<Image>();
            shellImg.sprite = XingShuGlassSkin.ShellSprite;
            shellImg.type = Image.Type.Sliced;
            shellImg.color = Color.white;
            XingShuUIFactory.AddShadow(mainFrame, new Color(0f, 0f, 0f, 0.42f), new Vector2(0f, -12f));
            mainFrame.AddComponent<RectMask2D>();

            // Left translucent sidebar column inside unified shell
            GameObject leftCol = XingShuUIFactory.CreateUIObject("GlassSidebarColumn", mainFrame);
            Place(leftCol, Vector2.zero, Vector2.up, Vector2.zero, new Vector2(12f, 12f), new Vector2(256f, -24f));
            Image railImg = _glassRailImage = leftCol.AddComponent<Image>();
            railImg.sprite = XingShuGlassSkin.RailSprite;
            railImg.type = Image.Type.Sliced;
            railImg.color = Color.white;

            Text brandTitle = XingShuUIFactory.CreateText(leftCol, "GlassBrand", "星枢 · 毛玻璃框架", 17, XingShuGlassSkin.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(brandTitle.gameObject, Vector2.up, Vector2.one, Vector2.up, new Vector2(16f, -14f), new Vector2(-32f, 26f));
            Text brandVer = _glassVerText = XingShuUIFactory.CreateText(leftCol, "GlassVer", XingShu.Runtime.XingShuRuntimeHost.RuntimeVersion + " · v2", 11, XingShuGlassSkin.AccentBlue, TextAnchor.MiddleLeft);
            Place(brandVer.gameObject, Vector2.up, Vector2.one, Vector2.up, new Vector2(16f, -40f), new Vector2(-32f, 18f));

            GameObject sideScrollWrap = XingShuUIFactory.CreateUIObject("GlassSidebarScrollWrap", leftCol);
            Place(sideScrollWrap, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(-12f, -120f));
            ScrollRect sideScroll = XingShuUIFactory.CreateVerticalScrollView(sideScrollWrap, "SidebarScroll", out GameObject sidebar);
            sideScroll.scrollSensitivity = 36f;
            Transform sbBar = sideScroll.transform.Find("VerticalScrollbar");
            if (sbBar != null) sbBar.gameObject.SetActive(false);
            VerticalLayoutGroup layout = sidebar.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 8);
            layout.spacing = 6f; layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;

            _tabEntries.Clear();
            _externalPagesByTabId.Clear();

            for (int k = 0; k < DISPLAY_TAB_ORDER.Length; k++)
            {
                int tabId = DISPLAY_TAB_ORDER[k];
                if (!TabAvailable(tabId)) continue;
                if (tabId == 8) AddGlassSidebarDivider(sidebar, "系统设置");
                AddGlassSidebarTab(sidebar, tabId, TAB_NAMES[tabId], tabId == _currentTabIndex);
            }

            // Extension Framework + External Plugins section
            AddGlassSidebarDivider(sidebar, "扩展框架");
            AddGlassSidebarTab(sidebar, 23, "扩展总览", _currentTabIndex == 23);

            IXingShuExternalPage[] extPages = XingShuCenterPluginHost.GetPagesSnapshot();
            for (int i = 0; i < extPages.Length; i++)
            {
                IXingShuExternalPage ext = extPages[i];
                if (ext == null || !ext.IsAvailable()) continue;
                int extId = 1000 + i;
                _externalPagesByTabId[extId] = ext;
                AddGlassSidebarTab(sidebar, extId, ext.Title, _currentTabIndex == extId);
            }

            Button bottomBackBtn = XingShuUIFactory.CreateButton(leftCol, "ReturnOriginalQuick", "原版 UI 皮肤",
                XingShuGlassSkin.ButtonSprite, Color.white, XingShuGlassSkin.TextSecondary, () => SetCenterStyle(0), 12);
            _glassBottomBackImg = bottomBackBtn.GetComponent<Image>();
            Place(bottomBackBtn.gameObject, Vector2.zero, Vector2.right, Vector2.zero, new Vector2(12f, 12f), new Vector2(-24f, 36f));

            // Right Main Panel Area
            GameObject rightArea = XingShuUIFactory.CreateUIObject("GlassRightArea", mainFrame);
            RectTransform rightRt = rightArea.GetComponent<RectTransform>();
            rightRt.anchorMin = Vector2.zero; rightRt.anchorMax = Vector2.one;
            rightRt.offsetMin = new Vector2(280f, 12f); rightRt.offsetMax = new Vector2(-14f, -12f);

            GameObject titleBar = XingShuUIFactory.CreateUIObject("GlassHeader", rightArea);
            Place(titleBar, Vector2.up, Vector2.one, Vector2.up, new Vector2(0f, 0f), new Vector2(0f, 58f));
            Image headerBg = _glassHeaderImage = titleBar.AddComponent<Image>();
            headerBg.sprite = XingShuGlassSkin.GetRoundedPanelSprite(12, XingShuGlassSkin.HeaderFill, XingShuGlassSkin.DividerLine, 1f);
            headerBg.type = Image.Type.Sliced;
            headerBg.color = Color.white;
            headerBg.raycastTarget = true;

            _dragHandle = titleBar.AddComponent<XingShuUIDragHandle>();
            _dragHandle.targetRect = _mainFrameRt;
            _dragHandle.parentRect = _logicalRootRt ?? (_canvas.transform as RectTransform);

            Text title = _pageTitle = XingShuUIFactory.CreateText(titleBar, "GlassPageTitle", ResolveTabTitle(_currentTabIndex), 20, XingShuGlassSkin.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(title.gameObject, Vector2.zero, Vector2.up, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(360f, 36f));
            title.raycastTarget = false;

            Text badgeText = _glassBadgeText = XingShuUIFactory.CreateText(titleBar, "GlassBadge", "通透毛玻璃", 11, XingShuGlassSkin.AccentMint, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(badgeText.gameObject, Vector2.zero, Vector2.up, new Vector2(0f, 0.5f), new Vector2(200f, 0f), new Vector2(96f, 24f));
            badgeText.raycastTarget = false;

            Text hotkey = _hotkeyHint = XingShuUIFactory.CreateText(titleBar, "Hotkey", XingShuMobileUIHelper.IsMobile ? "拖动顶部移动" : XingShuHotkeys.Display(XingShuShortcut.Center) + " 打开 / 收起", 12, XingShuGlassSkin.TextMuted, TextAnchor.MiddleRight);
            Place(hotkey.gameObject, Vector2.right, Vector2.one, new Vector2(1f, 0.5f), new Vector2(-268f, 0f), new Vector2(180f, 28f));
            hotkey.raycastTarget = false;

            Button origBtn = XingShuUIFactory.CreateButton(titleBar, "SwitchOrigBtn", "原版 UI 皮肤",
                XingShuGlassSkin.ButtonSprite, Color.white, XingShuGlassSkin.TextPrimary, () => SetCenterStyle(0), 12);
            _glassOrigBtnImg = origBtn.GetComponent<Image>();
            RectTransform origRt = Place(origBtn.gameObject, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-132f, 0f), new Vector2(108f, 32f));

            Button sizeBtn = XingShuUIFactory.CreateButton(titleBar, "SizeCycleBtn", PanelScale >= 0.985f ? "标准" : $"{Mathf.RoundToInt(PanelScale * 100f)}%",
                XingShuGlassSkin.ButtonSprite, Color.white, XingShuGlassSkin.TextPrimary, CyclePanelSize, 12);
            _glassSizeBtnImg = sizeBtn.GetComponent<Image>();
            _sizeCycleLabel = sizeBtn.GetComponentInChildren<Text>();
            RectTransform sizeRt = Place(sizeBtn.gameObject, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-50f, 0f), new Vector2(72f, 32f));

            Button closeBtn = XingShuUIFactory.CreateButton(titleBar, "Close", "×",
                XingShuGlassSkin.ButtonSprite, Color.white, XingShuGlassSkin.TextPrimary, Close, 20);
            _glassCloseBtnImg = closeBtn.GetComponent<Image>();
            RectTransform closeRt = Place(closeBtn.gameObject, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(34f, 32f));

            _dragHandle.SetExcludedRects(closeRt, sizeRt, origRt);
            _dragHandle.BindEvents();

            // Content Viewport inside right area
            GameObject glassBodyHost = XingShuUIFactory.CreateUIObject("GlassBodyHost", rightArea);
            RectTransform gbhRt = glassBodyHost.GetComponent<RectTransform>();
            gbhRt.anchorMin = Vector2.zero; gbhRt.anchorMax = Vector2.one;
            gbhRt.offsetMin = new Vector2(-16f, -68f);
            gbhRt.offsetMax = new Vector2(16f, 12f);

            BuildContentArea(glassBodyHost);
            foreach (Transform page in _contentArea.transform)
            {
                if (XingShuMobileUIHelper.IsMobile && page.gameObject != _comingSoonView)
                {
                    XingShuMobilePageLayout.Adapt(page.gameObject);
                }
                XingShuGlassSkin.RestylePageForGlass(page.gameObject);
            }
            XingShuUITheme.RefreshFonts(canvasGo, glassMode: true);
        }

        private void AddGlassSidebarDivider(GameObject sidebar, string label)
        {
            GameObject sep = XingShuUIFactory.CreateUIObject("GlassSidebarGroup_" + label, sidebar);
            LayoutElement le = sep.AddComponent<LayoutElement>();
            le.minHeight = 24f; le.preferredHeight = 24f; le.flexibleHeight = 0f;
            Text txt = XingShuUIFactory.CreateText(sep, "Label", label, 11, XingShuGlassSkin.TextMuted, TextAnchor.LowerLeft, FontStyle.Bold);
            txt.rectTransform.anchorMin = Vector2.zero; txt.rectTransform.anchorMax = Vector2.one;
            txt.rectTransform.offsetMin = new Vector2(10f, 2f); txt.rectTransform.offsetMax = new Vector2(-8f, 0f);
            txt.raycastTarget = false;
        }

        private void AddGlassSidebarTab(GameObject sidebar, int tabId, string label, bool selected)
        {
            GameObject tab = XingShuUIFactory.CreateUIObject("Tab_" + tabId, sidebar);
            LayoutElement le = tab.AddComponent<LayoutElement>();
            le.minHeight = 40f; le.preferredHeight = 40f; le.flexibleHeight = 0f;

            Image image = tab.AddComponent<Image>();
            image.sprite = selected ? XingShuGlassSkin.TabSelectedSprite : XingShuGlassSkin.TabNormalSprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            Image dot = XingShuUIFactory.CreateImage(tab, "Dot", XingShuGlassSkin.GetRoundedPanelSprite(6, selected ? XingShuGlassSkin.AccentMint : XingShuGlassSkin.AccentBlue, Color.clear, 1f), Color.white, Image.Type.Sliced);
            dot.raycastTarget = false;
            Place(dot.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(7f, 7f));

            Text text = XingShuUIFactory.CreateText(tab, "Text", label, 16, selected ? XingShuGlassSkin.TextPrimary : XingShuGlassSkin.TextSecondary, TextAnchor.MiddleLeft, selected ? FontStyle.Bold : FontStyle.Normal);
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(28f, 0f); text.rectTransform.offsetMax = new Vector2(-10f, 0f);
            text.raycastTarget = false;

            Button button = tab.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { highlightedSprite = XingShuGlassSkin.TabHoverSprite, pressedSprite = XingShuGlassSkin.TabSelectedSprite };
            button.onClick.AddListener(delegate { SelectTab(tabId); });

            _tabEntries.Add(new TabNavEntry
            {
                TabId = tabId,
                Button = button,
                Bg = image,
                Label = text,
                IsPill = false,
            });
        }
    }
}

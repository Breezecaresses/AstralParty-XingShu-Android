using System;
using System.Collections.Generic;
using Core;
using Core.Scene;
using Core.Unit;
using FairyGUI;
using GameLogic;
using Tools;
using UI;
using UnityEngine;
using XingShuRuntime.CameraPort;
using XingShuRuntime.Core;
using XingShuRuntime.UI;

namespace XingShuRuntime.Utilities;

/// <summary>
/// Standalone HUD module displaying each hero's active skill CD, HP, and base ATK/DEF:
/// 1) Locked non-draggable pill anchored over each 3D board character inside UIBattleInfoPanel.com_AttrInfos
///    (exact same FairyGUI layer / sortingOrder 0 as monster target deduction).
/// 2) Adjustable compact status strip anchored directly beneath each left-side player avatar's coin/star bar,
///    draggable and slider-adjustable without overlapping native popups.
/// </summary>
public sealed class PlayerCombatStatusHud : MonoBehaviour
{
    private const float TextRefreshIntervalSeconds = 0.12f;

    private sealed class PillSlot
    {
        internal long PlayerId;
        internal GComponent Panel;
        internal GGraph Background;
        internal GGraph AccentBar;
        internal GTextField Label;
        internal float Width;
        internal float Height;
        internal float AnchorX;
        internal float AnchorY;
        internal string LastText = "";
        internal Color LastAccentColor;
        internal bool ActiveInFrame;
    }

    private static PlayerCombatStatusHud _instance;

    private readonly Dictionary<long, PillSlot> _headPills = new Dictionary<long, PillSlot>(4);
    private readonly Dictionary<long, PillSlot> _avatarPills = new Dictionary<long, PillSlot>(4);
    private readonly HashSet<long> _playersWithServerAddMove = new HashSet<long>();
    private readonly Dictionary<long, int> _lastRawExtraMove = new Dictionary<long, int>(4);
    private readonly Dictionary<long, int> _baselinePermanentAdd = new Dictionary<long, int>(4);
    private Tools.Signal _boundBuffSignal;

    private bool _enabled = XingShuConfigKeys.PlayerCombatHudEnabledDefault;
    private bool _showHead = XingShuConfigKeys.PlayerCombatHudShowHeadDefault;
    private bool _showAvatar = XingShuConfigKeys.PlayerCombatHudShowAvatarDefault;
    private float _offsetX = XingShuConfigKeys.PlayerCombatHudOffsetXDefault;
    private float _offsetY = XingShuConfigKeys.PlayerCombatHudOffsetYDefault;
    private float _headOffsetY = XingShuConfigKeys.PlayerCombatHudHeadOffsetYDefault;
    private float _scale = XingShuConfigKeys.PlayerCombatHudScaleDefault;
    private bool _locked = XingShuConfigKeys.PlayerCombatHudLockedDefault;
    private bool _showCd = XingShuConfigKeys.PlayerCombatHudShowCdDefault;
    private bool _showHp = XingShuConfigKeys.PlayerCombatHudShowHpDefault;
    private bool _showAtkDef = XingShuConfigKeys.PlayerCombatHudShowAtkDefDefault;
    private bool _showMove = XingShuConfigKeys.PlayerCombatHudShowMoveDefault;

    private GComponent _attachedHeadLayer;
    private GComponent _attachedAvatarParent;
    private float _nextTextRefresh;
    private long _draggingAvatarPlayerId;

    internal static bool IsEnabled => _instance != null
        ? _instance._enabled
        : XingShuConfigService.GetBool(XingShuConfigKeys.PlayerCombatHudEnabled, XingShuConfigKeys.PlayerCombatHudEnabledDefault);

    internal static bool ShowHead => _instance != null
        ? _instance._showHead
        : XingShuConfigService.GetBool(XingShuConfigKeys.PlayerCombatHudShowHead, XingShuConfigKeys.PlayerCombatHudShowHeadDefault);

    internal static bool ShowAvatar => _instance != null
        ? _instance._showAvatar
        : XingShuConfigService.GetBool(XingShuConfigKeys.PlayerCombatHudShowAvatar, XingShuConfigKeys.PlayerCombatHudShowAvatarDefault);

    internal static float OffsetX => _instance != null
        ? _instance._offsetX
        : FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.PlayerCombatHudOffsetX, XingShuConfigKeys.PlayerCombatHudOffsetXDefault),
            XingShuConfigKeys.PlayerCombatHudOffsetXMin, XingShuConfigKeys.PlayerCombatHudOffsetXMax, XingShuConfigKeys.PlayerCombatHudOffsetXDefault);

    internal static float OffsetY => _instance != null
        ? _instance._offsetY
        : FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.PlayerCombatHudOffsetY, XingShuConfigKeys.PlayerCombatHudOffsetYDefault),
            XingShuConfigKeys.PlayerCombatHudOffsetYMin, XingShuConfigKeys.PlayerCombatHudOffsetYMax, XingShuConfigKeys.PlayerCombatHudOffsetYDefault);

    internal static float HeadOffsetY => _instance != null
        ? _instance._headOffsetY
        : FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.PlayerCombatHudHeadOffsetY, XingShuConfigKeys.PlayerCombatHudHeadOffsetYDefault),
            XingShuConfigKeys.PlayerCombatHudHeadOffsetYMin, XingShuConfigKeys.PlayerCombatHudHeadOffsetYMax, XingShuConfigKeys.PlayerCombatHudHeadOffsetYDefault);

    internal static float Scale => _instance != null
        ? _instance._scale
        : FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.PlayerCombatHudScale, XingShuConfigKeys.PlayerCombatHudScaleDefault),
            XingShuConfigKeys.PlayerCombatHudScaleMin, XingShuConfigKeys.PlayerCombatHudScaleMax, XingShuConfigKeys.PlayerCombatHudScaleDefault);

    internal static bool Locked => _instance != null
        ? _instance._locked
        : XingShuConfigService.GetBool(XingShuConfigKeys.PlayerCombatHudLocked, XingShuConfigKeys.PlayerCombatHudLockedDefault);

    internal static bool ShowCd => _instance != null
        ? _instance._showCd
        : XingShuConfigService.GetBool(XingShuConfigKeys.PlayerCombatHudShowCd, XingShuConfigKeys.PlayerCombatHudShowCdDefault);

    internal static bool ShowHp => _instance != null
        ? _instance._showHp
        : XingShuConfigService.GetBool(XingShuConfigKeys.PlayerCombatHudShowHp, XingShuConfigKeys.PlayerCombatHudShowHpDefault);

    internal static bool ShowAtkDef => _instance != null
        ? _instance._showAtkDef
        : XingShuConfigService.GetBool(XingShuConfigKeys.PlayerCombatHudShowAtkDef, XingShuConfigKeys.PlayerCombatHudShowAtkDefDefault);

    internal static bool ShowMove => _instance != null
        ? _instance._showMove
        : XingShuConfigService.GetBool(XingShuConfigKeys.PlayerCombatHudShowMove, XingShuConfigKeys.PlayerCombatHudShowMoveDefault);

    internal static void SetEnabled(bool value)
    {
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerCombatHudEnabled, value);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._enabled = value;
            _instance._nextTextRefresh = 0f;
            if (!value) _instance.HideAllPills();
        }
    }

    internal static void SetShowHead(bool value)
    {
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerCombatHudShowHead, value);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._showHead = value;
            _instance._nextTextRefresh = 0f;
            if (!value) HidePool(_instance._headPills);
        }
    }

    internal static void SetShowAvatar(bool value)
    {
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerCombatHudShowAvatar, value);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._showAvatar = value;
            _instance._nextTextRefresh = 0f;
            if (!value)
            {
                _instance._draggingAvatarPlayerId = 0L;
                HidePool(_instance._avatarPills);
            }
        }
    }

    internal static void SetOffset(float offX, float offY)
    {
        float x = FloatingHintLayoutMath.FiniteClamp(
            offX, XingShuConfigKeys.PlayerCombatHudOffsetXMin, XingShuConfigKeys.PlayerCombatHudOffsetXMax, XingShuConfigKeys.PlayerCombatHudOffsetXDefault);
        float y = FloatingHintLayoutMath.FiniteClamp(
            offY, XingShuConfigKeys.PlayerCombatHudOffsetYMin, XingShuConfigKeys.PlayerCombatHudOffsetYMax, XingShuConfigKeys.PlayerCombatHudOffsetYDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.PlayerCombatHudOffsetX, x);
        XingShuConfigService.SetFloat(XingShuConfigKeys.PlayerCombatHudOffsetY, y);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._offsetX = x;
            _instance._offsetY = y;
        }
    }

    internal static void SetHeadOffsetY(float offY)
    {
        float y = FloatingHintLayoutMath.FiniteClamp(
            offY, XingShuConfigKeys.PlayerCombatHudHeadOffsetYMin, XingShuConfigKeys.PlayerCombatHudHeadOffsetYMax, XingShuConfigKeys.PlayerCombatHudHeadOffsetYDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.PlayerCombatHudHeadOffsetY, y);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._headOffsetY = y;
        }
    }

    internal static void SetScale(float scale)
    {
        float clamped = FloatingHintLayoutMath.FiniteClamp(
            scale, XingShuConfigKeys.PlayerCombatHudScaleMin, XingShuConfigKeys.PlayerCombatHudScaleMax, XingShuConfigKeys.PlayerCombatHudScaleDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.PlayerCombatHudScale, clamped);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._scale = clamped;
        }
    }

    internal static void SetLocked(bool locked)
    {
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerCombatHudLocked, locked);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._locked = locked;
            _instance._draggingAvatarPlayerId = 0L;
            _instance.RefreshAvatarInteractionStates(modalOpen: false);
        }
    }

    internal static void SetShowCd(bool value)
    {
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerCombatHudShowCd, value);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._showCd = value;
            _instance._nextTextRefresh = 0f;
            InvalidateText(_instance._headPills);
            InvalidateText(_instance._avatarPills);
        }
    }

    internal static void SetShowHp(bool value)
    {
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerCombatHudShowHp, value);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._showHp = value;
            _instance._nextTextRefresh = 0f;
            InvalidateText(_instance._headPills);
            InvalidateText(_instance._avatarPills);
        }
    }

    internal static void SetShowAtkDef(bool value)
    {
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerCombatHudShowAtkDef, value);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._showAtkDef = value;
            _instance._nextTextRefresh = 0f;
            InvalidateText(_instance._headPills);
            InvalidateText(_instance._avatarPills);
        }
    }

    internal static void SetShowMove(bool value)
    {
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerCombatHudShowMove, value);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._showMove = value;
            _instance._nextTextRefresh = 0f;
            InvalidateText(_instance._headPills);
            InvalidateText(_instance._avatarPills);
        }
    }

    internal static void ResetLayout()
    {
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerCombatHudEnabled, XingShuConfigKeys.PlayerCombatHudEnabledDefault);
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerCombatHudShowHead, XingShuConfigKeys.PlayerCombatHudShowHeadDefault);
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerCombatHudShowAvatar, XingShuConfigKeys.PlayerCombatHudShowAvatarDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.PlayerCombatHudOffsetX, XingShuConfigKeys.PlayerCombatHudOffsetXDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.PlayerCombatHudOffsetY, XingShuConfigKeys.PlayerCombatHudOffsetYDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.PlayerCombatHudHeadOffsetY, XingShuConfigKeys.PlayerCombatHudHeadOffsetYDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.PlayerCombatHudScale, XingShuConfigKeys.PlayerCombatHudScaleDefault);
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerCombatHudLocked, XingShuConfigKeys.PlayerCombatHudLockedDefault);
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerCombatHudShowCd, XingShuConfigKeys.PlayerCombatHudShowCdDefault);
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerCombatHudShowHp, XingShuConfigKeys.PlayerCombatHudShowHpDefault);
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerCombatHudShowAtkDef, XingShuConfigKeys.PlayerCombatHudShowAtkDefDefault);
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerCombatHudShowMove, XingShuConfigKeys.PlayerCombatHudShowMoveDefault);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._enabled = XingShuConfigKeys.PlayerCombatHudEnabledDefault;
            _instance._showHead = XingShuConfigKeys.PlayerCombatHudShowHeadDefault;
            _instance._showAvatar = XingShuConfigKeys.PlayerCombatHudShowAvatarDefault;
            _instance._offsetX = XingShuConfigKeys.PlayerCombatHudOffsetXDefault;
            _instance._offsetY = XingShuConfigKeys.PlayerCombatHudOffsetYDefault;
            _instance._headOffsetY = XingShuConfigKeys.PlayerCombatHudHeadOffsetYDefault;
            _instance._scale = XingShuConfigKeys.PlayerCombatHudScaleDefault;
            _instance._locked = XingShuConfigKeys.PlayerCombatHudLockedDefault;
            _instance._showCd = XingShuConfigKeys.PlayerCombatHudShowCdDefault;
            _instance._showHp = XingShuConfigKeys.PlayerCombatHudShowHpDefault;
            _instance._showAtkDef = XingShuConfigKeys.PlayerCombatHudShowAtkDefDefault;
            _instance._showMove = XingShuConfigKeys.PlayerCombatHudShowMoveDefault;
            _instance._draggingAvatarPlayerId = 0L;
            _instance._nextTextRefresh = 0f;
            InvalidateText(_instance._headPills);
            InvalidateText(_instance._avatarPills);
            _instance.RefreshAvatarInteractionStates(modalOpen: false);
        }
    }

    public static void Install(GameObject host)
    {
        if (_instance != null || host == null) return;
        _instance = host.AddComponent<PlayerCombatStatusHud>();
        _instance.LoadConfig();
    }

    private void LoadConfig()
    {
        _enabled = XingShuConfigService.GetBool(XingShuConfigKeys.PlayerCombatHudEnabled, XingShuConfigKeys.PlayerCombatHudEnabledDefault);
        _showHead = XingShuConfigService.GetBool(XingShuConfigKeys.PlayerCombatHudShowHead, XingShuConfigKeys.PlayerCombatHudShowHeadDefault);
        _showAvatar = XingShuConfigService.GetBool(XingShuConfigKeys.PlayerCombatHudShowAvatar, XingShuConfigKeys.PlayerCombatHudShowAvatarDefault);
        _offsetX = FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.PlayerCombatHudOffsetX, XingShuConfigKeys.PlayerCombatHudOffsetXDefault),
            XingShuConfigKeys.PlayerCombatHudOffsetXMin, XingShuConfigKeys.PlayerCombatHudOffsetXMax, XingShuConfigKeys.PlayerCombatHudOffsetXDefault);
        _offsetY = FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.PlayerCombatHudOffsetY, XingShuConfigKeys.PlayerCombatHudOffsetYDefault),
            XingShuConfigKeys.PlayerCombatHudOffsetYMin, XingShuConfigKeys.PlayerCombatHudOffsetYMax, XingShuConfigKeys.PlayerCombatHudOffsetYDefault);
        _headOffsetY = FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.PlayerCombatHudHeadOffsetY, XingShuConfigKeys.PlayerCombatHudHeadOffsetYDefault),
            XingShuConfigKeys.PlayerCombatHudHeadOffsetYMin, XingShuConfigKeys.PlayerCombatHudHeadOffsetYMax, XingShuConfigKeys.PlayerCombatHudHeadOffsetYDefault);
        _scale = FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.PlayerCombatHudScale, XingShuConfigKeys.PlayerCombatHudScaleDefault),
            XingShuConfigKeys.PlayerCombatHudScaleMin, XingShuConfigKeys.PlayerCombatHudScaleMax, XingShuConfigKeys.PlayerCombatHudScaleDefault);
        _locked = XingShuConfigService.GetBool(XingShuConfigKeys.PlayerCombatHudLocked, XingShuConfigKeys.PlayerCombatHudLockedDefault);
        _showCd = XingShuConfigService.GetBool(XingShuConfigKeys.PlayerCombatHudShowCd, XingShuConfigKeys.PlayerCombatHudShowCdDefault);
        _showHp = XingShuConfigService.GetBool(XingShuConfigKeys.PlayerCombatHudShowHp, XingShuConfigKeys.PlayerCombatHudShowHpDefault);
        _showAtkDef = XingShuConfigService.GetBool(XingShuConfigKeys.PlayerCombatHudShowAtkDef, XingShuConfigKeys.PlayerCombatHudShowAtkDefDefault);
        _showMove = XingShuConfigService.GetBool(XingShuConfigKeys.PlayerCombatHudShowMove, XingShuConfigKeys.PlayerCombatHudShowMoveDefault);
    }

    private void LateUpdate()
    {
        try
        {
            Tick();
        }
        catch (Exception)
        {
            ClearAll();
        }
    }

    private void Tick()
    {
        if (!_enabled || (!_showHead && !_showAvatar) || (!_showCd && !_showHp && !_showAtkDef && !_showMove)
            || !SimpleSingletonProvider<SceneManager>.hasInstance
            || SimpleSingletonProvider<SceneManager>.inst.currentType == null
            || SimpleSingletonProvider<SceneManager>.inst.currentType.Value != SceneType.Battle
            || !SimpleSingletonProvider<GameLogicManager>.hasInstance
            || !SimpleSingletonProvider<UIManager>.hasInstance)
        {
            ClearAll();
            return;
        }

        var uiMgr = SimpleSingletonProvider<UIManager>.inst;
        var logic = SimpleSingletonProvider<GameLogicManager>.inst;
        var fight = logic.fight?.battleFightData;
        if ((fight != null && !fight.isEnd) || ShownNativeWindows.Find<FightWindow>(uiMgr) != null)
        {
            HideAllPills();
            return;
        }

        var battleInfoUi = logic.battle?.battleInfo?.UIObject;
        var players = logic.battle?.PlayerDatas;
        if (battleInfoUi == null || battleInfoUi.isDisposed || !battleInfoUi.visible || !battleInfoUi.onStage
            || battleInfoUi.width < 320f || battleInfoUi.height < 240f
            || players == null)
        {
            HideAllPills();
            return;
        }

        bool refreshText = Time.unscaledTime >= _nextTextRefresh;
        EnsureBuffSignalBound(logic);
        if (refreshText)
        {
            _nextTextRefresh = Time.unscaledTime + TextRefreshIntervalSeconds;
        }

        TickHeadLayer(battleInfoUi, players, refreshText);
        TickAvatarLayer(battleInfoUi, refreshText);
    }

    private void TickHeadLayer(UIBattleInfoPanel battleInfoUi, List<BattlePlayerData> players, bool refreshText)
    {
        GComponent headLayer = battleInfoUi.com_AttrInfos ?? battleInfoUi.com_PlayerAttrInfos;
        Camera cam = BattleSceneController.inst != null ? BattleSceneController.inst.mainCamera : null;
        if (cam == null) cam = Camera.main;

        if (!_showHead || headLayer == null || headLayer.isDisposed || !headLayer.visible
            || cam == null || cam.targetTexture != null)
        {
            HidePool(_headPills);
            return;
        }

        if (_attachedHeadLayer != headLayer)
        {
            DisposePool(_headPills);
            _attachedHeadLayer = headLayer;
        }

        BeginFrame(_headPills);
        float headScale = Mathf.Clamp(_scale * 0.9f, 0.55f, 1.55f);
        float viewW = GRoot.inst != null && GRoot.inst.width > 0f ? GRoot.inst.width : battleInfoUi.width;
        float viewH = GRoot.inst != null && GRoot.inst.height > 0f ? GRoot.inst.height : battleInfoUi.height;

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null || p.player?.Hero == null || p.characterType != CharacterType.Hero
                || p.CharacterInst == null || !p.CharacterInst.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!TryGetBoardHeadAnchor(p.CharacterInst, cam, out Vector3 head) || head.z <= 0f)
            {
                continue;
            }

            Vector2 localPt = headLayer.GlobalToLocal(new Vector2(head.x, Screen.height - head.y));
            if (!PortMath.Finite(localPt.x) || !PortMath.Finite(localPt.y)
                || localPt.x < -16f || localPt.x > viewW + 16f
                || localPt.y < 36f || localPt.y > viewH + 24f)
            {
                continue;
            }

            long pid = p.player.Id;
            PillSlot slot = EnsureHeadPill(headLayer, pid);
            slot.ActiveInFrame = true;

            if (refreshText || string.IsNullOrEmpty(slot.LastText))
            {
                string ubb = BuildHeadStatusText(p);
                UpdatePillContent(slot, ubb, compact: false, accentColor: ResolveSlotAccentColor(p));
            }

            if (slot.Panel != null && !slot.Panel.isDisposed)
            {
                slot.Panel.visible = true;
                slot.Panel.SetScale(headScale, headScale);
                // Locked directly over visual character head inside com_AttrInfos (sortingOrder 0).
                float x = localPt.x - slot.Width * 0.5f * headScale;
                float y = localPt.y - slot.Height * headScale - 4f * headScale + _headOffsetY;
                slot.Panel.SetXY(x, y);
            }
        }

        HideInactive(_headPills, isAvatarPool: false);
    }

    private static bool TryGetBoardHeadAnchor(Character owner, Camera camera, out Vector3 head)
    {
        head = default(Vector3);
        if (owner == null || owner.characterAnimator == null || owner.characterAnimator.IsHide())
        {
            return false;
        }
        SpriteRenderer sr = owner.characterAnimator.GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null)
        {
            return false;
        }
        Bounds bounds = sr.bounds;
        Vector3 centerScreen = camera.WorldToScreenPoint(bounds.center);
        if (!NativeHeadAnchor.Finite(centerScreen) || centerScreen.z <= camera.nearClipPlane)
        {
            return false;
        }
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;
        for (int ix = -1; ix <= 1; ix += 2)
        {
            for (int iy = -1; iy <= 1; iy += 2)
            {
                for (int iz = -1; iz <= 1; iz += 2)
                {
                    Vector3 corner = camera.WorldToScreenPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(ix, iy, iz)));
                    if (!NativeHeadAnchor.Finite(corner) || corner.z <= camera.nearClipPlane)
                    {
                        return false;
                    }
                    if (corner.y < minY) minY = corner.y;
                    if (corner.y > maxY) maxY = corner.y;
                }
            }
        }
        // Full-frame character animations pad ~44% transparent headroom above the head.
        float visualTopY = Mathf.Lerp(minY, maxY, 0.56f);
        head = new Vector3(centerScreen.x, visualTopY, centerScreen.z);
        return NativeHeadAnchor.Finite(head);
    }

    private void TickAvatarLayer(UIBattleInfoPanel battleInfoUi, bool refreshText)
    {
        var battlePlayer = battleInfoUi.com_BattlePlayer;
        var container = battlePlayer?.com_Container;
        if (!_showAvatar || battlePlayer == null || !IsVisibleTree(battlePlayer)
            || container == null || !IsVisibleTree(container))
        {
            _draggingAvatarPlayerId = 0L;
            HidePool(_avatarPills);
            return;
        }

        GComponent host = battlePlayer.parent == battleInfoUi ? (GComponent)battleInfoUi : battlePlayer;
        GObject anchorSibling = host == battleInfoUi ? (GObject)battlePlayer : container;
        if (_attachedAvatarParent != host)
        {
            DisposePool(_avatarPills);
            _draggingAvatarPlayerId = 0L;
            _attachedAvatarParent = host;
        }

        bool modalOpen = XingShuUICenter.IsOpen || HandCardVisibility.BlockingWindow != null;
        BeginFrame(_avatarPills);

        UIBattleInfo_Button_PlayerInfo[] slots = new[]
        {
            container.com_Player_1,
            container.com_Player_2,
            container.com_Player_3,
            container.com_Player_4
        };

        for (int i = 0; i < slots.Length; i++)
        {
            var head = slots[i];
            if (head == null || head.isDisposed || !IsVisibleTree(head)) continue;
            var p = head.PlayerData;
            if (p == null || p.player?.Hero == null || p.characterType != CharacterType.Hero) continue;
            if (!TryComputeAvatarBottomAnchor(head, host, out Vector2 anchorLocal)) continue;

            long pid = p.player.Id;
            PillSlot slot = EnsureAvatarPill(host, anchorSibling, pid, modalOpen);
            slot.ActiveInFrame = true;

            bool interactive = !_locked && !modalOpen;
            if (slot.Panel.touchable != interactive)
            {
                slot.Panel.touchable = interactive;
                slot.Panel.opaque = interactive;
                slot.Panel.draggable = interactive;
            }
            if (!interactive && _draggingAvatarPlayerId == pid)
            {
                _draggingAvatarPlayerId = 0L;
            }

            if (refreshText || string.IsNullOrEmpty(slot.LastText))
            {
                string ubb = BuildAvatarStatusText(p);
                if (string.IsNullOrEmpty(ubb))
                {
                    slot.ActiveInFrame = false;
                    slot.Panel.visible = false;
                    continue;
                }
                UpdatePillContent(slot, ubb, compact: true, accentColor: ResolveSlotAccentColor(p));
            }

            slot.AnchorX = anchorLocal.x;
            slot.AnchorY = anchorLocal.y;

            if (slot.Panel != null && !slot.Panel.isDisposed)
            {
                slot.Panel.visible = true;
                slot.Panel.SetScale(_scale, _scale);
                if (_draggingAvatarPlayerId != pid)
                {
                    FloatingHintLayoutMath.ClampXYToBounds(
                        host.width, host.height,
                        slot.Width, slot.Height,
                        _scale,
                        slot.AnchorX + _offsetX, slot.AnchorY + _offsetY,
                        2f,
                        out float finalX, out float finalY);
                    slot.Panel.SetXY(finalX, finalY);
                }
            }
        }

        HideInactive(_avatarPills, isAvatarPool: true);
    }

    private static bool TryComputeAvatarBottomAnchor(UIBattleInfo_Button_PlayerInfo head, GComponent host, out Vector2 anchorLocal)
    {
        anchorLocal = Vector2.zero;
        var comPlayer = head.com_player;
        if (comPlayer == null || comPlayer.isDisposed || !comPlayer.onStage) return false;

        Vector2 globalPos;
        if (comPlayer is UICom_PlayerInfo pInfo && pInfo.list_Level != null && !pInfo.list_Level.isDisposed && pInfo.list_Level.visible)
        {
            Vector2 leftPt = (pInfo.txt_Gold != null && !pInfo.txt_Gold.isDisposed && pInfo.txt_Gold.visible)
                ? pInfo.txt_Gold.LocalToGlobal(new Vector2(-20f, 0f))
                : pInfo.list_Level.LocalToGlobal(new Vector2(-42f, 0f));
            Vector2 bottomPt = pInfo.list_Level.LocalToGlobal(new Vector2(0f, pInfo.list_Level.height));
            globalPos = new Vector2(leftPt.x, bottomPt.y + 1f);
        }
        else if (comPlayer.width > 10f && comPlayer.height > 10f)
        {
            globalPos = comPlayer.LocalToGlobal(new Vector2(comPlayer.width * 0.36f, comPlayer.height * 0.72f));
        }
        else
        {
            return false;
        }

        if (!PortMath.Finite(globalPos.x) || !PortMath.Finite(globalPos.y)) return false;
        anchorLocal = host.GlobalToLocal(globalPos);
        return PortMath.Finite(anchorLocal.x) && PortMath.Finite(anchorLocal.y);
    }

    private static bool IsVisibleTree(GObject obj)
    {
        if (obj == null || obj.isDisposed || !obj.onStage) return false;
        for (GObject node = obj; node != null; node = node.parent)
        {
            if (node.isDisposed || !node.visible || node.alpha <= 0.01f || node.displayObject == null || !node.displayObject.visible)
            {
                return false;
            }
        }
        return true;
    }

    private string BuildHeadStatusText(BattlePlayerData p)
    {
        var hero = p.player.Hero;
        string line1 = "";
        if (_showCd)
        {
            line1 = FormatCdToken(p);
        }

        if (_showHp)
        {
            int hp = p.Property?.HP != null ? p.Property.HP.Value : hero.Hp;
            int maxHp = (p.Property != null && p.Property.maxHP > 0) ? p.Property.maxHP : hero.MaxHp;
            string hpCol = hp <= 2 ? "#FF8DA1" : "#FFFFFF";
            string hpPart = $"[color={hpCol}]HP {hp}/{maxHp}[/color]";
            line1 = string.IsNullOrEmpty(line1) ? hpPart : (line1 + "  " + hpPart);
        }

        string line2 = _showAtkDef ? FormatAtkDefTokens(p) : "";
        if (_showMove)
        {
            string movePart = FormatMoveToken(p);
            if (!string.IsNullOrEmpty(movePart))
            {
                line2 = string.IsNullOrEmpty(line2) ? movePart : (line2 + " " + movePart);
            }
        }

        if (!string.IsNullOrEmpty(line1) && !string.IsNullOrEmpty(line2))
        {
            return line1 + "\n" + line2;
        }
        return !string.IsNullOrEmpty(line1) ? line1 : line2;
    }

    private string BuildAvatarStatusText(BattlePlayerData p)
    {
        string result = "";
        if (_showCd)
        {
            result = FormatCdToken(p);
        }
        if (_showAtkDef)
        {
            string atkDef = FormatAtkDefTokens(p);
            result = string.IsNullOrEmpty(result) ? atkDef : (result + "  " + atkDef);
        }
        if (_showMove)
        {
            string movePart = FormatMoveToken(p);
            if (!string.IsNullOrEmpty(movePart))
            {
                result = string.IsNullOrEmpty(result) ? movePart : (result + " " + movePart);
            }
        }
        if (string.IsNullOrEmpty(result) && _showHp)
        {
            var hero = p.player.Hero;
            int hp = p.Property?.HP != null ? p.Property.HP.Value : hero.Hp;
            int maxHp = (p.Property != null && p.Property.maxHP > 0) ? p.Property.maxHP : hero.MaxHp;
            string hpCol = hp <= 2 ? "#FF8DA1" : "#FFFFFF";
            result = $"[color={hpCol}]HP {hp}/{maxHp}[/color]";
        }
        return result;
    }

    private void EnsureBuffSignalBound(GameLogicManager logic)
    {
        var sig = logic?.buff?.buff;
        if (_boundBuffSignal == sig) return;
        if (_boundBuffSignal != null)
        {
            try { _boundBuffSignal.RemoveListener(OnBuffSignalChanged); } catch { }
        }
        _boundBuffSignal = sig;
        if (_boundBuffSignal != null)
        {
            _boundBuffSignal.AddListener(OnBuffSignalChanged);
        }
    }

    private void OnBuffSignalChanged()
    {
        _nextTextRefresh = 0f;
    }

    private string FormatMoveToken(BattlePlayerData p)
    {
        if (p == null || p.player?.Hero == null) return "";
        long pid = p.player.Id;
        int extraAdd = p.Property?.ExtraAddMovePoint != null ? p.Property.ExtraAddMovePoint.Value : 0;
        int extraMinus = p.Property?.ExtraMinusMovePoint != null ? p.Property.ExtraMinusMovePoint.Value : 0;
        int rawNet = extraAdd - extraMinus;
        if (_lastRawExtraMove.TryGetValue(pid, out int prevRaw) && prevRaw != rawNet)
        {
            _playersWithServerAddMove.Add(pid);
        }
        _lastRawExtraMove[pid] = rawNet;
        if (extraAdd != 0 || extraMinus != 0)
        {
            _playersWithServerAddMove.Add(pid);
        }
        bool hasServerUpdate = _playersWithServerAddMove.Contains(pid);

        int additionMoveSum = 0;
        if (!hasServerUpdate && extraAdd == 0 && extraMinus == 0 && p.player.Hero.AdditionAttrs != null)
        {
            foreach (var kv in p.player.Hero.AdditionAttrs)
            {
                var attr = kv.Value;
                if (attr != null)
                {
                    additionMoveSum += attr.MovePoint + attr.NextMovePoint + attr.SkillAppendMovePoint2;
                }
            }
        }

        int buffTempAdd = CollectBuffTempMoveBonus(p);
        int fallbackAdd = Math.Max(0, additionMoveSum + p.player.Hero.AppendMovePoint1 + p.player.Hero.AppendMovePoint2);
        int observedAdd = (hasServerUpdate || extraAdd != 0 || extraMinus != 0) ? Math.Max(0, extraAdd) : fallbackAdd;

        if (!_baselinePermanentAdd.TryGetValue(pid, out int baselinePerm))
        {
            baselinePerm = buffTempAdd == 0 ? observedAdd : fallbackAdd;
            _baselinePermanentAdd[pid] = baselinePerm;
        }
        else if (buffTempAdd == 0)
        {
            // When no temporary speed buff is present, ExtraAddMovePoint tracks permanent move-speed sources.
            baselinePerm = observedAdd;
            _baselinePermanentAdd[pid] = baselinePerm;
        }

        int pveUpgradeMove = ComputePveStarUpgradeMoveBonus(p);

        PlayerCombatStatusPolicy.ResolveMoveSpeedBreakdown(
            extraAdd,
            extraMinus,
            hasServerUpdate,
            additionMoveSum,
            p.player.Hero.AppendMovePoint1,
            p.player.Hero.AppendMovePoint2,
            buffTempAdd,
            baselinePerm,
            pveUpgradeMove,
            out int permanentNet,
            out int tempAdd);

        return PlayerCombatStatusPolicy.FormatMoveSpeedToken(permanentNet, tempAdd);
    }

    private static int ComputePveStarUpgradeMoveBonus(BattlePlayerData p)
    {
        if (p?.player?.Hero == null) return 0;
        var roomInfo = GameLogicManager.inst?.room?.curRoomInfo;
        if (roomInfo == null || !roomInfo.IsPVE()) return 0;

        int heroId = p.player.Hero.HeroId;
        int star = p.Property?.level != null ? p.Property.level.Value : p.player.Hero.Lv;
        int pveLv = p.player.PVEHeroLV;
        if (p.player.Hero.PveHeroStrengthen != null && p.player.Hero.PveHeroStrengthen.Level > pveLv)
        {
            pveLv = p.player.Hero.PveHeroStrengthen.Level;
        }
        if (heroId <= 0 || star <= 0 || pveLv <= 0) return 0;

        int total = 0;
        try
        {
            var enhDict = StaticConfigure.PVENurturance?.EnhancementDict;
            if (enhDict == null || !enhDict.TryGetValue(heroId, out var enh) || enh?.PVENurturanceEnhancementConfigureItems == null)
            {
                return 0;
            }
            var strDict = StaticConfigure.STRPVENurturance?.LocalDict;
            var items = enh.PVENurturanceEnhancementConfigureItems;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null || item.Lv <= 0 || item.Lv > pveLv) continue;
                string desc = null;
                if (strDict != null && strDict.TryGetValue(item.DescriptionID, out var strCfg) && strCfg != null)
                {
                    desc = strCfg.Simplified;
                }
                if (string.IsNullOrEmpty(desc) && item.DescriptionID > 0)
                {
                    try { desc = item.DescriptionID.GetLocal(UIStringType.PVENurturance); } catch { }
                }
                if (!string.IsNullOrEmpty(desc)
                    && PlayerCombatStatusPolicy.TryParsePveStarMoveBonus(desc, star, out _, out int delta)
                    && delta > 0)
                {
                    total += delta;
                }
            }
        }
        catch
        {
            return total;
        }
        return total;
    }

    private static int CollectBuffTempMoveBonus(BattlePlayerData p)
    {
        var dict = p?.buffContainer?._buffDict;
        if (dict == null || dict.Count == 0) return 0;

        int loveStacks = 0;
        foreach (var kv in dict)
        {
            var b = kv.Value;
            if (b != null && b.BuffId == 3021101 && b.Progress > 0)
            {
                loveStacks += b.Progress;
            }
        }

        int sum = 0;
        foreach (var kv in dict)
        {
            var b = kv.Value;
            if (b == null || b.BuffId <= 0) continue;

            BuffInfoConfigure cfg = null;
            try { cfg = b.BuffId.GetBuffConfigure(); } catch { }
            string name = SafeGetBuffLocal(cfg != null ? cfg.NameId : 0);
            string desc = SafeGetBuffLocal(cfg != null ? cfg.DescId : 0);

            long param0 = 0L;
            long param1 = 0L;
            if (b.Params != null)
            {
                b.Params.TryGetValue(0, out param0);
                b.Params.TryGetValue(1, out param1);
            }

            if (PlayerCombatStatusPolicy.TryExtractBuffTempMoveDelta(
                b.BuffId,
                b.Progress,
                b.KeepRound,
                name,
                desc,
                param0,
                param1,
                loveStacks,
                out int delta) && delta > 0)
            {
                sum += delta;
            }
        }
        return sum;
    }

    private static string SafeGetBuffLocal(int id)
    {
        if (id <= 0) return "";
        try
        {
            return id.GetLocal(UIStringType.Buff) ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static string FormatCdToken(BattlePlayerData p)
    {
        int skillId = p.player.GetBattleActiveSkillId();
        int curCd = p.Property?.activeSkillCD != null ? p.Property.activeSkillCD.Value : p.SkillCD;
        int maxCd = 0;
        if (skillId > 0 && StaticConfigure.Skill?.InfoDict != null
            && StaticConfigure.Skill.InfoDict.TryGetValue(skillId, out var scfg) && scfg != null)
        {
            maxCd = scfg.Round;
        }
        if (skillId > 0 && maxCd > 0)
        {
            string cdCol = curCd <= 0 ? "#7CFF9B" : "#FFC857";
            return $"[color={cdCol}]CD {curCd}/{maxCd}[/color]";
        }
        return "[color=#B8C8DC]被动[/color]";
    }

    private static string FormatAtkDefTokens(BattlePlayerData p)
    {
        var hero = p.player.Hero;
        int baseAtk = hero.Attack;
        int baseDef = hero.Defense;
        int curAtk = p.Property?.ATK != null ? p.Property.ATK.Value : baseAtk;
        int curDef = p.Property?.DEF != null ? p.Property.DEF.Value : baseDef;
        return PlayerCombatStatusPolicy.FormatAtkDefTokens(baseAtk, curAtk, baseDef, curDef);
    }

    private static Color ResolveSlotAccentColor(BattlePlayerData p)
    {
        switch (p.player.Slot)
        {
            case 0: return new Color(0.95f, 0.28f, 0.42f, 1f);
            case 1: return new Color(0.55f, 0.88f, 0.25f, 1f);
            case 2: return new Color(0.28f, 0.68f, 0.98f, 1f);
            case 3: return new Color(0.96f, 0.72f, 0.22f, 1f);
            default: return new Color(0.94f, 0.16f, 0.47f, 1f);
        }
    }

    private PillSlot EnsureHeadPill(GComponent headLayer, long playerId)
    {
        if (_headPills.TryGetValue(playerId, out var slot)
            && slot.Panel != null && !slot.Panel.isDisposed && slot.Panel.parent == headLayer)
        {
            return slot;
        }
        if (slot != null && slot.Panel != null && !slot.Panel.isDisposed)
        {
            slot.Panel.Dispose();
        }

        var panel = new GComponent
        {
            name = "XINGSHU_PlayerHeadHud_" + playerId,
            touchable = false,
            opaque = false,
            draggable = false,
            sortingOrder = 0,
        };
        panel.SetPivot(0f, 0f, false);

        slot = CreatePillVisuals(panel, playerId, compact: false);
        headLayer.AddChild(panel);
        _headPills[playerId] = slot;
        return slot;
    }

    private PillSlot EnsureAvatarPill(GComponent host, GObject anchorSibling, long playerId, bool modalOpen)
    {
        int targetIndex = anchorSibling != null && anchorSibling.parent == host
            ? Math.Min(host.numChildren, host.GetChildIndex(anchorSibling) + 1)
            : host.numChildren;

        if (_avatarPills.TryGetValue(playerId, out var slot)
            && slot.Panel != null && !slot.Panel.isDisposed && slot.Panel.parent == host)
        {
            if (anchorSibling != null && anchorSibling.parent == host)
            {
                int desired = Math.Min(host.numChildren - 1, host.GetChildIndex(anchorSibling) + 1);
                if (host.GetChildIndex(slot.Panel) != desired)
                {
                    host.SetChildIndex(slot.Panel, desired);
                }
            }
            return slot;
        }

        if (slot != null && slot.Panel != null && !slot.Panel.isDisposed)
        {
            slot.Panel.Dispose();
        }

        bool interactive = !_locked && !modalOpen;
        var panel = new GComponent
        {
            name = "XINGSHU_PlayerAvatarHud_" + playerId,
            touchable = interactive,
            opaque = interactive,
            draggable = interactive,
            sortingOrder = 0,
        };
        XingShuOverlayInput.Protect(panel);
        if (!interactive)
        {
            panel.touchable = false;
            panel.opaque = false;
        }
        panel.SetPivot(0f, 0f, false);

        long capturedId = playerId;
        panel.onDragStart.Set((EventCallback0)delegate
        {
            _draggingAvatarPlayerId = capturedId;
        });
        panel.onDragMove.Set((EventCallback0)delegate
        {
            UpdateAvatarOffsetFromDrag(capturedId, saveConfig: false);
        });
        panel.onDragEnd.Set((EventCallback0)delegate
        {
            UpdateAvatarOffsetFromDrag(capturedId, saveConfig: true);
            _draggingAvatarPlayerId = 0L;
        });

        slot = CreatePillVisuals(panel, playerId, compact: true);
        host.AddChildAt(panel, targetIndex);
        _avatarPills[playerId] = slot;
        return slot;
    }

    private static PillSlot CreatePillVisuals(GComponent panel, long playerId, bool compact)
    {
        var bg = new GGraph { touchable = false };
        panel.AddChild(bg);

        var accent = new GGraph { touchable = false };
        panel.AddChild(accent);

        var label = new GTextField
        {
            name = compact ? "XINGSHU_PlayerAvatarHudLabel" : "XINGSHU_PlayerHeadHudLabel",
            touchable = false,
            autoSize = AutoSizeType.Both,
            align = compact ? AlignType.Left : AlignType.Center,
            singleLine = compact,
            UBBEnabled = true,
            stroke = 1.1f,
            strokeColor = new Color(0f, 0f, 0f, 0.94f),
        };
        var tf = label.textFormat;
        tf.size = compact
            ? (Application.isMobilePlatform ? 15 : 13)
            : (Application.isMobilePlatform ? 18 : 15);
        tf.lineSpacing = compact ? 1 : 3;
        tf.bold = true;
        tf.color = new Color(0.96f, 0.98f, 1f, 1f);
        label.textFormat = tf;
        panel.AddChild(label);

        return new PillSlot
        {
            PlayerId = playerId,
            Panel = panel,
            Background = bg,
            AccentBar = accent,
            Label = label,
        };
    }

    private static void UpdatePillContent(PillSlot slot, string ubbText, bool compact, Color accentColor)
    {
        if (slot.LastText == ubbText && slot.LastAccentColor == accentColor && slot.Width > 0f) return;
        slot.LastText = ubbText;
        slot.LastAccentColor = accentColor;
        slot.Label.text = ubbText;

        float padX = compact ? 14f : 18f;
        float padY = compact ? 6f : 10f;
        float minW = compact ? (Application.isMobilePlatform ? 112f : 96f) : (Application.isMobilePlatform ? 130f : 110f);
        float minH = compact ? 20f : 26f;
        float w = Mathf.Max(minW, slot.Label.width + padX);
        float h = Mathf.Max(minH, slot.Label.height + padY);

        float textX = compact ? 9f : (w - slot.Label.width) * 0.5f + 1.5f;
        float textY = (h - slot.Label.height) * 0.5f;
        slot.Label.SetXY(textX, textY);

        slot.Width = w;
        slot.Height = h;
        slot.Panel.SetSize(w, h);
        float bgAlpha = compact ? 0.80f : 0.84f;
        float radius = compact ? 5f : 7f;
        slot.Background.DrawRoundRect(w, h, new Color(0.055f, 0.068f, 0.11f, bgAlpha), new[] { radius, radius, radius, radius });
        slot.AccentBar.DrawRoundRect(3f, Mathf.Max(10f, h - 8f), accentColor, new[] { 2f, 2f, 2f, 2f });
        slot.AccentBar.SetXY(3.5f, 4f);
    }

    private void UpdateAvatarOffsetFromDrag(long playerId, bool saveConfig)
    {
        if (_attachedAvatarParent == null || _attachedAvatarParent.isDisposed) return;
        if (!_avatarPills.TryGetValue(playerId, out var slot) || slot.Panel == null || slot.Panel.isDisposed) return;

        FloatingHintLayoutMath.ClampXYToBounds(
            _attachedAvatarParent.width, _attachedAvatarParent.height,
            slot.Width, slot.Height,
            _scale, slot.Panel.x, slot.Panel.y,
            2f,
            out float cx, out float cy);
        slot.Panel.SetXY(cx, cy);

        float nextX = FloatingHintLayoutMath.FiniteClamp(
            cx - slot.AnchorX,
            XingShuConfigKeys.PlayerCombatHudOffsetXMin, XingShuConfigKeys.PlayerCombatHudOffsetXMax, _offsetX);
        float nextY = FloatingHintLayoutMath.FiniteClamp(
            cy - slot.AnchorY,
            XingShuConfigKeys.PlayerCombatHudOffsetYMin, XingShuConfigKeys.PlayerCombatHudOffsetYMax, _offsetY);

        if (saveConfig)
        {
            SetOffset(nextX, nextY);
        }
        else
        {
            _offsetX = nextX;
            _offsetY = nextY;
        }
    }

    private void RefreshAvatarInteractionStates(bool modalOpen)
    {
        bool interactive = !_locked && !modalOpen;
        foreach (var kv in _avatarPills)
        {
            var panel = kv.Value.Panel;
            if (panel == null || panel.isDisposed) continue;
            panel.touchable = interactive;
            panel.opaque = interactive;
            panel.draggable = interactive;
        }
    }

    private static void BeginFrame(Dictionary<long, PillSlot> pool)
    {
        foreach (var kv in pool)
        {
            kv.Value.ActiveInFrame = false;
        }
    }

    private void HideInactive(Dictionary<long, PillSlot> pool, bool isAvatarPool)
    {
        foreach (var kv in pool)
        {
            if (!kv.Value.ActiveInFrame && kv.Value.Panel != null && !kv.Value.Panel.isDisposed)
            {
                if (isAvatarPool && _draggingAvatarPlayerId == kv.Key) _draggingAvatarPlayerId = 0L;
                kv.Value.Panel.visible = false;
            }
        }
    }

    private static void InvalidateText(Dictionary<long, PillSlot> pool)
    {
        foreach (var slot in pool.Values)
        {
            slot.LastText = "";
        }
    }

    private static void HidePool(Dictionary<long, PillSlot> pool)
    {
        foreach (var kv in pool)
        {
            if (kv.Value.Panel != null && !kv.Value.Panel.isDisposed)
            {
                kv.Value.Panel.visible = false;
            }
        }
    }

    private static void DisposePool(Dictionary<long, PillSlot> pool)
    {
        foreach (var kv in pool)
        {
            if (kv.Value.Panel != null && !kv.Value.Panel.isDisposed)
            {
                kv.Value.Panel.Dispose();
            }
        }
        pool.Clear();
    }

    private void HideAllPills()
    {
        _draggingAvatarPlayerId = 0L;
        HidePool(_headPills);
        HidePool(_avatarPills);
    }

    private void ClearAll()
    {
        _draggingAvatarPlayerId = 0L;
        _playersWithServerAddMove.Clear();
        _lastRawExtraMove.Clear();
        _baselinePermanentAdd.Clear();
        if (_boundBuffSignal != null)
        {
            try { _boundBuffSignal.RemoveListener(OnBuffSignalChanged); } catch { }
            _boundBuffSignal = null;
        }
        DisposePool(_headPills);
        DisposePool(_avatarPills);
        _attachedHeadLayer = null;
        _attachedAvatarParent = null;
    }

    private void OnDestroy()
    {
        ClearAll();
        if (_instance == this) _instance = null;
    }
}

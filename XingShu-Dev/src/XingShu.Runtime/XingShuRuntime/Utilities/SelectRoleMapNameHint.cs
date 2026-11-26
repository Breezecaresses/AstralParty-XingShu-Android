using System;
using FairyGUI;
using GameLogic;
using Tools;
using UI;
using UnityEngine;
using XingShuRuntime.Core;

namespace XingShuRuntime.Utilities;

/// <summary>
/// Configurable floating display of the active map name (and mode/difficulty) on the
/// native character-selection screen (<see cref="RoomHeroPanel"/>). Supports dragging
/// when unlocked as well as size and normalized position sliders.
/// </summary>
internal sealed class SelectRoleMapNameHint : MonoBehaviour
{
    private static SelectRoleMapNameHint _instance;
    private const float RefreshIntervalSeconds = 0.15f;

    private bool _enabled;
    private float _posX = XingShuConfigKeys.SelectRoleMapNamePosXDefault;
    private float _posY = XingShuConfigKeys.SelectRoleMapNamePosYDefault;
    private float _scale = XingShuConfigKeys.SelectRoleMapNameScaleDefault;
    private bool _locked = XingShuConfigKeys.SelectRoleMapNameLockedDefault;
    private bool _dragging;

    private float _nextTick;
    private GComponent _panel;
    private GGraph _background;
    private GGraph _accentBar;
    private GTextField _titleLabel;
    private GTextField _mapLabel;
    private GTextField _metaLabel;
    private UIRoomHeroPanel _attachedUi;
    private float _panelWidth = 260f;
    private float _panelHeight = 96f;
    private string _lastTextKey = "";

    internal static bool IsEnabled => _instance != null && _instance._enabled;
    internal static float PosX => _instance != null ? _instance._posX
        : FloatingHintLayoutMath.FiniteClamp(XingShuConfigService.GetFloat(XingShuConfigKeys.SelectRoleMapNamePosX, XingShuConfigKeys.SelectRoleMapNamePosXDefault), 0f, 1f, XingShuConfigKeys.SelectRoleMapNamePosXDefault);
    internal static float PosY => _instance != null ? _instance._posY
        : FloatingHintLayoutMath.FiniteClamp(XingShuConfigService.GetFloat(XingShuConfigKeys.SelectRoleMapNamePosY, XingShuConfigKeys.SelectRoleMapNamePosYDefault), 0f, 1f, XingShuConfigKeys.SelectRoleMapNamePosYDefault);
    internal static float Scale => _instance != null ? _instance._scale
        : FloatingHintLayoutMath.FiniteClamp(XingShuConfigService.GetFloat(XingShuConfigKeys.SelectRoleMapNameScale, XingShuConfigKeys.SelectRoleMapNameScaleDefault), XingShuConfigKeys.SelectRoleMapNameScaleMin, XingShuConfigKeys.SelectRoleMapNameScaleMax, XingShuConfigKeys.SelectRoleMapNameScaleDefault);
    internal static bool Locked => _instance != null ? _instance._locked
        : XingShuConfigService.GetBool(XingShuConfigKeys.SelectRoleMapNameLocked, XingShuConfigKeys.SelectRoleMapNameLockedDefault);

    internal static void SetEnabled(bool value)
    {
        if (_instance == null)
        {
            XingShuConfigService.SetBool(XingShuConfigKeys.SelectRoleMapNameEnabled, value);
            XingShuConfigService.Save();
            return;
        }
        _instance._enabled = value;
        XingShuConfigService.SetBool(XingShuConfigKeys.SelectRoleMapNameEnabled, value);
        XingShuConfigService.Save();
        if (!value)
        {
            _instance.RemoveBadge();
        }
        else
        {
            _instance._nextTick = 0f;
        }
    }

    internal static void SetPosition(float x, float y)
    {
        float nx = FloatingHintLayoutMath.FiniteClamp(x, 0f, 1f, XingShuConfigKeys.SelectRoleMapNamePosXDefault);
        float ny = FloatingHintLayoutMath.FiniteClamp(y, 0f, 1f, XingShuConfigKeys.SelectRoleMapNamePosYDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.SelectRoleMapNamePosX, nx);
        XingShuConfigService.SetFloat(XingShuConfigKeys.SelectRoleMapNamePosY, ny);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._posX = nx;
            _instance._posY = ny;
            _instance.RefreshTransformOnly();
        }
    }

    internal static void SetScale(float scale)
    {
        float clamped = FloatingHintLayoutMath.FiniteClamp(
            scale,
            XingShuConfigKeys.SelectRoleMapNameScaleMin,
            XingShuConfigKeys.SelectRoleMapNameScaleMax,
            XingShuConfigKeys.SelectRoleMapNameScaleDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.SelectRoleMapNameScale, clamped);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._scale = clamped;
            _instance.RefreshTransformOnly();
        }
    }

    internal static void SetLocked(bool locked)
    {
        XingShuConfigService.SetBool(XingShuConfigKeys.SelectRoleMapNameLocked, locked);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._locked = locked;
            _instance._dragging = false;
            _instance._lastTextKey = "";
            _instance.RefreshInteractionState();
            _instance._nextTick = 0f;
        }
    }

    internal static void ResetLayout()
    {
        XingShuConfigService.SetFloat(XingShuConfigKeys.SelectRoleMapNamePosX, XingShuConfigKeys.SelectRoleMapNamePosXDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.SelectRoleMapNamePosY, XingShuConfigKeys.SelectRoleMapNamePosYDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.SelectRoleMapNameScale, XingShuConfigKeys.SelectRoleMapNameScaleDefault);
        XingShuConfigService.SetBool(XingShuConfigKeys.SelectRoleMapNameLocked, XingShuConfigKeys.SelectRoleMapNameLockedDefault);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._posX = XingShuConfigKeys.SelectRoleMapNamePosXDefault;
            _instance._posY = XingShuConfigKeys.SelectRoleMapNamePosYDefault;
            _instance._scale = XingShuConfigKeys.SelectRoleMapNameScaleDefault;
            _instance._locked = XingShuConfigKeys.SelectRoleMapNameLockedDefault;
            _instance._dragging = false;
            _instance._lastTextKey = "";
            _instance.RefreshInteractionState();
            _instance.RefreshTransformOnly();
            _instance._nextTick = 0f;
        }
    }

    public static void Install(GameObject host)
    {
        if (_instance != null || host == null) return;
        _instance = host.AddComponent<SelectRoleMapNameHint>();
        _instance.LoadConfig();
    }

    private void LoadConfig()
    {
        _enabled = XingShuConfigService.GetBool(
            XingShuConfigKeys.SelectRoleMapNameEnabled,
            XingShuConfigKeys.SelectRoleMapNameEnabledDefault);
        _posX = FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.SelectRoleMapNamePosX, XingShuConfigKeys.SelectRoleMapNamePosXDefault),
            0f, 1f, XingShuConfigKeys.SelectRoleMapNamePosXDefault);
        _posY = FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.SelectRoleMapNamePosY, XingShuConfigKeys.SelectRoleMapNamePosYDefault),
            0f, 1f, XingShuConfigKeys.SelectRoleMapNamePosYDefault);
        _scale = FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.SelectRoleMapNameScale, XingShuConfigKeys.SelectRoleMapNameScaleDefault),
            XingShuConfigKeys.SelectRoleMapNameScaleMin, XingShuConfigKeys.SelectRoleMapNameScaleMax, XingShuConfigKeys.SelectRoleMapNameScaleDefault);
        _locked = XingShuConfigService.GetBool(
            XingShuConfigKeys.SelectRoleMapNameLocked,
            XingShuConfigKeys.SelectRoleMapNameLockedDefault);
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextTick) return;
        _nextTick = Time.unscaledTime + RefreshIntervalSeconds;
        try
        {
            Tick();
        }
        catch (Exception)
        {
            RemoveBadge();
        }
    }

    private void Tick()
    {
        if (!_enabled
            || !SimpleSingletonProvider<UIManager>.hasInstance
            || !SimpleSingletonProvider<GameLogicManager>.hasInstance)
        {
            RemoveBadge();
            return;
        }

        var uiMgr = SimpleSingletonProvider<UIManager>.inst;
        var panel = uiMgr.currentPanel as RoomHeroPanel;
        if (panel == null || !panel.IsOpen())
        {
            RemoveBadge();
            return;
        }

        UIRoomHeroPanel ui = panel.UIObject;
        if (ui == null || ui.isDisposed || !ui.visible || !ui.onStage || ui.width < 480f || ui.height < 360f)
        {
            RemoveBadge();
            return;
        }

        var logic = SimpleSingletonProvider<GameLogicManager>.inst;
        var roomInfo = logic.room?.curRoomInfo;
        if (roomInfo == null || roomInfo.MapId <= 0)
        {
            RemoveBadge();
            return;
        }

        string mapName = ResolveMapName(roomInfo.MapId);
        if (string.IsNullOrEmpty(mapName))
        {
            RemoveBadge();
            return;
        }

        int mapType = 0;
        bool isPve = false;
        int diff = -1;
        try { mapType = (int)roomInfo.MapType; } catch { }
        try { isPve = roomInfo.IsPVE(); } catch { }
        try { diff = (int)roomInfo.Difficulty; } catch { }

        string subtitle = ResolveModeAndDifficulty(mapType, isPve, diff);
        EnsureBadge(ui);

        string textKey = mapName + "\n" + subtitle + "|" + Mathf.RoundToInt(ui.width) + "x" + Mathf.RoundToInt(ui.height)
            + "|" + Mathf.RoundToInt(_scale * 100f) + "|" + (_locked ? "L" : "U");
        if (_lastTextKey != textKey)
        {
            _lastTextKey = textKey;
            ApplyLayout(ui, mapName, subtitle);
        }
        else
        {
            RefreshTransformOnly();
        }
    }

    private static string ResolveMapName(int mapId)
    {
        MapInfoConfigure mapCfg = null;
        if (StaticConfigure.Map?.InfoDict != null)
        {
            StaticConfigure.Map.InfoDict.TryGetValue(mapId, out mapCfg);
        }
        if (mapCfg == null)
        {
            mapCfg = mapId.GetMapDataConfigure();
        }
        if (mapCfg != null && mapCfg.MapName > 0)
        {
            string localized = mapCfg.MapName.GetLocal(UIStringType.Map);
            if (!string.IsNullOrEmpty(localized)) return localized;
        }
        return "地图 #" + mapId;
    }

    private static string ResolveModeAndDifficulty(int mapType, bool isPve, int diff)
    {
        string modeName = "";
        try
        {
            GameModeInfoConfigure modeCfg = mapType.GetGameModeInfoConfigure();
            if (modeCfg != null && modeCfg.NameID > 0)
            {
                modeName = modeCfg.NameID.GetLocal(UIStringType.GameMode) ?? "";
            }
        }
        catch { }

        string diffText = "";
        try
        {
            if (isPve)
            {
                var diffCfg = diff.GetChoosingTimeLimitDifficultyConfigure();
                if (diffCfg != null && diffCfg.DescriptionID > 0)
                {
                    diffText = diffCfg.DescriptionID.GetLocal(UIStringType.ChoosingTimeLimit) ?? "";
                }
                if (string.IsNullOrEmpty(diffText) && diff >= 0)
                {
                    diffText = "难度 " + (diff + 1);
                }
            }
        }
        catch { }

        if (!string.IsNullOrEmpty(modeName) && !string.IsNullOrEmpty(diffText))
        {
            return modeName + " · " + diffText;
        }
        return !string.IsNullOrEmpty(modeName) ? modeName : diffText;
    }

    private void EnsureBadge(UIRoomHeroPanel ui)
    {
        if (_panel != null && !_panel.isDisposed && _panel.parent == ui && _attachedUi == ui
            && _background != null && _mapLabel != null && !_mapLabel.isDisposed)
        {
            return;
        }

        RemoveBadge();
        _attachedUi = ui;

        _panel = new GComponent
        {
            name = "XINGSHU_SelectRoleMapNamePanel",
            touchable = !_locked,
            opaque = !_locked,
            draggable = !_locked,
        };
        _panel.SetPivot(0f, 0f, false);
        _panel.onDragStart.Set((EventCallback0)delegate
        {
            _dragging = true;
        });
        _panel.onDragMove.Set((EventCallback0)delegate
        {
            ClampCurrentPanelBounds();
        });
        _panel.onDragEnd.Set((EventCallback0)delegate
        {
            CommitDraggedPosition();
        });

        _background = new GGraph { touchable = false };
        _panel.AddChild(_background);

        _accentBar = new GGraph { touchable = false };
        _panel.AddChild(_accentBar);

        _titleLabel = new GTextField
        {
            name = "XINGSHU_MapBadgeCaption",
            touchable = false,
            autoSize = AutoSizeType.Height,
            align = AlignType.Left,
            stroke = 1f,
            strokeColor = new Color(0f, 0f, 0f, 0.88f),
        };
        var tfCaption = _titleLabel.textFormat;
        tfCaption.size = Application.isMobilePlatform ? 18 : 15;
        tfCaption.bold = true;
        tfCaption.color = new Color(0.98f, 0.44f, 0.72f, 1f);
        _titleLabel.textFormat = tfCaption;
        _panel.AddChild(_titleLabel);

        _mapLabel = new GTextField
        {
            name = "XINGSHU_MapBadgeTitle",
            touchable = false,
            autoSize = AutoSizeType.Height,
            align = AlignType.Left,
            stroke = 1.5f,
            strokeColor = new Color(0f, 0f, 0f, 0.92f),
        };
        var tfMap = _mapLabel.textFormat;
        tfMap.size = Application.isMobilePlatform ? 30 : 25;
        tfMap.bold = true;
        tfMap.color = new Color(0.96f, 0.98f, 1f, 1f);
        _mapLabel.textFormat = tfMap;
        _panel.AddChild(_mapLabel);

        _metaLabel = new GTextField
        {
            name = "XINGSHU_MapBadgeMeta",
            touchable = false,
            autoSize = AutoSizeType.Height,
            align = AlignType.Left,
            stroke = 1f,
            strokeColor = new Color(0f, 0f, 0f, 0.85f),
        };
        var tfMeta = _metaLabel.textFormat;
        tfMeta.size = Application.isMobilePlatform ? 18 : 15;
        tfMeta.color = new Color(0.75f, 0.86f, 1f, 0.96f);
        _metaLabel.textFormat = tfMeta;
        _panel.AddChild(_metaLabel);

        ui.AddChild(_panel);
    }

    private void ApplyLayout(UIRoomHeroPanel ui, string mapName, string subtitle)
    {
        float width = Mathf.Clamp(ui.width * 0.165f, 240f, 320f);
        float innerWidth = width - 28f;

        _titleLabel.SetSize(innerWidth, _titleLabel.height);
        _titleLabel.SetXY(16f, 10f);
        _titleLabel.text = _locked ? "当前地图" : "当前地图 · 可拖动";

        _mapLabel.SetSize(innerWidth, _mapLabel.height);
        _mapLabel.SetXY(16f, 10f + _titleLabel.height + 3f);
        _mapLabel.text = mapName;

        float contentBottom = 10f + _titleLabel.height + 3f + _mapLabel.height;
        if (!string.IsNullOrEmpty(subtitle))
        {
            _metaLabel.visible = true;
            _metaLabel.SetSize(innerWidth, _metaLabel.height);
            _metaLabel.SetXY(16f, contentBottom + 3f);
            _metaLabel.text = subtitle;
            contentBottom += 3f + _metaLabel.height;
        }
        else
        {
            _metaLabel.visible = false;
        }

        float height = contentBottom + 12f;
        _panelWidth = width;
        _panelHeight = height;
        _panel.SetSize(width, height);
        _background.DrawRoundRect(width, height, new Color(0.055f, 0.068f, 0.11f, 0.88f), new[] { 10f, 10f, 10f, 10f });
        _accentBar.DrawRoundRect(4f, Mathf.Max(20f, height - 20f), new Color(0.94f, 0.16f, 0.47f, 1f), new[] { 2f, 2f, 2f, 2f });
        _accentBar.SetXY(6f, 10f);

        RefreshInteractionState();
        RefreshTransformOnly();
    }

    private void RefreshInteractionState()
    {
        if (_panel == null || _panel.isDisposed) return;
        _panel.touchable = !_locked;
        _panel.opaque = !_locked;
        _panel.draggable = !_locked;
    }

    private void RefreshTransformOnly()
    {
        if (_panel == null || _panel.isDisposed || _attachedUi == null || _attachedUi.isDisposed) return;
        _panel.SetScale(_scale, _scale);
        if (_dragging) return;
        FloatingHintLayoutMath.ComputeClampedXY(
            _attachedUi.width, _attachedUi.height,
            _panelWidth, _panelHeight,
            _scale, _posX, _posY,
            FloatingHintLayoutMath.DefaultScreenMargin,
            out float posX, out float posY);
        _panel.SetXY(posX, posY);
    }

    private void ClampCurrentPanelBounds()
    {
        if (_panel == null || _panel.isDisposed || _attachedUi == null || _attachedUi.isDisposed) return;
        FloatingHintLayoutMath.ClampXYToBounds(
            _attachedUi.width, _attachedUi.height,
            _panelWidth, _panelHeight,
            _scale, _panel.x, _panel.y,
            FloatingHintLayoutMath.DefaultScreenMargin,
            out float cx, out float cy);
        _panel.SetXY(cx, cy);
    }

    private void CommitDraggedPosition()
    {
        _dragging = false;
        if (_panel == null || _panel.isDisposed || _attachedUi == null || _attachedUi.isDisposed) return;
        ClampCurrentPanelBounds();
        FloatingHintLayoutMath.ComputeNormalizedFromXY(
            _attachedUi.width, _attachedUi.height,
            _panelWidth, _panelHeight,
            _scale, _panel.x, _panel.y,
            FloatingHintLayoutMath.DefaultScreenMargin,
            out _posX, out _posY);
        XingShuConfigService.SetFloat(XingShuConfigKeys.SelectRoleMapNamePosX, _posX);
        XingShuConfigService.SetFloat(XingShuConfigKeys.SelectRoleMapNamePosY, _posY);
        XingShuConfigService.Save();
    }

    private void RemoveBadge()
    {
        _dragging = false;
        if (_panel != null && !_panel.isDisposed) _panel.Dispose();
        _panel = null;
        _background = null;
        _accentBar = null;
        _titleLabel = null;
        _mapLabel = null;
        _metaLabel = null;
        _attachedUi = null;
        _lastTextKey = "";
    }

    private void OnDestroy()
    {
        RemoveBadge();
        if (_instance == this) _instance = null;
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using Core.Scene;
using Core.Unit;
using FairyGUI;
using GameLogic;
using Tools;
using UI;
using UnityEngine;
using party.model;
using XingShuRuntime.Core;

namespace XingShuRuntime.Utilities;

/// <summary>
/// Configurable floating display of played battle cards and their point rolls / bonus deltas
/// on the native combat window (<see cref="FightWindow"/>).
/// Supports dragging each side independently when unlocked as well as size and placement controls.
/// </summary>
internal sealed class BattleCardPointHint : MonoBehaviour
{
    private sealed class PlayedCardRecord
    {
        public int Guid;
        public int CardId;
        public string Name;
        public int MinDelta;
        public int MaxDelta;
    }

    private sealed class SideTracker
    {
        public readonly List<PlayedCardRecord> Played = new List<PlayedCardRecord>();
        public readonly HashSet<int> SeenGuids = new HashSet<int>();
        public int LastMinTotal;
        public int LastMaxTotal;
        public bool Initialized;

        public void Reset(int initialValue)
        {
            Played.Clear();
            SeenGuids.Clear();
            LastMinTotal = initialValue;
            LastMaxTotal = initialValue;
            Initialized = true;
        }

        public void Clear()
        {
            Played.Clear();
            SeenGuids.Clear();
            LastMinTotal = 0;
            LastMaxTotal = 0;
            Initialized = false;
        }
    }

    private static BattleCardPointHint _instance;
    private const float RefreshIntervalSeconds = 0.05f;

    private bool _enabled;
    private float _atkX = XingShuConfigKeys.BattleCardPointAtkXDefault;
    private float _atkY = XingShuConfigKeys.BattleCardPointAtkYDefault;
    private float _defX = XingShuConfigKeys.BattleCardPointDefXDefault;
    private float _defY = XingShuConfigKeys.BattleCardPointDefYDefault;
    private float _scale = XingShuConfigKeys.BattleCardPointScaleDefault;
    private bool _locked = XingShuConfigKeys.BattleCardPointLockedDefault;
    private bool _draggingAtk;
    private bool _draggingDef;

    private float _nextTick;
    private long _battleId;
    private long _attackerId;
    private long _defenderId;
    private FightWindow _attachedWindow;

    private readonly Dictionary<long, Dictionary<int, CardInfoConfigure>> _cardCache =
        new Dictionary<long, Dictionary<int, CardInfoConfigure>>();

    private readonly SideTracker _atkTracker = new SideTracker();
    private readonly SideTracker _defTracker = new SideTracker();

    private GComponent _atkPanel;
    private GGraph _atkBg;
    private GTextField _atkLabel;
    private string _atkLastText = "";
    private float _atkWidth = 260f;
    private float _atkHeight = 120f;

    private GComponent _defPanel;
    private GGraph _defBg;
    private GTextField _defLabel;
    private string _defLastText = "";
    private float _defWidth = 260f;
    private float _defHeight = 120f;

    internal static bool IsEnabled => _instance != null && _instance._enabled;
    internal static float AtkX => _instance != null ? _instance._atkX
        : FloatingHintLayoutMath.FiniteClamp(XingShuConfigService.GetFloat(XingShuConfigKeys.BattleCardPointAtkX, XingShuConfigKeys.BattleCardPointAtkXDefault), 0f, 1f, XingShuConfigKeys.BattleCardPointAtkXDefault);
    internal static float AtkY => _instance != null ? _instance._atkY
        : FloatingHintLayoutMath.FiniteClamp(XingShuConfigService.GetFloat(XingShuConfigKeys.BattleCardPointAtkY, XingShuConfigKeys.BattleCardPointAtkYDefault), 0f, 1f, XingShuConfigKeys.BattleCardPointAtkYDefault);
    internal static float DefX => _instance != null ? _instance._defX
        : FloatingHintLayoutMath.FiniteClamp(XingShuConfigService.GetFloat(XingShuConfigKeys.BattleCardPointDefX, XingShuConfigKeys.BattleCardPointDefXDefault), 0f, 1f, XingShuConfigKeys.BattleCardPointDefXDefault);
    internal static float DefY => _instance != null ? _instance._defY
        : FloatingHintLayoutMath.FiniteClamp(XingShuConfigService.GetFloat(XingShuConfigKeys.BattleCardPointDefY, XingShuConfigKeys.BattleCardPointDefYDefault), 0f, 1f, XingShuConfigKeys.BattleCardPointDefYDefault);
    internal static float Scale => _instance != null ? _instance._scale
        : FloatingHintLayoutMath.FiniteClamp(XingShuConfigService.GetFloat(XingShuConfigKeys.BattleCardPointScale, XingShuConfigKeys.BattleCardPointScaleDefault), XingShuConfigKeys.BattleCardPointScaleMin, XingShuConfigKeys.BattleCardPointScaleMax, XingShuConfigKeys.BattleCardPointScaleDefault);
    internal static bool Locked => _instance != null ? _instance._locked
        : XingShuConfigService.GetBool(XingShuConfigKeys.BattleCardPointLocked, XingShuConfigKeys.BattleCardPointLockedDefault);

    internal static void SetEnabled(bool value)
    {
        if (_instance == null)
        {
            XingShuConfigService.SetBool(XingShuConfigKeys.BattleCardPointHintEnabled, value);
            XingShuConfigService.Save();
            return;
        }
        _instance._enabled = value;
        XingShuConfigService.SetBool(XingShuConfigKeys.BattleCardPointHintEnabled, value);
        XingShuConfigService.Save();
        if (!value)
        {
            _instance.ClearAll();
        }
        else
        {
            _instance._nextTick = 0f;
        }
    }

    internal static void SetMirrorMarginX(float marginNorm)
    {
        float clamped = FloatingHintLayoutMath.FiniteClamp(marginNorm, 0f, 0.45f, XingShuConfigKeys.BattleCardPointAtkXDefault);
        float defX = 1f - clamped;
        XingShuConfigService.SetFloat(XingShuConfigKeys.BattleCardPointAtkX, clamped);
        XingShuConfigService.SetFloat(XingShuConfigKeys.BattleCardPointDefX, defX);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._atkX = clamped;
            _instance._defX = defX;
            _instance.RefreshTransformsOnly();
        }
    }

    internal static void SetUnifiedY(float yNorm)
    {
        float clamped = FloatingHintLayoutMath.FiniteClamp(yNorm, 0f, 1f, XingShuConfigKeys.BattleCardPointAtkYDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.BattleCardPointAtkY, clamped);
        XingShuConfigService.SetFloat(XingShuConfigKeys.BattleCardPointDefY, clamped);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._atkY = clamped;
            _instance._defY = clamped;
            _instance.RefreshTransformsOnly();
        }
    }

    internal static void SetScale(float scale)
    {
        float clamped = FloatingHintLayoutMath.FiniteClamp(
            scale,
            XingShuConfigKeys.BattleCardPointScaleMin,
            XingShuConfigKeys.BattleCardPointScaleMax,
            XingShuConfigKeys.BattleCardPointScaleDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.BattleCardPointScale, clamped);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._scale = clamped;
            _instance.RefreshTransformsOnly();
        }
    }

    internal static void SetLocked(bool locked)
    {
        XingShuConfigService.SetBool(XingShuConfigKeys.BattleCardPointLocked, locked);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._locked = locked;
            _instance._draggingAtk = false;
            _instance._draggingDef = false;
            _instance.RefreshInteractionStates();
            _instance._nextTick = 0f;
        }
    }

    internal static void ResetLayout()
    {
        XingShuConfigService.SetFloat(XingShuConfigKeys.BattleCardPointAtkX, XingShuConfigKeys.BattleCardPointAtkXDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.BattleCardPointAtkY, XingShuConfigKeys.BattleCardPointAtkYDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.BattleCardPointDefX, XingShuConfigKeys.BattleCardPointDefXDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.BattleCardPointDefY, XingShuConfigKeys.BattleCardPointDefYDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.BattleCardPointScale, XingShuConfigKeys.BattleCardPointScaleDefault);
        XingShuConfigService.SetBool(XingShuConfigKeys.BattleCardPointLocked, XingShuConfigKeys.BattleCardPointLockedDefault);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._atkX = XingShuConfigKeys.BattleCardPointAtkXDefault;
            _instance._atkY = XingShuConfigKeys.BattleCardPointAtkYDefault;
            _instance._defX = XingShuConfigKeys.BattleCardPointDefXDefault;
            _instance._defY = XingShuConfigKeys.BattleCardPointDefYDefault;
            _instance._scale = XingShuConfigKeys.BattleCardPointScaleDefault;
            _instance._locked = XingShuConfigKeys.BattleCardPointLockedDefault;
            _instance._draggingAtk = false;
            _instance._draggingDef = false;
            _instance.RefreshInteractionStates();
            _instance.RefreshTransformsOnly();
            _instance._nextTick = 0f;
        }
    }

    public static void Install(GameObject host)
    {
        if (_instance != null || host == null) return;
        _instance = host.AddComponent<BattleCardPointHint>();
        _instance.LoadConfig();
    }

    private void LoadConfig()
    {
        _enabled = XingShuConfigService.GetBool(
            XingShuConfigKeys.BattleCardPointHintEnabled,
            XingShuConfigKeys.BattleCardPointHintEnabledDefault);
        _atkX = FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.BattleCardPointAtkX, XingShuConfigKeys.BattleCardPointAtkXDefault),
            0f, 1f, XingShuConfigKeys.BattleCardPointAtkXDefault);
        _atkY = FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.BattleCardPointAtkY, XingShuConfigKeys.BattleCardPointAtkYDefault),
            0f, 1f, XingShuConfigKeys.BattleCardPointAtkYDefault);
        _defX = FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.BattleCardPointDefX, XingShuConfigKeys.BattleCardPointDefXDefault),
            0f, 1f, XingShuConfigKeys.BattleCardPointDefXDefault);
        _defY = FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.BattleCardPointDefY, XingShuConfigKeys.BattleCardPointDefYDefault),
            0f, 1f, XingShuConfigKeys.BattleCardPointDefYDefault);
        _scale = FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.BattleCardPointScale, XingShuConfigKeys.BattleCardPointScaleDefault),
            XingShuConfigKeys.BattleCardPointScaleMin, XingShuConfigKeys.BattleCardPointScaleMax, XingShuConfigKeys.BattleCardPointScaleDefault);
        _locked = XingShuConfigService.GetBool(
            XingShuConfigKeys.BattleCardPointLocked,
            XingShuConfigKeys.BattleCardPointLockedDefault);
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
            ClearAll();
        }
    }

    private void Tick()
    {
        if (!_enabled
            || !SimpleSingletonProvider<SceneManager>.hasInstance
            || SimpleSingletonProvider<SceneManager>.inst.currentType == null
            || SimpleSingletonProvider<SceneManager>.inst.currentType.Value != SceneType.Battle
            || !SimpleSingletonProvider<GameLogicManager>.hasInstance
            || !SimpleSingletonProvider<UIManager>.hasInstance)
        {
            ClearAll();
            return;
        }

        var logic = SimpleSingletonProvider<GameLogicManager>.inst;
        if (logic.battle?.PlayerDatas == null)
        {
            ClearAll();
            return;
        }

        // Always cache hand cards during battle before they leave _HandCardData on play
        foreach (var player in logic.battle.PlayerDatas)
        {
            CacheCards(player);
        }

        var fight = logic.fight?.battleFightData;
        var window = ShownNativeWindows.Find<FightWindow>(SimpleSingletonProvider<UIManager>.inst);
        if (fight == null || fight.attackerInfo == null || fight.defenderInfo == null
            || window == null || window.isDisposed || !window.isShowing
            || window.width < 480f || window.height < 400f)
        {
            ClearSession();
            return;
        }

        if (_battleId != fight.battleId
            || _attackerId != fight.attackerInfo.PlayerId
            || _defenderId != fight.defenderInfo.PlayerId)
        {
            _battleId = fight.battleId;
            _attackerId = fight.attackerInfo.PlayerId;
            _defenderId = fight.defenderInfo.PlayerId;
            _atkTracker.Reset(fight.attackerInfo.InitAtk);
            _defTracker.Reset(fight.defenderInfo.InitDef);
            RemovePanels();
        }

        UpdateSideTracker(_atkTracker, fight.attackerInfo, isAttack: true);
        UpdateSideTracker(_defTracker, fight.defenderInfo, isAttack: false);

        string atkHeader = _locked ? "攻方卡牌点数" : "攻方卡牌点数 · 可拖动";
        string atkText = BuildSideText(atkHeader, _atkTracker, fight.attackerInfo);
        if (!string.IsNullOrEmpty(atkText))
        {
            RenderSidePanel(window, isAttacker: true, atkText);
        }
        else
        {
            RemoveSidePanel(isAttacker: true);
        }

        string defHeader = _locked ? "防方卡牌点数" : "防方卡牌点数 · 可拖动";
        string defText = BuildSideText(defHeader, _defTracker, fight.defenderInfo);
        if (!string.IsNullOrEmpty(defText))
        {
            RenderSidePanel(window, isAttacker: false, defText);
        }
        else
        {
            RemoveSidePanel(isAttacker: false);
        }
    }

    private void CacheCards(BattlePlayerData player)
    {
        if (player?.player == null || player.cardContainer?._HandCardData == null) return;
        long pid = player.player.Id;
        if (!_cardCache.TryGetValue(pid, out var dict))
        {
            dict = new Dictionary<int, CardInfoConfigure>();
            _cardCache[pid] = dict;
        }
        foreach (var kv in player.cardContainer._HandCardData)
        {
            if (kv.Value?.Config != null)
            {
                dict[kv.Key] = kv.Value.Config;
            }
        }
    }

    private void UpdateSideTracker(SideTracker tracker, BattleRole role, bool isAttack)
    {
        if (role == null) return;
        int curMin = isAttack ? role.MinAtk : role.MinDef;
        int curMax = isAttack ? role.MaxAtk : role.MaxDef;

        if (!tracker.Initialized)
        {
            tracker.Reset(isAttack ? role.InitAtk : role.InitDef);
        }

        if (role.UseCards != null && role.UseCards.Count > 0)
        {
            var newGuids = new List<int>();
            for (int i = 0; i < role.UseCards.Count; i++)
            {
                int guid = role.UseCards[i];
                if (!tracker.SeenGuids.Contains(guid))
                {
                    tracker.SeenGuids.Add(guid);
                    newGuids.Add(guid);
                }
            }

            if (newGuids.Count > 0)
            {
                int totalDeltaMin = curMin - tracker.LastMinTotal;
                int totalDeltaMax = curMax - tracker.LastMaxTotal;

                for (int i = 0; i < newGuids.Count; i++)
                {
                    int guid = newGuids[i];
                    CardInfoConfigure cfg = ResolveCardConfig(role.PlayerId, guid);
                    int minD = 0;
                    int maxD = 0;

                    if (newGuids.Count == 1 && totalDeltaMin >= 0 && totalDeltaMax >= totalDeltaMin && (totalDeltaMin > 0 || totalDeltaMax > 0))
                    {
                        minD = totalDeltaMin;
                        maxD = totalDeltaMax;
                    }
                    else if (cfg != null && cfg.Params != null && cfg.Params.Count >= 2 && cfg.Params[0] <= cfg.Params[1])
                    {
                        minD = cfg.Params[0];
                        maxD = cfg.Params[1];
                    }
                    else if (cfg != null && cfg.Params != null && cfg.Params.Count == 1)
                    {
                        minD = cfg.Params[0];
                        maxD = cfg.Params[0];
                    }

                    tracker.Played.Add(new PlayedCardRecord
                    {
                        Guid = guid,
                        CardId = cfg != null ? cfg.Id : 0,
                        Name = ResolveCardName(cfg, tracker.Played.Count + 1),
                        MinDelta = minD,
                        MaxDelta = maxD,
                    });
                }
            }
        }

        tracker.LastMinTotal = curMin;
        tracker.LastMaxTotal = curMax;
    }

    private CardInfoConfigure ResolveCardConfig(long playerId, int guid)
    {
        if (_cardCache.TryGetValue(playerId, out var dict) && dict.TryGetValue(guid, out var cfg))
        {
            return cfg;
        }
        try
        {
            var direct = guid.GetCardConfigure();
            if (direct != null) return direct;
        }
        catch { }
        return null;
    }

    private static string ResolveCardName(CardInfoConfigure cfg, int index)
    {
        if (cfg != null && cfg.NameID > 0)
        {
            try
            {
                string localized = cfg.NameID.GetLocal(UIStringType.Card);
                if (!string.IsNullOrEmpty(localized)) return localized;
            }
            catch { }
        }
        return "战斗牌 #" + index;
    }

    private static string ResolveCardNameById(int cardId, int index)
    {
        if (cardId > 0)
        {
            try
            {
                var cfg = cardId.GetCardConfigure();
                if (cfg != null && cfg.NameID > 0)
                {
                    string localized = cfg.NameID.GetLocal(UIStringType.Card);
                    if (!string.IsNullOrEmpty(localized)) return localized;
                }
            }
            catch { }
        }
        return "战斗牌 #" + index;
    }

    private static string BuildSideText(string header, SideTracker tracker, BattleRole role)
    {
        bool hasRevealed = role.CardCombatBonus != null && role.CardCombatBonus.Count > 0;
        if (!hasRevealed && tracker.Played.Count == 0 && (role.UseCards == null || role.UseCards.Count == 0))
        {
            return null;
        }

        var sb = new StringBuilder();
        sb.Append("[b][color=#FF77B4]").Append(header).Append("[/color][/b]");

        if (hasRevealed)
        {
            int sum = 0;
            for (int i = 0; i < role.CardCombatBonus.Count; i++)
            {
                var cb = role.CardCombatBonus[i];
                string cname = ResolveCardNameById(cb.CardId, i + 1);
                string pts = cb.CardBonus >= 0 ? "+" + cb.CardBonus : cb.CardBonus.ToString();
                sum += cb.CardBonus;
                sb.Append('\n').Append(cname).Append("  [b][color=#FFC832]").Append(pts).Append("[/color][/b]");
            }
            string sumStr = sum >= 0 ? "+" + sum : sum.ToString();
            sb.Append("\n[color=#9BD1FF]卡牌总点数：[b]").Append(sumStr).Append("[/b][/color]");
            return sb.ToString();
        }

        int minSum = 0;
        int maxSum = 0;
        bool hasAnyEstimate = false;
        for (int i = 0; i < tracker.Played.Count; i++)
        {
            var rec = tracker.Played[i];
            minSum += rec.MinDelta;
            maxSum += rec.MaxDelta;
            sb.Append('\n').Append(rec.Name).Append("  ");
            if (rec.MinDelta == rec.MaxDelta)
            {
                if (rec.MinDelta != 0)
                {
                    hasAnyEstimate = true;
                    string pts = rec.MinDelta > 0 ? "+" + rec.MinDelta : rec.MinDelta.ToString();
                    sb.Append("[b][color=#FFC832]").Append(pts).Append("[/color][/b]");
                }
                else
                {
                    sb.Append("[color=#A9B6CE]已打出[/color]");
                }
            }
            else
            {
                hasAnyEstimate = true;
                sb.Append("[b][color=#7EC8FF]+").Append(rec.MinDelta).Append('~').Append(rec.MaxDelta).Append("[/color][/b]");
            }
        }

        if (hasAnyEstimate)
        {
            if (minSum == maxSum)
            {
                sb.Append("\n[color=#9BD1FF]当前累计：[b]+").Append(minSum).Append("[/b][/color]");
            }
            else
            {
                sb.Append("\n[color=#9BD1FF]待揭示区间：[b]+").Append(minSum).Append('~').Append(maxSum).Append("[/b][/color]");
            }
        }
        else
        {
            sb.Append("\n[color=#A9B6CE]等待服务器揭示点数[/color]");
        }

        return sb.ToString();
    }

    private void RenderSidePanel(FightWindow window, bool isAttacker, string ubbText)
    {
        _attachedWindow = window;
        GComponent panel = isAttacker ? _atkPanel : _defPanel;
        GGraph bg = isAttacker ? _atkBg : _defBg;
        GTextField label = isAttacker ? _atkLabel : _defLabel;
        string lastText = isAttacker ? _atkLastText : _defLastText;

        if (panel == null || panel.isDisposed || panel.parent != window || bg == null || label == null || label.isDisposed)
        {
            RemoveSidePanel(isAttacker);
            panel = new GComponent
            {
                name = isAttacker ? "XINGSHU_BattleCardPoints_Atk" : "XINGSHU_BattleCardPoints_Def",
                touchable = !_locked,
                opaque = !_locked,
                draggable = !_locked,
            };
            panel.SetPivot(0f, 0f, false);
            bool capturedIsAttacker = isAttacker;
            panel.onDragStart.Set((EventCallback0)delegate
            {
                if (capturedIsAttacker) _draggingAtk = true;
                else _draggingDef = true;
            });
            panel.onDragMove.Set((EventCallback0)delegate
            {
                ClampSidePanelBounds(capturedIsAttacker);
            });
            panel.onDragEnd.Set((EventCallback0)delegate
            {
                CommitSideDrag(capturedIsAttacker);
            });

            bg = new GGraph { touchable = false };
            panel.AddChild(bg);

            label = new GTextField
            {
                name = isAttacker ? "XINGSHU_BattleCardPointsLabel_Atk" : "XINGSHU_BattleCardPointsLabel_Def",
                touchable = false,
                autoSize = AutoSizeType.Height,
                align = AlignType.Left,
                UBBEnabled = true,
                stroke = 1.2f,
                strokeColor = new Color(0f, 0f, 0f, 0.9f),
            };
            var tf = label.textFormat;
            tf.size = Application.isMobilePlatform ? 24 : 20;
            tf.lineSpacing = 5;
            tf.color = new Color(0.95f, 0.97f, 1f, 1f);
            label.textFormat = tf;
            panel.AddChild(label);
            window.AddChild(panel);

            if (isAttacker) { _atkPanel = panel; _atkBg = bg; _atkLabel = label; }
            else { _defPanel = panel; _defBg = bg; _defLabel = label; }
            lastText = "";
        }

        float width = Mathf.Clamp(window.width * 0.21f, 240f, 340f);
        label.SetSize(width - 24f, label.height);
        label.SetXY(12f, 10f);
        if (lastText != ubbText)
        {
            label.text = ubbText;
            if (isAttacker) _atkLastText = ubbText;
            else _defLastText = ubbText;
        }

        float height = label.height + 20f;
        if (isAttacker) { _atkWidth = width; _atkHeight = height; }
        else { _defWidth = width; _defHeight = height; }

        panel.SetSize(width, height);
        bg.DrawRoundRect(width, height, new Color(0.055f, 0.068f, 0.11f, 0.86f), new[] { 10f, 10f, 10f, 10f });
        RefreshSideInteraction(panel);
        RefreshSideTransform(isAttacker);
    }

    private void RefreshSideInteraction(GComponent panel)
    {
        if (panel == null || panel.isDisposed) return;
        panel.touchable = !_locked;
        panel.opaque = !_locked;
        panel.draggable = !_locked;
    }

    private void RefreshInteractionStates()
    {
        RefreshSideInteraction(_atkPanel);
        RefreshSideInteraction(_defPanel);
    }

    private void RefreshTransformsOnly()
    {
        RefreshSideTransform(isAttacker: true);
        RefreshSideTransform(isAttacker: false);
    }

    private void RefreshSideTransform(bool isAttacker)
    {
        GComponent panel = isAttacker ? _atkPanel : _defPanel;
        if (panel == null || panel.isDisposed || _attachedWindow == null || _attachedWindow.isDisposed) return;
        panel.SetScale(_scale, _scale);
        bool dragging = isAttacker ? _draggingAtk : _draggingDef;
        if (dragging) return;
        float w = isAttacker ? _atkWidth : _defWidth;
        float h = isAttacker ? _atkHeight : _defHeight;
        float nx = isAttacker ? _atkX : _defX;
        float ny = isAttacker ? _atkY : _defY;
        FloatingHintLayoutMath.ComputeClampedXY(
            _attachedWindow.width, _attachedWindow.height,
            w, h,
            _scale, nx, ny,
            FloatingHintLayoutMath.DefaultScreenMargin,
            out float posX, out float posY);
        panel.SetXY(posX, posY);
    }

    private void ClampSidePanelBounds(bool isAttacker)
    {
        GComponent panel = isAttacker ? _atkPanel : _defPanel;
        if (panel == null || panel.isDisposed || _attachedWindow == null || _attachedWindow.isDisposed) return;
        float w = isAttacker ? _atkWidth : _defWidth;
        float h = isAttacker ? _atkHeight : _defHeight;
        FloatingHintLayoutMath.ClampXYToBounds(
            _attachedWindow.width, _attachedWindow.height,
            w, h,
            _scale, panel.x, panel.y,
            FloatingHintLayoutMath.DefaultScreenMargin,
            out float cx, out float cy);
        panel.SetXY(cx, cy);
    }

    private void CommitSideDrag(bool isAttacker)
    {
        if (isAttacker) _draggingAtk = false;
        else _draggingDef = false;
        GComponent panel = isAttacker ? _atkPanel : _defPanel;
        if (panel == null || panel.isDisposed || _attachedWindow == null || _attachedWindow.isDisposed) return;
        ClampSidePanelBounds(isAttacker);
        float w = isAttacker ? _atkWidth : _defWidth;
        float h = isAttacker ? _atkHeight : _defHeight;
        FloatingHintLayoutMath.ComputeNormalizedFromXY(
            _attachedWindow.width, _attachedWindow.height,
            w, h,
            _scale, panel.x, panel.y,
            FloatingHintLayoutMath.DefaultScreenMargin,
            out float nx, out float ny);
        if (isAttacker)
        {
            _atkX = nx;
            _atkY = ny;
            XingShuConfigService.SetFloat(XingShuConfigKeys.BattleCardPointAtkX, nx);
            XingShuConfigService.SetFloat(XingShuConfigKeys.BattleCardPointAtkY, ny);
        }
        else
        {
            _defX = nx;
            _defY = ny;
            XingShuConfigService.SetFloat(XingShuConfigKeys.BattleCardPointDefX, nx);
            XingShuConfigService.SetFloat(XingShuConfigKeys.BattleCardPointDefY, ny);
        }
        XingShuConfigService.Save();
    }

    private void RemoveSidePanel(bool isAttacker)
    {
        if (isAttacker)
        {
            _draggingAtk = false;
            if (_atkPanel != null && !_atkPanel.isDisposed) _atkPanel.Dispose();
            _atkPanel = null;
            _atkBg = null;
            _atkLabel = null;
            _atkLastText = "";
        }
        else
        {
            _draggingDef = false;
            if (_defPanel != null && !_defPanel.isDisposed) _defPanel.Dispose();
            _defPanel = null;
            _defBg = null;
            _defLabel = null;
            _defLastText = "";
        }
    }

    private void RemovePanels()
    {
        RemoveSidePanel(isAttacker: true);
        RemoveSidePanel(isAttacker: false);
        _attachedWindow = null;
    }

    private void ClearSession()
    {
        RemovePanels();
        _atkTracker.Clear();
        _defTracker.Clear();
        _battleId = 0L;
        _attackerId = 0L;
        _defenderId = 0L;
    }

    private void ClearAll()
    {
        ClearSession();
        _cardCache.Clear();
    }

    private void OnDestroy()
    {
        ClearAll();
        if (_instance == this) _instance = null;
    }
}

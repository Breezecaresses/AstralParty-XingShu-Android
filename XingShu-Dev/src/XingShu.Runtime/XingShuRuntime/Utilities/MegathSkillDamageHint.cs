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
using XingShuRuntime.Core;

namespace XingShuRuntime.Utilities;

/// <summary>
/// Standalone board HUD module displaying Megath's active skill ("轨道轰炸") theoretical max damage.
/// Strictly visible ONLY when the local player is playing Megath (HeroId == 122) in an active battle.
/// </summary>
public sealed class MegathSkillDamageHint : MonoBehaviour
{
    private const float RefreshIntervalSeconds = 0.15f;

    private static MegathSkillDamageHint _instance;

    private readonly List<int> _costsBuffer = new List<int>(16);

    private bool _enabled = XingShuConfigKeys.MegathDamageHintEnabledDefault;
    private float _posX = XingShuConfigKeys.MegathDamageHintPosXDefault;
    private float _posY = XingShuConfigKeys.MegathDamageHintPosYDefault;
    private float _scale = XingShuConfigKeys.MegathDamageHintScaleDefault;
    private bool _locked = XingShuConfigKeys.MegathDamageHintLockedDefault;
    private float _nextTick;

    private GComponent _attachedContainer;
    private GComponent _panel;
    private GGraph _background;
    private GGraph _accentBar;
    private GTextField _titleLabel;
    private GTextField _formulaLabel;
    private float _panelWidth = 240f;
    private float _panelHeight = 64f;
    private string _lastTitle = "";
    private string _lastFormula = "";
    private bool _dragging;

    internal static bool IsEnabled => _instance != null
        ? _instance._enabled
        : XingShuConfigService.GetBool(XingShuConfigKeys.MegathDamageHintEnabled, XingShuConfigKeys.MegathDamageHintEnabledDefault);

    internal static float PosX => _instance != null
        ? _instance._posX
        : FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.MegathDamageHintPosX, XingShuConfigKeys.MegathDamageHintPosXDefault),
            0f, 1f, XingShuConfigKeys.MegathDamageHintPosXDefault);

    internal static float PosY => _instance != null
        ? _instance._posY
        : FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.MegathDamageHintPosY, XingShuConfigKeys.MegathDamageHintPosYDefault),
            0f, 1f, XingShuConfigKeys.MegathDamageHintPosYDefault);

    internal static float Scale => _instance != null
        ? _instance._scale
        : FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.MegathDamageHintScale, XingShuConfigKeys.MegathDamageHintScaleDefault),
            XingShuConfigKeys.MegathDamageHintScaleMin, XingShuConfigKeys.MegathDamageHintScaleMax, XingShuConfigKeys.MegathDamageHintScaleDefault);

    internal static bool Locked => _instance != null
        ? _instance._locked
        : XingShuConfigService.GetBool(XingShuConfigKeys.MegathDamageHintLocked, XingShuConfigKeys.MegathDamageHintLockedDefault);

    internal static void SetEnabled(bool value)
    {
        XingShuConfigService.SetBool(XingShuConfigKeys.MegathDamageHintEnabled, value);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._enabled = value;
            _instance._nextTick = 0f;
            if (!value) _instance.RemovePanel();
        }
    }

    internal static void SetPosition(float normX, float normY)
    {
        float x = FloatingHintLayoutMath.FiniteClamp(normX, 0f, 1f, XingShuConfigKeys.MegathDamageHintPosXDefault);
        float y = FloatingHintLayoutMath.FiniteClamp(normY, 0f, 1f, XingShuConfigKeys.MegathDamageHintPosYDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.MegathDamageHintPosX, x);
        XingShuConfigService.SetFloat(XingShuConfigKeys.MegathDamageHintPosY, y);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._posX = x;
            _instance._posY = y;
            _instance.RefreshTransformOnly();
        }
    }

    internal static void SetScale(float scale)
    {
        float clamped = FloatingHintLayoutMath.FiniteClamp(
            scale, XingShuConfigKeys.MegathDamageHintScaleMin, XingShuConfigKeys.MegathDamageHintScaleMax, XingShuConfigKeys.MegathDamageHintScaleDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.MegathDamageHintScale, clamped);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._scale = clamped;
            _instance.RefreshTransformOnly();
        }
    }

    internal static void SetLocked(bool locked)
    {
        XingShuConfigService.SetBool(XingShuConfigKeys.MegathDamageHintLocked, locked);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._locked = locked;
            _instance._lastTitle = "";
            _instance.RefreshInteractionState();
            _instance._nextTick = 0f;
        }
    }

    internal static void ResetLayout()
    {
        XingShuConfigService.SetFloat(XingShuConfigKeys.MegathDamageHintPosX, XingShuConfigKeys.MegathDamageHintPosXDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.MegathDamageHintPosY, XingShuConfigKeys.MegathDamageHintPosYDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.MegathDamageHintScale, XingShuConfigKeys.MegathDamageHintScaleDefault);
        XingShuConfigService.SetBool(XingShuConfigKeys.MegathDamageHintLocked, XingShuConfigKeys.MegathDamageHintLockedDefault);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._posX = XingShuConfigKeys.MegathDamageHintPosXDefault;
            _instance._posY = XingShuConfigKeys.MegathDamageHintPosYDefault;
            _instance._scale = XingShuConfigKeys.MegathDamageHintScaleDefault;
            _instance._locked = XingShuConfigKeys.MegathDamageHintLockedDefault;
            _instance._dragging = false;
            _instance._lastTitle = "";
            _instance.RefreshInteractionState();
            _instance.RefreshTransformOnly();
            _instance._nextTick = 0f;
        }
    }

    public static void Install(GameObject host)
    {
        if (_instance != null || host == null) return;
        _instance = host.AddComponent<MegathSkillDamageHint>();
        _instance.LoadConfig();
    }

    private void LoadConfig()
    {
        _enabled = XingShuConfigService.GetBool(XingShuConfigKeys.MegathDamageHintEnabled, XingShuConfigKeys.MegathDamageHintEnabledDefault);
        _posX = FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.MegathDamageHintPosX, XingShuConfigKeys.MegathDamageHintPosXDefault),
            0f, 1f, XingShuConfigKeys.MegathDamageHintPosXDefault);
        _posY = FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.MegathDamageHintPosY, XingShuConfigKeys.MegathDamageHintPosYDefault),
            0f, 1f, XingShuConfigKeys.MegathDamageHintPosYDefault);
        _scale = FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.MegathDamageHintScale, XingShuConfigKeys.MegathDamageHintScaleDefault),
            XingShuConfigKeys.MegathDamageHintScaleMin, XingShuConfigKeys.MegathDamageHintScaleMax, XingShuConfigKeys.MegathDamageHintScaleDefault);
        _locked = XingShuConfigService.GetBool(XingShuConfigKeys.MegathDamageHintLocked, XingShuConfigKeys.MegathDamageHintLockedDefault);
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
            RemovePanel();
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
            RemovePanel();
            return;
        }

        var uiMgr = SimpleSingletonProvider<UIManager>.inst;
        var logic = SimpleSingletonProvider<GameLogicManager>.inst;
        var fight = logic.fight?.battleFightData;
        if ((fight != null && !fight.isEnd) || ShownNativeWindows.Find<FightWindow>(uiMgr) != null)
        {
            RemovePanel();
            return;
        }

        var self = logic.battle?.GetSelfPlayerData();
        if (self == null
            || self.player?.Hero == null
            || self.characterType != CharacterType.Hero
            || self.player.Hero.HeroId != MegathSkillDamageMath.MegathHeroId)
        {
            RemovePanel();
            return;
        }

        var battleInfoUi = logic.battle?.battleInfo?.UIObject;
        if (battleInfoUi == null || battleInfoUi.isDisposed || !battleInfoUi.visible || !battleInfoUi.onStage
            || battleInfoUi.width < 320f || battleInfoUi.height < 240f)
        {
            RemovePanel();
            return;
        }

        int skillId = self.player.GetBattleActiveSkillId();
        if (skillId <= 0)
        {
            skillId = HandCardDisplayManager.IsPVEMode() ? MegathSkillDamageMath.PveSkillId : MegathSkillDamageMath.PvpSkillId;
        }

        SkillInfoConfigure skillCfg = null;
        if (StaticConfigure.Skill?.InfoDict != null)
        {
            StaticConfigure.Skill.InfoDict.TryGetValue(skillId, out skillCfg);
        }

        _costsBuffer.Clear();
        var handCards = self.cardContainer?._HandCards;
        if (handCards != null)
        {
            for (int i = 0; i < handCards.Count; i++)
            {
                var hc = handCards[i];
                if (hc == null) continue;
                int c = hc.BattleCost >= 0 ? hc.BattleCost : (hc.Config != null ? hc.Config.Cost : 0);
                _costsBuffer.Add(Math.Max(0, c));
            }
        }

        bool hasPrecisionMarkedMonster = HasAliveMonsterWithBuff(
            logic.battle?.PlayerDatas,
            MegathSkillDamageMath.PrecisionStrikeBuffId);

        var summary = MegathSkillDamageMath.Compute(
            skillId,
            HandCardDisplayManager.IsPVEMode(),
            skillCfg?.Params,
            _costsBuffer,
            hasPrecisionMarkedMonster);

        int maxCd = skillCfg != null ? skillCfg.Round : 3;
        int curCd = self.Property?.activeSkillCD != null ? self.Property.activeSkillCD.Value : self.SkillCD;
        string cdSuffix = curCd > 0 ? $" [color=#9FB2C9](CD {curCd}/{Math.Max(curCd, maxCd)})[/color]" : "";
        string dragTag = _locked ? "" : " [color=#A6B8CE]· 拖动[/color]";
        string title = $"[b]梅加斯 理论伤害 [color=#FFD54F]{summary.TotalDamage}[/color][/b]{cdSuffix}{dragTag}";
        string formula = MegathSkillDamageMath.FormatFormulaLine(summary);

        EnsurePanel(battleInfoUi);
        ApplyLayout(battleInfoUi, title, formula);
    }

    private static bool HasAliveMonsterWithBuff(List<BattlePlayerData> players, int buffId)
    {
        if (players == null) return false;
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null || p.characterType != CharacterType.Monster) continue;
            if (p.Property?.HP == null || p.Property.HP.Value <= 0) continue;
            var dict = p.buffContainer?._buffDict;
            if (dict == null || dict.Count == 0) continue;
            foreach (var buff in dict.Values)
            {
                if (buff != null && buff.BuffId == buffId)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void EnsurePanel(GComponent container)
    {
        if (_panel != null && !_panel.isDisposed && _panel.parent == container && _attachedContainer == container
            && _background != null && _titleLabel != null && _formulaLabel != null)
        {
            return;
        }

        RemovePanel();
        _attachedContainer = container;

        _panel = new GComponent
        {
            name = "XINGSHU_MegathSkillDamagePanel",
            touchable = !_locked,
            opaque = !_locked,
            draggable = !_locked,
            sortingOrder = 0,
        };
        XingShuOverlayInput.Protect(_panel);
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
            name = "XINGSHU_MegathDmgTitle",
            touchable = false,
            autoSize = AutoSizeType.Both,
            align = AlignType.Left,
            UBBEnabled = true,
            stroke = 1.2f,
            strokeColor = new Color(0f, 0f, 0f, 0.92f),
        };
        var tfTitle = _titleLabel.textFormat;
        tfTitle.size = Application.isMobilePlatform ? 22 : 18;
        tfTitle.bold = true;
        tfTitle.color = new Color(0.97f, 0.98f, 1f, 1f);
        _titleLabel.textFormat = tfTitle;
        _panel.AddChild(_titleLabel);

        _formulaLabel = new GTextField
        {
            name = "XINGSHU_MegathDmgFormula",
            touchable = false,
            autoSize = AutoSizeType.Both,
            align = AlignType.Left,
            UBBEnabled = true,
            stroke = 1f,
            strokeColor = new Color(0f, 0f, 0f, 0.88f),
        };
        var tfFormula = _formulaLabel.textFormat;
        tfFormula.size = Application.isMobilePlatform ? 20 : 16;
        tfFormula.color = new Color(1f, 0.88f, 0.48f, 1f);
        _formulaLabel.textFormat = tfFormula;
        _panel.AddChild(_formulaLabel);

        container.AddChild(_panel);
    }

    private void ApplyLayout(GComponent container, string title, string formula)
    {
        bool changed = false;
        if (_lastTitle != title)
        {
            _lastTitle = title;
            _titleLabel.text = title;
            changed = true;
        }
        if (_lastFormula != formula)
        {
            _lastFormula = formula;
            _formulaLabel.text = formula;
            changed = true;
        }

        if (changed)
        {
            _titleLabel.SetXY(14f, 8f);
            float contentW = Mathf.Max(_titleLabel.width, _formulaLabel.width);
            float minW = Application.isMobilePlatform ? 230f : 196f;
            float width = Mathf.Clamp(contentW + 26f, minW, Mathf.Max(minW, container.width * 0.42f));
            _formulaLabel.SetXY(14f, 8f + _titleLabel.height + 3f);
            float height = 8f + _titleLabel.height + 3f + _formulaLabel.height + 10f;

            _panelWidth = width;
            _panelHeight = height;
            _panel.SetSize(width, height);
            _background.DrawRoundRect(width, height, new Color(0.055f, 0.068f, 0.11f, 0.88f), new[] { 9f, 9f, 9f, 9f });
            _accentBar.DrawRoundRect(4f, Mathf.Max(18f, height - 16f), new Color(1f, 0.72f, 0.22f, 1f), new[] { 2f, 2f, 2f, 2f });
            _accentBar.SetXY(5f, 8f);
        }

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
        if (_panel == null || _panel.isDisposed || _attachedContainer == null || _attachedContainer.isDisposed) return;
        _panel.SetScale(_scale, _scale);
        if (_dragging) return;
        FloatingHintLayoutMath.ComputeClampedXY(
            _attachedContainer.width, _attachedContainer.height,
            _panelWidth, _panelHeight,
            _scale, _posX, _posY,
            FloatingHintLayoutMath.DefaultScreenMargin,
            out float posX, out float posY);
        _panel.SetXY(posX, posY);
    }

    private void ClampCurrentPanelBounds()
    {
        if (_panel == null || _panel.isDisposed || _attachedContainer == null || _attachedContainer.isDisposed) return;
        FloatingHintLayoutMath.ClampXYToBounds(
            _attachedContainer.width, _attachedContainer.height,
            _panelWidth, _panelHeight,
            _scale, _panel.x, _panel.y,
            FloatingHintLayoutMath.DefaultScreenMargin,
            out float cx, out float cy);
        _panel.SetXY(cx, cy);
    }

    private void CommitDraggedPosition()
    {
        _dragging = false;
        if (_panel == null || _panel.isDisposed || _attachedContainer == null || _attachedContainer.isDisposed) return;
        ClampCurrentPanelBounds();
        FloatingHintLayoutMath.ComputeNormalizedFromXY(
            _attachedContainer.width, _attachedContainer.height,
            _panelWidth, _panelHeight,
            _scale, _panel.x, _panel.y,
            FloatingHintLayoutMath.DefaultScreenMargin,
            out float nx, out float ny);
        SetPosition(nx, ny);
    }

    private void RemovePanel()
    {
        _dragging = false;
        if (_panel != null && !_panel.isDisposed)
        {
            _panel.Dispose();
        }
        _panel = null;
        _background = null;
        _accentBar = null;
        _titleLabel = null;
        _formulaLabel = null;
        _attachedContainer = null;
        _lastTitle = "";
        _lastFormula = "";
    }

    private void OnDestroy()
    {
        RemovePanel();
        if (_instance == this) _instance = null;
    }
}

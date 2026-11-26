using XingShuRuntime.Core;
using XingShuRuntime.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace XingShuRuntime.UI
{
    /// <summary>
    /// Merged page view for Defense &amp; Battle Card Points (防御与卡牌点数, Tab 13):
    /// combines DefensePointHint and BattleCardPointHint settings in one scrollable panel.
    /// </summary>
    internal sealed class XingShuCombatPointsView
    {
        private readonly GameObject _root;
        private Toggle _defenseHintToggle;
        private Toggle _battleCardHintToggle;
        private Toggle _battleCardLockToggle;
        private Slider _battleCardScaleSlider;
        private Slider _battleCardMarginXSlider;
        private Slider _battleCardYSlider;
        private Text _battleCardScaleText;
        private Text _battleCardMarginXText;
        private Text _battleCardYText;
        private bool _suppressCallbacks;

        internal GameObject Root => _root;

        internal XingShuCombatPointsView(Transform parent)
        {
            _root = XingShuUIFactory.CreateUIObject("Page_CombatPoints", parent.gameObject);
            BuildUI();
            UpdateSync();
        }

        private void BuildUI()
        {
            XingShuViewCardHelper.CreatePageScroll(_root, "CombatPointsScrollView", out GameObject content);

            // Card 1: 防御点数提示
            GameObject defCard = XingShuViewCardHelper.CreateCardContainer(content, "Card_DefenseHint", 132f);
            Text defTitle = XingShuUIFactory.CreateText(defCard, "Title", "防御点数提示", 18, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(defTitle.rectTransform, new Vector2(20f, -14f), new Vector2(450f, 28f));

            Text defDesc = XingShuUIFactory.CreateText(defCard, "Description",
                "本人防御时显示压至最低伤害、避免击飞所需的总防御和骰点。\n含已同步防御牌；数值未完全揭示时按保守上限估算，不代替操作。",
                13, XingShuUITheme.SecondaryText, TextAnchor.UpperLeft);
            XingShuViewCardHelper.PlaceStretchTop(defDesc.rectTransform, 54f, 64f, 40f);

            _defenseHintToggle = XingShuUIFactory.CreateToggle(defCard, "EnableDefenseHint", "显示提示", DefensePointHint.IsEnabled,
                value => { if (!_suppressCallbacks) DefensePointHint.SetEnabled(value); });
            XingShuViewCardHelper.PlaceTopRight(_defenseHintToggle.GetComponent<RectTransform>(), new Vector2(-20f, -12f), new Vector2(160f, 32f));

            // Card 2: 战斗卡牌点数提示
            GameObject card = XingShuViewCardHelper.CreateCardContainer(content, "Card_BattleCardPointHint", 282f);
            Text title = XingShuUIFactory.CreateText(card, "Title", "战斗卡牌点数提示", 18, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(title.rectTransform, new Vector2(20f, -12f), new Vector2(450f, 26f));

            Text desc = XingShuUIFactory.CreateText(card, "Description",
                "战斗时显示攻防双方卡牌与点数明细；未锁定时可在局内直接拖动调整位置",
                12, XingShuUITheme.SecondaryText, TextAnchor.MiddleLeft);
            XingShuViewCardHelper.PlaceStretchTop(desc.rectTransform, 40f, 20f, 40f);

            _battleCardHintToggle = XingShuUIFactory.CreateToggle(card, "EnableBattleCardHint", "显示点数", BattleCardPointHint.IsEnabled,
                value => { if (!_suppressCallbacks) BattleCardPointHint.SetEnabled(value); });
            XingShuViewCardHelper.PlaceTopRight(_battleCardHintToggle.GetComponent<RectTransform>(), new Vector2(-20f, -10f), new Vector2(165f, 34f));

            _battleCardLockToggle = XingShuUIFactory.CreateToggle(card, "BattleCardLockToggle", "锁定位置（禁止拖动）", BattleCardPointHint.Locked,
                value => { if (!_suppressCallbacks) BattleCardPointHint.SetLocked(value); });
            XingShuViewCardHelper.PlaceTopLeft(_battleCardLockToggle.GetComponent<RectTransform>(), new Vector2(20f, -66f), new Vector2(240f, 32f));

            Button resetBtn = XingShuViewCardHelper.CreateActionButton(card, "ResetBattleCardLayout", "恢复默认",
                () => { BattleCardPointHint.ResetLayout(); UpdateSync(); }, 104f, 30f, 12);
            XingShuViewCardHelper.PlaceTopRight(resetBtn.GetComponent<RectTransform>(), new Vector2(-20f, -66f), new Vector2(104f, 30f));

            XingShuViewCardHelper.AddPercentSliderRow(card, "BattleCardScale", "显示大小（60%–180%）", 106f,
                XingShuConfigKeys.BattleCardPointScaleMin, XingShuConfigKeys.BattleCardPointScaleMax,
                BattleCardPointHint.Scale,
                v => { BattleCardPointHint.SetScale(Mathf.Round(v * 20f) / 20f); UpdateSync(); },
                () => _suppressCallbacks,
                out _battleCardScaleSlider, out _battleCardScaleText);

            XingShuViewCardHelper.AddPercentSliderRow(card, "BattleCardMarginX", "两侧水平边距（0%–45%）", 160f,
                0f, 0.45f, Mathf.Clamp(BattleCardPointHint.AtkX, 0f, 0.45f),
                v => { BattleCardPointHint.SetMirrorMarginX(v); UpdateSync(); },
                () => _suppressCallbacks,
                out _battleCardMarginXSlider, out _battleCardMarginXText);

            XingShuViewCardHelper.AddPercentSliderRow(card, "BattleCardY", "垂直位置 Y（上 → 下）", 214f,
                0f, 1f, BattleCardPointHint.AtkY,
                v => { BattleCardPointHint.SetUnifiedY(v); UpdateSync(); },
                () => _suppressCallbacks,
                out _battleCardYSlider, out _battleCardYText);
        }

        internal void UpdateSync()
        {
            float scale = BattleCardPointHint.Scale;
            float marginX = Mathf.Clamp(BattleCardPointHint.AtkX, 0f, 0.45f);
            float posY = BattleCardPointHint.AtkY;
            bool locked = BattleCardPointHint.Locked;
            _suppressCallbacks = true;
            if (_defenseHintToggle != null && _defenseHintToggle.isOn != DefensePointHint.IsEnabled)
                _defenseHintToggle.isOn = DefensePointHint.IsEnabled;
            if (_battleCardHintToggle != null && _battleCardHintToggle.isOn != BattleCardPointHint.IsEnabled)
                _battleCardHintToggle.isOn = BattleCardPointHint.IsEnabled;
            if (_battleCardScaleSlider != null && Mathf.Abs(_battleCardScaleSlider.value - scale) > 0.005f)
                _battleCardScaleSlider.value = scale;
            if (_battleCardMarginXSlider != null && Mathf.Abs(_battleCardMarginXSlider.value - marginX) > 0.005f)
                _battleCardMarginXSlider.value = marginX;
            if (_battleCardYSlider != null && Mathf.Abs(_battleCardYSlider.value - posY) > 0.005f)
                _battleCardYSlider.value = posY;
            if (_battleCardLockToggle != null && _battleCardLockToggle.isOn != locked)
                _battleCardLockToggle.isOn = locked;
            _suppressCallbacks = false;
            if (_battleCardScaleText != null) _battleCardScaleText.text = $"{Mathf.RoundToInt(scale * 100f)}%";
            if (_battleCardMarginXText != null) _battleCardMarginXText.text = $"{Mathf.RoundToInt(BattleCardPointHint.AtkX * 100f)}%";
            if (_battleCardYText != null) _battleCardYText.text = $"{Mathf.RoundToInt(posY * 100f)}%";
        }
    }
}

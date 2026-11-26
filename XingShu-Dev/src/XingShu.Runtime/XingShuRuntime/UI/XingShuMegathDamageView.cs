using XingShuRuntime.Core;
using XingShuRuntime.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace XingShuRuntime.UI
{
    /// <summary>
    /// Standalone page view for Megath Orbital Bombardment Damage Hint (梅加斯伤害, Tab 15).
    /// </summary>
    internal sealed class XingShuMegathDamageView
    {
        private readonly GameObject _root;
        private Toggle _megathEnabledToggle;
        private Toggle _megathLockToggle;
        private Slider _megathScaleSlider;
        private Slider _megathXSlider;
        private Slider _megathYSlider;
        private Text _megathScaleText;
        private Text _megathXText;
        private Text _megathYText;
        private bool _suppressCallbacks;

        internal GameObject Root => _root;

        internal XingShuMegathDamageView(Transform parent)
        {
            _root = XingShuUIFactory.CreateUIObject("Page_MegathDamage", parent.gameObject);
            BuildUI();
            UpdateSync();
        }

        private void BuildUI()
        {
            XingShuViewCardHelper.CreatePageScroll(_root, "MegathDamageScrollView", out GameObject content);

            GameObject card = XingShuViewCardHelper.CreateCardContainer(content, "MegathDamageHintCard", 282f);
            Text title = XingShuUIFactory.CreateText(card, "Title", "梅加斯伤害计算面板", 18, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(title.rectTransform, new Vector2(20f, -12f), new Vector2(450f, 26f));

            Text desc = XingShuUIFactory.CreateText(card, "Description",
                "仅游玩梅加斯时显示主动技能「轨道轰炸」理论最大伤害；未锁定时可在局内直接拖动",
                12, XingShuUITheme.SecondaryText, TextAnchor.MiddleLeft);
            XingShuViewCardHelper.PlaceStretchTop(desc.rectTransform, 40f, 20f, 40f);

            _megathEnabledToggle = XingShuUIFactory.CreateToggle(card, "EnableMegathHint", "显示面板", MegathSkillDamageHint.IsEnabled,
                value => { if (!_suppressCallbacks) MegathSkillDamageHint.SetEnabled(value); });
            XingShuViewCardHelper.PlaceTopRight(_megathEnabledToggle.GetComponent<RectTransform>(), new Vector2(-20f, -10f), new Vector2(165f, 34f));

            _megathLockToggle = XingShuUIFactory.CreateToggle(card, "MegathLockToggle", "锁定位置（禁止拖动）", MegathSkillDamageHint.Locked,
                value => { if (!_suppressCallbacks) MegathSkillDamageHint.SetLocked(value); });
            XingShuViewCardHelper.PlaceTopLeft(_megathLockToggle.GetComponent<RectTransform>(), new Vector2(20f, -66f), new Vector2(240f, 32f));

            Button resetBtn = XingShuViewCardHelper.CreateActionButton(card, "ResetMegathLayout", "恢复默认",
                () => { MegathSkillDamageHint.ResetLayout(); UpdateSync(); }, 104f, 30f, 12);
            XingShuViewCardHelper.PlaceTopRight(resetBtn.GetComponent<RectTransform>(), new Vector2(-20f, -66f), new Vector2(104f, 30f));

            XingShuViewCardHelper.AddPercentSliderRow(card, "MegathScale", "显示大小（60%–180%）", 106f,
                XingShuConfigKeys.MegathDamageHintScaleMin, XingShuConfigKeys.MegathDamageHintScaleMax,
                MegathSkillDamageHint.Scale,
                v => { MegathSkillDamageHint.SetScale(Mathf.Round(v * 20f) / 20f); UpdateSync(); },
                () => _suppressCallbacks,
                out _megathScaleSlider, out _megathScaleText);

            XingShuViewCardHelper.AddPercentSliderRow(card, "MegathX", "水平位置 X（左 → 右）", 160f,
                0f, 1f, MegathSkillDamageHint.PosX,
                v => { MegathSkillDamageHint.SetPosition(v, MegathSkillDamageHint.PosY); UpdateSync(); },
                () => _suppressCallbacks,
                out _megathXSlider, out _megathXText);

            XingShuViewCardHelper.AddPercentSliderRow(card, "MegathY", "垂直位置 Y（上 → 下）", 214f,
                0f, 1f, MegathSkillDamageHint.PosY,
                v => { MegathSkillDamageHint.SetPosition(MegathSkillDamageHint.PosX, v); UpdateSync(); },
                () => _suppressCallbacks,
                out _megathYSlider, out _megathYText);

            GameObject ruleCard = XingShuViewCardHelper.CreateCardContainer(content, "MegathRuleCard", 108f);
            Text ruleTitle = XingShuUIFactory.CreateText(ruleCard, "RuleTitle", "计算口径说明", 14, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(ruleTitle.rectTransform, new Vector2(20f, -12f), new Vector2(240f, 22f));
            Text ruleBody = XingShuUIFactory.CreateText(ruleCard, "RuleBody",
                "• 轰炸次数 = 牌堆数 / 2（向下取整，如 9 张 = 4 炮）；每炮基础伤害 = 星能点 / 2（向上取整）+ 2。\n• 默认只计算牌堆轰炸伤害；目标带有「精准打击」（轨道炮命中后附加）标记时额外 +1/炮。",
                12, XingShuUITheme.SecondaryText, TextAnchor.UpperLeft);
            XingShuViewCardHelper.PlaceStretchTop(ruleBody.rectTransform, 40f, 56f, 40f);
        }

        internal void UpdateSync()
        {
            float scale = MegathSkillDamageHint.Scale;
            float px = MegathSkillDamageHint.PosX;
            float py = MegathSkillDamageHint.PosY;
            bool locked = MegathSkillDamageHint.Locked;
            _suppressCallbacks = true;
            if (_megathEnabledToggle != null && _megathEnabledToggle.isOn != MegathSkillDamageHint.IsEnabled)
                _megathEnabledToggle.isOn = MegathSkillDamageHint.IsEnabled;
            if (_megathScaleSlider != null && Mathf.Abs(_megathScaleSlider.value - scale) > 0.005f)
                _megathScaleSlider.value = scale;
            if (_megathXSlider != null && Mathf.Abs(_megathXSlider.value - px) > 0.005f)
                _megathXSlider.value = px;
            if (_megathYSlider != null && Mathf.Abs(_megathYSlider.value - py) > 0.005f)
                _megathYSlider.value = py;
            if (_megathLockToggle != null && _megathLockToggle.isOn != locked)
                _megathLockToggle.isOn = locked;
            _suppressCallbacks = false;
            if (_megathScaleText != null) _megathScaleText.text = $"{Mathf.RoundToInt(scale * 100f)}%";
            if (_megathXText != null) _megathXText.text = $"{Mathf.RoundToInt(px * 100f)}%";
            if (_megathYText != null) _megathYText.text = $"{Mathf.RoundToInt(py * 100f)}%";
        }
    }
}

using XingShuRuntime.Targeting;
using UnityEngine;
using UnityEngine.UI;

namespace XingShuRuntime.UI
{
    /// <summary>
    /// Standalone page view for Monster Targeting (怪物索敌, Tab 11).
    /// </summary>
    internal sealed class XingShuMonsterTargetView
    {
        private readonly GameObject _root;
        private Toggle _targetingToggle;
        private Toggle _arrowBoostToggle;
        private Text _targetingSummary;
        private bool _suppressCallbacks;

        internal GameObject Root => _root;

        internal XingShuMonsterTargetView(Transform parent)
        {
            _root = XingShuUIFactory.CreateUIObject("Page_MonsterTarget", parent.gameObject);
            BuildUI();
            UpdateSync();
        }

        private void BuildUI()
        {
            XingShuViewCardHelper.CreatePageScroll(_root, "MonsterTargetScrollView", out GameObject content);

            GameObject card = XingShuViewCardHelper.CreateCardContainer(content, "MonsterTargetCard", 280f);
            Text title = XingShuUIFactory.CreateText(card, "Title", "怪物索敌 · 最大10格", 18, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(title.rectTransform, new Vector2(24f, -16f), new Vector2(360f, 28f));

            _targetingToggle = XingShuUIFactory.CreateToggle(card, "TargetingToggle", "地图索敌", MonsterTargetRuntime.IsEnabled,
                value => { if (!_suppressCallbacks) MonsterTargetRuntime.SetEnabled(value); });
            XingShuViewCardHelper.PlaceTopRight(_targetingToggle.GetComponent<RectTransform>(), new Vector2(-24f, -14f), new Vector2(160f, 32f));

            _arrowBoostToggle = XingShuUIFactory.CreateToggle(card, "ArrowBoostToggle", "放大怪物箭头", MonsterTargetRuntime.ArrowBoostEnabled,
                value => { if (!_suppressCallbacks) MonsterTargetRuntime.SetArrowBoostEnabled(value); });
            XingShuViewCardHelper.PlaceTopRight(_arrowBoostToggle.GetComponent<RectTransform>(), new Vector2(-196f, -14f), new Vector2(180f, 32f));

            Text hint = XingShuUIFactory.CreateText(card, "Hint", "沿怪物箭头前进，不掉头；最大10格（先行1格＋剩余9格），技能加步暂不计入。", 12, XingShuUITheme.SecondaryText, TextAnchor.MiddleLeft);
            XingShuViewCardHelper.PlaceStretchTop(hint.rectTransform, 52f, 24f, 48f);

            _targetingSummary = XingShuUIFactory.CreateText(card, "Summary", "等待进入 PVE 对局", 13, XingShuUITheme.PrimaryText, TextAnchor.UpperLeft);
            _targetingSummary.supportRichText = false;
            XingShuViewCardHelper.PlaceStretchTop(_targetingSummary.rectTransform, 86f, 126f, 48f);

            Button previous = XingShuViewCardHelper.CreateActionButton(card, "PreviousMonster", "◀ 上个怪物", () => MonsterTargetRuntime.Select(-1), 132f, 32f);
            Button next = XingShuViewCardHelper.CreateActionButton(card, "NextMonster", "下个怪物 ▶", () => MonsterTargetRuntime.Select(1), 132f, 32f);
            XingShuViewCardHelper.PlaceTopLeft(previous.GetComponent<RectTransform>(), new Vector2(24f, -228f), new Vector2(132f, 32f));
            XingShuViewCardHelper.PlaceTopLeft(next.GetComponent<RectTransform>(), new Vector2(172f, -228f), new Vector2(132f, 32f));
        }

        internal void UpdateSync()
        {
            if (_targetingSummary != null && _targetingSummary.text != MonsterTargetRuntime.Summary)
                _targetingSummary.text = MonsterTargetRuntime.Summary;
            _suppressCallbacks = true;
            if (_targetingToggle != null && _targetingToggle.isOn != MonsterTargetRuntime.IsEnabled)
                _targetingToggle.isOn = MonsterTargetRuntime.IsEnabled;
            if (_arrowBoostToggle != null && _arrowBoostToggle.isOn != MonsterTargetRuntime.ArrowBoostEnabled)
                _arrowBoostToggle.isOn = MonsterTargetRuntime.ArrowBoostEnabled;
            _suppressCallbacks = false;
        }
    }
}

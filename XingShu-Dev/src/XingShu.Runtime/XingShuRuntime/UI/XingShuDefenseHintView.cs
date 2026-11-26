using XingShuRuntime.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace XingShuRuntime.UI
{
    /// <summary>
    /// Standalone page view for Defense Point Hint (防御提示, Tab 13).
    /// </summary>
    internal sealed class XingShuDefenseHintView
    {
        private readonly GameObject _root;
        private Toggle _defenseHintToggle;
        private bool _suppressCallbacks;

        internal GameObject Root => _root;

        internal XingShuDefenseHintView(Transform parent)
        {
            _root = XingShuUIFactory.CreateUIObject("Page_DefenseHint", parent.gameObject);
            BuildUI();
            UpdateSync();
        }

        private void BuildUI()
        {
            XingShuViewCardHelper.CreatePageScroll(_root, "DefenseHintScrollView", out GameObject content);

            GameObject card = XingShuViewCardHelper.CreateCardContainer(content, "DefenseHintCard", 132f);
            Text title = XingShuUIFactory.CreateText(card, "Title", "防御点数提示", 18, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(title.rectTransform, new Vector2(20f, -14f), new Vector2(450f, 28f));

            Text desc = XingShuUIFactory.CreateText(card, "Description",
                "本人防御时显示压至最低伤害、避免击飞所需的总防御和骰点。\n含已同步防御牌；数值未完全揭示时按保守上限估算，不代替操作。",
                13, XingShuUITheme.SecondaryText, TextAnchor.UpperLeft);
            XingShuViewCardHelper.PlaceStretchTop(desc.rectTransform, 54f, 64f, 40f);

            _defenseHintToggle = XingShuUIFactory.CreateToggle(card, "EnableDefenseHint", "显示提示", DefensePointHint.IsEnabled,
                value => { if (!_suppressCallbacks) DefensePointHint.SetEnabled(value); });
            XingShuViewCardHelper.PlaceTopRight(_defenseHintToggle.GetComponent<RectTransform>(), new Vector2(-20f, -12f), new Vector2(160f, 32f));
        }

        internal void UpdateSync()
        {
            _suppressCallbacks = true;
            if (_defenseHintToggle != null && _defenseHintToggle.isOn != DefensePointHint.IsEnabled)
                _defenseHintToggle.isOn = DefensePointHint.IsEnabled;
            _suppressCallbacks = false;
        }
    }
}

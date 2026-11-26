using XingShuRuntime.Social;
using UnityEngine;
using UnityEngine.UI;

namespace XingShuRuntime.UI
{
    /// <summary>
    /// Standalone page view for Quick Auto Thanks (快捷致谢, Tab 18).
    /// </summary>
    internal sealed class XingShuQuickThanksView
    {
        private readonly GameObject _root;
        private Toggle _autoThanksToggle;
        private bool _suppressCallbacks;

        internal GameObject Root => _root;

        internal XingShuQuickThanksView(Transform parent)
        {
            _root = XingShuUIFactory.CreateUIObject("Page_QuickThanks", parent.gameObject);
            BuildUI();
            UpdateSync();
        }

        private void BuildUI()
        {
            XingShuViewCardHelper.CreatePageScroll(_root, "QuickThanksScrollView", out GameObject content);

            GameObject card = XingShuViewCardHelper.CreateCardContainer(content, "Card_AutoThanks", 112f);
            Text title = XingShuUIFactory.CreateText(card, "ThanksTitle", "自动快捷致谢", 18, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(title.rectTransform, new Vector2(20f, -14f), new Vector2(300f, 26f));

            Text desc = XingShuUIFactory.CreateText(card, "ThanksDesc",
                "收到队友赠送卡牌 / 星币 / 治疗时，自动触发游戏原生快捷感谢表情与语音回应。",
                13, XingShuUITheme.SecondaryText, TextAnchor.UpperLeft);
            XingShuViewCardHelper.PlaceStretchTop(desc.rectTransform, 50f, 44f, 40f);

            _autoThanksToggle = XingShuUIFactory.CreateToggle(card, "AutoThanksToggle", "开启自动致谢", XingShuAutoThanks.IsEnabled,
                v => { if (!_suppressCallbacks) XingShuAutoThanks.IsEnabled = v; });
            XingShuViewCardHelper.PlaceTopRight(_autoThanksToggle.GetComponent<RectTransform>(), new Vector2(-20f, -12f), new Vector2(165f, 32f));
        }

        internal void UpdateSync()
        {
            _suppressCallbacks = true;
            if (_autoThanksToggle != null && _autoThanksToggle.isOn != XingShuAutoThanks.IsEnabled)
                _autoThanksToggle.isOn = XingShuAutoThanks.IsEnabled;
            _suppressCallbacks = false;
        }
    }
}

using XingShuRuntime.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace XingShuRuntime.UI
{
    /// <summary>
    /// Standalone page view for Daily Star Coin Shop Assistant (星币商店, Tab 20).
    /// </summary>
    internal sealed class XingShuStarCoinShopView
    {
        private readonly GameObject _root;
        private Toggle _dailyShopToggle;
        private Text _dailyShopStatus;
        private bool _suppressCallbacks;

        internal GameObject Root => _root;

        internal XingShuStarCoinShopView(Transform parent)
        {
            _root = XingShuUIFactory.CreateUIObject("Page_StarCoinShop", parent.gameObject);
            BuildUI();
            UpdateSync();
        }

        private void BuildUI()
        {
            XingShuViewCardHelper.CreatePageScroll(_root, "StarCoinShopScrollView", out GameObject content);

            GameObject shopCard = XingShuViewCardHelper.CreateCardContainer(content, "Card_DailyStarCoinShop", 156f);
            Text shopTitle = XingShuUIFactory.CreateText(shopCard, "Title", "每日星币商店", 18, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(shopTitle.rectTransform, new Vector2(20f, -12f), new Vector2(360f, 26f));

            Text shopDesc = XingShuUIFactory.CreateText(shopCard, "Description",
                "每天登录后处理官方随机货架：先领免费商品，再买完剩余星币商品。\n默认关闭，按游戏账号保存；星币不足时保留未购商品，不兑换其他货币。",
                12, XingShuUITheme.SecondaryText, TextAnchor.UpperLeft);
            XingShuViewCardHelper.PlaceStretchTop(shopDesc.rectTransform, 44f, 44f, 40f);

            _dailyShopStatus = XingShuUIFactory.CreateText(shopCard, "Status", DailyStarCoinShop.Status, 12, XingShuUITheme.MutedText, TextAnchor.MiddleLeft);
            XingShuViewCardHelper.PlaceStretchTop(_dailyShopStatus.rectTransform, 102f, 36f, 40f);

            _dailyShopToggle = XingShuUIFactory.CreateToggle(shopCard, "DailyShopToggle", "自动购买", DailyStarCoinShop.IsEnabled,
                value => { if (!_suppressCallbacks) DailyStarCoinShop.SetEnabled(value); });
            XingShuViewCardHelper.PlaceTopRight(_dailyShopToggle.GetComponent<RectTransform>(), new Vector2(-20f, -10f), new Vector2(165f, 34f));
        }

        internal void UpdateSync()
        {
            _suppressCallbacks = true;
            if (_dailyShopToggle != null)
            {
                _dailyShopToggle.isOn = DailyStarCoinShop.IsEnabled;
                _dailyShopToggle.interactable = DailyStarCoinShop.CanConfigure;
            }
            if (_dailyShopStatus != null && _dailyShopStatus.text != DailyStarCoinShop.Status)
                _dailyShopStatus.text = DailyStarCoinShop.Status;
            _suppressCallbacks = false;
        }
    }
}

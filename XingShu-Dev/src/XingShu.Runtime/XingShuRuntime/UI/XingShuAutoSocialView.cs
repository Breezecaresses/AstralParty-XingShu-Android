using XingShuRuntime.Core;
using XingShuRuntime.Social;
using XingShuRuntime.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace XingShuRuntime.UI
{
    /// <summary>
    /// Merged page view for Automated Social Features (自动化社交, Tab 18):
    /// combines Auto Quick Thanks, Avatar Auto Praise, Daily Star Coin Shop, and Auto Settlement Screenshot.
    /// </summary>
    internal sealed class XingShuAutoSocialView
    {
        private readonly GameObject _root;

        private Toggle _autoThanksToggle;
        private Toggle _autoPraiseToggle;
        private Slider _praiseScaleSlider;
        private Slider _praiseOffsetXSlider;
        private Slider _praiseOffsetYSlider;
        private Text _praiseScaleValueText;
        private Text _praiseOffsetXText;
        private Text _praiseOffsetYText;
        private Text _praiseParamText;

        private Toggle _dailyShopToggle;
        private Text _dailyShopStatus;

        private Toggle _settlementScreenshotToggle;
        private Text _settlementScreenshotPathText;

        private bool _suppressCallbacks;

        internal GameObject Root => _root;

        internal XingShuAutoSocialView(Transform parent)
        {
            _root = XingShuUIFactory.CreateUIObject("Page_AutoSocial", parent.gameObject);
            BuildUI();
            UpdateSync();
        }

        private void BuildUI()
        {
            XingShuViewCardHelper.CreatePageScroll(_root, "AutoSocialScrollView", out GameObject content);

            // Card 1: 自动快捷致谢
            GameObject thanksCard = XingShuViewCardHelper.CreateCardContainer(content, "Card_AutoThanks", 112f);
            Text thanksTitle = XingShuUIFactory.CreateText(thanksCard, "ThanksTitle", "自动快捷致谢", 18, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(thanksTitle.rectTransform, new Vector2(20f, -14f), new Vector2(300f, 26f));

            Text thanksDesc = XingShuUIFactory.CreateText(thanksCard, "ThanksDesc",
                "收到队友赠送卡牌 / 星币 / 治疗时，自动触发游戏原生快捷感谢表情与语音回应。",
                13, XingShuUITheme.SecondaryText, TextAnchor.UpperLeft);
            XingShuViewCardHelper.PlaceStretchTop(thanksDesc.rectTransform, 50f, 44f, 40f);

            _autoThanksToggle = XingShuUIFactory.CreateToggle(thanksCard, "AutoThanksToggle", "开启自动致谢", XingShuAutoThanks.IsEnabled,
                v => { if (!_suppressCallbacks) XingShuAutoThanks.IsEnabled = v; });
            XingShuViewCardHelper.PlaceTopRight(_autoThanksToggle.GetComponent<RectTransform>(), new Vector2(-20f, -12f), new Vector2(165f, 32f));

            // Card 2: 头像点赞
            GameObject praiseCard = XingShuViewCardHelper.CreateCardContainer(content, "Card_AutoPraise", 282f);
            Text praiseTitle = XingShuUIFactory.CreateText(praiseCard, "PraiseTitle", "头像点赞", 18, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(praiseTitle.rectTransform, new Vector2(20f, -12f), new Vector2(260f, 24f));

            Text praiseDesc = XingShuUIFactory.CreateText(praiseCard, "PraiseDesc",
                "头像旁点选/取消点赞标记，结算时只为选中的真人玩家自动点赞",
                12, XingShuUITheme.SecondaryText, TextAnchor.MiddleLeft);
            XingShuViewCardHelper.PlaceStretchTop(praiseDesc.rectTransform, 38f, 20f, 40f);

            _autoPraiseToggle = XingShuUIFactory.CreateToggle(praiseCard, "AutoPraiseToggle", "开启点赞", XingShuAutoPraise.IsEnabled,
                v => { if (!_suppressCallbacks) XingShuAutoPraise.IsEnabled = v; });
            XingShuViewCardHelper.PlaceTopRight(_autoPraiseToggle.GetComponent<RectTransform>(), new Vector2(-20f, -10f), new Vector2(150f, 32f));

            Text scaleLabel = XingShuUIFactory.CreateText(praiseCard, "PraiseScaleLabel", "点赞图标大小（60%–200%）", 12, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft);
            XingShuViewCardHelper.PlaceTopLeft(scaleLabel.rectTransform, new Vector2(20f, -64f), new Vector2(260f, 20f));

            _praiseScaleValueText = XingShuUIFactory.CreateText(praiseCard, "PraiseScaleValue", $"{Mathf.RoundToInt(XingShuAutoPraise.PraiseScale * 100f)}%", 13, XingShuUITheme.NumericAccent, TextAnchor.MiddleRight, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopRight(_praiseScaleValueText.rectTransform, new Vector2(-122f, -64f), new Vector2(56f, 20f));

            Button resetScaleBtn = XingShuViewCardHelper.CreateActionButton(praiseCard, "ResetPraiseScale", "恢复默认",
                () => { XingShuAutoPraise.PraiseScale = XingShuConfigKeys.AutoPraiseScaleDefault; UpdateSync(); }, 88f, 30f, 12);
            XingShuViewCardHelper.PlaceTopRight(resetScaleBtn.GetComponent<RectTransform>(), new Vector2(-20f, -60f), new Vector2(88f, 30f));

            _praiseScaleSlider = XingShuUIFactory.CreateSlider(praiseCard, "PraiseScaleSlider",
                XingShuConfigKeys.AutoPraiseScaleMin, XingShuConfigKeys.AutoPraiseScaleMax,
                XingShuAutoPraise.PraiseScale, OnPraiseScaleSliderChanged);
            XingShuViewCardHelper.PlaceStretchTop(_praiseScaleSlider.GetComponent<RectTransform>(), 96f, 28f, 40f);

            XingShuViewCardHelper.AddSliderRow(praiseCard, "PraiseOffsetX", "点赞 X 偏移", 130f,
                XingShuConfigKeys.AutoPraiseOffsetMin, XingShuConfigKeys.AutoPraiseOffsetMax,
                XingShuAutoPraise.OffsetX, FormatSignedPx(XingShuAutoPraise.OffsetX),
                v => { XingShuAutoPraise.OffsetX = Mathf.Round(v); UpdateSync(); },
                () => _suppressCallbacks,
                out _praiseOffsetXSlider, out _praiseOffsetXText);

            XingShuViewCardHelper.AddSliderRow(praiseCard, "PraiseOffsetY", "点赞 Y 偏移", 186f,
                XingShuConfigKeys.AutoPraiseOffsetMin, XingShuConfigKeys.AutoPraiseOffsetMax,
                XingShuAutoPraise.OffsetY, FormatSignedPx(XingShuAutoPraise.OffsetY),
                v => { XingShuAutoPraise.OffsetY = Mathf.Round(v); UpdateSync(); },
                () => _suppressCallbacks,
                out _praiseOffsetYSlider, out _praiseOffsetYText);

            _praiseParamText = XingShuUIFactory.CreateText(praiseCard, "PraiseParamSummary", "", 11, XingShuUITheme.MutedText, TextAnchor.MiddleLeft);
            XingShuViewCardHelper.PlaceTopLeft(_praiseParamText.rectTransform, new Vector2(20f, -244f), new Vector2(460f, 22f));

            Button resetPosBtn = XingShuViewCardHelper.CreateActionButton(praiseCard, "ResetPraisePos", "重置位置",
                () => { XingShuAutoPraise.ResetPosition(); UpdateSync(); }, 88f, 28f, 12);
            XingShuViewCardHelper.PlaceTopRight(resetPosBtn.GetComponent<RectTransform>(), new Vector2(-20f, -240f), new Vector2(88f, 28f));

            // Card 3: 每日星币商店
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

            // Card 4: 自动结算战绩截图
            GameObject screenshotCard = XingShuViewCardHelper.CreateCardContainer(content, "Card_AutoSettlementScreenshot", 152f);
            Text shotTitle = XingShuUIFactory.CreateText(screenshotCard, "Title", "自动结算战绩截图", 18, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(shotTitle.rectTransform, new Vector2(20f, -12f), new Vector2(360f, 26f));

            Text shotDesc = XingShuUIFactory.CreateText(screenshotCard, "Description",
                "对局结算战绩界面稳定后自动保存 PNG 截图，每局仅保存一次。",
                12, XingShuUITheme.SecondaryText, TextAnchor.UpperLeft);
            XingShuViewCardHelper.PlaceStretchTop(shotDesc.rectTransform, 44f, 24f, 40f);

            _settlementScreenshotPathText = XingShuUIFactory.CreateText(screenshotCard, "Status", FormatScreenshotPathSummary(), 12, XingShuUITheme.MutedText, TextAnchor.MiddleLeft);
            var spRt = _settlementScreenshotPathText.rectTransform;
            spRt.anchorMin = new Vector2(0f, 1f);
            spRt.anchorMax = new Vector2(1f, 1f);
            spRt.pivot = new Vector2(0f, 1f);
            spRt.anchoredPosition = new Vector2(20f, -84f);
#if !XINGSHU_ANDROID_PREVIEW
            spRt.sizeDelta = new Vector2(-190f, 46f);
            Button openFolderBtn = XingShuViewCardHelper.CreateActionButton(
                screenshotCard,
                "OpenScreenshotFolder",
                "打开截图目录",
                () => XingShuRuntime.MatchPort.SettlementScreenshot.OpenFolder(),
                140f, 34f, 12);
            XingShuViewCardHelper.PlaceTopRight(openFolderBtn.GetComponent<RectTransform>(), new Vector2(-20f, -90f), new Vector2(140f, 34f));
#else
            spRt.sizeDelta = new Vector2(-40f, 46f);
#endif

            _settlementScreenshotToggle = XingShuUIFactory.CreateToggle(screenshotCard, "AutoScreenshotToggle", "自动截图",
                XingShuRuntime.MatchPort.SettlementScreenshot.IsEnabled,
                value => { if (!_suppressCallbacks) XingShuRuntime.MatchPort.SettlementScreenshot.SetEnabled(value); });
            XingShuViewCardHelper.PlaceTopRight(_settlementScreenshotToggle.GetComponent<RectTransform>(), new Vector2(-20f, -10f), new Vector2(165f, 34f));
        }

        private static string FormatSignedPx(float value)
        {
            int rounded = Mathf.RoundToInt(value);
            return (rounded >= 0 ? "+" : "") + rounded + "px";
        }

        private void OnPraiseScaleSliderChanged(float val)
        {
            if (_suppressCallbacks) return;
            XingShuAutoPraise.PraiseScale = Mathf.Round(val * 20f) / 20f;
            UpdateSync();
        }

        private static string FormatScreenshotPathSummary()
        {
            string status = XingShuRuntime.MatchPort.SettlementScreenshot.Status;
            string lastPath = XingShuRuntime.MatchPort.SettlementScreenshot.LastSavedPath;
            string folder = XingShuRuntime.MatchPort.SettlementScreenshot.OutputFolder;
            string target = !string.IsNullOrEmpty(lastPath) ? "最近保存：" + lastPath : "保存目录：" + (folder ?? "未就绪");
            return status + "\n" + target;
        }

        internal void UpdateSync()
        {
            float scale = XingShuAutoPraise.PraiseScale;
            float ox = XingShuAutoPraise.OffsetX;
            float oy = XingShuAutoPraise.OffsetY;
            _suppressCallbacks = true;
            if (_autoThanksToggle != null && _autoThanksToggle.isOn != XingShuAutoThanks.IsEnabled)
                _autoThanksToggle.isOn = XingShuAutoThanks.IsEnabled;
            if (_autoPraiseToggle != null && _autoPraiseToggle.isOn != XingShuAutoPraise.IsEnabled)
                _autoPraiseToggle.isOn = XingShuAutoPraise.IsEnabled;
            if (_praiseScaleSlider != null && Mathf.Abs(_praiseScaleSlider.value - scale) > 0.001f)
                _praiseScaleSlider.value = scale;
            if (_praiseOffsetXSlider != null && Mathf.Abs(_praiseOffsetXSlider.value - ox) > 0.5f)
                _praiseOffsetXSlider.value = ox;
            if (_praiseOffsetYSlider != null && Mathf.Abs(_praiseOffsetYSlider.value - oy) > 0.5f)
                _praiseOffsetYSlider.value = oy;
            if (_dailyShopToggle != null)
            {
                _dailyShopToggle.isOn = DailyStarCoinShop.IsEnabled;
                _dailyShopToggle.interactable = DailyStarCoinShop.CanConfigure;
            }
            if (_settlementScreenshotToggle != null && _settlementScreenshotToggle.isOn != XingShuRuntime.MatchPort.SettlementScreenshot.IsEnabled)
                _settlementScreenshotToggle.isOn = XingShuRuntime.MatchPort.SettlementScreenshot.IsEnabled;
            _suppressCallbacks = false;

            if (_praiseScaleValueText != null) _praiseScaleValueText.text = $"{Mathf.RoundToInt(scale * 100f)}%";
            if (_praiseOffsetXText != null) _praiseOffsetXText.text = FormatSignedPx(ox);
            if (_praiseOffsetYText != null) _praiseOffsetYText.text = FormatSignedPx(oy);
            if (_praiseParamText != null) _praiseParamText.text = $"当前参数：大小 {Mathf.RoundToInt(scale * 100f)}% · 偏移 ({FormatSignedPx(ox)}, {FormatSignedPx(oy)})";
            if (_dailyShopStatus != null && _dailyShopStatus.text != DailyStarCoinShop.Status)
                _dailyShopStatus.text = DailyStarCoinShop.Status;
            if (_settlementScreenshotPathText != null)
            {
                string summary = FormatScreenshotPathSummary();
                if (_settlementScreenshotPathText.text != summary) _settlementScreenshotPathText.text = summary;
            }
        }
    }
}

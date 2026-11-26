using UnityEngine;
using UnityEngine.UI;

namespace XingShuRuntime.UI
{
    /// <summary>
    /// Standalone page view for Auto Settlement Screenshot (结算截图, Tab 21).
    /// </summary>
    internal sealed class XingShuSettlementShotView
    {
        private readonly GameObject _root;
        private Toggle _settlementScreenshotToggle;
        private Text _settlementScreenshotPathText;
        private bool _suppressCallbacks;

        internal GameObject Root => _root;

        internal XingShuSettlementShotView(Transform parent)
        {
            _root = XingShuUIFactory.CreateUIObject("Page_SettlementShot", parent.gameObject);
            BuildUI();
            UpdateSync();
        }

        private void BuildUI()
        {
            XingShuViewCardHelper.CreatePageScroll(_root, "SettlementShotScrollView", out GameObject content);

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
            _suppressCallbacks = true;
            if (_settlementScreenshotToggle != null && _settlementScreenshotToggle.isOn != XingShuRuntime.MatchPort.SettlementScreenshot.IsEnabled)
                _settlementScreenshotToggle.isOn = XingShuRuntime.MatchPort.SettlementScreenshot.IsEnabled;
            if (_settlementScreenshotPathText != null)
            {
                string summary = FormatScreenshotPathSummary();
                if (_settlementScreenshotPathText.text != summary) _settlementScreenshotPathText.text = summary;
            }
            _suppressCallbacks = false;
        }
    }
}

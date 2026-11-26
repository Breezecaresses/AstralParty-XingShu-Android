using XingShuRuntime.Core;
using XingShuRuntime.Social;
using UnityEngine;
using UnityEngine.UI;

namespace XingShuRuntime.UI
{
    /// <summary>
    /// Standalone page view for Avatar Praise Selector (头像点赞, Tab 19).
    /// </summary>
    internal sealed class XingShuAvatarPraiseView
    {
        private readonly GameObject _root;
        private Toggle _autoPraiseToggle;
        private Slider _praiseScaleSlider;
        private Slider _praiseOffsetXSlider;
        private Slider _praiseOffsetYSlider;
        private Text _praiseScaleValueText;
        private Text _praiseOffsetXText;
        private Text _praiseOffsetYText;
        private Text _praiseParamText;
        private bool _suppressCallbacks;

        internal GameObject Root => _root;

        internal XingShuAvatarPraiseView(Transform parent)
        {
            _root = XingShuUIFactory.CreateUIObject("Page_AvatarPraise", parent.gameObject);
            BuildUI();
            UpdateSync();
        }

        private void BuildUI()
        {
            XingShuViewCardHelper.CreatePageScroll(_root, "AvatarPraiseScrollView", out GameObject content);

            GameObject card = XingShuViewCardHelper.CreateCardContainer(content, "Card_AutoPraise", 282f);
            Text title = XingShuUIFactory.CreateText(card, "PraiseTitle", "头像点赞", 18, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(title.rectTransform, new Vector2(20f, -12f), new Vector2(260f, 24f));

            Text desc = XingShuUIFactory.CreateText(card, "PraiseDesc",
                "头像旁点选/取消点赞标记，结算时只为选中的真人玩家自动点赞",
                12, XingShuUITheme.SecondaryText, TextAnchor.MiddleLeft);
            XingShuViewCardHelper.PlaceStretchTop(desc.rectTransform, 38f, 20f, 40f);

            _autoPraiseToggle = XingShuUIFactory.CreateToggle(card, "AutoPraiseToggle", "开启点赞", XingShuAutoPraise.IsEnabled,
                v => { if (!_suppressCallbacks) XingShuAutoPraise.IsEnabled = v; });
            XingShuViewCardHelper.PlaceTopRight(_autoPraiseToggle.GetComponent<RectTransform>(), new Vector2(-20f, -10f), new Vector2(150f, 32f));

            Text scaleLabel = XingShuUIFactory.CreateText(card, "PraiseScaleLabel", "点赞图标大小（60%–200%）", 12, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft);
            XingShuViewCardHelper.PlaceTopLeft(scaleLabel.rectTransform, new Vector2(20f, -64f), new Vector2(260f, 20f));

            _praiseScaleValueText = XingShuUIFactory.CreateText(card, "PraiseScaleValue", $"{Mathf.RoundToInt(XingShuAutoPraise.PraiseScale * 100f)}%", 13, XingShuUITheme.NumericAccent, TextAnchor.MiddleRight, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopRight(_praiseScaleValueText.rectTransform, new Vector2(-122f, -64f), new Vector2(56f, 20f));

            Button resetScaleBtn = XingShuViewCardHelper.CreateActionButton(card, "ResetPraiseScale", "恢复默认",
                () => { XingShuAutoPraise.PraiseScale = XingShuConfigKeys.AutoPraiseScaleDefault; UpdateSync(); }, 88f, 30f, 12);
            XingShuViewCardHelper.PlaceTopRight(resetScaleBtn.GetComponent<RectTransform>(), new Vector2(-20f, -60f), new Vector2(88f, 30f));

            _praiseScaleSlider = XingShuUIFactory.CreateSlider(card, "PraiseScaleSlider",
                XingShuConfigKeys.AutoPraiseScaleMin, XingShuConfigKeys.AutoPraiseScaleMax,
                XingShuAutoPraise.PraiseScale, OnPraiseScaleSliderChanged);
            XingShuViewCardHelper.PlaceStretchTop(_praiseScaleSlider.GetComponent<RectTransform>(), 96f, 28f, 40f);

            XingShuViewCardHelper.AddSliderRow(card, "PraiseOffsetX", "点赞 X 偏移", 130f,
                XingShuConfigKeys.AutoPraiseOffsetMin, XingShuConfigKeys.AutoPraiseOffsetMax,
                XingShuAutoPraise.OffsetX, FormatSignedPx(XingShuAutoPraise.OffsetX),
                v => { XingShuAutoPraise.OffsetX = Mathf.Round(v); UpdateSync(); },
                () => _suppressCallbacks,
                out _praiseOffsetXSlider, out _praiseOffsetXText);

            XingShuViewCardHelper.AddSliderRow(card, "PraiseOffsetY", "点赞 Y 偏移", 186f,
                XingShuConfigKeys.AutoPraiseOffsetMin, XingShuConfigKeys.AutoPraiseOffsetMax,
                XingShuAutoPraise.OffsetY, FormatSignedPx(XingShuAutoPraise.OffsetY),
                v => { XingShuAutoPraise.OffsetY = Mathf.Round(v); UpdateSync(); },
                () => _suppressCallbacks,
                out _praiseOffsetYSlider, out _praiseOffsetYText);

            _praiseParamText = XingShuUIFactory.CreateText(card, "PraiseParamSummary", "", 11, XingShuUITheme.MutedText, TextAnchor.MiddleLeft);
            XingShuViewCardHelper.PlaceTopLeft(_praiseParamText.rectTransform, new Vector2(20f, -244f), new Vector2(460f, 22f));

            Button resetPosBtn = XingShuViewCardHelper.CreateActionButton(card, "ResetPraisePos", "重置位置",
                () => { XingShuAutoPraise.ResetPosition(); UpdateSync(); }, 88f, 28f, 12);
            XingShuViewCardHelper.PlaceTopRight(resetPosBtn.GetComponent<RectTransform>(), new Vector2(-20f, -240f), new Vector2(88f, 28f));
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

        internal void UpdateSync()
        {
            float scale = XingShuAutoPraise.PraiseScale;
            float ox = XingShuAutoPraise.OffsetX;
            float oy = XingShuAutoPraise.OffsetY;
            _suppressCallbacks = true;
            if (_autoPraiseToggle != null && _autoPraiseToggle.isOn != XingShuAutoPraise.IsEnabled)
                _autoPraiseToggle.isOn = XingShuAutoPraise.IsEnabled;
            if (_praiseScaleSlider != null && Mathf.Abs(_praiseScaleSlider.value - scale) > .001f)
                _praiseScaleSlider.value = scale;
            if (_praiseOffsetXSlider != null && Mathf.Abs(_praiseOffsetXSlider.value - ox) > .5f)
                _praiseOffsetXSlider.value = ox;
            if (_praiseOffsetYSlider != null && Mathf.Abs(_praiseOffsetYSlider.value - oy) > .5f)
                _praiseOffsetYSlider.value = oy;
            _suppressCallbacks = false;
            if (_praiseScaleValueText != null) _praiseScaleValueText.text = $"{Mathf.RoundToInt(scale * 100f)}%";
            if (_praiseOffsetXText != null) _praiseOffsetXText.text = FormatSignedPx(ox);
            if (_praiseOffsetYText != null) _praiseOffsetYText.text = FormatSignedPx(oy);
            if (_praiseParamText != null) _praiseParamText.text = $"当前参数：大小 {Mathf.RoundToInt(scale * 100f)}% · 偏移 ({FormatSignedPx(ox)}, {FormatSignedPx(oy)})";
        }
    }
}

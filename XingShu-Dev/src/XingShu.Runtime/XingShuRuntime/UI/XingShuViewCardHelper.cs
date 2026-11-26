using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace XingShuRuntime.UI
{
    /// <summary>
    /// Shared scroll-page, card container, action button, and slider row builders for standalone module views.
    /// </summary>
    internal static class XingShuViewCardHelper
    {
        internal static ScrollRect CreatePageScroll(GameObject pageRoot, string scrollName, out GameObject contentArea)
        {
            RectTransform rootRt = pageRoot.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            ScrollRect scrollRect = XingShuUIFactory.CreateVerticalScrollView(pageRoot, scrollName, out contentArea);
            VerticalLayoutGroup vlg = contentArea.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.spacing = 12f;
                vlg.padding = new RectOffset(0, 12, 4, 28);
            }
            return scrollRect;
        }

        internal static GameObject CreateCardContainer(GameObject parent, string name, float height, bool featureHeader = false)
        {
            GameObject cardGo = XingShuUIFactory.CreateUIObject(name, parent);
            LayoutElement le = cardGo.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            le.flexibleHeight = 0f;

            string spriteName = featureHeader ? "card_feature_header.png" : "card_base.png";
            Sprite cardSprite = XingShuUIAssetService.GetSprite(spriteName);
            Image cardBg = cardGo.AddComponent<Image>();
            cardBg.sprite = cardSprite;
            cardBg.type = Image.Type.Sliced;
            cardBg.color = XingShuUIAssetService.IsFallbackSprite(cardSprite)
                ? (featureHeader ? XingShuUITheme.CardFeatureBase : XingShuUITheme.Card)
                : Color.white;
            return cardGo;
        }

        internal static Button CreateActionButton(GameObject parent, string name, string label, Action onClick, float width, float height, int fontSize = 13)
        {
            Sprite btnSec = XingShuUIAssetService.GetSprite("button_secondary.png");
            Button btn = XingShuUIFactory.CreateButton(
                parent,
                name,
                label,
                btnSec,
                Color.white,
                XingShuUITheme.PrimaryText,
                onClick,
                fontSize);
            RectTransform rt = btn.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, height);
            return btn;
        }

        internal static void PlaceTopLeft(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        internal static void PlaceTopRight(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        internal static void PlaceStretchTop(RectTransform rt, float topOffset, float height, float horizontalInset = 40f)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -topOffset);
            rt.sizeDelta = new Vector2(-horizontalInset, height);
        }

        internal static void AddPercentSliderRow(
            GameObject card,
            string id,
            string labelText,
            float topY,
            float min,
            float max,
            float initial,
            UnityAction<float> onChanged,
            Func<bool> isSuppressed,
            out Slider slider,
            out Text valueText)
        {
            AddSliderRow(card, id, labelText, topY, min, max, initial, $"{Mathf.RoundToInt(initial * 100f)}%", onChanged, isSuppressed, out slider, out valueText);
        }

        internal static void AddSliderRow(
            GameObject card,
            string id,
            string labelText,
            float topY,
            float min,
            float max,
            float initial,
            string initialValueText,
            UnityAction<float> onChanged,
            Func<bool> isSuppressed,
            out Slider slider,
            out Text valueText)
        {
            GameObject pair = XingShuUIFactory.CreateUIObject("MetricPair_" + id, card);
            var pRt = pair.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0f, 1f);
            pRt.anchorMax = new Vector2(1f, 1f);
            pRt.pivot = new Vector2(0.5f, 1f);
            pRt.anchoredPosition = new Vector2(0f, -topY);
            pRt.sizeDelta = new Vector2(-40f, 22f);
            var hlg = pair.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            Text lbl = XingShuUIFactory.CreateText(pair, id + "Label", labelText, 12, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft);
            var lblLe = lbl.gameObject.AddComponent<LayoutElement>();
            lblLe.flexibleWidth = 1f;
            lblLe.minWidth = 140f;

            valueText = XingShuUIFactory.CreateText(pair, id + "Value", initialValueText, 12, XingShuUITheme.NumericAccent, TextAnchor.MiddleRight, FontStyle.Bold);
            var valLe = valueText.gameObject.AddComponent<LayoutElement>();
            valLe.minWidth = 60f;
            valLe.preferredWidth = 72f;
            valLe.flexibleWidth = 0f;

            slider = XingShuUIFactory.CreateSlider(card, id + "Slider", min, max, initial,
                v => { if (!isSuppressed()) onChanged(v); });
            var sRt = slider.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0f, 1f);
            sRt.anchorMax = new Vector2(1f, 1f);
            sRt.pivot = new Vector2(0.5f, 1f);
            sRt.anchoredPosition = new Vector2(0f, -(topY + 24f));
            sRt.sizeDelta = new Vector2(-40f, 26f);
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace XingShuRuntime.UI
{
    /// <summary>
    /// XingShu 原生风格 UI 组件构建工厂 (Phase 2B-R4 生产整合版)
    /// 封装标准 UGUI 对象的创建、布局约束、防穿透与排版保底
    /// </summary>
    internal static class XingShuUIFactory
    {
        internal static GameObject CreateUIObject(string name, GameObject parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                go.transform.SetParent(parent.transform, false);
            }
            return go;
        }

        internal static Image CreateImage(GameObject parent, string name, Sprite sprite, Color color, Image.Type imageType = Image.Type.Simple)
        {
            GameObject go = CreateUIObject(name, parent);
            Image img = go.AddComponent<Image>();
            img.sprite = sprite ?? XingShuUIAssetService.WhiteSprite;
            img.color = color;
            img.type = (sprite != null && sprite.border != Vector4.zero) ? Image.Type.Sliced : imageType;
            return img;
        }

        internal static Text CreateText(
            GameObject parent,
            string name,
            string content,
            int fontSize,
            Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter,
            FontStyle style = FontStyle.Normal,
            HorizontalWrapMode horizontalWrap = HorizontalWrapMode.Overflow,
            VerticalWrapMode verticalWrap = VerticalWrapMode.Truncate)
        {
            GameObject go = CreateUIObject(name, parent);
            Text txt = go.AddComponent<Text>();
            txt.text = content;
            txt.font = XingShuUITheme.GetFontForText(content);
            txt.fontSize = fontSize;
            txt.raycastTarget = false;
            txt.color = color;
            txt.alignment = alignment;
            txt.fontStyle = style;
            txt.horizontalOverflow = horizontalWrap;
            txt.verticalOverflow = verticalWrap;
            if (XingShuGlassSkin.IsActive || fontSize <= 14)
            {
                XingShuUITheme.ApplyCrispFont(txt, true);
            }
            return txt;
        }

        internal static Shadow AddShadow(GameObject target, Color color = default, Vector2 effectDistance = default)
        {
            if (target == null) return null;
            Shadow s = target.AddComponent<Shadow>();
            s.effectColor = (color == default) ? new Color(0f, 0f, 0f, 0.65f) : color;
            s.effectDistance = (effectDistance == default) ? new Vector2(1f, -1f) : effectDistance;
            return s;
        }

        internal static Outline AddOutline(GameObject target, Color color = default, Vector2 effectDistance = default)
        {
            if (target == null) return null;
            Outline o = target.AddComponent<Outline>();
            o.effectColor = (color == default) ? new Color(0f, 0f, 0f, 0.75f) : color;
            o.effectDistance = (effectDistance == default) ? new Vector2(1f, -1f) : effectDistance;
            return o;
        }

        internal static Button CreateButton(GameObject parent, string name, string label, Sprite bgSprite, Color bgColor, Color textColor, Action onClick, int fontSize = 14)
        {
            bool glassActive = XingShuGlassSkin.IsActive;
            bool isPrimaryOrSelected = bgSprite != null && !string.IsNullOrEmpty(bgSprite.name)
                && (bgSprite.name.Contains("button_primary") || bgSprite.name.Contains("button_selected"));

            GameObject go = CreateUIObject(name, parent);
            Image img = go.AddComponent<Image>();
            if (glassActive && (bgSprite == null || bgSprite.name.Contains("button_")))
            {
                img.sprite = isPrimaryOrSelected ? XingShuGlassSkin.ButtonAccentSprite : XingShuGlassSkin.ButtonSprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.sprite = bgSprite ?? XingShuUIAssetService.WhiteSprite;
                img.color = bgColor;
                img.type = (bgSprite != null && bgSprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
                if (bgSprite != null && !string.IsNullOrEmpty(bgSprite.name))
                {
                    if (bgSprite.name.Contains("button_primary"))
                    {
                        XingShuNativeGameUI.Bind(img, XingShuNativeSprite.BtnLarge, Image.Type.Sliced, false, Color.white);
                    }
                    else if (bgSprite.name.Contains("button_selected"))
                    {
                        XingShuNativeGameUI.Bind(img, XingShuNativeSprite.BtnSmallOk, Image.Type.Sliced, false, Color.white);
                    }
                }
            }

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = glassActive ? new Color(1.06f, 1.06f, 1.10f, 1f) : new Color(1f, 0.93f, 0.97f, 1f);
            cb.pressedColor = glassActive ? new Color(0.86f, 0.90f, 0.96f, 1f) : new Color(0.88f, 0.78f, 0.83f, 1f);
            cb.disabledColor = new Color(0.72f, 0.72f, 0.72f, 0.5f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            if (onClick != null)
            {
                btn.onClick.AddListener(new UnityAction(onClick));
            }

            if (!string.IsNullOrEmpty(label))
            {
                Color labelColor = glassActive
                    ? (isPrimaryOrSelected ? XingShuGlassSkin.TextPrimary : textColor)
                    : (isPrimaryOrSelected ? Color.white : textColor);
                Text txt = CreateText(go, "Text", label, fontSize, labelColor, TextAnchor.MiddleCenter, FontStyle.Bold, HorizontalWrapMode.Overflow, VerticalWrapMode.Truncate);
                RectTransform txtRt = txt.rectTransform;
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = Vector2.zero;
                txtRt.offsetMax = Vector2.zero;
            }

            return btn;
        }

        internal static Slider CreateSlider(GameObject parent, string name, float min, float max, float value, Action<float> onValueChanged)
        {
            GameObject sliderRoot = CreateUIObject(name, parent);
            RectTransform sliderRt = sliderRoot.GetComponent<RectTransform>();
            sliderRt.sizeDelta = new Vector2(240f, 28f);
            AddPointerSurface(sliderRoot);

            // White capsule and official pink bar, with star trail and four-point star handle.
            Sprite trackSprite = XingShuUIAssetService.GetSprite("slider_track.png");
            GameObject bgGo = CreateUIObject("Background", sliderRoot);
            Image bgImg = bgGo.AddComponent<Image>();
            bgImg.sprite = trackSprite;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = Color.white;
            XingShuNativeGameUI.Bind(bgImg, XingShuNativeSprite.SliderBg, Image.Type.Sliced, false, Color.white);
            RectTransform bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.5f);
            bgRt.anchorMax = new Vector2(1f, 0.5f);
            bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.anchoredPosition = Vector2.zero;
            bgRt.sizeDelta = new Vector2(-20f, 18f);

            // The fill and star share their horizontal range, including both endpoints.
            GameObject fillArea = CreateUIObject("Fill Area", sliderRoot);
            RectTransform fillAreaRt = fillArea.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0f, 0.5f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.5f);
            fillAreaRt.pivot = new Vector2(0.5f, 0.5f);
            fillAreaRt.anchoredPosition = Vector2.zero;
            fillAreaRt.offsetMin = new Vector2(20f, -7f);
            fillAreaRt.offsetMax = new Vector2(-20f, 7f);

            GameObject fillGo = CreateUIObject("Fill", fillArea);
            Image fillImg = fillGo.AddComponent<Image>();
            fillImg.sprite = XingShuUIAssetService.GetRoundedFallbackSprite(8);
            fillImg.type = Image.Type.Sliced;
            fillImg.color = XingShuUITheme.ToggleActiveLime;
            RectTransform fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.sizeDelta = Vector2.zero;

            GameObject trailGo = CreateUIObject("Trail", fillGo);
            Image trailImg = trailGo.AddComponent<Image>();
            trailImg.sprite = XingShuUIAssetService.WhiteSprite;
            trailImg.color = Color.clear;
            trailImg.raycastTarget = false;
            XingShuNativeGameUI.Bind(trailImg, XingShuNativeSprite.SliderTrail, Image.Type.Simple, true, Color.white);
            RectTransform trailRt = trailGo.GetComponent<RectTransform>();
            trailRt.anchorMin = new Vector2(1f, 0.5f);
            trailRt.anchorMax = new Vector2(1f, 0.5f);
            trailRt.pivot = new Vector2(1f, 0.5f);
            trailRt.anchoredPosition = new Vector2(6f, 0f);
            trailRt.sizeDelta = new Vector2(72f, 20f);

            // Keep a 40px pointer target even in rows whose preferred height is 28px.
            GameObject handleArea = CreateUIObject("Handle Slide Area", sliderRoot);
            RectTransform handleAreaRt = handleArea.GetComponent<RectTransform>();
            handleAreaRt.anchorMin = new Vector2(0f, 0.5f);
            handleAreaRt.anchorMax = new Vector2(1f, 0.5f);
            handleAreaRt.pivot = new Vector2(0.5f, 0.5f);
            handleAreaRt.anchoredPosition = Vector2.zero;
            handleAreaRt.offsetMin = new Vector2(20f, -20f);
            handleAreaRt.offsetMax = new Vector2(-20f, 20f);

            GameObject handleGo = CreateUIObject("Handle", handleArea);
            Image handleImg = handleGo.AddComponent<Image>();
            handleImg.sprite = XingShuUIAssetService.GetRoundedFallbackSprite(28);
            handleImg.type = Image.Type.Simple;
            handleImg.preserveAspect = true;
            handleImg.color = XingShuUITheme.PresetSelectedLime;
            XingShuUIAssetService.ApplyGameSliderArtwork(fillImg, handleImg);
            RectTransform handleRt = handleGo.GetComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(44f, 0f);

            // 4. Slider Component
            Slider slider = sliderRoot.AddComponent<Slider>();
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;

            if (onValueChanged != null)
            {
                slider.onValueChanged.AddListener(new UnityAction<float>(onValueChanged));
            }

            return slider;
        }

        internal static Toggle CreateToggle(GameObject parent, string name, string label, bool initialValue, Action<bool> onValueChanged)
        {
            GameObject toggleRoot = CreateUIObject(name, parent);
            RectTransform toggleRt = toggleRoot.GetComponent<RectTransform>();
            toggleRt.sizeDelta = new Vector2(220f, 32f);
            AddPointerSurface(toggleRoot);

            Sprite offSprite = XingShuNativeGameUI.GetIfReady(XingShuNativeSprite.ToggleOff) ?? XingShuUIAssetService.GetRoundedFallbackSprite(16);
            Sprite onSprite = XingShuNativeGameUI.GetIfReady(XingShuNativeSprite.ToggleOn);

            GameObject bgGo = CreateUIObject("Background", toggleRoot);
            Image bgImg = bgGo.AddComponent<Image>();
            bgImg.sprite = offSprite;
            bgImg.raycastTarget = false;
            bgImg.type = Image.Type.Simple;
            bgImg.preserveAspect = true;
            bgImg.color = Color.white;
            AddOutline(bgGo, new Color(0.72f, 0.75f, 0.82f, 0.88f), new Vector2(1.1f, -1.1f));
            XingShuNativeGameUI.Bind(bgImg, XingShuNativeSprite.ToggleOff, Image.Type.Simple, true, Color.white);
            RectTransform bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.5f);
            bgRt.anchorMax = new Vector2(0f, 0.5f);
            bgRt.pivot = new Vector2(0f, 0.5f);
            bgRt.anchoredPosition = Vector2.zero;
            bgRt.sizeDelta = new Vector2(32f, 32f);

            // Inner pink concentric circle (game native "开" inside "关")
            GameObject checkGo = CreateUIObject("Checkmark", bgGo);
            Image checkImg = checkGo.AddComponent<Image>();
            checkImg.sprite = onSprite ?? XingShuUIAssetService.GetRoundedFallbackSprite(12);
            checkImg.type = Image.Type.Simple;
            checkImg.preserveAspect = true;
            checkImg.color = onSprite != null ? Color.white : XingShuUITheme.Pink;
            checkImg.raycastTarget = false;
            XingShuNativeGameUI.Bind(checkImg, XingShuNativeSprite.ToggleOn, Image.Type.Simple, true, Color.white);
            RectTransform checkRt = checkGo.GetComponent<RectTransform>();
            checkRt.anchorMin = new Vector2(0.5f, 0.5f);
            checkRt.anchorMax = new Vector2(0.5f, 0.5f);
            checkRt.pivot = new Vector2(0.5f, 0.5f);
            checkRt.sizeDelta = new Vector2(22f, 22f);
            checkRt.anchoredPosition = Vector2.zero;

            // Label
            Text labelTxt = CreateText(toggleRoot, "Label", label, 16, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold, HorizontalWrapMode.Overflow, VerticalWrapMode.Truncate);
            RectTransform labelRt = labelTxt.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(42f, 0f);
            labelRt.offsetMax = Vector2.zero;

            Toggle toggle = toggleRoot.AddComponent<Toggle>();
            toggle.navigation = new Navigation { mode = Navigation.Mode.None };
            toggle.transition = Selectable.Transition.None;
            toggle.graphic = null; // Visual sync handles state changes including SetIsOnWithoutNotify
            toggle.targetGraphic = bgImg;
            toggle.isOn = initialValue;

            checkImg.enabled = initialValue;
            checkGo.SetActive(true);
            AttachToggleSync(toggleRoot, toggle, bgRt, checkImg, checkRt);

            toggle.onValueChanged.AddListener(delegate(bool state)
            {
                checkImg.enabled = state;
                if (onValueChanged != null)
                {
                    onValueChanged(state);
                }
            });

            return toggle;
        }

        internal static Toggle CreateToggleRow(GameObject parent, string name, string label, bool initialValue, Action<bool> onValueChanged)
        {
            GameObject toggleRoot = CreateUIObject(name, parent);
            RectTransform toggleRt = toggleRoot.GetComponent<RectTransform>();
            toggleRt.anchorMin = new Vector2(0f, 1f);
            toggleRt.anchorMax = new Vector2(1f, 1f);
            toggleRt.pivot = new Vector2(0.5f, 1f);
            toggleRt.sizeDelta = new Vector2(0f, 34f);
            AddPointerSurface(toggleRoot);

            // Label (Left side, primary text)
            Text labelTxt = CreateText(toggleRoot, "Label", label, 14, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Normal, HorizontalWrapMode.Overflow, VerticalWrapMode.Truncate);
            RectTransform labelRt = labelTxt.rectTransform;
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            labelRt.offsetMin = new Vector2(24f, 0f);
            labelRt.offsetMax = new Vector2(-68f, 0f);

            Sprite offSprite = XingShuNativeGameUI.GetIfReady(XingShuNativeSprite.ToggleOff) ?? XingShuUIAssetService.GetRoundedFallbackSprite(16);
            Sprite onSprite = XingShuNativeGameUI.GetIfReady(XingShuNativeSprite.ToggleOn);

            GameObject bgGo = CreateUIObject("Background", toggleRoot);
            Image bgImg = bgGo.AddComponent<Image>();
            bgImg.sprite = offSprite;
            bgImg.raycastTarget = false;
            bgImg.type = Image.Type.Simple;
            bgImg.preserveAspect = true;
            bgImg.color = Color.white;
            AddOutline(bgGo, new Color(0.72f, 0.75f, 0.82f, 0.88f), new Vector2(1.1f, -1.1f));
            XingShuNativeGameUI.Bind(bgImg, XingShuNativeSprite.ToggleOff, Image.Type.Simple, true, Color.white);
            RectTransform bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(1f, 0.5f);
            bgRt.anchorMax = new Vector2(1f, 0.5f);
            bgRt.pivot = new Vector2(1f, 0.5f);
            bgRt.anchoredPosition = new Vector2(-24f, 0f);
            bgRt.sizeDelta = new Vector2(32f, 32f);

            GameObject checkGo = CreateUIObject("Checkmark", bgGo);
            Image checkImg = checkGo.AddComponent<Image>();
            checkImg.sprite = onSprite ?? XingShuUIAssetService.GetRoundedFallbackSprite(12);
            checkImg.type = Image.Type.Simple;
            checkImg.preserveAspect = true;
            checkImg.color = onSprite != null ? Color.white : XingShuUITheme.Pink;
            checkImg.raycastTarget = false;
            XingShuNativeGameUI.Bind(checkImg, XingShuNativeSprite.ToggleOn, Image.Type.Simple, true, Color.white);
            RectTransform checkRt = checkGo.GetComponent<RectTransform>();
            checkRt.anchorMin = new Vector2(0.5f, 0.5f);
            checkRt.anchorMax = new Vector2(0.5f, 0.5f);
            checkRt.pivot = new Vector2(0.5f, 0.5f);
            checkRt.sizeDelta = new Vector2(22f, 22f);
            checkRt.anchoredPosition = Vector2.zero;

            Toggle toggle = toggleRoot.AddComponent<Toggle>();
            toggle.navigation = new Navigation { mode = Navigation.Mode.None };
            toggle.transition = Selectable.Transition.None;
            toggle.graphic = null;
            toggle.targetGraphic = bgImg;
            toggle.isOn = initialValue;

            checkImg.enabled = initialValue;
            checkGo.SetActive(true);
            AttachToggleSync(toggleRoot, toggle, bgRt, checkImg, checkRt);

            toggle.onValueChanged.AddListener(delegate(bool state)
            {
                checkImg.enabled = state;
                if (onValueChanged != null)
                {
                    onValueChanged(state);
                }
            });

            return toggle;
        }

        private static void AttachToggleSync(GameObject toggleRoot, Toggle toggle, RectTransform bgRt, Image checkImg, RectTransform checkRt)
        {
            XingShuToggleVisualSync sync = toggleRoot.AddComponent<XingShuToggleVisualSync>();
            sync.Bind(toggle, bgRt, checkImg, checkRt);
        }

        private sealed class XingShuToggleVisualSync : MonoBehaviour
        {
            private Toggle _toggle;
            private RectTransform _bgRt;
            private Image _checkImg;
            private RectTransform _checkRt;
            private bool _lastInit;
            private bool _lastState;

            internal void Bind(Toggle toggle, RectTransform bgRt, Image checkImg, RectTransform checkRt)
            {
                _toggle = toggle;
                _bgRt = bgRt;
                _checkImg = checkImg;
                _checkRt = checkRt;
                SyncNow();
            }

            private void OnEnable()
            {
                SyncNow();
            }

            private void LateUpdate()
            {
                SyncNow();
            }

            private void SyncNow()
            {
                if (_toggle == null) return;
                bool isOn = _toggle.isOn;
                if (_checkImg != null && (!_lastInit || _lastState != isOn || _checkImg.enabled != isOn))
                {
                    _lastInit = true;
                    _lastState = isOn;
                    _checkImg.enabled = isOn;
                    _checkImg.preserveAspect = true;
                    if (_checkRt != null && _checkRt.anchoredPosition != Vector2.zero)
                    {
                        _checkRt.anchoredPosition = Vector2.zero;
                    }
                }
                if (_bgRt != null)
                {
                    if (_bgRt.localScale != Vector3.one) _bgRt.localScale = Vector3.one;
                    Vector2 sd = _bgRt.sizeDelta;
                    if (Mathf.Abs(sd.x - sd.y) > 0.5f || sd.x < 24f)
                    {
                        float s = Mathf.Clamp(Mathf.Min(sd.x, sd.y), 28f, 40f);
                        _bgRt.sizeDelta = new Vector2(s, s);
                    }
                }
            }
        }

        internal static void SetToggleRowStateWithoutNotify(Toggle toggle, bool state)
        {
            if (toggle == null) return;
            toggle.SetIsOnWithoutNotify(state);
            UpdateToggleRowVisual(toggle, state);
        }

        internal static void UpdateToggleRowVisual(Toggle toggle, bool state)
        {
            UpdateToggleVisual(toggle, state);
        }

        internal static void SetToggleStateWithoutNotify(Toggle toggle, bool state)
        {
            if (toggle == null) return;
            toggle.SetIsOnWithoutNotify(state);
            UpdateToggleVisual(toggle, state);
        }

        internal static void UpdateToggleVisual(Toggle toggle, bool state)
        {
            if (toggle == null) return;
            Transform bgTrans = toggle.transform.Find("Background");
            if (bgTrans != null)
            {
                Transform checkTrans = bgTrans.Find("Checkmark");
                if (checkTrans != null)
                {
                    Image checkImg = checkTrans.GetComponent<Image>();
                    if (checkImg != null) checkImg.enabled = state;
                    RectTransform checkRt = checkTrans.GetComponent<RectTransform>();
                    if (checkRt != null) checkRt.anchoredPosition = Vector2.zero;
                }
            }
        }

        internal static InputField CreateInputField(
            GameObject parent,
            string name,
            string placeholder,
            string initialValue,
            string contextId,
            Action<string> onValueChanged = null,
            Action<string> onEndEdit = null,
            int fontSize = 14)
        {
            GameObject root = CreateUIObject(name, parent);
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(240f, 34f);

            Sprite bgSprite = XingShuUIAssetService.GetSprite("button_secondary.png");
            Image bgImg = root.AddComponent<Image>();
            bgImg.sprite = bgSprite ?? XingShuUIAssetService.WhiteSprite;
            bgImg.type = (bgSprite != null && bgSprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
            bgImg.color = Color.white;
            XingShuNativeGameUI.Bind(bgImg, XingShuNativeSprite.InputField, Image.Type.Sliced, false, Color.white);

            // Text
            GameObject textObj = CreateUIObject("Text", root);
            Text textComp = textObj.AddComponent<Text>();
            textComp.font = XingShuUITheme.GetMainFont();
            textComp.fontSize = fontSize;
            textComp.color = XingShuUITheme.PrimaryText;
            textComp.alignment = TextAnchor.MiddleLeft;
            textComp.supportRichText = false;
            textComp.raycastTarget = false;
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(10f, 0f);
            textRt.offsetMax = new Vector2(-10f, 0f);

            // Placeholder
            GameObject phObj = CreateUIObject("Placeholder", root);
            Text phComp = phObj.AddComponent<Text>();
            phComp.font = XingShuUITheme.GetMainFont();
            phComp.fontSize = fontSize;
            phComp.color = XingShuUITheme.MutedText;
            phComp.alignment = TextAnchor.MiddleLeft;
            phComp.text = placeholder ?? "";
            phComp.raycastTarget = false;
            phComp.fontStyle = FontStyle.Italic;
            RectTransform phRt = phObj.GetComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = new Vector2(10f, 0f);
            phRt.offsetMax = new Vector2(-10f, 0f);

            InputField inputField = root.AddComponent<InputField>();
            inputField.textComponent = textComp;
            inputField.placeholder = phComp;
            inputField.text = initialValue ?? "";
            inputField.caretColor = XingShuUITheme.Pink;
            inputField.selectionColor = new Color(1f, 0.153f, 0.58f, 0.22f);
            inputField.caretWidth = 2;
            inputField.targetGraphic = bgImg;

            if (onValueChanged != null)
            {
                inputField.onValueChanged.AddListener(new UnityAction<string>(onValueChanged));
            }
            if (onEndEdit != null)
            {
                inputField.onEndEdit.AddListener(new UnityAction<string>(onEndEdit));
            }

            XingShuUITextEntryBridge bridge = root.AddComponent<XingShuUITextEntryBridge>();
            bridge.ContextId = contextId ?? "";

            return inputField;
        }

        internal static ScrollRect CreateVerticalScrollView(
            GameObject parent,
            string name,
            out GameObject contentArea)
        {
            GameObject scrollRoot = CreateUIObject(name, parent);
            RectTransform scrollRt = scrollRoot.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;

            ScrollRect scrollRect = scrollRoot.AddComponent<ScrollRect>();

            GameObject viewport = CreateUIObject("Viewport", scrollRoot);
            RectTransform vpRt = viewport.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = Vector2.zero;
            vpRt.offsetMax = Vector2.zero;
            vpRt.pivot = new Vector2(0f, 1f);
            viewport.AddComponent<RectMask2D>();
            // Empty space between cards must still receive scrolling and touch
            // drags instead of dropping input onto the page below it.
            AddPointerSurface(viewport);

            contentArea = CreateUIObject("Content", viewport);
            RectTransform contentRt = contentArea.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;

            VerticalLayoutGroup vlg = contentArea.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            ContentSizeFitter csf = contentArea.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            scrollRect.viewport = vpRt;
            scrollRect.content = contentRt;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.decelerationRate = 0.12f;
            scrollRect.scrollSensitivity = 36f;

            // Slim right-edge vertical scrollbar (auto-hidden when content fits)
            GameObject sbGo = CreateUIObject("VerticalScrollbar", scrollRoot);
            RectTransform sbRt = sbGo.GetComponent<RectTransform>();
            sbRt.anchorMin = new Vector2(1f, 0f);
            sbRt.anchorMax = new Vector2(1f, 1f);
            sbRt.pivot = new Vector2(1f, 0.5f);
            sbRt.anchoredPosition = Vector2.zero;
            sbRt.sizeDelta = new Vector2(6f, 0f);
            Image sbTrack = sbGo.AddComponent<Image>();
            sbTrack.sprite = XingShuUIAssetService.GetRoundedFallbackSprite(4);
            sbTrack.type = Image.Type.Sliced;
            sbTrack.color = new Color(1f, 1f, 1f, 0.10f);

            GameObject slideArea = CreateUIObject("SlidingArea", sbGo);
            RectTransform saRt = slideArea.GetComponent<RectTransform>();
            saRt.anchorMin = Vector2.zero;
            saRt.anchorMax = Vector2.one;
            saRt.offsetMin = Vector2.zero;
            saRt.offsetMax = Vector2.zero;

            GameObject handleGo = CreateUIObject("Handle", slideArea);
            RectTransform hRt = handleGo.GetComponent<RectTransform>();
            hRt.anchorMin = Vector2.zero;
            hRt.anchorMax = Vector2.one;
            hRt.offsetMin = Vector2.zero;
            hRt.offsetMax = Vector2.zero;
            Image handleImg = handleGo.AddComponent<Image>();
            handleImg.sprite = XingShuUIAssetService.GetRoundedFallbackSprite(4);
            handleImg.type = Image.Type.Sliced;
            handleImg.color = new Color(1f, 0.35f, 0.68f, 0.72f);

            Scrollbar scrollbar = sbGo.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = hRt;
            scrollbar.targetGraphic = handleImg;
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };

            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRect.verticalScrollbarSpacing = 6f;

            return scrollRect;
        }

        internal static GameObject CreateNativeDiagonalStripes(GameObject parent, float centerXRatio = 0.58f, float yellowWidth = 420f, float pinkWidth = 172f)
        {
            GameObject layer = CreateUIObject("NativeDiagonalStripes", parent);
            RectTransform layerRt = layer.GetComponent<RectTransform>();
            layerRt.anchorMin = Vector2.zero;
            layerRt.anchorMax = Vector2.one;
            layerRt.offsetMin = Vector2.zero;
            layerRt.offsetMax = Vector2.zero;

            GameObject yellowGo = CreateUIObject("StripeYellow", layer);
            Image yellowImg = yellowGo.AddComponent<Image>();
            yellowImg.sprite = XingShuUIAssetService.WhiteSprite;
            yellowImg.color = new Color(1f, 0.749f, 0f, 0.94f);
            yellowImg.raycastTarget = false;
            RectTransform yRt = yellowImg.rectTransform;
            yRt.anchorMin = new Vector2(centerXRatio, 0.5f);
            yRt.anchorMax = new Vector2(centerXRatio, 0.5f);
            yRt.pivot = new Vector2(1f, 0.5f);
            yRt.anchoredPosition = new Vector2(yellowWidth * 0.14f, 0f);
            yRt.sizeDelta = new Vector2(yellowWidth, 1600f);
            yRt.localRotation = Quaternion.Euler(0f, 0f, -21f);

            GameObject pinkGo = CreateUIObject("StripePink", layer);
            Image pinkImg = pinkGo.AddComponent<Image>();
            pinkImg.sprite = XingShuUIAssetService.WhiteSprite;
            pinkImg.color = new Color(1f, 0.145f, 0.510f, 0.94f);
            pinkImg.raycastTarget = false;
            RectTransform pRt = pinkImg.rectTransform;
            pRt.anchorMin = new Vector2(centerXRatio, 0.5f);
            pRt.anchorMax = new Vector2(centerXRatio, 0.5f);
            pRt.pivot = new Vector2(0f, 0.5f);
            pRt.anchoredPosition = new Vector2(yellowWidth * 0.14f, 0f);
            pRt.sizeDelta = new Vector2(pinkWidth, 1600f);
            pRt.localRotation = Quaternion.Euler(0f, 0f, -21f);

            return layer;
        }

        internal static Image CreateNativeTitleTag(GameObject parent, string name, string title, out Text labelOut, int fontSize = 20)
        {
            GameObject tagGo = CreateUIObject(name, parent);
            Image bg = tagGo.AddComponent<Image>();
            Sprite fb = XingShuUIAssetService.GetRoundedFallbackSprite(10);
            bg.sprite = XingShuNativeGameUI.GetIfReady(XingShuNativeSprite.TitleTag) ?? fb;
            bg.type = Image.Type.Sliced;
            bg.color = XingShuNativeGameUI.GetIfReady(XingShuNativeSprite.TitleTag) != null ? Color.white : XingShuUITheme.Yellow;
            bg.raycastTarget = false;
            XingShuNativeGameUI.Bind(bg, XingShuNativeSprite.TitleTag, Image.Type.Sliced, false, Color.white);

            labelOut = CreateText(tagGo, "Text", title ?? "", fontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            RectTransform lRt = labelOut.rectTransform;
            lRt.anchorMin = Vector2.zero;
            lRt.anchorMax = Vector2.one;
            lRt.offsetMin = new Vector2(14f, 2f);
            lRt.offsetMax = new Vector2(-14f, -2f);
            AddOutline(labelOut.gameObject, new Color(0.10f, 0.08f, 0.13f, 1f), new Vector2(1.8f, -1.8f));
            return bg;
        }

        private static void AddPointerSurface(GameObject root)
        {
            // A transparent Graphic makes the entire control row clickable,
            // including its label, without changing its visible layout.
            Image hit = root.AddComponent<Image>();
            hit.sprite = XingShuUIAssetService.WhiteSprite;
            hit.color = Color.clear;
            hit.raycastTarget = true;
        }
    }
}

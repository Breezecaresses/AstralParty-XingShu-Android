using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XingShu.Runtime;
using XingShuRuntime.Core;
using XingShuRuntime.Utilities;

namespace XingShuRuntime.UI
{
    /// <summary>
    /// Procedural anti-aliased SDF rounded-glass sprite and theme renderer for XingShu Function Center v2.
    /// Designed cleanly on pure UnityEngine.UI (zero shaders/native P/Invoke) so both PC & Android render
    /// frosted translucent cards, hairline borders, and external IXingShuExternalPage plugin mounts identically.
    /// </summary>
    internal static class XingShuGlassSkin
    {
        internal static readonly string[] GlassPresetNames = new string[]
        {
            "星夜深蓝",
            "星樱绯紫",
            "极光青雾",
            "琥珀暖金",
            "曜石纯黑",
            "自定义色调",
        };

        internal static Color BackdropDim { get; private set; } = new Color(0.02f, 0.04f, 0.08f, 0.56f);
        internal static Color ShellFill { get; private set; } = new Color(0.06f, 0.09f, 0.14f, 0.86f);
        internal static Color ShellBorder { get; private set; } = new Color(1f, 1f, 1f, 0.24f);
        internal static Color RailFill { get; private set; } = new Color(0.09f, 0.13f, 0.21f, 0.82f);
        internal static Color RailBorder { get; private set; } = new Color(1f, 1f, 1f, 0.16f);
        internal static Color HeaderFill { get; private set; } = new Color(0.14f, 0.19f, 0.28f, 0.80f);

        internal static Color CardFill { get; private set; } = new Color(0.13f, 0.18f, 0.27f, 0.78f);
        internal static Color CardAccentFill { get; private set; } = new Color(0.17f, 0.34f, 0.55f, 0.82f);
        internal static Color CardBorder { get; private set; } = new Color(1f, 1f, 1f, 0.22f);
        internal static Color CardAccentBorder { get; private set; } = new Color(0.45f, 0.78f, 1f, 0.44f);

        internal static Color TabNormalFill { get; private set; } = new Color(1f, 1f, 1f, 0.06f);
        internal static Color TabHoverFill { get; private set; } = new Color(1f, 1f, 1f, 0.14f);
        internal static Color TabSelectedFill { get; private set; } = new Color(0.28f, 0.58f, 0.92f, 0.42f);
        internal static Color TabSelectedBorder { get; private set; } = new Color(0.55f, 0.82f, 1f, 0.72f);

        internal static Color ButtonFill { get; private set; } = new Color(0.22f, 0.29f, 0.40f, 0.86f);
        internal static Color ButtonAccentFill { get; private set; } = new Color(0.20f, 0.52f, 0.90f, 0.92f);
        internal static Color InputFill { get; private set; } = new Color(0.05f, 0.08f, 0.13f, 0.78f);

        internal static readonly Color TextPrimary = new Color(0.96f, 0.98f, 1.00f, 1f);
        internal static readonly Color TextSecondary = new Color(0.84f, 0.89f, 0.96f, 1f);
        internal static readonly Color TextMuted = new Color(0.68f, 0.76f, 0.86f, 1f);
        internal static Color AccentBlue { get; private set; } = new Color(0.48f, 0.80f, 1.00f, 1f);
        internal static Color AccentMint { get; private set; } = new Color(0.36f, 0.89f, 0.69f, 1f);
        internal static readonly Color DividerLine = new Color(1f, 1f, 1f, 0.16f);

        private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();
        private static int _paletteRevision;
        private static int _controlSpritesRevision = -1;
        private static Sprite _cachedSliderTrackSprite;
        private static Sprite _cachedSliderFillSprite;
        private static Sprite _cachedSliderHandleSprite;
        private static Sprite _cachedToggleBgSprite;
        private static Sprite _cachedToggleCheckSprite;
        private static Sprite _cachedInputSprite;

        private sealed class CachedPageNodes
        {
            internal int ChildCount;
            internal float NextScanAt;
            internal Image[] Images;
            internal Text[] Texts;
        }

        private static readonly Dictionary<int, CachedPageNodes> _pageNodeCache = new Dictionary<int, CachedPageNodes>();

        internal static bool IsActive => XingShuUICenter.IsGlassActive;

        internal static void RefreshPaletteFromConfig()
        {
            int preset = XingShuConfigService.GetInt(XingShuConfigKeys.UiGlassThemePreset, XingShuConfigKeys.UiGlassThemePresetDefault);
            if (preset < 0 || preset > 5) preset = XingShuConfigKeys.UiGlassThemePresetDefault;

            float customHue = XingShuMobileUIHelper.FiniteClamp(
                XingShuConfigService.GetFloat(XingShuConfigKeys.UiGlassCustomHue, XingShuConfigKeys.UiGlassCustomHueDefault),
                XingShuConfigKeys.UiGlassCustomHueMin,
                XingShuConfigKeys.UiGlassCustomHueMax,
                XingShuConfigKeys.UiGlassCustomHueDefault);
            float shellAlpha = XingShuMobileUIHelper.FiniteClamp(
                XingShuConfigService.GetFloat(XingShuConfigKeys.UiGlassShellAlpha, XingShuConfigKeys.UiGlassShellAlphaDefault),
                XingShuConfigKeys.UiGlassShellAlphaMin,
                XingShuConfigKeys.UiGlassShellAlphaMax,
                XingShuConfigKeys.UiGlassShellAlphaDefault);
            float cardAlpha = XingShuMobileUIHelper.FiniteClamp(
                XingShuConfigService.GetFloat(XingShuConfigKeys.UiGlassCardAlpha, XingShuConfigKeys.UiGlassCardAlphaDefault),
                XingShuConfigKeys.UiGlassCardAlphaMin,
                XingShuConfigKeys.UiGlassCardAlphaMax,
                XingShuConfigKeys.UiGlassCardAlphaDefault);

            // Quantize alpha to 2.5% steps & hue to 5-degree steps to keep generated SDF sprite cache bounded.
            float qShellA = Mathf.Round(shellAlpha * 40f) / 40f;
            float qCardA = Mathf.Round(cardAlpha * 40f) / 40f;

            float hueDeg;
            float baseSat;
            float accentSat;
            switch (preset)
            {
                case 1: // 星樱绯紫
                    hueDeg = 328f; baseSat = 0.44f; accentSat = 0.68f;
                    break;
                case 2: // 极光青雾
                    hueDeg = 186f; baseSat = 0.48f; accentSat = 0.70f;
                    break;
                case 3: // 琥珀暖金
                    hueDeg = 34f; baseSat = 0.46f; accentSat = 0.74f;
                    break;
                case 4: // 曜石纯黑
                    hueDeg = 220f; baseSat = 0.06f; accentSat = 0.18f;
                    break;
                case 5: // 自定义色调
                    hueDeg = Mathf.Round(customHue / 5f) * 5f; baseSat = 0.46f; accentSat = 0.72f;
                    break;
                default: // 0 星夜深蓝
                    hueDeg = 214f; baseSat = 0.50f; accentSat = 0.70f;
                    break;
            }

            float h = (hueDeg % 360f) / 360f;
            Color shellRgb = Color.HSVToRGB(h, baseSat * 0.95f, preset == 4 ? 0.08f : 0.14f);
            Color railRgb = Color.HSVToRGB(h, baseSat, preset == 4 ? 0.11f : 0.19f);
            Color headerRgb = Color.HSVToRGB(h, baseSat * 0.85f, preset == 4 ? 0.14f : 0.25f);
            Color cardRgb = Color.HSVToRGB(h, baseSat * 0.82f, preset == 4 ? 0.13f : 0.24f);
            Color cardAccentRgb = Color.HSVToRGB(h, Mathf.Min(0.75f, accentSat * 0.78f), preset == 4 ? 0.22f : 0.42f);
            Color btnRgb = Color.HSVToRGB(h, baseSat * 0.68f, preset == 4 ? 0.24f : 0.36f);
            Color btnAccentRgb = Color.HSVToRGB(h, accentSat, preset == 4 ? 0.36f : 0.78f);
            Color inputRgb = Color.HSVToRGB(h, baseSat * 0.9f, 0.10f);
            Color accentLight = preset == 4 ? new Color(0.84f, 0.88f, 0.95f, 1f) : Color.HSVToRGB(h, 0.52f, 0.98f);
            Color secondaryAccent = preset == 1 ? new Color(1.00f, 0.62f, 0.84f, 1f)
                : (preset == 3 ? new Color(1.00f, 0.82f, 0.42f, 1f) : new Color(0.36f, 0.89f, 0.69f, 1f));

            BackdropDim = new Color(shellRgb.r * 0.35f, shellRgb.g * 0.35f, shellRgb.b * 0.35f, Mathf.Clamp(qShellA * 0.65f, 0.28f, 0.62f));
            ShellFill = new Color(shellRgb.r, shellRgb.g, shellRgb.b, qShellA);
            RailFill = new Color(railRgb.r, railRgb.g, railRgb.b, Mathf.Clamp(qShellA - 0.04f, 0.40f, 0.94f));
            HeaderFill = new Color(headerRgb.r, headerRgb.g, headerRgb.b, Mathf.Clamp(qCardA + 0.04f, 0.38f, 0.94f));
            CardFill = new Color(cardRgb.r, cardRgb.g, cardRgb.b, qCardA);
            CardAccentFill = new Color(cardAccentRgb.r, cardAccentRgb.g, cardAccentRgb.b, Mathf.Clamp(qCardA + 0.06f, 0.42f, 0.95f));
            ButtonFill = new Color(btnRgb.r, btnRgb.g, btnRgb.b, Mathf.Clamp(qCardA + 0.08f, 0.55f, 0.94f));
            ButtonAccentFill = new Color(btnAccentRgb.r, btnAccentRgb.g, btnAccentRgb.b, 0.92f);
            InputFill = new Color(inputRgb.r, inputRgb.g, inputRgb.b, Mathf.Clamp(qCardA + 0.02f, 0.45f, 0.90f));
            TabSelectedFill = new Color(accentLight.r * 0.55f, accentLight.g * 0.55f, accentLight.b * 0.55f, 0.44f);
            TabSelectedBorder = new Color(accentLight.r, accentLight.g, accentLight.b, 0.76f);
            CardAccentBorder = new Color(accentLight.r, accentLight.g, accentLight.b, 0.48f);
            ShellBorder = new Color(Mathf.Lerp(1f, accentLight.r, 0.25f), Mathf.Lerp(1f, accentLight.g, 0.25f), Mathf.Lerp(1f, accentLight.b, 0.25f), 0.24f);
            AccentBlue = accentLight;
            AccentMint = secondaryAccent;
            _paletteRevision++;
        }

        private static void EnsureControlSprites()
        {
            if (_controlSpritesRevision == _paletteRevision && _cachedSliderTrackSprite != null) return;
            _controlSpritesRevision = _paletteRevision;
            _cachedSliderTrackSprite = GetRoundedPanelSprite(6, InputFill, DividerLine, 1f);
            _cachedSliderFillSprite = GetRoundedPanelSprite(6, AccentBlue, Color.clear, 1f);
            _cachedSliderHandleSprite = GetRoundedPanelSprite(12, Color.white, AccentBlue, 1.5f);
            _cachedToggleBgSprite = GetRoundedPanelSprite(8, new Color(1f, 1f, 1f, 0.12f), new Color(1f, 1f, 1f, 0.44f), 1.4f);
            _cachedToggleCheckSprite = GetRoundedPanelSprite(6, AccentMint, Color.clear, 1f);
            _cachedInputSprite = GetRoundedPanelSprite(8, InputFill, new Color(1f, 1f, 1f, 0.28f), 1.2f);
        }

        internal static Sprite GetRoundedPanelSprite(int radius, Color fill, Color border, float borderThickness = 1.5f)
        {
            string key = $"{radius}|{ColorToKey(fill)}|{ColorToKey(border)}|{borderThickness:0.0}";
            if (_spriteCache.TryGetValue(key, out Sprite cached) && cached != null)
            {
                return cached;
            }

            int r = Mathf.Clamp(radius, 4, 28);
            int size = Mathf.Max(r * 2 + 12, 48);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f;
            float innerHalf = half - r - 0.5f;

            for (int y = 0; y < size; y++)
            {
                float dy = Mathf.Max(Mathf.Abs((y + 0.5f) - half) - innerHalf, 0f);
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs((x + 0.5f) - half) - innerHalf, 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) - r;

                    // Outer anti-aliased edge
                    float shapeAlpha = Mathf.Clamp01(0.5f - dist);
                    if (shapeAlpha <= 0.001f)
                    {
                        pixels[y * size + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    // Inner border blend near outer perimeter
                    float borderBlend = border.a > 0.001f
                        ? Mathf.Clamp01((dist + borderThickness) / borderThickness)
                        : 0f;

                    Color px = Color.Lerp(fill, border, borderBlend);
                    px.a *= shapeAlpha;
                    pixels[y * size + x] = (Color32)px;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            float b = r + 2f;
            Sprite sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(b, b, b, b));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            _spriteCache[key] = sprite;
            return sprite;
        }

        private static int ColorToKey(Color c)
        {
            Color32 c32 = c;
            return (c32.r << 24) | (c32.g << 16) | (c32.b << 8) | c32.a;
        }

        internal static Sprite ShellSprite => GetRoundedPanelSprite(20, ShellFill, ShellBorder, 1.5f);
        internal static Sprite RailSprite => GetRoundedPanelSprite(16, RailFill, RailBorder, 1.2f);
        internal static Sprite CardSprite => GetRoundedPanelSprite(12, CardFill, CardBorder, 1.2f);
        internal static Sprite CardAccentSprite => GetRoundedPanelSprite(12, CardAccentFill, CardAccentBorder, 1.4f);
        internal static Sprite TabNormalSprite => GetRoundedPanelSprite(10, TabNormalFill, new Color(1f, 1f, 1f, 0.08f), 1f);
        internal static Sprite TabHoverSprite => GetRoundedPanelSprite(10, TabHoverFill, new Color(1f, 1f, 1f, 0.18f), 1f);
        internal static Sprite TabSelectedSprite => GetRoundedPanelSprite(10, TabSelectedFill, TabSelectedBorder, 1.4f);
        internal static Sprite ButtonSprite => GetRoundedPanelSprite(9, ButtonFill, new Color(1f, 1f, 1f, 0.22f), 1.2f);
        internal static Sprite ButtonAccentSprite => GetRoundedPanelSprite(9, ButtonAccentFill, new Color(0.75f, 0.90f, 1f, 0.45f), 1.2f);
        internal static Sprite PillBadgeSprite => GetRoundedPanelSprite(10, new Color(0.32f, 0.65f, 1f, 0.22f), new Color(0.52f, 0.80f, 1f, 0.42f), 1f);

        internal static void ApplySelectableButton(Button btn, bool selected)
        {
            if (btn == null) return;
            ApplySelectableButton(btn.GetComponent<Image>(), btn.GetComponentInChildren<Text>(), selected);
        }

        internal static void ApplySelectableButton(Image img, Text txt, bool selected)
        {
            if (IsActive)
            {
                if (img != null)
                {
                    ApplySprite(img, selected ? ButtonAccentSprite : ButtonSprite, Image.Type.Sliced, false);
                }
                if (txt != null)
                {
                    txt.color = selected ? TextPrimary : TextSecondary;
                    XingShuUITheme.ApplyCrispFont(txt, true);
                }
                return;
            }

            Sprite sp = XingShuUIAssetService.GetSprite(selected ? "button_selected.png" : "button_secondary.png");
            if (img != null)
            {
                img.sprite = sp;
                img.type = (sp != null && sp.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
                img.color = XingShuUIAssetService.IsFallbackSprite(sp)
                    ? (selected ? XingShuUITheme.Pink : new Color(0.92f, 0.94f, 0.97f, 1f))
                    : Color.white;
            }
            if (txt != null)
            {
                txt.color = selected ? Color.white : XingShuUITheme.PrimaryText;
            }
        }

        internal static void ApplyStatusBadge(Image bg, Text txt, Color vanillaBgColor, Color vanillaTextColor, Color glassTextColor)
        {
            if (IsActive)
            {
                if (bg != null) ApplySprite(bg, PillBadgeSprite, Image.Type.Sliced, false);
                if (txt != null)
                {
                    txt.color = glassTextColor;
                    XingShuUITheme.ApplyCrispFont(txt, true);
                }
                return;
            }

            if (bg != null) bg.color = vanillaBgColor;
            if (txt != null) txt.color = vanillaTextColor;
        }

        internal static void InvalidateRestyleCache(GameObject pageRoot = null)
        {
            if (pageRoot == null)
            {
                _pageNodeCache.Clear();
                return;
            }
            _pageNodeCache.Remove(pageRoot.GetInstanceID());
        }

        /// <summary>
        /// Walks existing module page GameObjects and translates opaque light/game-colored surfaces,
        /// outlines, and dark text into clean translucent frosted glass styling so all 22+ views work
        /// seamlessly inside the Glassmorphism v2 container.
        /// </summary>
        internal static void RestylePageForGlass(GameObject pageRoot)
        {
            if (pageRoot == null) return;
            InvalidateRestyleCache(pageRoot);
            RestylePageInternal(pageRoot, forceRescan: true);
        }

        /// <summary>
        /// Zero-GC steady-state LateUpdate pass so dynamic controls clicked or refreshed during Update
        /// never flash vanilla artwork or dark text for even a single frame.
        /// </summary>
        internal static void RestylePageForGlassFast(GameObject pageRoot)
        {
            if (pageRoot == null) return;
            RestylePageInternal(pageRoot, forceRescan: false);
        }

        private static void RestylePageInternal(GameObject pageRoot, bool forceRescan)
        {
            EnsureControlSprites();
            int key = pageRoot.GetInstanceID();
            int childCount = pageRoot.transform.childCount;
            float now = Time.unscaledTime;

            if (!_pageNodeCache.TryGetValue(key, out CachedPageNodes nodes)
                || nodes == null
                || forceRescan
                || nodes.ChildCount != childCount
                || now >= nodes.NextScanAt)
            {
                XingShuNativeGameUI.ClearPendingBindingsUnder(pageRoot);
                Outline[] outlines = pageRoot.GetComponentsInChildren<Outline>(true);
                for (int i = 0; i < outlines.Length; i++)
                {
                    if (outlines[i] != null) UnityEngine.Object.Destroy(outlines[i]);
                }

                nodes = new CachedPageNodes
                {
                    ChildCount = childCount,
                    NextScanAt = now + 0.50f,
                    Images = pageRoot.GetComponentsInChildren<Image>(true),
                    Texts = pageRoot.GetComponentsInChildren<Text>(true),
                };
                _pageNodeCache[key] = nodes;
            }

            ApplyGlassStylingToNodes(nodes);
        }

        private static void ApplyGlassStylingToNodes(CachedPageNodes nodes)
        {
            Sprite sliderTrackSprite = _cachedSliderTrackSprite;
            Sprite sliderFillSprite = _cachedSliderFillSprite;
            Sprite sliderHandleSprite = _cachedSliderHandleSprite;
            Sprite toggleBgSprite = _cachedToggleBgSprite;
            Sprite toggleCheckSprite = _cachedToggleCheckSprite;
            Sprite inputSprite = _cachedInputSprite;
            Sprite pillBadgeSprite = PillBadgeSprite;
            Sprite btnNormalSprite = ButtonSprite;
            Sprite btnAccentSprite = ButtonAccentSprite;
            Sprite cardNormalSprite = CardSprite;
            Sprite cardAccentSprite = CardAccentSprite;

            Image[] images = nodes.Images;
            for (int i = 0; i < images.Length; i++)
            {
                Image img = images[i];
                if (img == null)
                {
                    nodes.NextScanAt = 0f;
                    continue;
                }
                if (img.GetComponent<Mask>() != null || img.GetComponent<RectMask2D>() != null) continue;

                string n = img.gameObject.name;
                if (n.Equals("Viewport", StringComparison.OrdinalIgnoreCase)) continue;
                string sp = img.sprite != null ? (img.sprite.name ?? "") : "";

                // 1. Sliders (track, fill, hide star trail, crisp handle)
                if (img.GetComponentInParent<Slider>() != null)
                {
                    if (n.Equals("Trail", StringComparison.OrdinalIgnoreCase) || sp.IndexOf("SliderTrail", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (img.enabled) img.enabled = false;
                        continue;
                    }
                    if (n.Equals("Background", StringComparison.OrdinalIgnoreCase) || sp.IndexOf("SliderBg", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ApplySprite(img, sliderTrackSprite, Image.Type.Sliced, false);
                        continue;
                    }
                    if (n.IndexOf("Fill", StringComparison.OrdinalIgnoreCase) >= 0 && img.transform.parent != null && img.transform.parent.name.IndexOf("Fill Area", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ApplySprite(img, sliderFillSprite, Image.Type.Sliced, false);
                        continue;
                    }
                    if (n.IndexOf("Handle", StringComparison.OrdinalIgnoreCase) >= 0 && img.transform.parent != null && img.transform.parent.name.IndexOf("Handle", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ApplySprite(img, sliderHandleSprite, Image.Type.Simple, true);
                        continue;
                    }
                }

                // 2. Toggles (box background & checkmark)
                if (img.GetComponentInParent<Toggle>() != null)
                {
                    if (n.Equals("Background", StringComparison.OrdinalIgnoreCase))
                    {
                        ApplySprite(img, toggleBgSprite, Image.Type.Sliced, false);
                        continue;
                    }
                    if (n.Equals("Checkmark", StringComparison.OrdinalIgnoreCase))
                    {
                        ApplySprite(img, toggleCheckSprite, Image.Type.Sliced, false);
                        continue;
                    }
                }

                // 3. Buttons (distinguish selected/primary vs standard glass pill)
                if (img.GetComponent<Button>() != null)
                {
                    bool isAccentBtn = img.sprite == btnAccentSprite
                        || sp.IndexOf("button_selected", StringComparison.OrdinalIgnoreCase) >= 0
                        || sp.IndexOf("button_primary", StringComparison.OrdinalIgnoreCase) >= 0
                        || sp.IndexOf("BtnSmallOk", StringComparison.OrdinalIgnoreCase) >= 0
                        || sp.IndexOf("BtnLargeOk", StringComparison.OrdinalIgnoreCase) >= 0
                        || sp.IndexOf("TabGold", StringComparison.OrdinalIgnoreCase) >= 0
                        || (img.sprite != btnNormalSprite && img.color.r > 0.72f && img.color.b > 0.30f && img.color.g < 0.52f);
                    ApplySprite(img, isAccentBtn ? btnAccentSprite : btnNormalSprite, Image.Type.Sliced, false);
                    continue;
                }

                // 4. InputFields
                if (img.GetComponent<InputField>() != null)
                {
                    ApplySprite(img, inputSprite, Image.Type.Sliced, false);
                    continue;
                }

                // 5. Scrollbar parts
                if (img.GetComponentInParent<Scrollbar>() != null) continue;

                // 6. Hotkey / Status badges
                if ((n.IndexOf("Badge", StringComparison.OrdinalIgnoreCase) >= 0
                     || sp.IndexOf("hotkey_badge", StringComparison.OrdinalIgnoreCase) >= 0
                     || sp.IndexOf("UidPill", StringComparison.OrdinalIgnoreCase) >= 0)
                    && n.IndexOf("Card", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    ApplySprite(img, pillBadgeSprite, Image.Type.Sliced, false);
                    continue;
                }

                // 7. Cards & section containers (including EngineSelectorCard, EnableRowCard, Header, Status, Shortcut_*, Native_RoundedLightCard)
                bool isSmallDeco = n.IndexOf("Dot", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Swatch", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Divider", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isSmallDeco) continue;

                bool looksLikeCard = n.IndexOf("Card", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.Equals("Header", StringComparison.OrdinalIgnoreCase)
                    || n.Equals("Status", StringComparison.OrdinalIgnoreCase)
                    || n.StartsWith("Shortcut_", StringComparison.OrdinalIgnoreCase)
                    || n.StartsWith("Row_", StringComparison.OrdinalIgnoreCase)
                    || n.IndexOf("Feature", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Panel", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Section", StringComparison.OrdinalIgnoreCase) >= 0
                    || sp.IndexOf("card_base", StringComparison.OrdinalIgnoreCase) >= 0
                    || sp.IndexOf("card_feature", StringComparison.OrdinalIgnoreCase) >= 0
                    || sp.IndexOf("RoundedLightCard", StringComparison.OrdinalIgnoreCase) >= 0
                    || sp.IndexOf("RoundedDarkFrame", StringComparison.OrdinalIgnoreCase) >= 0
                    || sp.IndexOf("LightPattern", StringComparison.OrdinalIgnoreCase) >= 0
                    || (img.color.a > 0.08f && sp.IndexOf("UISprite", StringComparison.OrdinalIgnoreCase) >= 0 && img.rectTransform.rect.height >= 36f);

                if (looksLikeCard)
                {
                    bool accent = n.IndexOf("Header", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("Feature", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("EngineSelector", StringComparison.OrdinalIgnoreCase) >= 0
                        || sp.IndexOf("card_feature", StringComparison.OrdinalIgnoreCase) >= 0;
                    ApplySprite(img, accent ? cardAccentSprite : cardNormalSprite, Image.Type.Sliced, false);
                    continue;
                }

                if (img.color.a > 0.08f && sp.IndexOf("button_", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ApplySprite(img, btnNormalSprite, Image.Type.Sliced, false);
                }
            }

            Text[] texts = nodes.Texts;
            for (int i = 0; i < texts.Length; i++)
            {
                Text txt = texts[i];
                if (txt == null)
                {
                    nodes.NextScanAt = 0f;
                    continue;
                }
                XingShuUITheme.ApplyCrispFont(txt, true);

                Color c = txt.color;
                float lum = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                string name = txt.gameObject.name;

                if (txt.GetComponentInParent<Button>() != null)
                {
                    if (lum < 0.58f && c != TextPrimary) txt.color = TextPrimary;
                    continue;
                }

                bool isBadgeText = name.IndexOf("Badge", StringComparison.OrdinalIgnoreCase) >= 0
                    || (txt.transform.parent != null && txt.transform.parent.name.IndexOf("Badge", StringComparison.OrdinalIgnoreCase) >= 0);
                if (isBadgeText)
                {
                    if (lum < 0.55f) txt.color = AccentMint;
                    continue;
                }

                if (name.IndexOf("Value", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Speed", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Zoom", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (c != AccentBlue) txt.color = AccentBlue;
                }
                else if (name.IndexOf("Status", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Hint", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Indicator", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (lum < 0.62f && c != AccentMint) txt.color = AccentMint;
                }
                else if (txt.fontStyle == FontStyle.Bold
                    || name == "Title"
                    || name == "Text"
                    || name == "Name"
                    || name == "CardTitle"
                    || name == "Label"
                    || name == "EngineLabel")
                {
                    if (lum < 0.75f && c != TextPrimary) txt.color = TextPrimary;
                }
                else if (lum < 0.75f && c != TextSecondary)
                {
                    txt.color = TextSecondary;
                }
            }
        }

        private static void ApplySprite(Image img, Sprite sprite, Image.Type type, bool preserveAspect)
        {
            if (img.sprite != sprite) img.sprite = sprite;
            if (img.type != type) img.type = type;
            if (img.preserveAspect != preserveAspect) img.preserveAspect = preserveAspect;
            if (img.color != Color.white) img.color = Color.white;
        }
    }

    /// <summary>
    /// Built-in overview page inside the Glassmorphism v2 Function Center listing
    /// mounted external optimization mods & plugins registered via XingShuCenterPluginHost.
    /// </summary>
    internal sealed class XingShuPluginHubView
    {
        private readonly GameObject _root;
        private readonly Action _onSwitchOriginalStyle;
        private Text _pluginSummaryText;
        private GameObject _content;
        private int _lastCount = -1;

        internal GameObject Root => _root;

        internal XingShuPluginHubView(Transform parent, Action onSwitchOriginalStyle)
        {
            _onSwitchOriginalStyle = onSwitchOriginalStyle;
            _root = XingShuUIFactory.CreateUIObject("Page_PluginHub", parent.gameObject);
            BuildUI();
            UpdateSync();
        }

        private void BuildUI()
        {
            XingShuViewCardHelper.CreatePageScroll(_root, "PluginHubScroll", out _content);

            GameObject hero = XingShuViewCardHelper.CreateCardContainer(_content, "Card_GlassFrameworkHeader", 156f, featureHeader: true);
            Text title = XingShuUIFactory.CreateText(hero, "Title", "毛玻璃扩展框架 · 外部优化 Mod / 插件宿主 (v2)", 18, XingShuGlassSkin.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(title.rectTransform, new Vector2(20f, -12f), new Vector2(560f, 28f));

            Text desc = XingShuUIFactory.CreateText(
                hero,
                "Description",
                "本界面为未来第三方优化 Mod 与外部功能插件提供统一毛玻璃圆角容器。所有插件通过 XingShuCenterPluginHost 注册后自动出现在左侧「扩展框架」栏位中，PC 与安卓端共享同一宿主契约。",
                13,
                XingShuGlassSkin.TextSecondary,
                TextAnchor.UpperLeft);
            XingShuViewCardHelper.PlaceStretchTop(desc.rectTransform, 44f, 44f, 40f);

            _pluginSummaryText = XingShuUIFactory.CreateText(hero, "Status", "", 13, XingShuGlassSkin.AccentMint, TextAnchor.MiddleLeft);
            XingShuViewCardHelper.PlaceStretchTop(_pluginSummaryText.rectTransform, 94f, 26f, 240f);

            Button backBtn = XingShuViewCardHelper.CreateActionButton(hero, "SwitchToOriginal", "切换原版 UI 皮肤", () => _onSwitchOriginalStyle?.Invoke(), 172f, 34f, 13);
            XingShuViewCardHelper.PlaceTopRight(backBtn.GetComponent<RectTransform>(), new Vector2(-20f, -102f), new Vector2(172f, 34f));

            RebuildMountedPluginCards();
        }

        private void RebuildMountedPluginCards()
        {
            IXingShuExternalPage[] pages = XingShuCenterPluginHost.GetPagesSnapshot();
            _lastCount = pages.Length;

            if (_pluginSummaryText != null)
            {
                _pluginSummaryText.text = pages.Length > 0
                    ? $"已挂载外部扩展页：{pages.Length} 个 · 运行时：{XingShuRuntimeHost.RuntimeVersion}"
                    : $"当前暂无外部插件注册页面（内置模块均已自适应毛玻璃外观）· 运行时：{XingShuRuntimeHost.RuntimeVersion}";
            }

            if (_content == null) return;
            for (int i = _content.transform.childCount - 1; i >= 1; i--)
            {
                UnityEngine.Object.Destroy(_content.transform.GetChild(i).gameObject);
            }

            if (pages.Length == 0)
            {
                GameObject guide = XingShuViewCardHelper.CreateCardContainer(_content, "Card_PluginSdkHint", 136f);
                Text gTitle = XingShuUIFactory.CreateText(guide, "Title", "外部优化 Mod 接入说明", 16, XingShuGlassSkin.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
                XingShuViewCardHelper.PlaceTopLeft(gTitle.rectTransform, new Vector2(20f, -12f), new Vector2(400f, 24f));

                Text gDesc = XingShuUIFactory.CreateText(
                    guide,
                    "Description",
                    "外部 C# 插件或优化模块只需调用：\nXingShuCenterPluginHost.RegisterSimplePage(id, title, category, subtitle, order, buildContent, onSync)\n即可免改星枢核心代码，直接拥有毛玻璃侧边栏入口、滚动容器与统一卡片控件。",
                    13,
                    XingShuGlassSkin.TextSecondary,
                    TextAnchor.UpperLeft);
                XingShuViewCardHelper.PlaceStretchTop(gDesc.rectTransform, 42f, 76f, 40f);
            }
            else
            {
                for (int i = 0; i < pages.Length; i++)
                {
                    IXingShuExternalPage p = pages[i];
                    GameObject card = XingShuViewCardHelper.CreateCardContainer(_content, "Card_Ext_" + p.Id, 92f);
                    Text t = XingShuUIFactory.CreateText(card, "Title", $"{p.Title}  ({p.Category})", 16, XingShuGlassSkin.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
                    XingShuViewCardHelper.PlaceTopLeft(t.rectTransform, new Vector2(20f, -12f), new Vector2(560f, 26f));

                    Text s = XingShuUIFactory.CreateText(card, "Description", string.IsNullOrEmpty(p.Subtitle) ? $"标识：{p.Id} · 排序：{p.Order}" : p.Subtitle, 13, XingShuGlassSkin.TextSecondary, TextAnchor.MiddleLeft);
                    XingShuViewCardHelper.PlaceStretchTop(s.rectTransform, 44f, 30f, 40f);
                }
            }
            XingShuGlassSkin.RestylePageForGlass(_root);
        }

        internal void UpdateSync()
        {
            IXingShuExternalPage[] pages = XingShuCenterPluginHost.GetPagesSnapshot();
            if (pages.Length != _lastCount)
            {
                RebuildMountedPluginCards();
            }
        }
    }
}

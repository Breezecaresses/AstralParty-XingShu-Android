using System;
using XingShuRuntime.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace XingShuRuntime.UI
{
    /// <summary>
    /// 功能中心 - 击飞预测 (Knockback Prediction) Standalone UI 页面 (Tab Index 2)
    /// 纯粹的 View 层，100% 委托 MonsterKillProbability 底层状态与调整契约
    /// </summary>
    internal sealed class XingShuKnockbackView
    {
        private readonly GameObject _root;

        // Card 1: Switch & Status
        private Toggle _enableToggle;
        private Text _statusText;

        // Card 2: Target & Preview
        private Button _previewBtn;
        private Text _previewBtnText;
        private Button _prevTargetBtn;
        private Button _nextTargetBtn;
        private Button _detailPageBtn;
        private Text _targetSummaryText;

        // Card 3: Fight Text Style
        private Text _styleSummaryText;
        private Button _fontMinusBtn;
        private Button _fontPlusBtn;
        private Button _xLeftBtn;
        private Button _xRightBtn;
        private Button _yUpBtn;
        private Button _yDownBtn;
        private Button _resetStyleBtn;

        // Visual V3 Controls
        private Text _previewLine1Text;
        private Text _previewLine2Text;
        private Button _autoColorBtn;
        private Text _autoColorBtnText;
        private Button _manualColorBtn;
        private Text _manualColorBtnText;
        private Button _rainbow100Btn;
        private Text _rainbow100BtnText;

        private InputField _probColorInput;
        private Image _probColorSwatch;
        private Text _probColorErrorText;

        private InputField _targetColorInput;
        private Image _targetColorSwatch;
        private Text _targetColorErrorText;

        // State caching (0 GC)
        private bool _suppressCallbacks;
        private bool _lastObservedEnabled;
        private bool _lastObservedPreview;
        private int _lastObservedFontSize = -1;
        private float _lastObservedFontX = float.NaN;
        private float _lastObservedFontY = float.NaN;
        private string _lastObservedProbColor = "";
        private string _lastObservedTargetColor = "";
        private string _lastObservedStatus = "";
        private string _lastObservedTargetSummary = "";
        private bool _lastObservedAutoColor;
        private bool _lastObservedRainbow100;

        internal GameObject Root => _root;

        internal XingShuKnockbackView(Transform parent)
        {
            _root = XingShuUIFactory.CreateUIObject("Page_Knockback", parent.gameObject);
            BuildUI();
        }

        private void BuildUI()
        {
            XingShuViewCardHelper.CreatePageScroll(_root, "KnockbackScrollView", out GameObject contentArea);

            // 1. 总开关与运行状态
            BuildSwitchCard(contentArea);

            // 2. 目标与拖动卡预览
            BuildTargetCard(contentArea);

            // 3. 局内战斗浮字外观
            BuildStyleCard(contentArea);

            // 4. 计算口径与说明
            BuildInfoCard(contentArea);

            UpdateSync(force: true);
        }

        private void BuildSwitchCard(GameObject parent)
        {
            GameObject cardGo = XingShuViewCardHelper.CreateCardContainer(parent, "SwitchCard", 96f);

            Text cardTitle = XingShuUIFactory.CreateText(cardGo, "CardTitle", "击飞预测总开关", 16, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(cardTitle.rectTransform, new Vector2(24f, -16f), new Vector2(220f, 24f));

            _statusText = XingShuUIFactory.CreateText(cardGo, "StatusText", "等待进入对局", 13, XingShuUITheme.SecondaryText, TextAnchor.MiddleLeft);
            RectTransform htRt = _statusText.rectTransform;
            htRt.anchorMin = htRt.anchorMax = htRt.pivot = Vector2.zero;
            htRt.anchoredPosition = new Vector2(24f, 16f);
            htRt.sizeDelta = new Vector2(460f, 22f);

            _enableToggle = XingShuUIFactory.CreateToggle(cardGo, "EnableToggle", "预测功能", MonsterKillProbability.IsEnabled, OnEnableToggleChanged);
            RectTransform tgRt = _enableToggle.GetComponent<RectTransform>();
            tgRt.anchorMin = tgRt.anchorMax = new Vector2(1f, 0.5f);
            tgRt.pivot = new Vector2(1f, 0.5f);
            tgRt.anchoredPosition = new Vector2(-24f, 0f);
            tgRt.sizeDelta = new Vector2(180f, 32f);
        }

        private void BuildTargetCard(GameObject parent)
        {
            GameObject cardGo = XingShuViewCardHelper.CreateCardContainer(parent, "TargetCard", 180f);

            Text cardTitle = XingShuUIFactory.CreateText(cardGo, "CardTitle", "战局目标与卡牌预览", 16, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(cardTitle.rectTransform, new Vector2(24f, -16f), new Vector2(220f, 24f));

            _previewBtn = XingShuViewCardHelper.CreateActionButton(cardGo, "Btn_PreviewToggle", "拖动卡预览：开", OnPreviewToggleClicked, 140f, 30f, 12);
            XingShuViewCardHelper.PlaceTopRight(_previewBtn.GetComponent<RectTransform>(), new Vector2(-24f, -12f), new Vector2(140f, 30f));
            _previewBtnText = _previewBtn.GetComponentInChildren<Text>();

            _targetSummaryText = XingShuUIFactory.CreateText(cardGo, "TargetSummary", "当前无交战或可见怪物目标", 13, XingShuUITheme.PrimaryText, TextAnchor.UpperLeft);
            XingShuViewCardHelper.PlaceStretchTop(_targetSummaryText.rectTransform, 50f, 75f, 48f);
            _targetSummaryText.lineSpacing = 1.15f;

            float btnW = 120f;
            float btnH = 30f;
            float startX = 24f;
            float gap = 12f;

            _prevTargetBtn = XingShuViewCardHelper.CreateActionButton(cardGo, "Btn_PrevTarget", "◀ 上个目标", OnPrevTargetClicked, btnW, btnH, 12);
            RectTransform ptRt = _prevTargetBtn.GetComponent<RectTransform>();
            ptRt.anchorMin = ptRt.anchorMax = ptRt.pivot = Vector2.zero;
            ptRt.anchoredPosition = new Vector2(startX, 14f);

            _detailPageBtn = XingShuViewCardHelper.CreateActionButton(cardGo, "Btn_DetailPage", "详情翻页", OnDetailPageClicked, btnW, btnH, 12);
            RectTransform dpRt = _detailPageBtn.GetComponent<RectTransform>();
            dpRt.anchorMin = dpRt.anchorMax = dpRt.pivot = Vector2.zero;
            dpRt.anchoredPosition = new Vector2(startX + btnW + gap, 14f);

            _nextTargetBtn = XingShuViewCardHelper.CreateActionButton(cardGo, "Btn_NextTarget", "下个目标 ▶", OnNextTargetClicked, btnW, btnH, 12);
            RectTransform ntRt = _nextTargetBtn.GetComponent<RectTransform>();
            ntRt.anchorMin = ntRt.anchorMax = ntRt.pivot = Vector2.zero;
            ntRt.anchoredPosition = new Vector2(startX + (btnW + gap) * 2, 14f);
        }

        private void BuildStyleCard(GameObject parent)
        {
            GameObject cardGo = XingShuViewCardHelper.CreateCardContainer(parent, "StyleCard", 376f);

            Text cardTitle = XingShuUIFactory.CreateText(cardGo, "CardTitle", "局内战斗浮字外观", 16, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(cardTitle.rectTransform, new Vector2(24f, -16f), new Vector2(220f, 24f));

            _styleSummaryText = XingShuUIFactory.CreateText(cardGo, "StyleSummary", "当前样式：字号 26  |  X: 50  |  Y: -32", 13, XingShuUITheme.NumericAccent, TextAnchor.MiddleRight, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopRight(_styleSummaryText.rectTransform, new Vector2(-24f, -16f), new Vector2(340f, 24f));

            GameObject previewBox = XingShuUIFactory.CreateUIObject("LivePreviewBox", cardGo);
            XingShuViewCardHelper.PlaceStretchTop(previewBox.GetComponent<RectTransform>(), 44f, 58f, 48f);

            Sprite pbSprite = XingShuUIAssetService.GetSprite("button_secondary.png");
            Image pbBg = previewBox.AddComponent<Image>();
            pbBg.sprite = pbSprite ?? XingShuUIAssetService.WhiteSprite;
            pbBg.type = (pbSprite != null && pbSprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
            pbBg.color = new Color(0.10f, 0.13f, 0.18f, 0.90f);

            _previewLine1Text = XingShuUIFactory.CreateText(previewBox, "PreviewLine1", "", 20, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold, HorizontalWrapMode.Overflow, VerticalWrapMode.Overflow);
            _previewLine1Text.supportRichText = true;
            XingShuViewCardHelper.PlaceStretchTop(_previewLine1Text.rectTransform, 6f, 22f, 28f);

            _previewLine2Text = XingShuUIFactory.CreateText(previewBox, "PreviewLine2", "", 20, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold, HorizontalWrapMode.Overflow, VerticalWrapMode.Overflow);
            _previewLine2Text.supportRichText = true;
            XingShuViewCardHelper.PlaceStretchTop(_previewLine2Text.rectTransform, 30f, 22f, 28f);

            float bW = 150f;
            float bH = 28f;
            float startX = 24f;
            float col2X = startX + bW + 16f;

            _fontMinusBtn = XingShuViewCardHelper.CreateActionButton(cardGo, "Btn_FontMinus", "字号 −2", delegate { MonsterKillProbability.AdjustStyle(-2, 0f, 0f); UpdateSync(force: true); }, bW, bH, 12);
            XingShuViewCardHelper.PlaceTopLeft(_fontMinusBtn.GetComponent<RectTransform>(), new Vector2(startX, -112f), new Vector2(bW, bH));

            _fontPlusBtn = XingShuViewCardHelper.CreateActionButton(cardGo, "Btn_FontPlus", "字号 +2", delegate { MonsterKillProbability.AdjustStyle(2, 0f, 0f); UpdateSync(force: true); }, bW, bH, 12);
            XingShuViewCardHelper.PlaceTopLeft(_fontPlusBtn.GetComponent<RectTransform>(), new Vector2(col2X, -112f), new Vector2(bW, bH));

            _xLeftBtn = XingShuViewCardHelper.CreateActionButton(cardGo, "Btn_XLeft", "◀ 左移 10", delegate { MonsterKillProbability.AdjustStyle(0, -10f, 0f); UpdateSync(force: true); }, bW, bH, 12);
            XingShuViewCardHelper.PlaceTopLeft(_xLeftBtn.GetComponent<RectTransform>(), new Vector2(startX, -146f), new Vector2(bW, bH));

            _xRightBtn = XingShuViewCardHelper.CreateActionButton(cardGo, "Btn_XRight", "右移 10 ▶", delegate { MonsterKillProbability.AdjustStyle(0, 10f, 0f); UpdateSync(force: true); }, bW, bH, 12);
            XingShuViewCardHelper.PlaceTopLeft(_xRightBtn.GetComponent<RectTransform>(), new Vector2(col2X, -146f), new Vector2(bW, bH));

            _yUpBtn = XingShuViewCardHelper.CreateActionButton(cardGo, "Btn_YUp", "▲ 上移 10", delegate { MonsterKillProbability.AdjustStyle(0, 0f, -10f); UpdateSync(force: true); }, bW, bH, 12);
            XingShuViewCardHelper.PlaceTopLeft(_yUpBtn.GetComponent<RectTransform>(), new Vector2(startX, -180f), new Vector2(bW, bH));

            _yDownBtn = XingShuViewCardHelper.CreateActionButton(cardGo, "Btn_YDown", "下移 10 ▼", delegate { MonsterKillProbability.AdjustStyle(0, 0f, 10f); UpdateSync(force: true); }, bW, bH, 12);
            XingShuViewCardHelper.PlaceTopLeft(_yDownBtn.GetComponent<RectTransform>(), new Vector2(col2X, -180f), new Vector2(bW, bH));

            Text modeLbl = XingShuUIFactory.CreateText(cardGo, "Mode_Label", "概率颜色模式", 13, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft);
            XingShuViewCardHelper.PlaceTopLeft(modeLbl.rectTransform, new Vector2(startX, -216f), new Vector2(80f, 28f));

            _autoColorBtn = XingShuViewCardHelper.CreateActionButton(cardGo, "Btn_AutoColor", "自动分段", delegate
            {
                MonsterKillProbability.AutoColorEnabled = true;
                UpdateSync(force: true);
            }, 84f, 28f, 12);
            _autoColorBtnText = _autoColorBtn.GetComponentInChildren<Text>();
            XingShuViewCardHelper.PlaceTopLeft(_autoColorBtn.GetComponent<RectTransform>(), new Vector2(108f, -216f), new Vector2(84f, 28f));

            _manualColorBtn = XingShuViewCardHelper.CreateActionButton(cardGo, "Btn_ManualColor", "手动", delegate
            {
                MonsterKillProbability.AutoColorEnabled = false;
                UpdateSync(force: true);
            }, 64f, 28f, 12);
            _manualColorBtnText = _manualColorBtn.GetComponentInChildren<Text>();
            XingShuViewCardHelper.PlaceTopLeft(_manualColorBtn.GetComponent<RectTransform>(), new Vector2(198f, -216f), new Vector2(64f, 28f));

            _rainbow100Btn = XingShuViewCardHelper.CreateActionButton(cardGo, "Btn_Rainbow100", "100%炫彩高亮：关", delegate
            {
                MonsterKillProbability.Rainbow100Enabled = !MonsterKillProbability.Rainbow100Enabled;
                UpdateSync(force: true);
            }, 160f, 28f, 12);
            _rainbow100BtnText = _rainbow100Btn.GetComponentInChildren<Text>();
            XingShuViewCardHelper.PlaceTopLeft(_rainbow100Btn.GetComponent<RectTransform>(), new Vector2(268f, -216f), new Vector2(160f, 28f));

            CreateColorRow(
                cardGo,
                "ProbColor",
                "手动概率色",
                -252f,
                MonsterKillProbability.ProbabilityColorHex,
                out _probColorInput,
                out _probColorSwatch,
                out _probColorErrorText,
                delegate(string hex)
                {
                    MonsterKillProbability.SetProbabilityColor(hex);
                    MonsterKillProbability.AutoColorEnabled = false;
                });

            CreateColorRow(
                cardGo,
                "TargetColor",
                "目标名字颜色",
                -288f,
                MonsterKillProbability.TargetColorHex,
                out _targetColorInput,
                out _targetColorSwatch,
                out _targetColorErrorText,
                delegate(string hex) { MonsterKillProbability.SetTargetColor(hex); });

            _resetStyleBtn = XingShuViewCardHelper.CreateActionButton(cardGo, "Btn_ResetStyle", "恢复默认文字与颜色样式 (字号26/X:50/Y:-32/自动分段)", delegate
            {
                MonsterKillProbability.AdjustStyle(0, 0f, 0f, reset: true);
                if (_probColorErrorText != null) _probColorErrorText.text = "";
                if (_targetColorErrorText != null) _targetColorErrorText.text = "";
                UpdateSync(force: true);
            }, bW * 2 + 16f, 28f, 12);
            XingShuViewCardHelper.PlaceTopLeft(_resetStyleBtn.GetComponent<RectTransform>(), new Vector2(startX, -326f), new Vector2(bW * 2 + 16f, 28f));
        }

        private void CreateColorRow(
            GameObject parent,
            string rowName,
            string labelTitle,
            float yPos,
            string initialHex,
            out InputField inputField,
            out Image swatchImg,
            out Text errorText,
            Action<string> onColorApplied)
        {
            Text label = XingShuUIFactory.CreateText(parent, rowName + "_Label", labelTitle, 13, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft);
            XingShuViewCardHelper.PlaceTopLeft(label.rectTransform, new Vector2(24f, yPos), new Vector2(80f, 28f));

            GameObject swatchObj = XingShuUIFactory.CreateUIObject(rowName + "_Swatch", parent);
            XingShuViewCardHelper.PlaceTopLeft(swatchObj.GetComponent<RectTransform>(), new Vector2(108f, yPos), new Vector2(26f, 26f));

            Sprite swatchSprite = XingShuUIAssetService.GetSprite("hotkey_badge.png") ?? XingShuUIAssetService.WhiteSprite;
            swatchImg = swatchObj.AddComponent<Image>();
            swatchImg.sprite = swatchSprite;
            swatchImg.type = (swatchSprite != null && swatchSprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
            MonsterKillProbability.TryParseHexColor(initialHex, out Color initCol, out _);
            swatchImg.color = initCol;

            inputField = XingShuUIFactory.CreateInputField(
                parent,
                rowName + "_Input",
                "#RRGGBB",
                initialHex,
                "Knockback." + rowName,
                null,
                null,
                12);
            XingShuViewCardHelper.PlaceTopLeft(inputField.GetComponent<RectTransform>(), new Vector2(142f, yPos), new Vector2(96f, 28f));

            InputField capturedInput = inputField;
            Image capturedSwatch = swatchImg;

            var presets = new[]
            {
                new { Name = "金色", Hex = "#FFD85A", Col = new Color(1f, 0.847f, 0.353f, 1f) },
                new { Name = "白色", Hex = "#F4F7FF", Col = new Color(0.957f, 0.969f, 1f, 1f) },
                new { Name = "青色", Hex = "#38BDF8", Col = new Color(0.22f, 0.741f, 0.973f, 1f) },
                new { Name = "绿色", Hex = "#4ADE80", Col = new Color(0.29f, 0.871f, 0.502f, 1f) },
                new { Name = "红色", Hex = "#F87171", Col = new Color(0.973f, 0.443f, 0.443f, 1f) }
            };

            errorText = XingShuUIFactory.CreateText(parent, rowName + "_Error", "", 11, XingShuUITheme.Danger, TextAnchor.MiddleLeft);
            XingShuViewCardHelper.PlaceTopLeft(errorText.rectTransform, new Vector2(504f, yPos), new Vector2(180f, 28f));
            Text capturedErr = errorText;

            float pStartX = 246f;
            float pWidth = 46f;
            float pGap = 5f;
            for (int i = 0; i < presets.Length; i++)
            {
                var p = presets[i];
                Button pBtn = XingShuViewCardHelper.CreateActionButton(parent, $"{rowName}_Preset_{i}", p.Name, delegate
                {
                    capturedInput.text = p.Hex;
                    capturedSwatch.color = p.Col;
                    capturedErr.text = "";
                    onColorApplied(p.Hex);
                    UpdateSync(force: true);
                }, pWidth, 26f, 11);
                XingShuViewCardHelper.PlaceTopLeft(pBtn.GetComponent<RectTransform>(), new Vector2(pStartX + i * (pWidth + pGap), yPos), new Vector2(pWidth, 26f));
            }

            capturedInput.onValueChanged.AddListener(delegate(string val)
            {
                if (_suppressCallbacks) return;
                if (MonsterKillProbability.TryParseHexColor(val, out Color col, out string canonical))
                {
                    capturedSwatch.color = col;
                    capturedErr.text = "";
                    onColorApplied(canonical);
                    UpdateSync(force: false);
                }
            });

            capturedInput.onEndEdit.AddListener(delegate(string val)
            {
                if (_suppressCallbacks) return;
                if (MonsterKillProbability.TryParseHexColor(val, out Color col, out string canonical))
                {
                    capturedInput.text = canonical;
                    capturedSwatch.color = col;
                    capturedErr.text = "";
                    onColorApplied(canonical);
                    UpdateSync(force: true);
                }
                else
                {
                    capturedErr.text = "格式无效 (需 #RRGGBB)";
                    UpdateSync(force: true);
                }
            });
        }

        private void BuildInfoCard(GameObject parent)
        {
            GameObject cardGo = XingShuViewCardHelper.CreateCardContainer(parent, "InfoCard", 110f);

            Text cardTitle = XingShuUIFactory.CreateText(cardGo, "CardTitle", "计算口径与说明", 14, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(cardTitle.rectTransform, new Vector2(24f, -14f), new Vector2(200f, 20f));

            Text infoText = XingShuUIFactory.CreateText(cardGo, "InfoContent",
                "• 一次攻击击飞，防御方按防御计算；双方普通 1–6 骰。\n• 概率按高低分色；主动技能冷却仅在地图上显示，战斗浮字不显示冷却。\n• 原版名字与血量保留；关闭功能中心不会停用后台预测。",
                12, XingShuUITheme.SecondaryText, TextAnchor.UpperLeft);
            XingShuViewCardHelper.PlaceStretchTop(infoText.rectTransform, 38f, 64f, 48f);
            infoText.lineSpacing = 1.2f;
        }

        private void OnEnableToggleChanged(bool value)
        {
            if (_suppressCallbacks) return;
            MonsterKillProbability.IsEnabled = value;
            UpdateSync(force: true);
        }

        private void OnPreviewToggleClicked()
        {
            MonsterKillProbability.PreviewCards = !MonsterKillProbability.PreviewCards;
            UpdateSync(force: true);
        }

        private void OnPrevTargetClicked()
        {
            MonsterKillProbability.PreviousTarget();
            UpdateSync(force: true);
        }

        private void OnNextTargetClicked()
        {
            MonsterKillProbability.NextTarget();
            UpdateSync(force: true);
        }

        private void OnDetailPageClicked()
        {
            MonsterKillProbability.NextDetail();
            UpdateSync(force: true);
        }

        internal void UpdateSync(bool force = false)
        {
            bool curEnabled = MonsterKillProbability.IsEnabled;
            bool curPreview = MonsterKillProbability.PreviewCards;
            int curFontSize = MonsterKillProbability.FightFontSize;
            float curFontX = MonsterKillProbability.FightX;
            float curFontY = MonsterKillProbability.FightY;
            bool curAutoColor = MonsterKillProbability.AutoColorEnabled;
            bool curRainbow100 = MonsterKillProbability.Rainbow100Enabled;
            string curProbColor = MonsterKillProbability.ProbabilityColorHex;
            string curTargetColor = MonsterKillProbability.TargetColorHex;
            string curStatus = MonsterKillProbability.StatusText;
            string curTargetSummary = MonsterKillProbability.GetCurrentTargetSummary();

            bool needsLiveRainbow = curRainbow100;

            if (!force && !needsLiveRainbow &&
                curEnabled == _lastObservedEnabled &&
                curPreview == _lastObservedPreview &&
                curFontSize == _lastObservedFontSize &&
                Mathf.Approximately(curFontX, _lastObservedFontX) &&
                Mathf.Approximately(curFontY, _lastObservedFontY) &&
                curAutoColor == _lastObservedAutoColor &&
                curRainbow100 == _lastObservedRainbow100 &&
                curProbColor == _lastObservedProbColor &&
                curTargetColor == _lastObservedTargetColor &&
                curStatus == _lastObservedStatus &&
                curTargetSummary == _lastObservedTargetSummary)
            {
                return;
            }

            _suppressCallbacks = true;
            try
            {
                if (curEnabled != _lastObservedEnabled || force)
                {
                    _enableToggle.isOn = curEnabled;
                    _lastObservedEnabled = curEnabled;
                }

                if (curPreview != _lastObservedPreview || force)
                {
                    if (_previewBtnText != null)
                    {
                        _previewBtnText.text = curPreview ? "拖动卡预览：开" : "拖动卡预览：关";
                        _previewBtnText.color = curPreview ? XingShuUITheme.LimeAccent : XingShuUITheme.SecondaryText;
                    }
                    _lastObservedPreview = curPreview;
                }

                if (curFontSize != _lastObservedFontSize ||
                    !Mathf.Approximately(curFontX, _lastObservedFontX) ||
                    !Mathf.Approximately(curFontY, _lastObservedFontY) ||
                    curAutoColor != _lastObservedAutoColor ||
                    curRainbow100 != _lastObservedRainbow100 || force)
                {
                    if (_styleSummaryText != null)
                    {
                        string modeStr = curAutoColor ? "自动分段" : "手动色";
                        if (curRainbow100) modeStr += "+炫彩";
                        _styleSummaryText.text = $"当前样式：字号 {curFontSize}  |  X: {curFontX:F0}  |  Y: {curFontY:F0}  |  {modeStr}";
                    }
                    _lastObservedFontSize = curFontSize;
                    _lastObservedFontX = curFontX;
                    _lastObservedFontY = curFontY;
                }

                if (curAutoColor != _lastObservedAutoColor || force)
                {
                    if (_autoColorBtnText != null)
                    {
                        _autoColorBtnText.color = curAutoColor ? XingShuUITheme.LimeAccent : XingShuUITheme.SecondaryText;
                    }
                    if (_manualColorBtnText != null)
                    {
                        _manualColorBtnText.color = (!curAutoColor) ? XingShuUITheme.LimeAccent : XingShuUITheme.SecondaryText;
                    }
                    _lastObservedAutoColor = curAutoColor;
                }

                if (curRainbow100 != _lastObservedRainbow100 || force)
                {
                    if (_rainbow100BtnText != null)
                    {
                        _rainbow100BtnText.text = curRainbow100 ? "100%炫彩高亮：开" : "100%炫彩高亮：关";
                        _rainbow100BtnText.color = curRainbow100 ? XingShuUITheme.LimeAccent : XingShuUITheme.SecondaryText;
                    }
                    _lastObservedRainbow100 = curRainbow100;
                }

                if (curProbColor != _lastObservedProbColor || force)
                {
                    if (_probColorInput != null && !_probColorInput.isFocused)
                    {
                        _probColorInput.text = curProbColor;
                    }
                    if (_probColorSwatch != null)
                    {
                        _probColorSwatch.color = MonsterKillProbability.ProbabilityColor;
                    }
                    _lastObservedProbColor = curProbColor;
                }

                if (curTargetColor != _lastObservedTargetColor || force)
                {
                    if (_targetColorInput != null && !_targetColorInput.isFocused)
                    {
                        _targetColorInput.text = curTargetColor;
                    }
                    if (_targetColorSwatch != null)
                    {
                        _targetColorSwatch.color = MonsterKillProbability.TargetColor;
                    }
                    _lastObservedTargetColor = curTargetColor;
                }

                int previewBase = Mathf.Clamp(curFontSize, 12, 20);
                string targetCol = curTargetColor;
                string p16Col = MonsterKillProbability.GetPreviewProbabilityColorHex(0.167f);
                string p100Col = MonsterKillProbability.GetPreviewProbabilityColorHex(1.000f);

                if (_previewLine1Text != null)
                {
                    _previewLine1Text.fontSize = XingShuMobileUIHelper.IsMobile ? Mathf.RoundToInt(Mathf.Max(22, previewBase) * XingShuMobilePageLayout.TextScale) : previewBase;
                    _previewLine1Text.text = $"<b><size={_previewLine1Text.fontSize}><color={p16Col}>击飞 16.7%</color>  <color={targetCol}>典狱长</color></size></b>";
                }
                if (_previewLine2Text != null)
                {
                    _previewLine2Text.fontSize = XingShuMobileUIHelper.IsMobile ? Mathf.RoundToInt(Mathf.Max(22, previewBase) * XingShuMobilePageLayout.TextScale) : previewBase;
                    _previewLine2Text.text = $"<b><size={_previewLine2Text.fontSize}><color={p100Col}>击飞 100%</color>  <color={targetCol}>闹钟猫头鹰</color></size></b>";
                }

                if (curStatus != _lastObservedStatus || force)
                {
                    if (_statusText != null)
                    {
                        _statusText.text = curStatus;
                    }
                    _lastObservedStatus = curStatus;
                }

                if (curTargetSummary != _lastObservedTargetSummary || force)
                {
                    if (_targetSummaryText != null)
                    {
                        _targetSummaryText.text = curTargetSummary;
                    }
                    _lastObservedTargetSummary = curTargetSummary;
                }
            }
            finally
            {
                _suppressCallbacks = false;
            }
        }
    }
}

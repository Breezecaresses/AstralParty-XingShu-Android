using XingShuRuntime.Core;
using XingShuRuntime.MatchPort;
using XingShuRuntime.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace XingShuRuntime.UI
{
    internal sealed class XingShuBattleStatsView
    {
        private static string StatsBgKey => XingShuMobileUIHelper.IsMobile ? XingShuConfigKeys.StatsWidgetAndroidBackground : XingShuConfigKeys.StatsWidgetPcBackground;
        private static string StatsTextKey => XingShuMobileUIHelper.IsMobile ? XingShuConfigKeys.StatsWidgetAndroidText : XingShuConfigKeys.StatsWidgetPcText;
        private static string StatsScaleKey => XingShuMobileUIHelper.IsMobile ? XingShuConfigKeys.StatsWidgetAndroidScale : XingShuConfigKeys.StatsWidgetPcScale;
        private static float StatsScaleDefault => XingShuMobileUIHelper.IsMobile ? XingShuConfigKeys.StatsWidgetAndroidScaleDefault : XingShuConfigKeys.StatsWidgetScaleDefault;
        private static string StatsLockKey => XingShuMobileUIHelper.IsMobile ? XingShuConfigKeys.StatsWidgetAndroidLock : XingShuConfigKeys.StatsWidgetPcLock;
        private static string StatsXKey => XingShuMobileUIHelper.IsMobile ? XingShuConfigKeys.StatsWidgetAndroidX : XingShuConfigKeys.StatsWidgetPcX;
        private static string StatsYKey => XingShuMobileUIHelper.IsMobile ? XingShuConfigKeys.StatsWidgetAndroidY : XingShuConfigKeys.StatsWidgetPcY;

        private readonly GameObject _root;
        private readonly XingShuMatchPlayerCard[] _players = new XingShuMatchPlayerCard[4];
        private readonly LayoutElement[] _playerLayouts = new LayoutElement[4];
        private Toggle _observeToggle;
        private Toggle _widgetLockToggle;
        private Slider _widgetBgSlider;
        private Text _widgetBgValueText;
        private Slider _widgetTextSlider;
        private Text _widgetTextValueText;
        private Slider _widgetScaleSlider;
        private Text _widgetScaleValueText;
        private XingShuStatsMetricsPanel _metricsPanel;
        private readonly Text _status;
        private readonly Toggle _floating;
        private bool _suppressCallbacks;
        private bool? _lastObservedObserve;
        private bool? _lastObservedLock;
        private float _lastObservedBg = -1f;
        private float _lastObservedTextAlpha = -1f;
        private float _lastObservedScale = -1f;
        private float _next;
        internal GameObject Root => _root;

        internal XingShuBattleStatsView(Transform parent)
        {
            _root = XingShuUIFactory.CreateUIObject("Page_BattleStats", parent.gameObject);
            XingShuDetailLayout.Fill(_root.GetComponent<RectTransform>());
            XingShuUIFactory.CreateVerticalScrollView(_root, "MatchDetails", out GameObject content);
            var header = Card(content, "MatchHeader", 112);
            XingShuDetailLayout.Text(header.transform, "对局数据 · 星枢评分 v2", 20, 12, 480, 30, 18);
            _status = XingShuDetailLayout.Text(header.transform, "等待有效对局", 20, 44, 700, 24, 12, true);
            _floating = XingShuUIFactory.CreateToggle(header, "Floating", "显示统计挂件", BattleStatsOverlay.AutoShowPreference,
                value => BattleStatsOverlay.AutoShowPreference = value);
            var toggleRt = _floating.GetComponent<RectTransform>();
            toggleRt.anchorMin = toggleRt.anchorMax = toggleRt.pivot = new Vector2(0, 1);
            toggleRt.anchoredPosition = new Vector2(20, -76); toggleRt.sizeDelta = new Vector2(280, 28);
            BuildWidgetSettingsCard(content);
            int mask = XingShuStatsMetricsPanel.ReadMask();
            float cardHeight = XingShuMatchPlayerCard.PreferredHeight(false, XingShuMobileUIHelper.IsMobile, mask);
            for (int i = 0; i < 4; i++)
            {
                var cardGo = Card(content, "Player" + i, cardHeight);
                _playerLayouts[i] = cardGo.GetComponent<LayoutElement>();
                _players[i] = new XingShuMatchPlayerCard(cardGo, false);
            }
            var note = Card(content, "RatingSource", 146);
            var text = XingShuDetailLayout.Text(note.transform,
                "评分与 Hub 相同：取综合、输出、辅助三项最高分，再按死亡次数扣分。\n输出统计含官方溢伤（以结算为准）；「实际伤害」按目标真实扣血累计，剔除溢伤，需开启局内实时观察。\n收到服务器结算后保留已观测实际伤害，其余切换为整局数据；缺失指标显示 —，不补成 0。\n辅助转账达到 20 时有基础贡献；星枢评分属于项目参考值。",
                20, 14, 0, 0, 12, true);
            XingShuDetailLayout.Fill(text.rectTransform, 20, 14, 20, 14);
            SyncWidgetSettingsFromConfig();
        }

        private void BuildWidgetSettingsCard(GameObject parent)
        {
            _suppressCallbacks = true;
            try
            {
                var cardGo = Card(parent, "Card_StatsWidgetSettings", 528f);

                Text cardTitle = XingShuUIFactory.CreateText(cardGo, "CardTitle", "对局数据挂件设置", 16, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
                RectTransform ctRt = cardTitle.rectTransform;
                ctRt.anchorMin = new Vector2(0f, 1f);
                ctRt.anchorMax = new Vector2(0f, 1f);
                ctRt.pivot = new Vector2(0f, 1f);
                ctRt.anchoredPosition = new Vector2(24f, -14f);
                ctRt.sizeDelta = new Vector2(240f, 22f);

                Text subtitle = XingShuUIFactory.CreateText(cardGo, "Subtitle", "轻量对局统计悬浮挂件独立采集、透明度、缩放与停靠锁定", 12, XingShuUITheme.SecondaryText, TextAnchor.MiddleLeft);
                RectTransform stRt = subtitle.rectTransform;
                stRt.anchorMin = new Vector2(0f, 1f);
                stRt.anchorMax = new Vector2(0f, 1f);
                stRt.pivot = new Vector2(0f, 1f);
                stRt.anchoredPosition = new Vector2(24f, -38f);
                stRt.sizeDelta = new Vector2(460f, 18f);

                bool initObserve = XingShuConfigService.GetBool(XingShuConfigKeys.MatchObserveEnabled, XingShuConfigKeys.MatchObserveEnabledDefault);
                _observeToggle = XingShuUIFactory.CreateToggleRow(cardGo, "ObserveToggle", "开启数据采集", initObserve, OnObserveToggleChanged);
                RectTransform otRt = _observeToggle.GetComponent<RectTransform>();
                otRt.anchoredPosition = new Vector2(0f, -64f);
                otRt.sizeDelta = new Vector2(0f, 32f);

                bool initLock = XingShuConfigService.GetBool(StatsLockKey, XingShuConfigKeys.StatsWidgetLockDefault);
                _widgetLockToggle = XingShuUIFactory.CreateToggleRow(cardGo, "LockToggle", "锁定挂件位置", initLock, OnWidgetLockToggleChanged);
                RectTransform ltRt = _widgetLockToggle.GetComponent<RectTransform>();
                ltRt.anchoredPosition = new Vector2(0f, -100f);
                ltRt.sizeDelta = new Vector2(0f, 32f);

                Text bgLabel = XingShuUIFactory.CreateText(cardGo, "BgLabel", "背景不透明度", 13, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft);
                RectTransform bglRt = bgLabel.rectTransform;
                bglRt.anchorMin = new Vector2(0f, 1f);
                bglRt.anchorMax = new Vector2(0f, 1f);
                bglRt.pivot = new Vector2(0f, 1f);
                bglRt.anchoredPosition = new Vector2(24f, -138f);
                bglRt.sizeDelta = new Vector2(160f, 20f);

                float initBg = XingShuConfigService.GetFloat(StatsBgKey, XingShuConfigKeys.StatsWidgetBackgroundDefault);
                _widgetBgValueText = XingShuUIFactory.CreateText(cardGo, "BgValue", $"{Mathf.RoundToInt(initBg * 100f)}%", 13, XingShuUITheme.CyanAccent, TextAnchor.MiddleRight, FontStyle.Bold);
                RectTransform bgvRt = _widgetBgValueText.rectTransform;
                bgvRt.anchorMin = new Vector2(1f, 1f);
                bgvRt.anchorMax = new Vector2(1f, 1f);
                bgvRt.pivot = new Vector2(1f, 1f);
                bgvRt.anchoredPosition = new Vector2(-24f, -138f);
                bgvRt.sizeDelta = new Vector2(80f, 20f);

                _widgetBgSlider = XingShuUIFactory.CreateSlider(cardGo, "BgSlider", 0.15f, 1.0f, initBg, OnWidgetBgSliderChanged);
                RectTransform bgsRt = _widgetBgSlider.GetComponent<RectTransform>();
                bgsRt.anchorMin = new Vector2(0f, 1f);
                bgsRt.anchorMax = new Vector2(1f, 1f);
                bgsRt.pivot = new Vector2(0.5f, 1f);
                bgsRt.anchoredPosition = new Vector2(0f, -160f);
                bgsRt.sizeDelta = new Vector2(-48f, 24f);

                Text textLabel = XingShuUIFactory.CreateText(cardGo, "TextLabel", "文字不透明度", 13, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft);
                RectTransform tlRt = textLabel.rectTransform;
                tlRt.anchorMin = new Vector2(0f, 1f);
                tlRt.anchorMax = new Vector2(0f, 1f);
                tlRt.pivot = new Vector2(0f, 1f);
                tlRt.anchoredPosition = new Vector2(24f, -190f);
                tlRt.sizeDelta = new Vector2(160f, 20f);

                float initText = XingShuConfigService.GetFloat(StatsTextKey, XingShuConfigKeys.StatsWidgetTextAlphaDefault);
                _widgetTextValueText = XingShuUIFactory.CreateText(cardGo, "TextValue", $"{Mathf.RoundToInt(initText * 100f)}%", 13, XingShuUITheme.CyanAccent, TextAnchor.MiddleRight, FontStyle.Bold);
                RectTransform tvRt = _widgetTextValueText.rectTransform;
                tvRt.anchorMin = new Vector2(1f, 1f);
                tvRt.anchorMax = new Vector2(1f, 1f);
                tvRt.pivot = new Vector2(1f, 1f);
                tvRt.anchoredPosition = new Vector2(-24f, -190f);
                tvRt.sizeDelta = new Vector2(80f, 20f);

                _widgetTextSlider = XingShuUIFactory.CreateSlider(cardGo, "TextSlider", 0.55f, 1.0f, initText, OnWidgetTextSliderChanged);
                RectTransform tsRt = _widgetTextSlider.GetComponent<RectTransform>();
                tsRt.anchorMin = new Vector2(0f, 1f);
                tsRt.anchorMax = new Vector2(1f, 1f);
                tsRt.pivot = new Vector2(0.5f, 1f);
                tsRt.anchoredPosition = new Vector2(0f, -212f);
                tsRt.sizeDelta = new Vector2(-48f, 24f);

                Text scaleLabel = XingShuUIFactory.CreateText(cardGo, "ScaleLabel", "挂件缩放比例", 13, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft);
                RectTransform sclRt = scaleLabel.rectTransform;
                sclRt.anchorMin = new Vector2(0f, 1f);
                sclRt.anchorMax = new Vector2(0f, 1f);
                sclRt.pivot = new Vector2(0f, 1f);
                sclRt.anchoredPosition = new Vector2(24f, -242f);
                sclRt.sizeDelta = new Vector2(160f, 20f);

                float initScale = XingShuConfigService.GetFloat(StatsScaleKey, StatsScaleDefault);
                _widgetScaleValueText = XingShuUIFactory.CreateText(cardGo, "ScaleValue", $"{Mathf.RoundToInt(initScale * 100f)}%", 13, XingShuUITheme.CyanAccent, TextAnchor.MiddleRight, FontStyle.Bold);
                RectTransform scvRt = _widgetScaleValueText.rectTransform;
                scvRt.anchorMin = new Vector2(1f, 1f);
                scvRt.anchorMax = new Vector2(1f, 1f);
                scvRt.pivot = new Vector2(1f, 1f);
                scvRt.anchoredPosition = new Vector2(-24f, -242f);
                scvRt.sizeDelta = new Vector2(80f, 20f);

                _widgetScaleSlider = XingShuUIFactory.CreateSlider(cardGo, "ScaleSlider", 0.60f, 2.00f, initScale, OnWidgetScaleSliderChanged);
                RectTransform scsRt = _widgetScaleSlider.GetComponent<RectTransform>();
                scsRt.anchorMin = new Vector2(0f, 1f);
                scsRt.anchorMax = new Vector2(1f, 1f);
                scsRt.pivot = new Vector2(0.5f, 1f);
                scsRt.anchoredPosition = new Vector2(0f, -264f);
                scsRt.sizeDelta = new Vector2(-48f, 24f);

                Sprite btnSprite = XingShuUIAssetService.GetSprite("button_secondary.png");
                Button resetPosBtn = XingShuUIFactory.CreateButton(cardGo, "ResetPosBtn", "重置挂件位置",
                    btnSprite,
                    XingShuUIAssetService.IsFallbackSprite(btnSprite) ? XingShuUITheme.ButtonSecondaryBase : Color.white,
                    XingShuUITheme.PrimaryText,
                    OnResetWidgetPositionClicked, 13);
                RectTransform rpbRt = resetPosBtn.GetComponent<RectTransform>();
                rpbRt.anchorMin = new Vector2(1f, 1f);
                rpbRt.anchorMax = new Vector2(1f, 1f);
                rpbRt.pivot = new Vector2(1f, 1f);
                rpbRt.anchoredPosition = new Vector2(-24f, -298f);
                rpbRt.sizeDelta = new Vector2(130f, 30f);

                _metricsPanel = new XingShuStatsMetricsPanel(cardGo, -340f, RefreshCardsNow);

                Text noteText = XingShuUIFactory.CreateText(cardGo, "Note", "位置重置为屏幕右上默认停靠点 (X: 1.0, Y: 0.12)；缩放范围为 0.60x ~ 2.00x", 11, XingShuUITheme.MutedText, TextAnchor.MiddleLeft);
                RectTransform ntRt = noteText.rectTransform;
                ntRt.anchorMin = new Vector2(0f, 0f);
                ntRt.anchorMax = new Vector2(1f, 0f);
                ntRt.pivot = new Vector2(0f, 0f);
                ntRt.anchoredPosition = new Vector2(24f, 8f);
                ntRt.sizeDelta = new Vector2(-48f, 18f);
            }
            finally
            {
                _suppressCallbacks = false;
            }
        }

        private void SyncWidgetSettingsFromConfig()
        {
            if (_suppressCallbacks) return;
            _suppressCallbacks = true;
            try
            {
                bool obs = XingShuConfigService.GetBool(XingShuConfigKeys.MatchObserveEnabled, XingShuConfigKeys.MatchObserveEnabledDefault);
                if (!_lastObservedObserve.HasValue || _lastObservedObserve.Value != obs)
                {
                    _lastObservedObserve = obs;
                    if (_observeToggle != null) XingShuUIFactory.SetToggleRowStateWithoutNotify(_observeToggle, obs);
                }

                bool lockPos = XingShuConfigService.GetBool(StatsLockKey, XingShuConfigKeys.StatsWidgetLockDefault);
                if (!_lastObservedLock.HasValue || _lastObservedLock.Value != lockPos)
                {
                    _lastObservedLock = lockPos;
                    if (_widgetLockToggle != null) XingShuUIFactory.SetToggleRowStateWithoutNotify(_widgetLockToggle, lockPos);
                }

                float bg = XingShuConfigService.GetFloat(StatsBgKey, XingShuConfigKeys.StatsWidgetBackgroundDefault);
                if (!Mathf.Approximately(_lastObservedBg, bg))
                {
                    _lastObservedBg = bg;
                    if (_widgetBgSlider != null) _widgetBgSlider.SetValueWithoutNotify(bg);
                    if (_widgetBgValueText != null) _widgetBgValueText.text = $"{Mathf.RoundToInt(bg * 100f)}%";
                }

                float textAlpha = XingShuConfigService.GetFloat(StatsTextKey, XingShuConfigKeys.StatsWidgetTextAlphaDefault);
                if (!Mathf.Approximately(_lastObservedTextAlpha, textAlpha))
                {
                    _lastObservedTextAlpha = textAlpha;
                    if (_widgetTextSlider != null) _widgetTextSlider.SetValueWithoutNotify(textAlpha);
                    if (_widgetTextValueText != null) _widgetTextValueText.text = $"{Mathf.RoundToInt(textAlpha * 100f)}%";
                }

                float scale = XingShuConfigService.GetFloat(StatsScaleKey, StatsScaleDefault);
                if (!Mathf.Approximately(_lastObservedScale, scale))
                {
                    _lastObservedScale = scale;
                    if (_widgetScaleSlider != null) _widgetScaleSlider.SetValueWithoutNotify(scale);
                    if (_widgetScaleValueText != null) _widgetScaleValueText.text = $"{Mathf.RoundToInt(scale * 100f)}%";
                }

                _metricsPanel?.SyncFromConfig();
            }
            finally
            {
                _suppressCallbacks = false;
            }
        }

        private void OnObserveToggleChanged(bool val)
        {
            if (_suppressCallbacks) return;
            if (BattleStatsOverlay.Instance != null)
            {
                BattleStatsOverlay.IsObserving = val;
            }
            else
            {
                XingShuConfigService.SetBool(XingShuConfigKeys.MatchObserveEnabled, val);
                XingShuConfigService.Save();
            }
        }

        private void OnWidgetLockToggleChanged(bool val)
        {
            if (_suppressCallbacks) return;
            XingShuConfigService.SetBool(StatsLockKey, val);
            XingShuConfigService.Save();
            BattleStatsOverlay.RefreshWidgetPreferences();
        }

        private void OnWidgetBgSliderChanged(float val)
        {
            if (_widgetBgValueText != null) _widgetBgValueText.text = $"{Mathf.RoundToInt(val * 100f)}%";
            if (_suppressCallbacks) return;
            XingShuConfigService.SetFloat(StatsBgKey, val);
            XingShuConfigService.Save();
            BattleStatsOverlay.RefreshWidgetPreferences();
        }

        private void OnWidgetTextSliderChanged(float val)
        {
            if (_widgetTextValueText != null) _widgetTextValueText.text = $"{Mathf.RoundToInt(val * 100f)}%";
            if (_suppressCallbacks) return;
            XingShuConfigService.SetFloat(StatsTextKey, val);
            XingShuConfigService.Save();
            BattleStatsOverlay.RefreshWidgetPreferences();
        }

        private void OnWidgetScaleSliderChanged(float val)
        {
            if (_widgetScaleValueText != null) _widgetScaleValueText.text = $"{Mathf.RoundToInt(val * 100f)}%";
            if (_suppressCallbacks) return;
            XingShuConfigService.SetFloat(StatsScaleKey, val);
            XingShuConfigService.Save();
            BattleStatsOverlay.RefreshWidgetPreferences();
        }

        private void OnResetWidgetPositionClicked()
        {
            XingShuConfigService.SetFloat(StatsXKey, XingShuConfigKeys.StatsWidgetXDefault);
            XingShuConfigService.SetFloat(StatsYKey, XingShuConfigKeys.StatsWidgetYDefault);
            XingShuConfigService.Save();
            BattleStatsOverlay.RefreshWidgetPreferences();
        }

        private static GameObject Card(GameObject parent, string name, float height)
        {
            var go = XingShuUIFactory.CreateUIObject(name, parent);
            go.AddComponent<LayoutElement>().preferredHeight = height;
            XingShuDetailLayout.Panel(go.GetComponent<RectTransform>());
            return go;
        }

        private void RefreshCardsNow()
        {
            var frame = BattleStatsOverlay.GetCurrentFrame();
            int mask = XingShuStatsMetricsPanel.ReadMask();
            float cardHeight = XingShuMatchPlayerCard.PreferredHeight(false, XingShuMobileUIHelper.IsMobile, mask);
            for (int i = 0; i < 4; i++)
            {
                if (_playerLayouts[i] != null) _playerLayouts[i].preferredHeight = cardHeight;
                _players[i].Refresh(frame?.Players[i], i, mask);
            }
        }

        internal void UpdateSync()
        {
            if (Time.unscaledTime < _next) return; _next = Time.unscaledTime + .25f;
            var frame = BattleStatsOverlay.GetCurrentFrame();
            _floating.SetIsOnWithoutNotify(BattleStatsOverlay.AutoShowPreference);
            _status.text = frame == null ? "等待有效对局" : frame.Status + " · " + frame.Footer;
            SyncWidgetSettingsFromConfig();
            int mask = XingShuStatsMetricsPanel.ReadMask();
            float cardHeight = XingShuMatchPlayerCard.PreferredHeight(false, XingShuMobileUIHelper.IsMobile, mask);
            for (int i = 0; i < 4; i++)
            {
                if (_playerLayouts[i] != null) _playerLayouts[i].preferredHeight = cardHeight;
                _players[i].Refresh(frame?.Players[i], i, mask);
            }
        }
    }
}

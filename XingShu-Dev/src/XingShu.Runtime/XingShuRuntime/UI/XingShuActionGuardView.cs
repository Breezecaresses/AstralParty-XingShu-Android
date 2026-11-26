using XingShuRuntime.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace XingShuRuntime.UI
{
    /// <summary>
    /// 功能中心 - 战局守护 (ActionTimeGuard) Standalone UI 页面 (Tab Index 6)
    /// 纯粹的 UI View，只保留战局守护自身的总开关、实时监控看板与运行机制卡片
    /// </summary>
    internal sealed class XingShuActionGuardView
    {
        private readonly GameObject _root;

        private Toggle _enableToggle;
        private Image _statusBadgeBg;
        private Text _statusBadgeText;
        private Text _statusDetailText;

        private Text _rpcStatusText;
        private Text _battleStatusText;
        private Text _interceptCountText;
        private Text _lastInterceptTimeText;
        private Text _reconnectCountText;
        private Text _antiCheatStatusText;

        private float _lastSyncTime;
        private bool _lastObservedEnabled;
        private bool _lastObservedBattle;
        private bool _lastObservedHook;
        private int _lastObservedInterceptCount = -1;
        private int _lastObservedReconnectCount = -1;
        private string _lastObservedInterceptTime = "";
        private long _lastObservedRoomId = -1L;
        private int _lastObservedBadgeState = -1;
        private bool _lastObservedGlassActive;
        private bool _suppressCallbacks;

        internal GameObject Root => _root;

        internal XingShuActionGuardView(Transform parent)
        {
            _root = XingShuUIFactory.CreateUIObject("Page_ActionGuard", parent.gameObject);
            BuildUI();
        }

        private void BuildUI()
        {
            XingShuViewCardHelper.CreatePageScroll(_root, "AutomationScroll", out GameObject content);

            // 1. 顶部主功能与状态卡片
            GameObject card1 = XingShuViewCardHelper.CreateCardContainer(content, "Card_HeaderStatus", 112f, featureHeader: true);

            Text titleText = XingShuUIFactory.CreateText(card1, "Title", "战局守护", 20, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(titleText.rectTransform, new Vector2(24f, -14f), new Vector2(240f, 26f));

            Text subText = XingShuUIFactory.CreateText(card1, "Subtitle", "超时保护 · 战局状态恢复", 13, XingShuUITheme.SecondaryText, TextAnchor.MiddleLeft);
            XingShuViewCardHelper.PlaceTopLeft(subText.rectTransform, new Vector2(24f, -44f), new Vector2(360f, 20f));

            GameObject badgeGo = XingShuUIFactory.CreateUIObject("StatusBadge", card1);
            RectTransform bRt = badgeGo.GetComponent<RectTransform>();
            bRt.anchorMin = bRt.anchorMax = new Vector2(1f, 1f);
            bRt.pivot = new Vector2(1f, 1f);
            bRt.anchoredPosition = new Vector2(-24f, -16f);
            bRt.sizeDelta = new Vector2(160f, 28f);

            _statusBadgeBg = badgeGo.AddComponent<Image>();
            _statusBadgeBg.sprite = XingShuUIAssetService.GetSprite("status_badge.png");
            _statusBadgeBg.type = Image.Type.Sliced;
            _statusBadgeBg.color = new Color(0.12f, 0.16f, 0.22f, 0.85f);

            _statusBadgeText = XingShuUIFactory.CreateText(badgeGo, "BadgeText", "初始化...", 12, XingShuUITheme.PrimaryText, TextAnchor.MiddleCenter, FontStyle.Bold);
            RectTransform sbtRt = _statusBadgeText.rectTransform;
            sbtRt.anchorMin = Vector2.zero;
            sbtRt.anchorMax = Vector2.one;
            sbtRt.offsetMin = Vector2.zero;
            sbtRt.offsetMax = Vector2.zero;

            _enableToggle = XingShuUIFactory.CreateToggle(card1, "EnableToggle", "启用战局守护", AutoResetActionTimeGUI.IsEnabled, OnToggleValueChanged);
            RectTransform toggleRt = _enableToggle.GetComponent<RectTransform>();
            toggleRt.anchorMin = toggleRt.anchorMax = toggleRt.pivot = Vector2.zero;
            toggleRt.anchoredPosition = new Vector2(24f, 12f);
            toggleRt.sizeDelta = new Vector2(220f, 32f);

            _statusDetailText = XingShuUIFactory.CreateText(card1, "StatusDetail", "战局中自动拦截服务端超时踢出协议并恢复同步；设置自动保存", 12, XingShuUITheme.MutedText, TextAnchor.MiddleRight);
            RectTransform sdtRt = _statusDetailText.rectTransform;
            sdtRt.anchorMin = sdtRt.anchorMax = sdtRt.pivot = new Vector2(1f, 0f);
            sdtRt.anchoredPosition = new Vector2(-24f, 18f);
            sdtRt.sizeDelta = new Vector2(400f, 20f);

            // 2. 防护状态看板卡片
            GameObject card2 = XingShuViewCardHelper.CreateCardContainer(content, "Card_Dashboard", 160f);

            Text c2Title = XingShuUIFactory.CreateText(card2, "Card2Title", "防护状态看板", 15, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(c2Title.rectTransform, new Vector2(20f, -12f), new Vector2(260f, 22f));

            BuildMetricRow(card2, "超时协议保护", out _rpcStatusText, 20f, -38f, 380f);
            BuildMetricRow(card2, "累计拦截次数", out _interceptCountText, 20f, -68f, 380f);
            BuildMetricRow(card2, "累计恢复同步", out _reconnectCountText, 20f, -98f, 380f);

            BuildMetricRow(card2, "战局运行状态", out _battleStatusText, 440f, -38f, 380f);
            BuildMetricRow(card2, "最近拦截时间", out _lastInterceptTimeText, 440f, -68f, 380f);
            BuildMetricRow(card2, "防作弊与挂机", out _antiCheatStatusText, 440f, -98f, 380f);

            Text noteText = XingShuUIFactory.CreateText(card2, "DashboardNote", "核心防护钩子由本地战局守护维持，对局异常时自动执行无感状态同步", 11, XingShuUITheme.MutedText, TextAnchor.MiddleLeft);
            RectTransform ntRt = noteText.rectTransform;
            ntRt.anchorMin = Vector2.zero;
            ntRt.anchorMax = new Vector2(1f, 0f);
            ntRt.pivot = new Vector2(0.5f, 0f);
            ntRt.anchoredPosition = new Vector2(0f, 8f);
            ntRt.sizeDelta = new Vector2(-40f, 18f);

            // 3. 运行机制说明卡片
            GameObject card3 = XingShuViewCardHelper.CreateCardContainer(content, "Card_Mechanism", 104f);

            Text c3Title = XingShuUIFactory.CreateText(card3, "Card3Title", "运行机制说明", 15, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(c3Title.rectTransform, new Vector2(20f, -12f), new Vector2(260f, 22f));

            string mechanismContent =
                "• 超时协议拦截：当对局出现超时踢出指令时，战局守护按既定逻辑拦截并请求房间全量无感同步。\n" +
                "• 战局无感拉回：若底层网络中断，将自动调用游戏原生安全重连流恢复战局，杜绝误退回登录界面。\n" +
                "• 零额外网络开销：局外大厅与主菜单保持静默待机，绝不发送任何自建网络请求，保护账号安全。";

            Text mechTxt = XingShuUIFactory.CreateText(card3, "MechanismContent", mechanismContent, 11, XingShuUITheme.SecondaryText, TextAnchor.UpperLeft);
            RectTransform mtRt = mechTxt.rectTransform;
            mtRt.anchorMin = Vector2.zero;
            mtRt.anchorMax = Vector2.one;
            mtRt.pivot = new Vector2(0f, 1f);
            mtRt.offsetMin = new Vector2(20f, 8f);
            mtRt.offsetMax = new Vector2(-20f, -34f);

            UpdateSync();
        }

        private void BuildMetricRow(GameObject parent, string label, out Text valueText, float x, float y, float width)
        {
            GameObject rowGo = XingShuUIFactory.CreateUIObject("MetricRow_" + label, parent);
            XingShuViewCardHelper.PlaceTopLeft(rowGo.GetComponent<RectTransform>(), new Vector2(x, y), new Vector2(width, 26f));

            Text lbl = XingShuUIFactory.CreateText(rowGo, "Label", label + "：", 13, XingShuUITheme.SecondaryText, TextAnchor.MiddleLeft);
            RectTransform lRt = lbl.rectTransform;
            lRt.anchorMin = Vector2.zero;
            lRt.anchorMax = new Vector2(0f, 1f);
            lRt.pivot = new Vector2(0f, 0.5f);
            lRt.anchoredPosition = Vector2.zero;
            lRt.sizeDelta = new Vector2(150f, 0f);

            valueText = XingShuUIFactory.CreateText(rowGo, "Value", "-", 13, XingShuUITheme.PrimaryText, TextAnchor.MiddleRight, FontStyle.Bold);
            RectTransform vRt = valueText.rectTransform;
            vRt.anchorMin = new Vector2(1f, 0f);
            vRt.anchorMax = Vector2.one;
            vRt.pivot = new Vector2(1f, 0.5f);
            vRt.anchoredPosition = Vector2.zero;
            vRt.sizeDelta = new Vector2(220f, 0f);
        }

        private void OnToggleValueChanged(bool value)
        {
            if (_suppressCallbacks) return;
            AutoResetActionTimeGUI.SetEnabled(value);
        }

        internal void UpdateSync()
        {
            if (Time.unscaledTime - _lastSyncTime < 0.25f) return;
            _lastSyncTime = Time.unscaledTime;

            bool enabled = AutoResetActionTimeGUI.IsEnabled;
            bool isBattle = AutoResetActionTimeGUI.IsInBattle;
            bool hookRegistered = AutoResetActionTimeGUI.IsHookRegistered;
            int interceptCount = AutoResetActionTimeGUI.InterceptedKickCount;
            int reconnectCount = AutoResetActionTimeGUI.ReconnectSuccessCount;
            string lastIntercept = AutoResetActionTimeGUI.LastInterceptTime;
            long roomId = AutoResetActionTimeGUI.CurrentRoomId;

            if (enabled != _lastObservedEnabled)
            {
                _lastObservedEnabled = enabled;
                if (_enableToggle != null && _enableToggle.isOn != enabled)
                {
                    _suppressCallbacks = true;
                    _enableToggle.isOn = enabled;
                    _suppressCallbacks = false;
                }
            }

            int badgeState = !enabled ? 0 : (!isBattle ? 1 : (hookRegistered ? 3 : 2));
            bool glassActive = XingShuGlassSkin.IsActive;
            if (_statusBadgeText != null && (badgeState != _lastObservedBadgeState || glassActive != _lastObservedGlassActive))
            {
                _lastObservedBadgeState = badgeState;
                _lastObservedGlassActive = glassActive;
                _lastObservedBattle = isBattle;
                _lastObservedHook = hookRegistered;
                if (!enabled)
                {
                    _statusBadgeText.text = "已关闭";
                    XingShuGlassSkin.ApplyStatusBadge(_statusBadgeBg, _statusBadgeText,
                        new Color(0.12f, 0.16f, 0.22f, 0.85f), XingShuUITheme.BadgeMuted, XingShuGlassSkin.TextMuted);
                }
                else if (!isBattle)
                {
                    _statusBadgeText.text = "已就绪 · 等待对局";
                    XingShuGlassSkin.ApplyStatusBadge(_statusBadgeBg, _statusBadgeText,
                        new Color(0.25f, 0.20f, 0.08f, 0.85f), XingShuUITheme.Pink, XingShuGlassSkin.AccentBlue);
                }
                else
                {
                    _statusBadgeText.text = hookRegistered ? "战局中保护生效" : "保护已启用 · 等待挂接";
                    XingShuGlassSkin.ApplyStatusBadge(_statusBadgeBg, _statusBadgeText,
                        new Color(0.08f, 0.22f, 0.16f, 0.85f), XingShuUITheme.BadgeActive, XingShuGlassSkin.AccentMint);
                }
            }

            if (_rpcStatusText != null)
            {
                string rpcText = hookRegistered ? "已就绪" : (enabled ? "等待就绪" : "未就绪");
                Color rpcColor = hookRegistered ? XingShuUITheme.Positive : (enabled ? XingShuUITheme.NumericAccent : XingShuUITheme.MutedText);
                if (_rpcStatusText.text != rpcText)
                {
                    _rpcStatusText.text = rpcText;
                    _rpcStatusText.color = rpcColor;
                }
            }

            if (_battleStatusText != null && roomId != _lastObservedRoomId || _battleStatusText != null)
            {
                _lastObservedRoomId = roomId;
                string battleText = isBattle ? (roomId > 0 ? $"对局中 (ID: {roomId})" : "战斗准备中") : "当前无有效战局";
                Color battleColor = isBattle ? new Color(0.22f, 0.74f, 0.97f) : XingShuUITheme.MutedText;
                if (_battleStatusText.text != battleText)
                {
                    _battleStatusText.text = battleText;
                    _battleStatusText.color = battleColor;
                }
            }

            if (interceptCount != _lastObservedInterceptCount)
            {
                _lastObservedInterceptCount = interceptCount;
                if (_interceptCountText != null) _interceptCountText.text = $"{interceptCount} 次";
            }

            if (lastIntercept != _lastObservedInterceptTime)
            {
                _lastObservedInterceptTime = lastIntercept;
                if (_lastInterceptTimeText != null) _lastInterceptTimeText.text = string.IsNullOrEmpty(lastIntercept) ? "无" : lastIntercept;
            }

            if (reconnectCount != _lastObservedReconnectCount)
            {
                _lastObservedReconnectCount = reconnectCount;
                if (_reconnectCountText != null) _reconnectCountText.text = $"{reconnectCount} 次";
            }

            if (_antiCheatStatusText != null)
            {
                string acText = enabled ? "已接管保护" : "未启用";
                Color acColor = enabled ? XingShuUITheme.Positive : XingShuUITheme.MutedText;
                if (_antiCheatStatusText.text != acText)
                {
                    _antiCheatStatusText.text = acText;
                    _antiCheatStatusText.color = acColor;
                }
            }
        }
    }
}

using XingShuRuntime.Targeting;
using XingShuRuntime.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace XingShuRuntime.UI
{
    /// <summary>
    /// Merged page view for Monster Action Visibility (怪物行动可视, Tab 11):
    /// combines 10-step Monster Targeting, real-time Dice Roll &amp; Path Selection visualization,
    /// and Monster Skill Cooldown Ledger.
    /// </summary>
    internal sealed class XingShuMonsterActionView
    {
        private readonly GameObject _root;
        private Toggle _targetingToggle;
        private Toggle _arrowBoostToggle;
        private Text _targetingSummary;
        private Toggle _rollMoveToggle;
        private Toggle _pathMarkersToggle;
        private Text _actionSummaryText;
        private Text _cooldownSummaryText;
        private double _nextCooldownRefresh;
        private bool _suppressCallbacks;

        internal GameObject Root => _root;

        internal XingShuMonsterActionView(Transform parent)
        {
            _root = XingShuUIFactory.CreateUIObject("Page_MonsterAction", parent.gameObject);
            BuildUI();
            UpdateSync();
        }

        private void BuildUI()
        {
            XingShuViewCardHelper.CreatePageScroll(_root, "MonsterActionScrollView", out GameObject content);

            // Card 1: 怪物索敌 · 10格推演与箭头放大
            GameObject targetCard = XingShuViewCardHelper.CreateCardContainer(content, "Card_MonsterTarget", 276f);
            Text targetTitle = XingShuUIFactory.CreateText(targetCard, "Title", "怪物索敌 · 最大10格推演", 18, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(targetTitle.rectTransform, new Vector2(20f, -14f), new Vector2(340f, 28f));

            _targetingToggle = XingShuUIFactory.CreateToggle(targetCard, "TargetingToggle", "地图索敌", MonsterTargetRuntime.IsEnabled,
                value => { if (!_suppressCallbacks) MonsterTargetRuntime.SetEnabled(value); });
            XingShuViewCardHelper.PlaceTopRight(_targetingToggle.GetComponent<RectTransform>(), new Vector2(-20f, -12f), new Vector2(150f, 32f));

            _arrowBoostToggle = XingShuUIFactory.CreateToggle(targetCard, "ArrowBoostToggle", "放大怪物箭头", MonsterTargetRuntime.ArrowBoostEnabled,
                value => { if (!_suppressCallbacks) MonsterTargetRuntime.SetArrowBoostEnabled(value); });
            XingShuViewCardHelper.PlaceTopRight(_arrowBoostToggle.GetComponent<RectTransform>(), new Vector2(-180f, -12f), new Vector2(170f, 32f));

            Text targetHint = XingShuUIFactory.CreateText(targetCard, "Description",
                "沿怪物箭头前进，不掉头；最大10格（先行1格＋剩余9格），同距按红＞蓝＞绿规则锁定。",
                12, XingShuUITheme.SecondaryText, TextAnchor.MiddleLeft);
            XingShuViewCardHelper.PlaceStretchTop(targetHint.rectTransform, 48f, 22f, 40f);

            _targetingSummary = XingShuUIFactory.CreateText(targetCard, "Summary", "等待进入 PVE 对局", 13, XingShuUITheme.PrimaryText, TextAnchor.UpperLeft);
            _targetingSummary.supportRichText = false;
            XingShuViewCardHelper.PlaceStretchTop(_targetingSummary.rectTransform, 80f, 128f, 40f);

            Button previous = XingShuViewCardHelper.CreateActionButton(targetCard, "PreviousMonster", "◀ 上个怪物", () => MonsterTargetRuntime.Select(-1), 132f, 32f);
            Button next = XingShuViewCardHelper.CreateActionButton(targetCard, "NextMonster", "下个怪物 ▶", () => MonsterTargetRuntime.Select(1), 132f, 32f);
            XingShuViewCardHelper.PlaceTopLeft(previous.GetComponent<RectTransform>(), new Vector2(20f, -224f), new Vector2(132f, 32f));
            XingShuViewCardHelper.PlaceTopLeft(next.GetComponent<RectTransform>(), new Vector2(166f, -224f), new Vector2(132f, 32f));

            // Card 2: 怪物投点数与移动路径选择可视化
            GameObject moveCard = XingShuViewCardHelper.CreateCardContainer(content, "Card_MonsterRollAndMove", 216f);
            Text moveTitle = XingShuUIFactory.CreateText(moveCard, "Title", "怪物投点数与移动选择可视", 18, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(moveTitle.rectTransform, new Vector2(20f, -14f), new Vector2(320f, 28f));

            _rollMoveToggle = XingShuUIFactory.CreateToggle(moveCard, "RollMoveToggle", "投点与选路", MonsterTargetRuntime.RollMoveEnabled,
                value => { if (!_suppressCallbacks) MonsterTargetRuntime.SetRollMoveEnabled(value); });
            XingShuViewCardHelper.PlaceTopRight(_rollMoveToggle.GetComponent<RectTransform>(), new Vector2(-20f, -12f), new Vector2(150f, 32f));

            _pathMarkersToggle = XingShuUIFactory.CreateToggle(moveCard, "PathMarkersToggle", "地图步数气泡", MonsterTargetRuntime.PathMarkersEnabled,
                value => { if (!_suppressCallbacks) MonsterTargetRuntime.SetPathMarkersEnabled(value); });
            XingShuViewCardHelper.PlaceTopRight(_pathMarkersToggle.GetComponent<RectTransform>(), new Vector2(-180f, -12f), new Vector2(170f, 32f));

            Text moveHint = XingShuUIFactory.CreateText(moveCard, "Description",
                "实时监听服务端投骰 (ThrowDiceS2C)、追击与移动路径 (MoveS2C)：怪物头顶同步展示骰点/剩步，棋盘格子标注行进步序（蓝）与追击/终点（红/橙）。",
                12, XingShuUITheme.SecondaryText, TextAnchor.UpperLeft);
            XingShuViewCardHelper.PlaceStretchTop(moveHint.rectTransform, 48f, 38f, 40f);

            _actionSummaryText = XingShuUIFactory.CreateText(moveCard, "Status", "", 13, XingShuUITheme.NumericAccent, TextAnchor.UpperLeft);
            _actionSummaryText.supportRichText = false;
            XingShuViewCardHelper.PlaceStretchTop(_actionSummaryText.rectTransform, 94f, 108f, 40f);

            // Card 3: 怪物技能冷却与状态推演
            GameObject cdCard = XingShuViewCardHelper.CreateCardContainer(content, "Card_MonsterCooldown", 246f);
            Text cdTitle = XingShuUIFactory.CreateText(cdCard, "Title", "怪物技能冷却推演", 18, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(cdTitle.rectTransform, new Vector2(20f, -14f), new Vector2(360f, 28f));

            Text cdDesc = XingShuUIFactory.CreateText(cdCard, "Description",
                "只读监听怪物主动技能施放与服务端推送，按真实回合演进冷却状态并与击飞预测浮字共享。",
                12, XingShuUITheme.SecondaryText, TextAnchor.MiddleLeft);
            XingShuViewCardHelper.PlaceStretchTop(cdDesc.rectTransform, 46f, 22f, 40f);

            _cooldownSummaryText = XingShuUIFactory.CreateText(cdCard, "Summary", "", 13, XingShuUITheme.PrimaryText, TextAnchor.UpperLeft);
            XingShuViewCardHelper.PlaceStretchTop(_cooldownSummaryText.rectTransform, 78f, 152f, 40f);
        }

        internal void UpdateSync()
        {
            if (_targetingSummary != null && _targetingSummary.text != MonsterTargetRuntime.Summary)
                _targetingSummary.text = MonsterTargetRuntime.Summary;
            if (_actionSummaryText != null && _actionSummaryText.text != MonsterTargetRuntime.ActionSummary)
                _actionSummaryText.text = MonsterTargetRuntime.ActionSummary;

            _suppressCallbacks = true;
            if (_targetingToggle != null && _targetingToggle.isOn != MonsterTargetRuntime.IsEnabled)
                _targetingToggle.isOn = MonsterTargetRuntime.IsEnabled;
            if (_arrowBoostToggle != null && _arrowBoostToggle.isOn != MonsterTargetRuntime.ArrowBoostEnabled)
                _arrowBoostToggle.isOn = MonsterTargetRuntime.ArrowBoostEnabled;
            if (_rollMoveToggle != null && _rollMoveToggle.isOn != MonsterTargetRuntime.RollMoveEnabled)
                _rollMoveToggle.isOn = MonsterTargetRuntime.RollMoveEnabled;
            if (_pathMarkersToggle != null && _pathMarkersToggle.isOn != MonsterTargetRuntime.PathMarkersEnabled)
                _pathMarkersToggle.isOn = MonsterTargetRuntime.PathMarkersEnabled;
            _suppressCallbacks = false;

            double now = ThinkingClock.RealSeconds();
            if (now >= _nextCooldownRefresh)
            {
                _nextCooldownRefresh = now + 0.25;
                if (_cooldownSummaryText != null)
                {
                    string next = MonsterCooldownRuntime.GetStatusSummary();
                    if (_cooldownSummaryText.text != next) _cooldownSummaryText.text = next;
                }
            }
        }
    }
}

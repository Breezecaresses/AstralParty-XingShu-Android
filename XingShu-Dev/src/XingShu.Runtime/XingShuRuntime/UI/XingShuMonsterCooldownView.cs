using XingShuRuntime.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace XingShuRuntime.UI
{
    /// <summary>
    /// Standalone page view for Monster Skill Cooldown Ledger (怪物冷却, Tab 12).
    /// </summary>
    internal sealed class XingShuMonsterCooldownView
    {
        private readonly GameObject _root;
        private Text _summaryText;
        private float _nextRefresh;

        internal GameObject Root => _root;

        internal XingShuMonsterCooldownView(Transform parent)
        {
            _root = XingShuUIFactory.CreateUIObject("Page_MonsterCooldown", parent.gameObject);
            BuildUI();
            UpdateSync();
        }

        private void BuildUI()
        {
            XingShuViewCardHelper.CreatePageScroll(_root, "MonsterCooldownScrollView", out GameObject content);

            GameObject statusCard = XingShuViewCardHelper.CreateCardContainer(content, "MonsterCooldownStatusCard", 230f);
            Text title = XingShuUIFactory.CreateText(statusCard, "Title", "怪物技能冷却与状态推演", 18, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(title.rectTransform, new Vector2(20f, -14f), new Vector2(460f, 28f));

            Text subtitle = XingShuUIFactory.CreateText(statusCard, "Subtitle",
                "独立运行时账本实时追踪每名怪物的主动技能施放与回合演进（状态：已挂载运行）",
                12, XingShuUITheme.SecondaryText, TextAnchor.MiddleLeft);
            XingShuViewCardHelper.PlaceStretchTop(subtitle.rectTransform, 44f, 20f, 40f);

            _summaryText = XingShuUIFactory.CreateText(statusCard, "Summary", "", 13, XingShuUITheme.PrimaryText, TextAnchor.UpperLeft);
            XingShuViewCardHelper.PlaceStretchTop(_summaryText.rectTransform, 76f, 138f, 40f);

            GameObject infoCard = XingShuViewCardHelper.CreateCardContainer(content, "MonsterCooldownInfoCard", 122f);
            Text infoTitle = XingShuUIFactory.CreateText(infoCard, "InfoTitle", "工作机制说明", 15, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(infoTitle.rectTransform, new Vector2(20f, -12f), new Vector2(260f, 22f));

            Text infoBody = XingShuUIFactory.CreateText(infoCard, "InfoBody",
                "• 仅监听服务端推送与行动通知，不发送额外轮询请求、不篡改原生技能冷却状态。\n" +
                "• 怪物施放主动技后立即记录技能冷却，并按真实回合推进推演；结果供「击飞预测」浮字与提示共用。",
                12, XingShuUITheme.SecondaryText, TextAnchor.UpperLeft);
            XingShuViewCardHelper.PlaceStretchTop(infoBody.rectTransform, 42f, 68f, 40f);
        }

        internal void UpdateSync()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.25f;
            if (_summaryText == null) return;
            string next = MonsterCooldownRuntime.GetStatusSummary();
            if (_summaryText.text != next)
            {
                _summaryText.text = next;
            }
        }
    }
}

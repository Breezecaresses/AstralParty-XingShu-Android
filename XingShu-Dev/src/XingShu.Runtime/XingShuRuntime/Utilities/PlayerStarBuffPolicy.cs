using System;
using System.Collections.Generic;

namespace XingShuRuntime.Utilities
{
internal enum StarBuffCategory
{
    Move = 0,
    Hero = 1,
    Combat = 2,
    Debuff = 3,
    Event = 4,
    Other = 5,
}

internal static class PlayerStarBuffPolicy
{
    private static readonly string[] MoveKeywords =
    {
        "疾行", "移速", "移动", "加速", "减速", "缓行", "迟缓", "迅捷",
        "冲刺", "风行", "禁足", "定身", "步伐", "步数",
    };

    private static readonly string[] DebuffKeywords =
    {
        "中毒", "诅咒", "灼烧", "流血", "眩晕", "冰冻", "沉默", "易伤",
        "虚弱", "破甲", "封禁", "失明", "麻痹", "混乱", "禁锢",
    };

    private static readonly string[] CombatKeywords =
    {
        "攻击", "防御", "护盾", "暴击", "伤害", "闪避", "反击", "狂暴",
        "增伤", "减伤", "无敌", "免疫", "连击", "强化", "吸血", "护甲",
    };

    private static readonly string[] EventKeywords =
    {
        "筹码", "星币", "金币", "抽牌", "商店", "幸运", "治愈", "治疗",
        "回复", "恢复", "再生", "折扣", "任务",
    };

    internal static StarBuffCategory ClassifyBuff(
        int buffId,
        int buffTypeValue,
        bool hasPoisonOrCurseTag,
        bool isPropertyBuff,
        string name,
        string desc)
    {
        string text = (name ?? "") + "\n" + (desc ?? "");
        if (ContainsAny(text, MoveKeywords))
        {
            return StarBuffCategory.Move;
        }
        if (hasPoisonOrCurseTag || ContainsAny(text, DebuffKeywords))
        {
            return StarBuffCategory.Debuff;
        }
        if (isPropertyBuff || buffTypeValue == 2 || buffTypeValue == 3 || buffId >= 100000)
        {
            return StarBuffCategory.Hero;
        }
        if (buffTypeValue == 4 || ContainsAny(text, CombatKeywords))
        {
            return StarBuffCategory.Combat;
        }
        if (buffTypeValue == 5 || ContainsAny(text, EventKeywords))
        {
            return StarBuffCategory.Event;
        }
        return StarBuffCategory.Other;
    }

    internal static string GetCategoryLabel(StarBuffCategory category)
    {
        switch (category)
        {
            case StarBuffCategory.Move: return "疾行/移速";
            case StarBuffCategory.Hero: return "角色专属";
            case StarBuffCategory.Combat: return "攻防战斗";
            case StarBuffCategory.Debuff: return "减益/毒咒";
            case StarBuffCategory.Event: return "事件/回复";
            default: return "其他Buff";
        }
    }

    internal static bool ShouldShowBuff(
        int buffId,
        StarBuffCategory category,
        bool catMove,
        bool catHero,
        bool catCombat,
        bool catDebuff,
        bool catEvent,
        bool catOther,
        HashSet<int> disabledIds,
        HashSet<int> enabledIds)
    {
        if (disabledIds != null && disabledIds.Contains(buffId))
        {
            return false;
        }
        if (enabledIds != null && enabledIds.Contains(buffId))
        {
            return true;
        }
        switch (category)
        {
            case StarBuffCategory.Move: return catMove;
            case StarBuffCategory.Hero: return catHero;
            case StarBuffCategory.Combat: return catCombat;
            case StarBuffCategory.Debuff: return catDebuff;
            case StarBuffCategory.Event: return catEvent;
            default: return catOther;
        }
    }

    internal static HashSet<int> ParseIdSet(string raw)
    {
        var result = new HashSet<int>();
        if (string.IsNullOrEmpty(raw))
        {
            return result;
        }
        string[] parts = raw.Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (int.TryParse(parts[i].Trim(), out int id) && id > 0)
            {
                result.Add(id);
            }
        }
        return result;
    }

    internal static string SerializeIdSet(HashSet<int> ids)
    {
        if (ids == null)
        {
            return "";
        }
        var sorted = new List<int>();
        foreach (int id in ids)
        {
            if (id > 0 && !sorted.Contains(id))
            {
                sorted.Add(id);
            }
        }
        sorted.Sort();
        if (sorted.Count == 0)
        {
            return "";
        }
        var parts = new string[sorted.Count];
        for (int i = 0; i < sorted.Count; i++)
        {
            parts[i] = sorted[i].ToString();
        }
        return string.Join(",", parts);
    }

    internal static void ComputeAnchorPosition(
        int anchorMode,
        float starMinX,
        float starMinY,
        float starWidth,
        float starHeight,
        float stripWidth,
        float stripHeight,
        float offsetX,
        float offsetY,
        out float x,
        out float y)
    {
        switch (anchorMode)
        {
            case 1: // 星级左侧
                x = starMinX - stripWidth - 4f + offsetX;
                y = starMinY + (starHeight - stripHeight) * 0.5f + offsetY;
                break;
            case 2: // 星级下方
                x = starMinX + offsetX;
                y = starMinY + starHeight + 2f + offsetY;
                break;
            default: // 星级右侧 (anchorMode == 0)
                x = starMinX + starWidth + 4f + offsetX;
                y = starMinY + (starHeight - stripHeight) * 0.5f + offsetY;
                break;
        }
    }

    private static bool ContainsAny(string text, string[] keywords)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }
        for (int i = 0; i < keywords.Length; i++)
        {
            if (text.IndexOf(keywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        return false;
    }
}
}

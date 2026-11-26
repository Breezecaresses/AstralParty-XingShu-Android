using System;
using System.Collections.Generic;
using XingShuRuntime.Utilities;

internal static class PlayerStarBuffTests
{
    private static int _checks;
    private static int _failed;

    private static void Check(bool condition, string message)
    {
        _checks++;
        if (!condition)
        {
            _failed++;
            Console.WriteLine("FAIL: " + message);
        }
    }

    private static int Main()
    {
        // 1. Extra Move Speed policy tests
        Check(PlayerCombatStatusPolicy.ResolveExtraMoveSpeed(2, 0, true, 0, 0, 0) == 2, "star move +2 resolves to +2");
        Check(PlayerCombatStatusPolicy.ResolveExtraMoveSpeed(5, 2, true, 0, 0, 0) == 3, "add 5 minus 2 resolves to +3");
        Check(PlayerCombatStatusPolicy.ResolveExtraMoveSpeed(0, 3, true, 0, 0, 0) == -3, "slow -3 resolves to -3");
        Check(PlayerCombatStatusPolicy.ResolveExtraMoveSpeed(0, 0, true, 2, 1, 0) == 0, "server reset to 0 overrides stale reconnect fallback");
        Check(PlayerCombatStatusPolicy.ResolveExtraMoveSpeed(0, 0, false, 2, 1, 0) == 3, "reconnect fallback sums AdditionAttrs + AppendMovePoint");

        Check(PlayerCombatStatusPolicy.FormatMoveSpeedToken(2) == "[color=#7CFF9B]移速+2[/color]", "format +2 token");
        Check(PlayerCombatStatusPolicy.FormatMoveSpeedToken(-1) == "[color=#FF8DA1]移速-1[/color]", "format -1 token");
        Check(PlayerCombatStatusPolicy.FormatMoveSpeedToken(0) == "", "format 0 returns empty");
        Check(PlayerCombatStatusPolicy.FormatMoveSpeedToken(0, 4) == "移速[color=#82D8FF]+4[/color]", "temp +4 displays in light blue #82D8FF");
        Check(PlayerCombatStatusPolicy.FormatMoveSpeedToken(-1, 4) == "移速[color=#FF8DA1]-1[/color][color=#82D8FF]+4[/color]", "permanent -1 + temp +4 keeps both colors");
        Check(PlayerCombatStatusPolicy.FormatMoveSpeedToken(2, 3) == "移速[color=#7CFF9B]+2[/color][color=#82D8FF]+3[/color]", "permanent +2 + temp +3 keeps both colors");

        // 1b. Temp move-speed buff extraction & breakdown anti-double-counting
        int mvDelta;
        Check(PlayerCombatStatusPolicy.TryExtractBuffTempMoveDelta(3020101, 4, 1, "感情过量摄入", "获得等量的移动速度", 0, 0, 4, out mvDelta) && mvDelta == 4, "extracts Sugar skill +4 temp speed");
        Check(PlayerCombatStatusPolicy.TryExtractBuffTempMoveDelta(21026, 0, 1, "疾行", "移动速度[color=#94FF46]+3[/color]，持续1回合", 0, 0, 0, out mvDelta) && mvDelta == 3, "extracts 疾行 +3 from colored desc");
        Check(PlayerCombatStatusPolicy.TryExtractBuffTempMoveDelta(501, 3, 2, "极速", "每层使自身移动速度+1", 0, 0, 0, out mvDelta) && mvDelta == 3, "extracts per-stack move speed +3");
        Check(!PlayerCombatStatusPolicy.TryExtractBuffTempMoveDelta(9901, 2, 1, "加急加快", "移动时投掷2枚骰子，生效一次", 0, 0, 0, out mvDelta), "does NOT mistake extra dice buff for +move");
        Check(!PlayerCombatStatusPolicy.TryExtractBuffTempMoveDelta(9902, 0, 0, "星级天赋", "达到3星时攻击力+1、移动速度+2", 0, 0, 0, out mvDelta), "does NOT extract star-upgrade passive desc");

        int permNet, tempNet;
        PlayerCombatStatusPolicy.ResolveMoveSpeedBreakdown(0, 1, true, 0, 0, 0, 4, 0, out permNet, out tempNet);
        Check(permNet == -1 && tempNet == 4, "syncs +4 immediately before server AddMove updates");
        PlayerCombatStatusPolicy.ResolveMoveSpeedBreakdown(4, 1, true, 0, 0, 0, 4, 0, out permNet, out tempNet);
        Check(permNet == -1 && tempNet == 4, "does NOT double-count once server AddMove syncs +4");
        PlayerCombatStatusPolicy.ResolveMoveSpeedBreakdown(2, 0, true, 0, 0, 0, 3, 2, out permNet, out tempNet);
        Check(permNet == 2 && tempNet == 3, "preserves 3-star +2 baseline + skill +3 before server sync");
        PlayerCombatStatusPolicy.ResolveMoveSpeedBreakdown(5, 0, true, 0, 0, 0, 3, 2, out permNet, out tempNet);
        Check(permNet == 2 && tempNet == 3, "preserves 3-star +2 baseline + skill +3 after server sync");

        // 1c. Final ATK/DEF single-value display + PVE star-upgrade base move speed
        string adBuffed = PlayerCombatStatusPolicy.FormatAtkDefTokens(1, 10, 0, 2);
        Check(adBuffed == "攻[color=#82D8FF]10[/color] 防[color=#82D8FF]2[/color]", "formats single final buffed ATK/DEF without parentheses");
        string adBase = PlayerCombatStatusPolicy.FormatAtkDefTokens(2, 2, 1, 1);
        Check(adBase == "攻[color=#FF9E7D]2[/color] 防[color=#9EC8FF]1[/color]", "formats single base ATK/DEF cleanly");

        int reqStar, pveMv;
        Check(PlayerCombatStatusPolicy.TryParsePveStarMoveBonus("达到1星时攻击力+1、移动速度+1", 3, out reqStar, out pveMv) && reqStar == 1 && pveMv == 1, "parses Molly Lv1 1-star +1 move");
        Check(PlayerCombatStatusPolicy.TryParsePveStarMoveBonus("达到3星时攻击力+1、移动速度+2", 3, out reqStar, out pveMv) && reqStar == 3 && pveMv == 2, "parses Molly Lv4 3-star +2 move");
        Check(!PlayerCombatStatusPolicy.TryParsePveStarMoveBonus("达到3星时攻击力+1、移动速度+2", 2, out reqStar, out pveMv), "skips 3-star move bonus when hero is only 2 stars");
        PlayerCombatStatusPolicy.ResolveMoveSpeedBreakdown(2, 0, true, 0, 0, 0, 0, 2, 3, out permNet, out tempNet);
        Check(permNet == 5 && tempNet == 0 && PlayerCombatStatusPolicy.FormatMoveSpeedToken(permNet, tempNet) == "[color=#7CFF9B]移速+5[/color]", "combines Molly 3-star +3 with +2 chip into 移速+5");
        PlayerCombatStatusPolicy.ResolveMoveSpeedBreakdown(2, 0, true, 0, 0, 0, 4, 2, 3, out permNet, out tempNet);
        Check(permNet == 5 && tempNet == 4, "combines PVE star +3, chip +2, and +4 temp skill buff");

        // 2. Buff classification tests
        Check(PlayerStarBuffPolicy.ClassifyBuff(21026, 1, false, false, "疾行", "移动速度+3") == StarBuffCategory.Move, "疾行 classified as Move");
        Check(PlayerStarBuffPolicy.ClassifyBuff(9001, 1, false, false, "迟缓", "移动速度-2") == StarBuffCategory.Move, "减速 classified as Move");
        Check(PlayerStarBuffPolicy.ClassifyBuff(9002, 1, true, false, "剧毒", "每回合失去1点生命") == StarBuffCategory.Debuff, "poison tag classified as Debuff");
        Check(PlayerStarBuffPolicy.ClassifyBuff(10004, 1, false, true, "治愈", "回合开始回复") == StarBuffCategory.Hero, "property buff classified as Hero");
        Check(PlayerStarBuffPolicy.ClassifyBuff(1221202, 2, false, false, "精准打击", "轨道轰炸伤害+1") == StarBuffCategory.Hero, "Mark buff classified as Hero");
        Check(PlayerStarBuffPolicy.ClassifyBuff(5001, 4, false, false, "狂暴", "攻击力+2") == StarBuffCategory.Combat, "Battle buff classified as Combat");
        Check(PlayerStarBuffPolicy.ClassifyBuff(6001, 5, false, false, "购物狂", "商店折扣") == StarBuffCategory.Event, "Event buff classified as Event");
        Check(PlayerStarBuffPolicy.ClassifyBuff(7001, 1, false, false, "神秘印章", "未知效果") == StarBuffCategory.Other, "unknown normal buff classified as Other");

        // 3. Filter & tri-state override tests
        var dis = new HashSet<int> { 5001 };
        var en = new HashSet<int> { 9002 };
        Check(PlayerStarBuffPolicy.ShouldShowBuff(21026, StarBuffCategory.Move, true, false, false, false, false, false, dis, en), "enabled Move category shows buff");
        Check(!PlayerStarBuffPolicy.ShouldShowBuff(5001, StarBuffCategory.Combat, true, true, true, true, true, true, dis, en), "explicit disabled override hides buff even if category on");
        Check(PlayerStarBuffPolicy.ShouldShowBuff(9002, StarBuffCategory.Debuff, false, false, false, false, false, false, dis, en), "explicit enabled override shows buff even if category off");

        // 4. ID set serialization round-trip
        var parsed = PlayerStarBuffPolicy.ParseIdSet(" 10004, abc, 21026; -5, 10004 , 9001 ");
        Check(parsed.Count == 3 && parsed.Contains(10004) && parsed.Contains(21026) && parsed.Contains(9001), "ParseIdSet filters invalid and deduplicates");
        Check(PlayerStarBuffPolicy.SerializeIdSet(parsed) == "9001,10004,21026", "SerializeIdSet sorts ascending");

        // 5. Anchor calculation tests
        float ax, ay;
        PlayerStarBuffPolicy.ComputeAnchorPosition(0, 100f, 50f, 80f, 24f, 60f, 28f, 4f, 0f, out ax, out ay);
        Check(Math.Abs(ax - 188f) < 0.01f && Math.Abs(ay - 48f) < 0.01f, "anchor right of stars");
        PlayerStarBuffPolicy.ComputeAnchorPosition(1, 100f, 50f, 80f, 24f, 60f, 28f, -4f, 0f, out ax, out ay);
        Check(Math.Abs(ax - 32f) < 0.01f && Math.Abs(ay - 48f) < 0.01f, "anchor left of stars");
        PlayerStarBuffPolicy.ComputeAnchorPosition(2, 100f, 50f, 80f, 24f, 60f, 28f, 0f, 2f, out ax, out ay);
        Check(Math.Abs(ax - 100f) < 0.01f && Math.Abs(ay - 78f) < 0.01f, "anchor below stars");

        Console.WriteLine("PlayerStarBuff & MoveSpeed tests: " + (_checks - _failed) + "/" + _checks + " passed");
        return _failed == 0 ? 0 : 1;
    }
}

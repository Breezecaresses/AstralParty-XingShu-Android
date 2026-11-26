using System;
using XingShuRuntime.Utilities;

internal static class MegathSkillDamageMathTests
{
    private static int _passed;

    private static void Assert(bool cond, string message)
    {
        if (!cond) throw new Exception("Assertion failed: " + message);
        _passed++;
    }

    private static void Main()
    {
        Assert(MegathSkillDamageMath.MegathHeroId == 122, "Megath HeroId is 122");
        Assert(MegathSkillDamageMath.PrecisionStrikeBuffId == 1221202, "Precision strike BuffId is 1221202");

        // 1. PVE 12202: 6 cards, total cost 7 => floor(6/2)=3 shots, ceil(7/3)=3 bonus, 3+3=6/shot => 18 total
        var pve7 = MegathSkillDamageMath.Compute(12202, true, new[] { 3, 2, 6, 3, 1 }, new[] { 1, 1, 2, 1, 1, 1 });
        Assert(pve7.CardCount == 6 && pve7.TotalCost == 7, "PVE 7-cost hand count and total cost");
        Assert(pve7.Shots == 3 && pve7.CostBonusPerShot == 3 && pve7.DamagePerShot == 6 && pve7.TotalDamage == 18, "PVE 7-cost uses ceil(7/3)=3 -> 3*6=18");
        Assert(MegathSkillDamageMath.FormatFormulaLine(pve7) == "6张(费7) = 3炮 * 6伤", "PVE 7-cost formula formatting");

        // 2. 9 cards, total cost 4 => floor(9/2)=4 shots, ceil(4/3)=2 bonus, 3+2=5/shot => 20 total (exact user check)
        var pve9c4 = MegathSkillDamageMath.Compute(12202, true, new[] { 3, 2, 6, 3, 1 }, new[] { 0, 1, 0, 1, 0, 1, 0, 1, 0 });
        Assert(pve9c4.Shots == 4 && pve9c4.CostBonusPerShot == 2 && pve9c4.DamagePerShot == 5 && pve9c4.TotalDamage == 20, "9 cards 4 cost => 4 shots * 5 dmg = 20");
        Assert(MegathSkillDamageMath.FormatFormulaLine(pve9c4) == "9张(费4) = 4炮 * 5伤", "9 cards 4 cost formula");

        // 3. 10 cards, total cost 8 => floor(10/2)=5 shots, ceil(8/3)=3 bonus, 3+3=6/shot => 30 total
        var pve10c8 = MegathSkillDamageMath.Compute(12203, true, new[] { 3, 2, 6, 3, 1, 3 }, new[] { 1, 1, 1, 1, 1, 1, 1, 1, 0, 0 });
        Assert(pve10c8.Shots == 5 && pve10c8.CostBonusPerShot == 3 && pve10c8.DamagePerShot == 6 && pve10c8.TotalDamage == 30, "10 cards 8 cost => 5*6=30");
        Assert(MegathSkillDamageMath.FormatFormulaLine(pve10c8) == "10张(费8) = 5炮 * 6伤", "No '+ 轨道炮' in default formula even for 12203");

        // 4. Precision boundaries: cost 0 => +0, cost 1..3 => +1, cost 4 => +2
        var c0 = MegathSkillDamageMath.Compute(12202, true, new[] { 3, 2, 6, 3, 1 }, new[] { 0, 0 });
        Assert(c0.CostBonusPerShot == 0 && c0.DamagePerShot == 3 && c0.TotalDamage == 3, "0 cost => +0 bonus");
        var c1 = MegathSkillDamageMath.Compute(12202, true, new[] { 3, 2, 6, 3, 1 }, new[] { 1, 0 });
        Assert(c1.CostBonusPerShot == 1 && c1.DamagePerShot == 4 && c1.TotalDamage == 4, "1 cost => ceil(1/3)=1 bonus");
        var c3 = MegathSkillDamageMath.Compute(12202, true, new[] { 3, 2, 6, 3, 1 }, new[] { 1, 2 });
        Assert(c3.CostBonusPerShot == 1 && c3.DamagePerShot == 4 && c3.TotalDamage == 4, "3 cost => ceil(3/3)=1 bonus");

        // 5. Marked monster (Precision Strike Buff 1221202) => keeps default 18 and appends marked +1/shot=21
        var marked = MegathSkillDamageMath.Compute(12203, true, new[] { 3, 2, 6, 3, 1, 3 }, new[] { 1, 1, 2, 1, 1, 1 }, hasPrecisionMarkedMonster: true);
        Assert(marked.HasPrecisionMarkedMonster && marked.TotalDamage == 18 && marked.MarkedDamagePerShot == 7 && marked.MarkedTotalDamage == 21, "Marked monster adds +1/shot reference without altering unmarked total");
        Assert(MegathSkillDamageMath.FormatFormulaLine(marked) == "6张(费7) = 3炮 * 6伤 · 精准标+1/炮=21", "Marked formula appends +1/shot total");

        // 6. PVP 12201 ignores cost scaling: 7 cards => 3 shots * 2 dmg = 6
        var pvp7 = MegathSkillDamageMath.Compute(12201, false, new[] { 2, 2 }, new[] { 3, 3, 3, 2, 1, 0, 1 });
        Assert(!pvp7.IsPveScaling && pvp7.Shots == 3 && pvp7.DamagePerShot == 2 && pvp7.TotalDamage == 6, "PVP uses fixed 2 dmg per shot");
        Assert(MegathSkillDamageMath.FormatFormulaLine(pvp7) == "7张 = 3炮 * 2伤", "PVP formula formatting");

        // 7. Fewer than 2 cards => 0 shots, 0 damage
        var low1 = MegathSkillDamageMath.Compute(12202, true, null, new[] { 3 });
        Assert(low1.Shots == 0 && low1.TotalDamage == 0, "1 card produces 0 shots and 0 damage");
        Assert(MegathSkillDamageMath.FormatFormulaLine(low1) == "1张(费3) = 不足2张(无法开炮)", "Zero shot message formatting");

        // 8. 0 cards & negative cost clamping
        var empty = MegathSkillDamageMath.Compute(12201, false, null, new int[0]);
        Assert(empty.CardCount == 0 && empty.TotalCost == 0 && empty.TotalDamage == 0, "Empty hand yields 0 damage");
        var neg = MegathSkillDamageMath.Compute(12202, true, new[] { 4, 2, 6, 3, 2 }, new[] { -2, 3, 0, 3 });
        Assert(neg.TotalCost == 6 && neg.Shots == 2 && neg.CostBonusPerShot == 4 && neg.DamagePerShot == 8 && neg.TotalDamage == 16, "Dynamic skillParams ceil(6/3)*2=4 and negative cost guard");

        Console.WriteLine("ALL MEGATH SKILL DAMAGE TESTS PASSED (" + _passed + " assertions).");
    }
}

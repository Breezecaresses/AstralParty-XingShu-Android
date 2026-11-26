using System;
using System.Collections.Generic;
using XingShuRuntime.Targeting;

internal static class MonsterActionTests
{
    private static int _passed;

    private static void Assert(bool cond, string label)
    {
        if (!cond) throw new Exception("FAIL: " + label);
        _passed++;
    }

    private static void TestRollSummary()
    {
        Assert(MonsterActionPolicy.FormatRollSummary(null, 0, 0) == "", "empty roll summary");
        Assert(MonsterActionPolicy.FormatRollSummary(new int[] { 5 }, 5, 0) == "骰 5", "single die no bonus");
        Assert(MonsterActionPolicy.FormatRollSummary(new int[] { 3, 4 }, 7, 5) == "骰 3+4=7（剩5步）", "multi die with remaining");
        Assert(MonsterActionPolicy.FormatRollSummary(new int[] { 4 }, 6, 3) == "骰 4→6步（剩3步）", "single die with move bonus");
        Assert(MonsterActionPolicy.FormatRollSummary(null, 4, 2) == "步数 4（剩2步）", "movePoint fallback");
        Assert(MonsterActionPolicy.FormatRollSummary(null, 0, 2) == "移动中（剩2步）", "remaining steps only");
    }

    private static void TestPathChoiceAndMarkers()
    {
        Assert(MonsterActionPolicy.FormatPathChoiceSummary(null, -1, 6) == "", "empty path");
        Assert(MonsterActionPolicy.FormatPathChoiceSummary(new int[] { 12, 13, 14 }, 14, 6) == "选路 #12→#13→#14 · 追击#14", "short path with pursuit");
        Assert(MonsterActionPolicy.FormatPathChoiceSummary(new int[] { 1, 2, 3, 4, 5 }, -1, 3) == "选路 #1→#2→#3…(共5格)", "truncated path");
        Assert(MonsterActionPolicy.FormatPathChoiceSummary(null, 27, 6) == "追击#27", "pursuit only");

        List<MonsterPathMarker> markers = MonsterActionPolicy.BuildStepMarkers(new int[] { 10, 11, 12, 13 }, 10, 12);
        Assert(markers.Count == 3, "skips starting currentNodeId");
        Assert(markers[0].NodeId == 11 && markers[0].StepIndex == 1 && !markers[0].IsDestination && !markers[0].IsPursuitTarget, "step 1");
        Assert(markers[1].NodeId == 12 && markers[1].StepIndex == 2 && !markers[1].IsDestination && markers[1].IsPursuitTarget, "step 2 pursuit");
        Assert(markers[2].NodeId == 13 && markers[2].StepIndex == 3 && markers[2].IsDestination && !markers[2].IsPursuitTarget, "step 3 destination");

        List<MonsterPathMarker> emptyMarkers = MonsterActionPolicy.BuildStepMarkers(new int[] { 10 }, 10, -1);
        Assert(emptyMarkers.Count == 0, "single same node produces 0 forward markers");
    }

    public static int Main()
    {
        TestRollSummary();
        TestPathChoiceAndMarkers();
        Console.WriteLine("MonsterActionTests PASS (" + _passed + " assertions)");
        return 0;
    }
}

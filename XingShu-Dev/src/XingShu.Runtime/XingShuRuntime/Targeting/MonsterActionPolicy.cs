using System;
using System.Collections.Generic;
using System.Text;

namespace XingShuRuntime.Targeting
{
    public struct MonsterPathMarker
    {
        public int NodeId;
        public int StepIndex;
        public bool IsDestination;
        public bool IsPursuitTarget;
    }

    /// <summary>
    /// Pure formatting and path-marker policy for Monster Action Visualization (dice roll + movement choice).
    /// Kept dependency-free (C# 5 compatible) for direct unit testing.
    /// </summary>
    public static class MonsterActionPolicy
    {
        public static string FormatRollSummary(IList<int> diceVals, int movePoint, int remainingSteps)
        {
            int count = diceVals != null ? diceVals.Count : 0;
            int diceSum = 0;
            StringBuilder diceSb = null;
            if (count > 0)
            {
                diceSb = new StringBuilder();
                int valid = 0;
                for (int i = 0; i < count; i++)
                {
                    int v = diceVals[i];
                    if (v < 0) v = 0;
                    diceSum += v;
                    if (valid > 0) diceSb.Append('+');
                    diceSb.Append(v);
                    valid++;
                }
            }

            var result = new StringBuilder();
            if (count > 0)
            {
                result.Append("骰 ");
                result.Append(diceSb.ToString());
                if (count > 1)
                {
                    result.Append('=').Append(diceSum);
                }
                if (movePoint > 0 && movePoint != diceSum)
                {
                    result.Append("→").Append(movePoint).Append("步");
                }
            }
            else if (movePoint > 0)
            {
                result.Append("步数 ").Append(movePoint);
            }
            else if (remainingSteps > 0)
            {
                result.Append("移动中");
            }
            else
            {
                return "";
            }

            if (remainingSteps > 0)
            {
                result.Append("（剩").Append(remainingSteps).Append("步）");
            }
            return result.ToString();
        }

        public static string FormatPathChoiceSummary(IList<int> pathNodes, int pursuitNodeId, int maxNodes)
        {
            int limit = maxNodes > 0 ? maxNodes : 6;
            int count = pathNodes != null ? pathNodes.Count : 0;
            if (count <= 0 && pursuitNodeId < 0)
            {
                return "";
            }

            var sb = new StringBuilder();
            if (count > 0)
            {
                sb.Append("选路 ");
                int shown = Math.Min(count, limit);
                for (int i = 0; i < shown; i++)
                {
                    if (i > 0) sb.Append("→");
                    sb.Append('#').Append(pathNodes[i]);
                }
                if (count > shown)
                {
                    sb.Append("…(共").Append(count).Append("格)");
                }
            }

            if (pursuitNodeId >= 0)
            {
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append("追击#").Append(pursuitNodeId);
            }

            return sb.ToString();
        }

        public static List<MonsterPathMarker> BuildStepMarkers(IList<int> pathNodes, int currentNodeId, int pursuitNodeId)
        {
            var markers = new List<MonsterPathMarker>();
            if (pathNodes == null || pathNodes.Count == 0)
            {
                return markers;
            }

            int startIndex = 0;
            if (currentNodeId >= 0 && pathNodes[0] == currentNodeId)
            {
                startIndex = 1;
            }

            int step = 0;
            for (int i = startIndex; i < pathNodes.Count; i++)
            {
                int nodeId = pathNodes[i];
                if (nodeId < 0) continue;
                step++;
                markers.Add(new MonsterPathMarker
                {
                    NodeId = nodeId,
                    StepIndex = step,
                    IsDestination = i == pathNodes.Count - 1,
                    IsPursuitTarget = pursuitNodeId >= 0 && nodeId == pursuitNodeId,
                });
            }
            return markers;
        }
    }
}

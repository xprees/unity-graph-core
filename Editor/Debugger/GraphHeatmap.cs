using System.Collections.Generic;
using UnityEngine;
using XNode;

namespace Xprees.Graph.Core.Editor.Debugger
{
    /// Largest visit and traversal counts of one graph, the scale of the heatmap.
    public readonly struct HeatScale
    {
        public readonly int MaxNodeVisits;
        public readonly int MaxEdgeCount;

        public HeatScale(int maxNodeVisits, int maxEdgeCount)
        {
            MaxNodeVisits = maxNodeVisits;
            MaxEdgeCount = maxEdgeCount;
        }
    }

    /// Heatmap math and colors. Pure, so it is testable without any window.
    public static class GraphHeatmap
    {
        public static readonly Color ColdColor = new(0.25f, 0.45f, 0.85f);
        public static readonly Color HotColor = new(1f, 0.45f, 0.1f);

        public const float MinEdgeThickness = 5f;
        public const float MaxEdgeThickness = 14f;

        /// 0 for no visits, 1 for the maximum. The log scale keeps rarely used paths visible next to a hot loop.
        public static float Normalize(int count, int max, bool logScale)
        {
            if (count <= 0 || max <= 0) return 0f;
            if (count >= max) return 1f;

            return logScale ? Mathf.Log(1f + count) / Mathf.Log(1f + max) : (float) count / max;
        }

        public static Color Evaluate(float t) => Color.Lerp(ColdColor, HotColor, Mathf.Clamp01(t));

        public static float EdgeThickness(float t) => Mathf.Lerp(MinEdgeThickness, MaxEdgeThickness, Mathf.Clamp01(t));

        /// Maximum counts per graph over everything the tracer recorded.
        public static Dictionary<NodeGraph, HeatScale> BuildScales(GraphRuntimeTracer tracer)
        {
            var nodeMax = new Dictionary<NodeGraph, int>();
            var edgeMax = new Dictionary<NodeGraph, int>();

            foreach (var pair in tracer.NodeTraces)
            {
                if (pair.Key == null || pair.Key.graph == null) continue;

                Raise(nodeMax, pair.Key.graph, pair.Value.VisitCount);
            }

            foreach (var pair in tracer.EdgeTraces)
            {
                var node = pair.Key.Output?.node;
                if (node == null || node.graph == null) continue;

                Raise(edgeMax, node.graph, pair.Value.Count);
            }

            var result = new Dictionary<NodeGraph, HeatScale>();
            foreach (var graph in nodeMax.Keys) result[graph] = new HeatScale(nodeMax[graph], edgeMax.GetValueOrDefault(graph));
            foreach (var graph in edgeMax.Keys)
            {
                if (!result.ContainsKey(graph)) result[graph] = new HeatScale(0, edgeMax[graph]);
            }

            return result;
        }

        private static void Raise(Dictionary<NodeGraph, int> map, NodeGraph graph, int value)
        {
            if (!map.TryGetValue(graph, out var current) || value > current) map[graph] = value;
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Xprees.Graph.Core.Editor.Layout
{
    /// Layered flow layout: lanes per entry, columns by flow depth (left -> right), crossing reduction inside the columns,
    /// fixed gaps, grid steps. Returns positions relative to (0, 0), which is the top-left of the arranged block.
    public static class LayeredLayout
    {
        private const int sweepIterations = 8;

        public struct Item
        {
            public int Id;
            public Vector2 Size;

            /// Current position: orders lanes and breaks ties, nothing else.
            public Vector2 Position;

            public bool IsEntry;
            public int EntryPriority;
        }

        public sealed class Lane
        {
            public int Root;

            /// Columns left -> right, each ordered top -> bottom.
            public readonly List<List<int>> Layers = new();
        }

        public sealed class Result
        {
            public readonly Dictionary<int, Vector2> Positions = new();
            public readonly List<Lane> Lanes = new();
            public readonly Dictionary<int, int> LayerOf = new();
        }

        public static Result Arrange(
            IReadOnlyList<Item> items,
            IEnumerable<LayoutEdge> edges,
            float gapH = LayoutGeometry.GapH,
            float gapV = LayoutGeometry.GapV
        )
        {
            var result = new Result();
            if (items == null || items.Count == 0) return result;

            var byId = new Dictionary<int, Item>();
            var index = new Dictionary<int, int>();
            for (var i = 0; i < items.Count; i++)
            {
                byId[items[i].Id] = items[i];
                index[items[i].Id] = i;
            }

            var graph = new FlowGraph(byId, index, edges);
            var laneTop = 0f;
            foreach (var lane in BuildLanes(byId, index, graph))
            {
                result.Lanes.Add(lane);
                var laneBottom = PlaceLane(lane, byId, graph, laneTop, gapH, gapV, result);
                laneTop = laneBottom + gapV;
                laneTop = LayoutGeometry.CeilToGrid(laneTop);
            }

            return result;
        }

        #region Graph

        /// Connections between the arranged items, outputs ordered by port, then by target position.
        private sealed class FlowGraph
        {
            public readonly Dictionary<int, List<int>> Outs = new();
            public readonly Dictionary<int, List<int>> Ins = new();

            public FlowGraph(Dictionary<int, Item> items, Dictionary<int, int> index, IEnumerable<LayoutEdge> edges)
            {
                foreach (var id in items.Keys)
                {
                    Outs[id] = new List<int>();
                    Ins[id] = new List<int>();
                }

                var ports = new Dictionary<(int, int), int>();
                foreach (var edge in edges ?? Enumerable.Empty<LayoutEdge>())
                {
                    if (edge.From == edge.To || !items.ContainsKey(edge.From) || !items.ContainsKey(edge.To)) continue;

                    var key = (edge.From, edge.To);
                    if (ports.TryGetValue(key, out var port))
                    {
                        if (edge.Port < port) ports[key] = edge.Port;
                        continue;
                    }

                    ports[key] = edge.Port;
                    Outs[edge.From].Add(edge.To);
                    Ins[edge.To].Add(edge.From);
                }

                foreach (var pair in Outs)
                {
                    var from = pair.Key;
                    pair.Value.Sort((a, b) =>
                    {
                        var byPort = ports[(from, a)].CompareTo(ports[(from, b)]);
                        if (byPort != 0) return byPort;

                        var byY = items[a].Position.y.CompareTo(items[b].Position.y);
                        return byY != 0 ? byY : index[a].CompareTo(index[b]);
                    });
                }
            }

            /// Order of <paramref name="to"/> among the outputs of <paramref name="from"/>, as a fraction in (0, 1).
            public float PortFraction(int from, int to)
            {
                var outs = Outs[from];
                return (outs.IndexOf(to) + 1f) / (outs.Count + 1f);
            }
        }

        #endregion

        #region Lanes and layers

        private static IEnumerable<Lane> BuildLanes(Dictionary<int, Item> items, Dictionary<int, int> index, FlowGraph graph)
        {
            int Compare(int a, int b)
            {
                var ia = items[a];
                var ib = items[b];
                var pa = ia.IsEntry ? ia.EntryPriority : 1;
                var pb = ib.IsEntry ? ib.EntryPriority : 1;
                if (pa != pb) return pa.CompareTo(pb);

                var byY = ia.Position.y.CompareTo(ib.Position.y);
                if (byY != 0) return byY;

                var byX = ia.Position.x.CompareTo(ib.Position.x);
                return byX != 0 ? byX : index[a].CompareTo(index[b]);
            }

            var roots = items.Keys.Where(id => items[id].IsEntry || graph.Ins[id].Count == 0).ToList();
            roots.Sort(Compare);

            var lanes = new List<Lane>();
            var laneOf = new Dictionary<int, Lane>();
            var preorder = new Dictionary<int, int>();

            void Claim(int root)
            {
                var lane = new Lane { Root = root };
                lanes.Add(lane);
                var stack = new Stack<int>();
                stack.Push(root);
                while (stack.Count > 0)
                {
                    var id = stack.Pop();
                    if (laneOf.ContainsKey(id)) continue;

                    laneOf[id] = lane;
                    preorder[id] = preorder.Count;
                    var outs = graph.Outs[id];
                    for (var i = outs.Count - 1; i >= 0; i--)
                    {
                        // Every entry starts its own lane, even when another flow leads into it
                        if (!laneOf.ContainsKey(outs[i]) && !items[outs[i]].IsEntry) stack.Push(outs[i]);
                    }
                }
            }

            foreach (var root in roots)
            {
                if (!laneOf.ContainsKey(root)) Claim(root);
            }

            // Cycles nobody enters from outside: seed them from their top-left node
            while (laneOf.Count < items.Count)
            {
                var rest = items.Keys.Where(id => !laneOf.ContainsKey(id)).ToList();
                rest.Sort(Compare);
                Claim(rest[0]);
            }

            foreach (var lane in lanes)
            {
                var members = new HashSet<int>(laneOf.Where(p => p.Value == lane).Select(p => p.Key));
                var layers = AssignLayers(lane.Root, members, graph);
                var count = layers.Values.Max() + 1;
                for (var i = 0; i < count; i++) lane.Layers.Add(new List<int>());
                foreach (var id in members.OrderBy(id => preorder[id])) lane.Layers[layers[id]].Add(id);
            }

            return lanes;
        }

        /// Longest path from the root over the forward edges; back edges (to a node on the DFS stack) are ignored, which breaks cycles.
        private static Dictionary<int, int> AssignLayers(int root, HashSet<int> members, FlowGraph graph)
        {
            var back = FindBackEdges(root, members, graph);
            var inDegree = members.ToDictionary(id => id, _ => 0);
            foreach (var from in members)
            {
                foreach (var to in graph.Outs[from])
                {
                    if (members.Contains(to) && !back.Contains((from, to))) inDegree[to]++;
                }
            }

            var layer = members.ToDictionary(id => id, _ => 0);
            var queue = new Queue<int>(members.Where(id => inDegree[id] == 0));
            while (queue.Count > 0)
            {
                var from = queue.Dequeue();
                foreach (var to in graph.Outs[from])
                {
                    if (!members.Contains(to) || back.Contains((from, to))) continue;

                    layer[to] = Mathf.Max(layer[to], layer[from] + 1);
                    if (--inDegree[to] == 0) queue.Enqueue(to);
                }
            }

            return layer;
        }

        private static HashSet<(int, int)> FindBackEdges(int root, HashSet<int> members, FlowGraph graph)
        {
            var back = new HashSet<(int, int)>();
            var state = new Dictionary<int, int>(); // 1 = on the stack, 2 = done
            var stack = new Stack<(int id, int next)>();
            stack.Push((root, 0));
            state[root] = 1;
            while (stack.Count > 0)
            {
                var (id, next) = stack.Pop();
                var outs = graph.Outs[id];
                if (next >= outs.Count)
                {
                    state[id] = 2;
                    continue;
                }

                stack.Push((id, next + 1));
                var to = outs[next];
                if (!members.Contains(to)) continue;

                state.TryGetValue(to, out var toState);
                if (toState == 1)
                {
                    back.Add((id, to));
                }
                else if (toState == 0)
                {
                    state[to] = 1;
                    stack.Push((to, 0));
                }
            }

            return back;
        }

        #endregion

        #region Crossing reduction

        private static void ReduceCrossings(Lane lane, FlowGraph graph, Dictionary<int, int> layerOf)
        {
            var layers = lane.Layers;
            if (layers.Count < 2) return;

            var best = layers.Select(l => l.ToList()).ToList();
            var bestCrossings = CountCrossings(layers, graph, layerOf);
            for (var iteration = 0; iteration < sweepIterations && bestCrossings > 0; iteration++)
            {
                var down = iteration % 2 == 0;
                Sweep(layers, graph, layerOf, down);
                var crossings = CountCrossings(layers, graph, layerOf);
                if (crossings >= bestCrossings) continue;

                bestCrossings = crossings;
                best = layers.Select(l => l.ToList()).ToList();
            }

            for (var i = 0; i < layers.Count; i++) layers[i] = best[i];
        }

        /// One barycenter pass: down orders each column by its predecessors, up by its successors. Stable, so ties keep the order.
        private static void Sweep(List<List<int>> layers, FlowGraph graph, Dictionary<int, int> layerOf, bool down)
        {
            var position = new Dictionary<int, float>();
            foreach (var layer in layers)
            {
                for (var i = 0; i < layer.Count; i++) position[layer[i]] = i;
            }

            var start = down ? 1 : layers.Count - 2;
            var end = down ? layers.Count : -1;
            var step = down ? 1 : -1;
            for (var l = start; l != end; l += step)
            {
                var layer = layers[l];
                var keys = new Dictionary<int, float>();
                foreach (var id in layer)
                {
                    var sum = 0f;
                    var count = 0;
                    if (down)
                    {
                        foreach (var from in graph.Ins[id])
                        {
                            if (!layerOf.TryGetValue(from, out var fromLayer) || fromLayer >= l) continue;

                            sum += position[from] + graph.PortFraction(from, id);
                            count++;
                        }
                    }
                    else
                    {
                        foreach (var to in graph.Outs[id])
                        {
                            if (!layerOf.TryGetValue(to, out var toLayer) || toLayer <= l) continue;

                            sum += position[to];
                            count++;
                        }
                    }

                    keys[id] = count > 0 ? sum / count : position[id];
                }

                var ordered = layer.Select((id, i) => (id, i)).OrderBy(x => keys[x.id]).ThenBy(x => x.i).Select(x => x.id).ToList();
                layers[l] = ordered;
                for (var i = 0; i < ordered.Count; i++) position[ordered[i]] = i;
            }
        }

        /// Crossings between adjacent columns, plus outputs of one node drawn out of their port order.
        private static int CountCrossings(List<List<int>> layers, FlowGraph graph, Dictionary<int, int> layerOf)
        {
            var position = new Dictionary<int, int>();
            foreach (var layer in layers)
            {
                for (var i = 0; i < layer.Count; i++) position[layer[i]] = i;
            }

            var crossings = 0;
            for (var l = 0; l < layers.Count - 1; l++)
            {
                var edges = new List<(int from, int to, float port)>();
                foreach (var from in layers[l])
                {
                    foreach (var to in graph.Outs[from])
                    {
                        if (layerOf.TryGetValue(to, out var toLayer) && toLayer == l + 1) edges.Add((from, to, graph.PortFraction(from, to)));
                    }
                }

                for (var i = 0; i < edges.Count; i++)
                {
                    for (var j = i + 1; j < edges.Count; j++)
                    {
                        var a = edges[i];
                        var b = edges[j];
                        if (a.to == b.to) continue;

                        var source = a.from == b.from ? a.port.CompareTo(b.port) : position[a.from].CompareTo(position[b.from]);
                        var target = position[a.to].CompareTo(position[b.to]);
                        if (source * target < 0) crossings++;
                    }
                }
            }

            return crossings;
        }

        /// Test hook: reduces the crossings of explicit columns (ids must be unique). Returns the crossing count after.
        internal static int ReduceCrossings(List<List<int>> layers, IEnumerable<LayoutEdge> edges)
        {
            var items = layers.SelectMany(l => l).Select((id, i) => new Item { Id = id, Position = new Vector2(0, i) }).ToList();
            var byId = items.ToDictionary(i => i.Id);
            var index = items.Select((item, i) => (item.Id, i)).ToDictionary(x => x.Id, x => x.i);
            var graph = new FlowGraph(byId, index, edges);
            var layerOf = new Dictionary<int, int>();
            for (var l = 0; l < layers.Count; l++)
            {
                foreach (var id in layers[l]) layerOf[id] = l;
            }

            var lane = new Lane();
            lane.Layers.AddRange(layers.Select(l => l.ToList()));
            ReduceCrossings(lane, graph, layerOf);
            for (var l = 0; l < layers.Count; l++) layers[l] = lane.Layers[l];
            return CountCrossings(layers, graph, layerOf);
        }

        /// Test hook: crossing count of explicit columns.
        internal static int CountCrossings(List<List<int>> layers, IEnumerable<LayoutEdge> edges)
        {
            var items = layers.SelectMany(l => l).Select((id, i) => new Item { Id = id, Position = new Vector2(0, i) }).ToList();
            var graph = new FlowGraph(items.ToDictionary(i => i.Id), items.Select((item, i) => (item.Id, i)).ToDictionary(x => x.Id, x => x.i),
                edges);
            var layerOf = new Dictionary<int, int>();
            for (var l = 0; l < layers.Count; l++)
            {
                foreach (var id in layers[l]) layerOf[id] = l;
            }

            return CountCrossings(layers, graph, layerOf);
        }

        #endregion

        #region Coordinates

        /// Places one lane from <paramref name="top"/> down. Columns: x by the widest node of the previous column + gap. Rows: a node starts
        /// level with the average of its predecessors, pushed down below the previous node of its column. Returns the lane's bottom.
        private static float PlaceLane(
            Lane lane,
            Dictionary<int, Item> items,
            FlowGraph graph,
            float top,
            float gapH,
            float gapV,
            Result result
        )
        {
            var layerOf = new Dictionary<int, int>();
            for (var l = 0; l < lane.Layers.Count; l++)
            {
                foreach (var id in lane.Layers[l]) layerOf[id] = l;
            }

            ReduceCrossings(lane, graph, layerOf);

            var y = new Dictionary<int, float>();
            var x = 0f;
            var bottom = 0f;
            for (var l = 0; l < lane.Layers.Count; l++)
            {
                var layer = lane.Layers[l];
                var previousBottom = float.NegativeInfinity;
                var width = 0f;
                foreach (var id in layer)
                {
                    var preds = graph.Ins[id].Where(from => layerOf.TryGetValue(from, out var fl) && fl < l).ToList();
                    var desired = preds.Count > 0 ? LayoutGeometry.RoundToGrid(preds.Average(from => y[from])) : 0f;
                    var min = float.IsNegativeInfinity(previousBottom) ? 0f : LayoutGeometry.CeilToGrid(previousBottom + gapV);
                    var rowY = Mathf.Max(desired, min);
                    y[id] = rowY;

                    var size = items[id].Size;
                    previousBottom = rowY + size.y;
                    width = Mathf.Max(width, size.x);
                    bottom = Mathf.Max(bottom, previousBottom);
                    result.Positions[id] = new Vector2(x, top + rowY);
                    result.LayerOf[id] = l;
                }

                x = LayoutGeometry.CeilToGrid(x + width + gapH);
            }

            return top + bottom;
        }

        #endregion

    }
}
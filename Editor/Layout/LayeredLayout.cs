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

            private FlowGraph()
            {
            }

            /// Independent copy of the connections (the waypoints of long edges are added to the copy only).
            public FlowGraph Copy()
            {
                var copy = new FlowGraph();
                foreach (var pair in Outs) copy.Outs[pair.Key] = pair.Value.ToList();
                foreach (var pair in Ins) copy.Ins[pair.Key] = pair.Value.ToList();
                return copy;
            }

            /// Puts <paramref name="newTarget"/> in the place of <paramref name="oldTarget"/> among the outputs of <paramref name="from"/>.
            public void Replace(int from, int oldTarget, int newTarget)
            {
                var outs = Outs[from];
                outs[outs.IndexOf(oldTarget)] = newTarget;
                Ins[newTarget].Add(from);
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

        /// Height of the invisible waypoint a long edge leaves in every column it skips. Real nodes are stacked around it, so the edge is
        /// not hidden behind them.
        private const float dummyHeight = 24f;

        /// Most nodes a straight edge may pass behind (its own ends are not counted).
        private const int maxNodesOnEdge = 1;

        private const int compactionPasses = 4;

        /// <summary>
        /// Places one lane from <paramref name="top"/> down.
        /// 1. Every edge that skips columns becomes a chain of invisible waypoints (one per skipped column). They take part in the crossing
        ///    reduction and in the rows, so real nodes sit above or below a long edge instead of on it.
        /// 2. Columns: x by the widest node of the previous column + gap. Rows: a node starts level with the average of its predecessors,
        ///    pushed down below the previous node of its column.
        /// 3. Compaction: a few barycenter passes pull every node (vertically, order and gaps kept) towards the average of its neighbours,
        ///    which shortens the edges.
        /// 4. Nudge: if a straight edge still passes behind more than <see cref="maxNodesOnEdge"/> nodes, the extra nodes move away from it.
        /// Returns the bottom of the lane.
        /// </summary>
        private static float PlaceLane(
            Lane lane,
            Dictionary<int, Item> realItems,
            FlowGraph realGraph,
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

            // Work on copies: the waypoints never leak into the result or the lane
            var items = new Dictionary<int, Item>(realItems);
            var graph = realGraph.Copy();
            var work = new Lane { Root = lane.Root };
            work.Layers.AddRange(lane.Layers.Select(l => l.ToList()));
            var dummies = new HashSet<int>();
            SplitLongEdges(work, items, graph, layerOf, dummies);

            ReduceCrossings(work, graph, layerOf);

            var y = new Dictionary<int, float>();
            var columnX = new float[work.Layers.Count];
            var x = 0f;
            for (var l = 0; l < work.Layers.Count; l++)
            {
                columnX[l] = x;
                var layer = work.Layers[l];
                var previousBottom = float.NegativeInfinity;
                var width = 0f;
                foreach (var id in layer)
                {
                    var size = items[id].Size;
                    var preds = graph.Ins[id].Where(from => layerOf.TryGetValue(from, out var fl) && fl < l).ToList();
                    // Real nodes keep the top-aligned rule, a waypoint sits level with the middle of what feeds it
                    var desired = preds.Count == 0
                        ? 0f
                        : dummies.Contains(id)
                            ? LayoutGeometry.RoundToGrid(preds.Average(from => y[from] + items[from].Size.y / 2f) - size.y / 2f)
                            : LayoutGeometry.RoundToGrid(preds.Average(from => y[from]));
                    var min = float.IsNegativeInfinity(previousBottom) ? 0f : LayoutGeometry.CeilToGrid(previousBottom + gapV);
                    y[id] = Mathf.Max(desired, min);
                    previousBottom = y[id] + size.y;
                    width = Mathf.Max(width, size.x);
                }

                x = LayoutGeometry.CeilToGrid(x + width + gapH);
            }

            Compact(work, items, graph, layerOf, y, gapV);
            Nudge(work, items, realGraph, layerOf, dummies, columnX, y, gapV);

            // Compaction and nudging can push rows above 0: shift the lane back so that it starts at 0
            var minY = work.Layers.SelectMany(l => l).Where(id => !dummies.Contains(id)).Min(id => y[id]);
            var bottom = 0f;
            for (var l = 0; l < work.Layers.Count; l++)
            {
                foreach (var id in work.Layers[l])
                {
                    if (dummies.Contains(id)) continue;

                    var rowY = y[id] - minY;
                    bottom = Mathf.Max(bottom, rowY + items[id].Size.y);
                    result.Positions[id] = new Vector2(columnX[l], top + rowY);
                    result.LayerOf[id] = l;
                }
            }

            return top + bottom;
        }

        /// Replaces every edge that skips columns by a chain of waypoints, one in each skipped column.
        private static void SplitLongEdges(
            Lane work,
            Dictionary<int, Item> items,
            FlowGraph graph,
            Dictionary<int, int> layerOf,
            HashSet<int> dummies
        )
        {
            var next = -1;
            foreach (var from in work.Layers.SelectMany(l => l).Where(id => id >= 0).ToList())
            {
                foreach (var to in graph.Outs[from].ToList())
                {
                    if (!layerOf.TryGetValue(from, out var fromLayer) || !layerOf.TryGetValue(to, out var toLayer) || toLayer - fromLayer < 2)
                    {
                        continue;
                    }

                    var previous = from;
                    for (var l = fromLayer + 1; l < toLayer; l++)
                    {
                        var id = next--;
                        items[id] = new Item { Id = id, Size = new Vector2(0f, dummyHeight), Position = items[from].Position };
                        graph.Outs[id] = new List<int>();
                        graph.Ins[id] = new List<int>();
                        if (previous == from)
                        {
                            // Keeps the port order of the real node: the waypoint takes the place of the target
                            graph.Replace(from, to, id);
                        }
                        else
                        {
                            graph.Outs[previous].Add(id);
                            graph.Ins[id].Add(previous);
                        }

                        work.Layers[l].Add(id);
                        layerOf[id] = l;
                        dummies.Add(id);
                        previous = id;
                    }

                    graph.Outs[previous].Add(to);
                    var ins = graph.Ins[to];
                    ins[ins.IndexOf(from)] = previous;
                }
            }
        }

        /// Pulls every node vertically towards the middle of its neighbours. Column order and gaps are kept, so the crossings do not change.
        private static void Compact(
            Lane work,
            Dictionary<int, Item> items,
            FlowGraph graph,
            Dictionary<int, int> layerOf,
            Dictionary<int, float> y,
            float gapV
        )
        {
            for (var pass = 0; pass < compactionPasses; pass++)
            {
                var forward = pass % 2 == 0;
                var start = forward ? 0 : work.Layers.Count - 1;
                var step = forward ? 1 : -1;
                for (var l = start; l >= 0 && l < work.Layers.Count; l += step)
                {
                    var layer = work.Layers[l];
                    for (var i = 0; i < layer.Count; i++)
                    {
                        var id = layer[i];
                        var neighbours = graph.Ins[id].Concat(graph.Outs[id]).Where(layerOf.ContainsKey).ToList();
                        if (neighbours.Count == 0) continue;

                        var size = items[id].Size;
                        var desired = LayoutGeometry.RoundToGrid(neighbours.Average(n => y[n] + items[n].Size.y / 2f) - size.y / 2f);
                        var min = i == 0
                            ? float.NegativeInfinity
                            : LayoutGeometry.CeilToGrid(y[layer[i - 1]] + items[layer[i - 1]].Size.y + gapV);
                        var max = i == layer.Count - 1
                            ? float.PositiveInfinity
                            : Mathf.Floor((y[layer[i + 1]] - gapV - size.y) / LayoutGeometry.Grid) * LayoutGeometry.Grid;
                        if (min > max) continue;

                        y[id] = Mathf.Clamp(desired, min, max);
                    }
                }
            }
        }

        /// While a straight edge passes behind more than <see cref="maxNodesOnEdge"/> real nodes, moves the extra ones away from it, pushing their
        /// column neighbours along so nothing overlaps.
        private static void Nudge(
            Lane work,
            Dictionary<int, Item> items,
            FlowGraph realGraph,
            Dictionary<int, int> layerOf,
            HashSet<int> dummies,
            float[] columnX,
            Dictionary<int, float> y,
            float gapV
        )
        {
            for (var round = 0; round < 24; round++)
            {
                var moved = false;
                foreach (var from in work.Layers.SelectMany(l => l).Where(id => id >= 0).ToList())
                {
                    foreach (var to in realGraph.Outs[from])
                    {
                        if (!layerOf.TryGetValue(from, out var fl) || !layerOf.TryGetValue(to, out var tl) || tl - fl < 2) continue;

                        var p0 = new Vector2(columnX[fl] + items[from].Size.x, y[from] + items[from].Size.y / 2f);
                        var p1 = new Vector2(columnX[tl], y[to] + items[to].Size.y / 2f);
                        var blockers = BlockersOf(work, items, dummies, columnX, y, fl, tl, p0, p1);
                        if (blockers.Count <= maxNodesOnEdge) continue;

                        // The first one stays, every further one steps off the edge (to the side it already sits on)
                        for (var k = maxNodesOnEdge; k < blockers.Count; k++)
                        {
                            MoveOffEdge(work, items, columnX, y, layerOf, blockers[k], p0, p1, gapV);
                            moved = true;
                        }
                    }
                }

                if (!moved) break;
            }
        }

        /// Real nodes strictly between the two columns that the straight edge p0 -> p1 passes behind, left to right.
        private static List<int> BlockersOf(
            Lane work,
            Dictionary<int, Item> items,
            HashSet<int> dummies,
            float[] columnX,
            Dictionary<int, float> y,
            int fromLayer,
            int toLayer,
            Vector2 p0,
            Vector2 p1
        )
        {
            var list = new List<int>();
            for (var l = fromLayer + 1; l < toLayer; l++)
            {
                foreach (var id in work.Layers[l])
                {
                    if (dummies.Contains(id)) continue;

                    var size = items[id].Size;
                    if (SegmentHitsRect(p0, p1, new Rect(columnX[l], y[id], size.x, size.y))) list.Add(id);
                }
            }

            return list;
        }

        private static bool SegmentHitsRect(Vector2 p0, Vector2 p1, Rect rect)
        {
            if (Mathf.Approximately(p1.x, p0.x)) return false;

            // The part of the segment above the rectangle's x range, compared with its y range
            var t0 = Mathf.Clamp01((rect.xMin - p0.x) / (p1.x - p0.x));
            var t1 = Mathf.Clamp01((rect.xMax - p0.x) / (p1.x - p0.x));
            var yA = Mathf.Lerp(p0.y, p1.y, t0);
            var yB = Mathf.Lerp(p0.y, p1.y, t1);
            return Mathf.Max(yA, yB) >= rect.yMin && Mathf.Min(yA, yB) <= rect.yMax;
        }

        /// Moves the node off the edge (up when it sits above the edge, else down) and pushes its column neighbours along.
        private static void MoveOffEdge(
            Lane work,
            Dictionary<int, Item> items,
            float[] columnX,
            Dictionary<int, float> y,
            Dictionary<int, int> layerOf,
            int id,
            Vector2 p0,
            Vector2 p1,
            float gapV
        )
        {
            var l = layerOf[id];
            var size = items[id].Size;
            var t = Mathf.Clamp01((columnX[l] + size.x / 2f - p0.x) / (p1.x - p0.x));
            var edgeY = Mathf.Lerp(p0.y, p1.y, t);
            var column = work.Layers[l];
            var index = column.IndexOf(id);
            if (y[id] + size.y / 2f < edgeY)
            {
                y[id] -= Mathf.Max(LayoutGeometry.Grid, LayoutGeometry.CeilToGrid(y[id] + size.y - edgeY + gapV));
                for (var i = index - 1; i >= 0; i--) y[column[i]] = Mathf.Min(y[column[i]], y[column[i + 1]] - gapV - items[column[i]].Size.y);
            }
            else
            {
                y[id] += Mathf.Max(LayoutGeometry.Grid, LayoutGeometry.CeilToGrid(edgeY - y[id] + gapV));
                for (var i = index + 1; i < column.Count; i++)
                    y[column[i]] = Mathf.Max(y[column[i]], y[column[i - 1]] + items[column[i - 1]].Size.y + gapV);
            }
        }

        #endregion

    }
}
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XNode;
using Xprees.Graph.Core.Base;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Editor.Layout;

namespace Xprees.Graph.Core.Tests
{
    public class GraphLayoutTests
    {
        private static readonly Vector2 nodeSize = new(200, 80);

        #region Helpers

        private static List<LayeredLayout.Item> Items(int count, Vector2? size = null, params int[] entries) =>
            Enumerable.Range(0, count).Select(i => new LayeredLayout.Item
            {
                Id = i, Size = size ?? nodeSize, Position = new Vector2(i * 10, i * 10), IsEntry = entries.Contains(i), EntryPriority = 1,
            }).ToList();

        private static LayoutEdge E(int from, int to, int port = 0) => new(from, to, port);

        private static void AssertNoOverlaps(IReadOnlyList<Rect> rects)
        {
            for (var i = 0; i < rects.Count; i++)
            {
                for (var j = i + 1; j < rects.Count; j++)
                {
                    Assert.IsFalse(LayoutGeometry.Overlaps(rects[i], rects[j]), $"{rects[i]} overlaps {rects[j]}");
                }
            }
        }

        private static GraphLayoutModel Model(params Rect[] nodes)
        {
            var model = new GraphLayoutModel();
            foreach (var rect in nodes) model.Add(rect);
            return model;
        }

        private static Rect R(float x, float y, float w = 200, float h = 80) => new(x, y, w, h);

        #endregion

        #region Layered layout

        [Test]
        public void Chain_OneColumnPerNode_SameRow()
        {
            var result = LayeredLayout.Arrange(Items(3), new[] { E(0, 1), E(1, 2) });

            Assert.AreEqual(1, result.Lanes.Count);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, new[] { result.LayerOf[0], result.LayerOf[1], result.LayerOf[2] });
            Assert.AreEqual(0f, result.Positions[0].x);
            Assert.AreEqual(LayoutGeometry.CeilToGrid(200 + LayoutGeometry.GapH), result.Positions[1].x);
            Assert.AreEqual(result.Positions[0].y, result.Positions[2].y);
        }

        [Test]
        public void Branch_ChildrenInOneColumn_InPortOrder()
        {
            // Port 1 target listed first: the port order must still win
            var result = LayeredLayout.Arrange(Items(3), new[] { E(0, 2, 1), E(0, 1, 0) });

            Assert.AreEqual(result.LayerOf[1], result.LayerOf[2]);
            Assert.Less(result.Positions[1].y, result.Positions[2].y, "port 0 above port 1");
            Assert.GreaterOrEqual(result.Positions[2].y - (result.Positions[1].y + nodeSize.y), LayoutGeometry.GapV);
        }

        [Test]
        public void Merge_TakesTheLongestPath()
        {
            // 0 -> 1 -> 2 -> 3 and 0 -> 3: 3 sits after 2
            var result = LayeredLayout.Arrange(Items(4), new[] { E(0, 1), E(1, 2), E(2, 3), E(0, 3, 1) });

            Assert.AreEqual(3, result.LayerOf[3]);
        }

        [Test]
        public void Loop_BackEdgeIgnored()
        {
            var result = LayeredLayout.Arrange(Items(3, null, 0), new[] { E(0, 1), E(1, 2), E(2, 1) });

            Assert.AreEqual(0, result.LayerOf[0]);
            Assert.AreEqual(1, result.LayerOf[1]);
            Assert.AreEqual(2, result.LayerOf[2]);
        }

        [Test]
        public void PureCycle_WithoutRoot_IsStillArranged()
        {
            var result = LayeredLayout.Arrange(Items(3), new[] { E(0, 1), E(1, 2), E(2, 0) });

            Assert.AreEqual(3, result.Positions.Count);
            Assert.AreEqual(0, result.Lanes[0].Root, "seeded from the top-left node");
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, result.LayerOf.Values);
        }

        [Test]
        public void SeveralEntries_OneLaneEach_StackedVertically()
        {
            // Lane A: 0 -> 1, lane B: 2 -> 3, 2 is an entry with an incoming edge from 1 (still its own lane)
            var items = Items(4, null, 0, 2);
            var result = LayeredLayout.Arrange(items, new[] { E(0, 1), E(2, 3), E(1, 2) });

            Assert.AreEqual(2, result.Lanes.Count);
            Assert.AreEqual(0, result.Lanes[0].Root);
            Assert.AreEqual(2, result.Lanes[1].Root);
            var laneABottom = Mathf.Max(result.Positions[0].y, result.Positions[1].y) + nodeSize.y;
            Assert.GreaterOrEqual(result.Positions[2].y, laneABottom + LayoutGeometry.GapV);
        }

        [Test]
        public void MainStart_IsTheFirstLane()
        {
            var items = Items(2, null, 0, 1);
            var start = items[1];
            start.EntryPriority = 0;
            start.Position = new Vector2(0, 5000);
            items[1] = start;

            var result = LayeredLayout.Arrange(items, new LayoutEdge[0]);

            Assert.AreEqual(1, result.Lanes[0].Root);
            Assert.Less(result.Positions[1].y, result.Positions[0].y);
        }

        [Test]
        public void ArrangedNodes_NeverOverlap_AndSitOnGridSteps()
        {
            var items = Enumerable.Range(0, 12).Select(i => new LayeredLayout.Item
            {
                Id = i, Size = new Vector2(150 + i * 17 % 160, 60 + i * 23 % 140), Position = new Vector2(i * 37 % 300, i * 53 % 400),
            }).ToList();
            var edges = new[]
                { E(0, 1), E(0, 2, 1), E(1, 3), E(2, 3), E(3, 4), E(4, 1), E(5, 6), E(6, 7, 0), E(6, 8, 1), E(6, 9, 2), E(9, 10), E(8, 10) };

            var result = LayeredLayout.Arrange(items, edges);

            AssertNoOverlaps(items.Select(i => new Rect(result.Positions[i.Id], i.Size)).ToList());
            foreach (var position in result.Positions.Values)
            {
                Assert.AreEqual(0f, position.x % LayoutGeometry.Grid, 0.001f);
                Assert.AreEqual(0f, position.y % LayoutGeometry.Grid, 0.001f);
            }
        }

        [Test]
        public void CrossingReduction_UntanglesTwistedColumns()
        {
            var layers = new List<List<int>> { new() { 0, 1 }, new() { 2, 3 } };
            var edges = new[] { E(0, 3), E(1, 2) };
            Assert.AreEqual(1, LayeredLayout.CountCrossings(layers, edges));

            Assert.AreEqual(0, LayeredLayout.ReduceCrossings(layers, edges));
        }

        #endregion

        #region Align / distribute

        [Test]
        public void Align_MovesOnOneAxisOnly()
        {
            var rects = new Dictionary<int, Rect> { [0] = R(10, 0, 100), [1] = R(50, 200, 300) };

            var left = AlignDistribute.Apply(rects, LayoutCommand.AlignLeft);
            Assert.AreEqual(new Vector2(10, 200), left[1]);

            var right = AlignDistribute.Apply(rects, LayoutCommand.AlignRight);
            Assert.AreEqual(new Vector2(250, 0), right[0]);

            var bottom = AlignDistribute.Apply(new Dictionary<int, Rect> { [0] = R(0, 0, 100, 50), [1] = R(300, 10, 100, 100) },
                LayoutCommand.AlignBottom);
            Assert.AreEqual(new Vector2(0, 60), bottom[0]);

            var centerH = AlignDistribute.Apply(rects, LayoutCommand.AlignCenterH);
            Assert.AreEqual(centerH[0].x + 50, centerH[1].x + 150, 0.001f);
            Assert.AreEqual(200f, centerH[1].y);
        }

        [Test]
        public void Distribute_PutsTheGapBetweenNeighbours()
        {
            var rects = new Dictionary<int, Rect> { [0] = R(0, 0, 100), [1] = R(900, 30, 150), [2] = R(20, 60, 200) };

            var result = AlignDistribute.Apply(rects, LayoutCommand.DistributeH);

            Assert.AreEqual(0f, result[0].x);
            Assert.AreEqual(LayoutGeometry.CeilToGrid(100 + LayoutGeometry.GapH), result[2].x, "order by x kept");
            Assert.Greater(result[1].x, result[2].x + 200);
            Assert.AreEqual(30f, result[1].y);
        }

        #endregion

        #region Free spot + group fit

        [Test]
        public void FreeSpot_NothingInTheWay_NoShift()
        {
            Assert.AreEqual(Vector2.zero, FreeSpotFinder.FindOffset(new[] { R(0, 0) }, new[] { R(500, 500) }));
        }

        [Test]
        public void FreeSpot_ShiftNeverOverlapsFixedRects()
        {
            var block = new[] { R(0, 0), R(280, 0), R(0, 120) };
            var obstacles = new List<Rect>();
            for (var i = 0; i < 25; i++) obstacles.Add(R(i % 5 * 230 - 100, i / 5 * 130 - 60, 180, 90));

            var offset = FreeSpotFinder.FindOffset(block, obstacles);

            Assert.IsTrue(FreeSpotFinder.IsFree(block, obstacles, offset));
            Assert.AreEqual(0f, offset.x % LayoutGeometry.Grid, 0.001f);
            Assert.AreEqual(0f, offset.y % LayoutGeometry.Grid, 0.001f);
        }

        [Test]
        public void FreeSpot_PrefersTheNearestSpot()
        {
            // An obstacle covering the block's right part: moving down past it is shorter than moving right past it
            var offset = FreeSpotFinder.FindOffset(new[] { R(0, 0, 400, 50) }, new[] { R(300, 0, 400, 50) });

            Assert.AreEqual(0f, offset.x);
            Assert.Greater(offset.y, 0f);
        }

        [Test]
        public void GroupFit_PadsContentAndKeepsMembers()
        {
            var fit = GroupFit.Fit(new[] { R(100, 100), R(400, 300) }).Value;

            Assert.AreEqual(100 - LayoutGeometry.GroupPadding, fit.xMin);
            Assert.AreEqual(100 - LayoutGeometry.GroupPadding - LayoutGeometry.GroupTitle, fit.yMin);
            Assert.AreEqual(600 + LayoutGeometry.GroupPadding, fit.xMax);

            var group = new LayoutNode { Rect = fit, Kind = LayoutNodeKind.Group };
            Assert.IsTrue(GraphLayoutModel.Contains(group, new Vector2(100, 100)));
            Assert.IsTrue(GraphLayoutModel.Contains(group, new Vector2(400, 300)));

            var tiny = GroupFit.Fit(new[] { R(0, 0, 10, 10) }).Value;
            Assert.AreEqual(LayoutGeometry.GroupMinWidth, tiny.width);
            Assert.AreEqual(LayoutGeometry.GroupMinBodyHeight + LayoutGeometry.GroupChrome, tiny.height);

            Assert.AreEqual(Rect.MinMaxRect(0, 0, 900, 500), GroupFit.Grow(Rect.MinMaxRect(0, 0, 900, 100), Rect.MinMaxRect(50, 50, 300, 500)));
        }

        #endregion

        #region Engine

        [Test]
        public void Engine_UnselectedNodesNeverMove_AndArrangedBlockAvoidsThem()
        {
            // 0 -> 1 -> 2 selected and spread out; 3 unselected sits where the arranged row would go
            var model = Model(R(0, 0), R(0, 300), R(0, 600), R(300, 0));
            model.Connect(0, 1);
            model.Connect(1, 2);

            var result = GraphLayoutEngine.Run(model, new[] { 0, 1, 2 }, LayoutCommand.Arrange);

            Assert.IsFalse(result.ContainsKey(3));
            var arranged = new[] { 0, 1, 2 }.Select(i => result.TryGetValue(i, out var r) ? r : model.Nodes[i].Rect).ToList();
            AssertNoOverlaps(arranged.Append(model.Nodes[3].Rect).ToList());
        }

        [Test]
        public void Engine_EmptySelection_ChangesNothing()
        {
            var model = Model(R(0, 0), R(10, 10));
            Assert.AreEqual(0, GraphLayoutEngine.Run(model, new int[0], LayoutCommand.Arrange).Count);
        }

        [Test]
        public void Engine_SelectedGroup_ArrangesContentsAndFits()
        {
            var model = new GraphLayoutModel();
            var group = model.Add(new Rect(0, 0, 2000, 1500), LayoutNodeKind.Group);
            model.Add(R(100, 100));
            model.Add(R(900, 900));
            model.Connect(1, 2);

            var result = GraphLayoutEngine.Run(model, new[] { group.Id }, LayoutCommand.Arrange);

            var fitted = result[group.Id];
            Assert.Less(fitted.width, 2000f, "group shrinks to fit its contents");
            var a = result.TryGetValue(1, out var ra) ? ra : model.Nodes[1].Rect;
            var b = result[2];
            Assert.AreEqual(a.y, b.y, "chain in one row");
            var after = new LayoutNode { Rect = fitted, Kind = LayoutNodeKind.Group };
            Assert.IsTrue(GraphLayoutModel.Contains(after, a.position));
            Assert.IsTrue(GraphLayoutModel.Contains(after, b.position));
        }

        [Test]
        public void Engine_NodesFromTwoUnselectedGroups_StayInTheirGroups()
        {
            var model = new GraphLayoutModel();
            model.Add(new Rect(0, 0, 800, 800), LayoutNodeKind.Group); // 0
            model.Add(new Rect(2000, 0, 800, 800), LayoutNodeKind.Group); // 1
            model.Add(R(50, 50)); // 2 in group 0
            model.Add(R(50, 500)); // 3 in group 0
            model.Add(R(2050, 50)); // 4 in group 1
            model.Add(R(2050, 500)); // 5 in group 1
            model.Connect(2, 3);
            model.Connect(3, 4); // across the groups
            model.Connect(4, 5);

            var result = GraphLayoutEngine.Run(model, new[] { 2, 3, 4, 5 }, LayoutCommand.Arrange);
            var rects = model.Nodes.Select(n => result.TryGetValue(n.Id, out var r) ? r : n.Rect).ToArray();
            var after = new GraphLayoutModel();
            foreach (var n in model.Nodes) after.Add(rects[n.Id], n.Kind);

            var parents = after.ComputeParents();
            Assert.AreEqual(0, parents[2]);
            Assert.AreEqual(0, parents[3]);
            Assert.AreEqual(1, parents[4]);
            Assert.AreEqual(1, parents[5]);
            Assert.AreEqual(rects[2].y, rects[3].y, "arranged in a row inside group 0");
            Assert.AreEqual(model.Nodes[1].Rect.position, rects[1].position, "unselected groups do not move");
        }

        [Test]
        public void Engine_NestedGroups_InsideOut()
        {
            var model = new GraphLayoutModel();
            model.Add(new Rect(0, 0, 3000, 3000), LayoutNodeKind.Group); // 0 outer
            model.Add(new Rect(100, 100, 1200, 1200), LayoutNodeKind.Group); // 1 inner
            model.Add(R(200, 200)); // 2 in inner
            model.Add(R(200, 900)); // 3 in inner
            model.Add(R(2000, 2000)); // 4 in outer
            model.Connect(2, 3);
            model.Connect(3, 4);

            CollectionAssert.AreEqual(new[] { -1, 0, 1, 1, 0 }, model.ComputeParents());

            var result = GraphLayoutEngine.Run(model, new[] { 0 }, LayoutCommand.Arrange);
            var rects = model.Nodes.Select(n => result.TryGetValue(n.Id, out var r) ? r : n.Rect).ToArray();

            Assert.Less(rects[1].width, 1200f, "inner group fitted to its chain");
            Assert.AreEqual(rects[2].y, rects[3].y);
            Assert.Greater(rects[4].xMin, rects[1].xMax, "outer flow: inner group block -> node 4");
            Assert.IsTrue(rects[0].Contains(rects[1].position) && rects[0].xMax >= rects[4].xMax, "outer group fits everything");
            var after = new GraphLayoutModel();
            foreach (var n in model.Nodes) after.Add(rects[n.Id], n.Kind);
            CollectionAssert.AreEqual(new[] { -1, 0, 1, 1, 0 }, after.ComputeParents(), "membership unchanged");
        }

        [Test]
        public void Engine_GroupContainingMovedNodes_Grows()
        {
            var model = new GraphLayoutModel();
            model.Add(new Rect(0, 0, 400, 400), LayoutNodeKind.Group); // 0
            model.Add(R(50, 50));
            model.Add(R(60, 150));
            model.Connect(1, 2);

            var result = GraphLayoutEngine.Run(model, new[] { 1, 2 }, LayoutCommand.Arrange);
            var group = result[0];

            Assert.GreaterOrEqual(group.xMax, result.TryGetValue(2, out var r2) ? r2.xMax : model.Nodes[2].Rect.xMax);
            Assert.AreEqual(0f, group.xMin, "grows, keeps its corner");
        }

        [Test]
        public void Engine_AlignLeft_MovesTopLevelItems()
        {
            var model = Model(R(48, 0), R(200, 300), R(500, 0, 50, 50));
            var result = GraphLayoutEngine.Run(model, new[] { 0, 1 }, LayoutCommand.AlignLeft);

            Assert.AreEqual(48f, result[1].x);
            Assert.IsFalse(result.ContainsKey(0));
            Assert.IsFalse(result.ContainsKey(2));
        }

        #endregion

        #region Undo on a ScriptableObject graph

        [Test]
        public void Apply_IsOneUndoStep_UndoRestoresPositions()
        {
            var graph = ScriptableObject.CreateInstance<GraphBase>();
            try
            {
                var start = graph.AddNode<StartNode>();
                var a = graph.AddNode<TestPassNode>();
                var b = graph.AddNode<TestPassNode>();
                var group = graph.AddNode<NodeGroup>();
                group.position = new Vector2(-2000, -2000);
                group.width = 300;
                group.height = 200;
                start.GetOutputPort(nameof(StartNode.start)).Connect(a.GetInputPort("input"));
                a.GetOutputPort("output").Connect(b.GetInputPort("input"));
                start.position = new Vector2(0, 0);
                a.position = new Vector2(10, 500);
                b.position = new Vector2(-300, 900);
                var before = graph.nodes.Select(n => n.position).ToArray();
                var order = graph.nodes.ToArray();

                var changed = GraphLayoutCommands.Apply(graph, new Node[] { start, a, b }, LayoutCommand.Arrange, _ => nodeSize);

                Assert.Greater(changed.Count, 0);
                Assert.AreEqual(start.position.y, a.position.y, "arranged in a row");
                Assert.AreEqual(LayoutCommandNames.UndoName(LayoutCommand.Arrange), Undo.GetCurrentGroupName());

                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();

                CollectionAssert.AreEqual(before, graph.nodes.Select(n => n.position).ToArray());
                CollectionAssert.AreEqual(order, graph.nodes.ToArray(), "node order unchanged");
            }
            finally
            {
                foreach (var node in graph.nodes.ToArray()) Object.DestroyImmediate(node);
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void Apply_NothingToChange_RecordsNoUndo()
        {
            var graph = ScriptableObject.CreateInstance<GraphBase>();
            try
            {
                var a = graph.AddNode<TestPassNode>();
                a.position = new Vector2(-8, -8);
                Undo.IncrementCurrentGroup();
                var group = Undo.GetCurrentGroup();

                var changed = GraphLayoutCommands.Apply(graph, new Node[] { a }, LayoutCommand.Arrange, _ => nodeSize);

                Assert.AreEqual(0, changed.Count);
                Assert.AreEqual(group, Undo.GetCurrentGroup());
            }
            finally
            {
                foreach (var node in graph.nodes.ToArray()) Object.DestroyImmediate(node);
                Object.DestroyImmediate(graph);
            }
        }

        #endregion

    }
}
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Xprees.Graph.Core.Base;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Editor.Debugger;
using Xprees.Graph.Core.Editor.Navigation;
using Xprees.Graph.Core.Editor.Nodes;

namespace Xprees.Graph.Core.Tests
{
    public class FollowPolicyTests
    {
        private static IEnumerable<KeyValuePair<int, string>> Records(params (int id, string node)[] records) =>
            records.Select(r => new KeyValuePair<int, string>(r.id, r.node));

        [Test]
        public void FirstSighting_CountsAsMoved()
        {
            var last = new Dictionary<int, string>();

            Assert.AreEqual(1, FollowPolicy.FindMoved(last, Records((1, "a")), null));
            Assert.AreEqual("a", last[1]);
        }

        [Test]
        public void SameActiveNode_NeverMovesTheView()
        {
            var last = new Dictionary<int, string> { [1] = "a" };

            for (var i = 0; i < 5; i++) Assert.IsNull(FollowPolicy.FindMoved(last, Records((1, "a")), null));
        }

        [Test]
        public void ChangedActiveNode_Moves_ThenQuietAgain()
        {
            var last = new Dictionary<int, string> { [1] = "a" };

            Assert.AreEqual(1, FollowPolicy.FindMoved(last, Records((1, "b")), null));
            Assert.IsNull(FollowPolicy.FindMoved(last, Records((1, "b")), null));
        }

        [Test]
        public void Filter_OnlyTheFilteredRecordMoves_OthersAreStillRemembered()
        {
            var last = new Dictionary<int, string> { [1] = "a", [2] = "x" };

            Assert.IsNull(FollowPolicy.FindMoved(last, Records((1, "a"), (2, "y")), 1));
            Assert.AreEqual("y", last[2]);
            Assert.AreEqual(1, FollowPolicy.FindMoved(last, Records((1, "b"), (2, "y")), 1));
        }

        [Test]
        public void SeveralMoved_LastOneWins_NullNodesIgnored()
        {
            var last = new Dictionary<int, string>();

            Assert.AreEqual(3, FollowPolicy.FindMoved(last, Records((1, "a"), (2, null), (3, "c")), null));
            Assert.IsFalse(last.ContainsKey(2));
        }
    }

    public class GraphNavigationPlanTests
    {
        private readonly List<GraphBase> _graphs = new();

        private GraphBase NewGraph()
        {
            var graph = ScriptableObject.CreateInstance<GraphBase>();
            _graphs.Add(graph);
            return graph;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var graph in _graphs) Object.DestroyImmediate(graph);
            _graphs.Clear();
        }

        private static List<GraphNavigation.Crumb> Chain(params GraphBase[] graphs) =>
            graphs.Select(g => new GraphNavigation.Crumb(g, null)).ToList();

        [Test]
        public void PlanShow_EmptyChain_IsInPlace()
        {
            var plan = GraphNavigation.PlanShow(Chain(), NewGraph(), out var index);

            Assert.AreEqual(GraphNavigation.ShowKind.InPlace, plan);
            Assert.AreEqual(-1, index);
        }

        [Test]
        public void PlanShow_CurrentTail_IsInPlace()
        {
            GraphBase root = NewGraph(), sub = NewGraph();

            Assert.AreEqual(GraphNavigation.ShowKind.InPlace, GraphNavigation.PlanShow(Chain(root, sub), sub, out var index));
            Assert.AreEqual(1, index);
        }

        [Test]
        public void PlanShow_Ancestor_GoesBack()
        {
            GraphBase root = NewGraph(), sub = NewGraph();

            Assert.AreEqual(GraphNavigation.ShowKind.GoBack, GraphNavigation.PlanShow(Chain(root, sub), root, out var index));
            Assert.AreEqual(0, index);
        }

        [Test]
        public void PlanShow_GraphNotInTrail_Pushes()
        {
            GraphBase root = NewGraph(), sub = NewGraph(), dialog = NewGraph();

            Assert.AreEqual(GraphNavigation.ShowKind.Push, GraphNavigation.PlanShow(Chain(root, sub), dialog, out _));
        }

        [Test]
        public void BuildPushChain_KeepsTheTrailAndAppends()
        {
            GraphBase root = NewGraph(), sub = NewGraph(), dialog = NewGraph();

            var pushed = GraphNavigation.BuildPushChain(Chain(root, sub), dialog, null);

            CollectionAssert.AreEqual(new[] { root, sub, dialog }, pushed.Select(c => c.graph));
        }

        [Test]
        public void PushThenBack_ReturnsToTheOriginalTrail()
        {
            GraphBase root = NewGraph(), sub = NewGraph(), dialog = NewGraph();
            var pushed = GraphNavigation.BuildPushChain(Chain(root, sub), dialog, null);

            // Going back = the dialog's parent crumb is the tail again
            Assert.AreEqual(GraphNavigation.ShowKind.GoBack, GraphNavigation.PlanShow(pushed, sub, out var index));
            CollectionAssert.AreEqual(new[] { root, sub }, pushed.Take(index + 1).Select(c => c.graph));
        }

        [Test]
        public void BuildEnterChain_FromTail_Appends_OtherwiseParentBecomesRoot()
        {
            GraphBase root = NewGraph(), sub = NewGraph(), other = NewGraph(), child = NewGraph();
            var fromRoot = root.AddNode<SubGraphNode>();
            var fromOther = other.AddNode<SubGraphNode>();

            var fromTail = GraphNavigation.BuildEnterChain(Chain(root), fromRoot, sub);
            CollectionAssert.AreEqual(new[] { root, sub }, fromTail.Select(c => c.graph));
            Assert.AreEqual(fromRoot, fromTail[1].enteredFrom);

            var elsewhere = GraphNavigation.BuildEnterChain(Chain(root, sub), fromOther, child);
            CollectionAssert.AreEqual(new[] { other, child }, elsewhere.Select(c => c.graph));
        }
    }

    public class NodeGroupOrderingTests
    {
        private static bool IsGroup(string s) => s.StartsWith("G");

        [Test]
        public void AlreadyOrdered_IsNeverTouched_EvenRepeated()
        {
            var list = new List<string> { "G3", "G1", "G2", "n1", "n2" };
            var expected = list.ToList();

            for (var i = 0; i < 10; i++) Assert.IsFalse(NodeGroupOrdering.MoveGroupsFirst(list, IsGroup));

            CollectionAssert.AreEqual(expected, list);
        }

        [Test]
        public void GroupAboveRegularNode_MovesStably_ThenSettles()
        {
            var list = new List<string> { "n1", "G2", "n2", "G1", "n3" };

            Assert.IsTrue(NodeGroupOrdering.MoveGroupsFirst(list, IsGroup));
            CollectionAssert.AreEqual(new[] { "G2", "G1", "n1", "n2", "n3" }, list);
            Assert.IsFalse(NodeGroupOrdering.MoveGroupsFirst(list, IsGroup));
        }

        [Test]
        public void ManyGroups_OldBehaviourWouldShuffle_NewOneKeepsOrder()
        {
            var list = new List<string> { "G1", "G2", "G3", "G4", "n1" };
            var expected = list.ToList();

            // The old per-group "move to index 0" on every repaint rotated the groups forever
            for (var repaint = 0; repaint < 20; repaint++)
            {
                foreach (var _ in list.Where(IsGroup).ToList()) NodeGroupOrdering.MoveGroupsFirst(list, IsGroup);
            }

            CollectionAssert.AreEqual(expected, list);
        }

        [Test]
        public void EmptyAndGroupFreeLists_AreOrdered()
        {
            Assert.IsTrue(NodeGroupOrdering.IsGroupsFirst(new List<string>(), IsGroup));
            Assert.IsTrue(NodeGroupOrdering.IsGroupsFirst(new List<string> { "n1", "n2" }, IsGroup));
        }
    }

    public class BreakpointDotLayoutTests
    {
        [Test]
        public void HitArea_IsInsideTheTitleBand_AndLargerThanTheDot()
        {
            var hit = BreakpointDotLayout.LocalHit(300f);
            var dot = BreakpointDotLayout.LocalDot(300f);

            Assert.LessOrEqual(hit.yMax, 30f);
            Assert.LessOrEqual(hit.xMax, 300f);
            Assert.Greater(hit.width, dot.width);
            Assert.IsTrue(hit.Contains(dot.center));
        }

        [TestCase(1f)]
        [TestCase(2f)]
        public void WindowHit_ScalesWithZoom(float zoom)
        {
            var origin = new Vector2(100f, 50f);
            var local = BreakpointDotLayout.LocalHit(300f);
            var window = BreakpointDotLayout.WindowHit(origin, 300f, zoom);

            Assert.IsTrue(window.Contains(origin + local.center / zoom));
            Assert.AreEqual(local.width / zoom, window.width, 0.001f);
        }

        [Test]
        public void IsClick_AllowsSmallJitterOnly()
        {
            Assert.IsTrue(BreakpointDotLayout.IsClick(new Vector2(10f, 10f), new Vector2(12f, 11f)));
            Assert.IsFalse(BreakpointDotLayout.IsClick(new Vector2(10f, 10f), new Vector2(40f, 10f)));
        }
    }
}
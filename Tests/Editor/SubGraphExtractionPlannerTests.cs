using System.Linq;
using NUnit.Framework;
using UnityEngine;
using XNode;
using Xprees.Graph.Core.Base;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Nodes.Start;
using Xprees.Graph.Core.Editor.Refactor;

namespace Xprees.Graph.Core.Tests
{
    public class SubGraphExtractionPlannerTests
    {
        private GraphBase _graph;
        private StartNode _start;
        private EndNode _end;
        private SubGraphNode _a, _b, _c;

        [SetUp]
        public void SetUp()
        {
            // Start -> A -> B -> C -> End
            _graph = ScriptableObject.CreateInstance<GraphBase>();
            _start = _graph.AddNode<StartNode>();
            _end = _graph.AddNode<EndNode>();
            _a = _graph.AddNode<SubGraphNode>();
            _b = _graph.AddNode<SubGraphNode>();
            _c = _graph.AddNode<SubGraphNode>();
            _start.GetOutputPort(nameof(StartNode.start)).Connect(_a.GetInputPort("input"));
            Link(_a, _b);
            Link(_b, _c);
            _c.GetOutputPort("output").Connect(_end.GetInputPort(nameof(EndNode.end)));
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        private static void Link(SubGraphNode from, SubGraphNode to) => from.GetOutputPort("output").Connect(to.GetInputPort("input"));

        private SubGraphExtractionResult Plan(params Node[] selection) => SubGraphExtractionPlanner.Plan(_graph, selection, requireAsset: false);

        [Test]
        public void SingleMiddleNode_HasEntryAndExit()
        {
            var result = Plan(_b);

            Assert.IsTrue(result.CanExtract, string.Join(" | ", result.errors));
            Assert.AreEqual(_b.GetInputPort("input"), result.plan.entryPort);
            Assert.AreEqual(_c.GetInputPort("input"), result.plan.exitTarget);
            CollectionAssert.AreEqual(new[] { _a.GetOutputPort("output") }, result.plan.entrySources);
        }

        [Test]
        public void TwoConnectedNodes_EntryIsFirstExitIsAfterLast()
        {
            var result = Plan(_a, _b);

            Assert.IsTrue(result.CanExtract);
            Assert.AreEqual(_a.GetInputPort("input"), result.plan.entryPort);
            Assert.AreEqual(_c.GetInputPort("input"), result.plan.exitTarget);
            Assert.AreEqual(2, result.plan.members.Count);
        }

        [Test]
        public void SelectedEnd_StaysAndBecomesTheExitTarget()
        {
            var result = Plan(_c, _end);

            Assert.IsTrue(result.CanExtract);
            Assert.AreEqual(_end.GetInputPort(nameof(EndNode.end)), result.plan.exitTarget);
            Assert.IsFalse(result.plan.members.Contains(_end));
        }

        [Test]
        public void StartAndEnd_AreNeverMoved()
        {
            var result = Plan(_start, _a, _end);

            Assert.IsFalse(result.plan.members.Contains(_start));
            Assert.IsFalse(result.plan.members.Contains(_end));
            Assert.IsNotEmpty(result.warnings);
        }

        [Test]
        public void TwoEntryPoints_AreRefused()
        {
            // A -> B and A2 -> B2, both flowing on to C: selecting B and B2 gives two entry ports
            var a2 = _graph.AddNode<SubGraphNode>();
            var b2 = _graph.AddNode<SubGraphNode>();
            Link(a2, b2);
            Link(b2, _c);

            var result = Plan(_b, b2);

            Assert.IsFalse(result.CanExtract);
        }

        [Test]
        public void AsyncStartNode_StaysAndItsPathMoves()
        {
            var trigger = _graph.AddNode<VoidEventAsyncStartNode>();
            var b2 = _graph.AddNode<SubGraphNode>();
            var c2 = _graph.AddNode<SubGraphNode>();
            trigger.GetOutputPort(nameof(AsyncStartBaseNode.start)).Connect(b2.GetInputPort("input"));
            Link(b2, c2);

            var result = Plan(trigger, b2, c2);

            Assert.IsTrue(result.CanExtract, string.Join(" | ", result.errors));
            CollectionAssert.AreEquivalent(new Node[] { b2, c2 }, result.plan.members);
            CollectionAssert.AreEqual(new[] { trigger.GetOutputPort(nameof(AsyncStartBaseNode.start)) }, result.plan.entrySources);
            Assert.IsNull(result.plan.exitTarget);
        }

        [Test]
        public void SuggestedName_UsesAsyncStartNodeName()
        {
            var trigger = _graph.AddNode<VoidEventAsyncStartNode>();
            trigger.name = "On Door Opened";
            var b2 = _graph.AddNode<SubGraphNode>();
            trigger.GetOutputPort(nameof(AsyncStartBaseNode.start)).Connect(b2.GetInputPort("input"));

            var result = Plan(trigger, b2);

            StringAssert.EndsWith("_On_Door_Opened", result.plan.suggestedName);
        }

        [Test]
        public void SuggestedName_PrefersNamedGroup()
        {
            var group = _graph.AddNode<NodeGroup>();
            group.name = "Intro";
            group.position = Vector2.zero;
            _b.position = new Vector2(50f, 50f);

            var result = Plan(group);

            StringAssert.EndsWith("_Intro", result.plan.suggestedName);
        }

        [Test]
        public void NothingSelected_IsRefused()
        {
            Assert.IsFalse(Plan().CanExtract);
        }

        [Test]
        public void NothingLeadsIn_IsRefused()
        {
            _start.GetOutputPort(nameof(StartNode.start)).ClearConnections();

            Assert.IsFalse(Plan(_a, _b).CanExtract);
        }

        [Test]
        public void SelectedGroup_BringsNodesInsideOfIt()
        {
            _a.position = new Vector2(-600f, 0f);
            _b.position = new Vector2(50f, 50f);
            _c.position = new Vector2(900f, 0f);
            var group = _graph.AddNode<NodeGroup>();
            group.position = Vector2.zero;
            group.width = 400;
            group.height = 400;

            var result = Plan(group);

            Assert.IsTrue(result.CanExtract, string.Join(" | ", result.errors));
            CollectionAssert.AreEquivalent(new Node[] { group, _b }, result.plan.members);
        }
    }
}

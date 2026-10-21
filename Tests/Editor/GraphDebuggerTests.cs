using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XNode;
using Xprees.Graph.Core.Base;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Debugging;
using Xprees.Graph.Core.Editor.Debugger;
using Xprees.Variables.Reference.Primitive;
using Object = UnityEngine.Object;

namespace Xprees.Graph.Core.Tests
{
    public class GraphDebuggerTests
    {
        private readonly List<GraphBase> _graphs = new();
        private readonly List<GraphParserBase> _parsers = new();
        private IGraphRuntimeObserver _previousObserver;
        private GraphRuntimeTracer _tracer;

        [SetUp]
        public void SetUp()
        {
            _previousObserver = GraphDebugHooks.Observer;
            GraphDebugHooks.Observer = null;
            GraphDebugHooks.ResetStatics();
            GraphBreakpoints.ResetSession();
            GraphParserSettings.TriggerAsyncStartFirstNode = false;
            TestRecordNode.Log.Clear();
            _tracer = new GraphRuntimeTracer();
        }

        [TearDown]
        public void TearDown()
        {
            GraphParserSettings.TriggerAsyncStartFirstNode = false;
            GraphDebugGate.CancelAll();
            foreach (var parser in _parsers) parser.Dispose();
            foreach (var graph in _graphs) Object.DestroyImmediate(graph);
            GraphBreakpoints.ResetSession();
            GraphDebugHooks.ResetStatics();
            GraphDebugHooks.Observer = _previousObserver;
        }

        #region Helpers

        private GraphBase NewGraph()
        {
            var graph = ScriptableObject.CreateInstance<GraphBase>();
            _graphs.Add(graph);
            return graph;
        }

        private static T Add<T>(GraphBase graph, string nodeName) where T : BaseNode
        {
            var node = graph.AddNode<T>();
            node.name = nodeName;
            return node;
        }

        private static void Link(NodePort from, NodePort to) => from.Connect(to);

        private static void Chain(params BaseNode[] nodes)
        {
            for (var i = 0; i < nodes.Length - 1; i++)
            {
                Link(OutputOf(nodes[i]), InputOf(nodes[i + 1]));
            }
        }

        private static NodePort OutputOf(BaseNode node) => node switch
        {
            StartNode => node.GetOutputPort(nameof(StartNode.start)),
            _ => node.GetOutputPort("output")
        };

        private static NodePort InputOf(BaseNode node) => node is EndNode ? node.GetInputPort(nameof(EndNode.end)) : node.GetInputPort("input");

        private GraphParserBase Parse(GraphBase graph, BaseNode start = null)
        {
            var parser = new GraphParserBase(graph, start);
            _parsers.Add(parser);
            return parser;
        }

        /// Start -> nodes... -> End
        private GraphBase Flow(out StartNode start, out EndNode end, params Func<GraphBase, BaseNode>[] makers)
        {
            var graph = NewGraph();
            start = graph.AddNode<StartNode>();
            start.name = "Start";
            end = graph.AddNode<EndNode>();
            end.name = "End";
            var chain = new List<BaseNode> { start };
            chain.AddRange(makers.Select(maker => maker(graph)));
            chain.Add(end);
            Chain(chain.ToArray());
            return graph;
        }

        private static Func<GraphBase, BaseNode> Rec(string nodeName) => g => Add<TestRecordNode>(g, nodeName);
        private static Func<GraphBase, BaseNode> Pass(string nodeName) => g => Add<TestPassNode>(g, nodeName);

        private static BaseNode Find(GraphBase graph, string nodeName) => graph.nodes.OfType<BaseNode>().First(n => n.name == nodeName);

        #endregion

        #region T0.1 characterisation (behaviour without any observer)

        [Test]
        public void PassthroughChain_TriggersEveryNodeAndStopsAtFirstNonPassthrough()
        {
            var graph = Flow(out _, out _, Pass("P1"), Pass("P2"), Rec("R"));
            var parser = Parse(graph);

            parser.Continue().GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "P1", "P2", "R" }, TestRecordNode.Log);
            Assert.AreEqual("R", parser.CurrentNode.name);
            Assert.IsFalse(parser.Ended);

            parser.Continue().GetAwaiter().GetResult();
            Assert.IsTrue(parser.Ended);
        }

        [Test]
        public void SubGraph_TraversesTheChildGraphWhileTheNodeIsTriggered()
        {
            var child = Flow(out _, out _, Rec("ChildR"));
            var parent = Flow(out _, out _, g =>
            {
                var sub = Add<SubGraphNode>(g, "Sub");
                sub.subGraph = child;
                return sub;
            }, Rec("AfterSub"));
            var parser = Parse(parent);

            parser.Continue().GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "ChildR", "AfterSub" }, TestRecordNode.Log);
        }

        [Test]
        public void AsyncStart_FirstConnectedNode_TriggerIsSkipped_CurrentBehaviour()
        {
            // Open question 1 of the plan. The flow parser is positioned on R1 and Continue calls R1.MoveNext,
            // so R1.Trigger never runs. When the AsyncStart fix (T0.7) lands this expectation flips to R1, R2.
            var graph = Flow(out _, out _);
            var asyncStart = Add<TestAsyncStartNode>(graph, "Async");
            var r1 = Add<TestRecordNode>(graph, "R1");
            var r2 = Add<TestRecordNode>(graph, "R2");
            var end = Add<EndNode>(graph, "AsyncEnd");
            Link(asyncStart.GetOutputPort("start"), r1.GetInputPort("input"));
            Chain(r1, r2, end);
            Parse(graph); // the graph has to be active for the async flow to run

            asyncStart.Fire();

            CollectionAssert.AreEqual(new[] { "R2" }, TestRecordNode.Log, "R1.Trigger is expected to be skipped (confirmed bug)");
        }

        [Test]
        public void AsyncStart_FirstConnectedNode_SwitchOn_TriggersIt([Values(false, true)] bool observed)
        {
            GraphParserSettings.TriggerAsyncStartFirstNode = true;
            if (observed) GraphDebugHooks.Observer = _tracer;
            var graph = Flow(out _, out _);
            var asyncStart = Add<TestAsyncStartNode>(graph, "Async");
            var r1 = Add<TestRecordNode>(graph, "R1");
            var r2 = Add<TestRecordNode>(graph, "R2");
            var end = Add<EndNode>(graph, "AsyncEnd");
            Link(asyncStart.GetOutputPort("start"), r1.GetInputPort("input"));
            Chain(r1, r2, end);
            Parse(graph);

            asyncStart.Fire();

            CollectionAssert.AreEqual(new[] { "R1", "R2" }, TestRecordNode.Log);
        }

        [Test]
        public void AsyncStart_WithoutConnection_SwitchOn_DoesNotLoop()
        {
            GraphParserSettings.TriggerAsyncStartFirstNode = true;
            var graph = Flow(out _, out _);
            var asyncStart = Add<TestAsyncStartNode>(graph, "Async");
            Parse(graph);

            asyncStart.Fire();

            CollectionAssert.IsEmpty(TestRecordNode.Log);
        }

        [Test]
        public void TriggerStartNode_TriggersPositionedNodeOnceThenContinues([Values(false, true)] bool observed)
        {
            if (observed) GraphDebugHooks.Observer = _tracer;
            var graph = Flow(out _, out _, Rec("R1"), Rec("R2"));
            var parser = new GraphParserBase(graph, Find(graph, "R1"), true);
            _parsers.Add(parser);

            while (!parser.Ended) parser.Continue().GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "R1", "R2" }, TestRecordNode.Log);
        }

        [Test]
        public void PlainStartNode_IsStillOnlyPositionedOn()
        {
            var graph = Flow(out _, out _, Rec("R1"), Rec("R2"));
            var parser = Parse(graph, Find(graph, "R1"));

            while (!parser.Ended) parser.Continue().GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "R2" }, TestRecordNode.Log);
        }

        [Test]
        public void Prelude_RunsBeforeTheStartNode([Values(false, true)] bool observed)
        {
            if (observed) GraphDebugHooks.Observer = _tracer;
            var graph = Flow(out var start, out _, Rec("R1"), Rec("R2"), Rec("R3"));
            var prelude = new BaseNode[] { start, Find(graph, "R1") };
            var parser = new GraphParserBase(graph, Find(graph, "R3"), true, prelude);
            _parsers.Add(parser);

            while (!parser.Ended) parser.Continue().GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "R1", "R3" }, TestRecordNode.Log);
        }

        [Test]
        public void SubGraph_OneShotStart_StartsInsideTheChildOnceAndTriggersIt()
        {
            var child = Flow(out _, out _, Rec("C1"), Rec("C2"));
            SubGraphNode sub = null;
            var parent = Flow(out _, out _, g =>
            {
                sub = Add<SubGraphNode>(g, "Sub");
                sub.subGraph = child;
                return sub;
            }, Rec("After"));
            sub.SetOneShotStart(Find(child, "C2"));
            var fromSub = new GraphParserBase(parent, sub, true);
            _parsers.Add(fromSub);

            while (!fromSub.Ended) fromSub.Continue().GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "C2", "After" }, TestRecordNode.Log);
            sub.SetOneShotStart(null);
        }

        [Test]
        public void ObserverOn_DoesNotChangeWhatIsTriggered()
        {
            var graph = Flow(out _, out _, Pass("P1"), Rec("R1"), Rec("R2"));
            var plain = Parse(graph);
            while (!plain.Ended) plain.Continue().GetAwaiter().GetResult();
            var expected = TestRecordNode.Log.ToArray();
            TestRecordNode.Log.Clear();

            GraphDebugHooks.Observer = _tracer;
            var observed = Parse(graph);
            while (!observed.Ended) observed.Continue().GetAwaiter().GetResult();

            CollectionAssert.AreEqual(expected, TestRecordNode.Log);
        }

        #endregion

        #region Hook order and contracts

        [Test]
        public void HookOrder_StartPassthroughLinearEnd()
        {
            var fake = new FakeObserver();
            GraphDebugHooks.Observer = fake;
            var graph = Flow(out _, out _, Pass("P"), Rec("R"));
            var parser = Parse(graph);

            parser.Continue().GetAwaiter().GetResult();
            parser.Continue().GetAwaiter().GetResult();

            var expected = new[]
            {
                "created:Start",
                "gate:Start", "resolve:Start", "edge:Start>P", "gate:P", "enter:P", "done:P",
                "resolve:P", "edge:P>R", "gate:R", "enter:R", "done:R",
                "resolve:R", "edge:R>End", "gate:End", "enter:End", "done:End", "ended:EndNode"
            };
            CollectionAssert.AreEqual(expected, fake.Events);
        }

        [Test]
        public void ObserverWithoutBreakpoints_ContinueCompletesSynchronously()
        {
            GraphDebugHooks.Observer = _tracer;
            var graph = Flow(out _, out _, Pass("P"), Rec("R"));
            var parser = Parse(graph);

            var task = parser.Continue();

            Assert.AreEqual(UniTaskStatus.Succeeded, task.Status);
        }

        [Test]
        public void ThrowingTrigger_RestoresAmbientStackAndReportsFaulted()
        {
            var fake = new FakeObserver();
            GraphDebugHooks.Observer = fake;
            var graph = Flow(out _, out _, g =>
            {
                var node = Add<TestRecordNode>(g, "Boom");
                node.throwOnTrigger = true;
                return node;
            });
            var parser = Parse(graph);

            Assert.Throws<InvalidOperationException>(() => parser.Continue().GetAwaiter().GetResult());

            Assert.IsNull(GraphDebugHooks.TriggeringParser);
            Assert.AreEqual("ended:Faulted", fake.Events.Last());
        }

        [Test]
        public void BranchWithBothOutputsToSameNode_ReportedPortIsExact_NoReportIsAmbiguous()
        {
            GraphDebugHooks.Observer = _tracer;
            var graph = NewGraph();
            var start = graph.AddNode<StartNode>();
            var branch = Add<TestBranchNode>(graph, "Branch");
            var target = Add<TestRecordNode>(graph, "Target");
            var end = Add<EndNode>(graph, "End");
            branch.result = false;
            Link(start.GetOutputPort(nameof(StartNode.start)), branch.GetInputPort("input"));
            Link(branch.GetOutputPort("pass"), target.GetInputPort("input"));
            Link(branch.GetOutputPort("fail"), target.GetInputPort("input"));
            Chain(target, end);
            var parser = Parse(graph);

            parser.Continue().GetAwaiter().GetResult();

            var failEdge = _tracer.GetEdge(branch.GetOutputPort("fail"), target.GetInputPort("input"));
            var passEdge = _tracer.GetEdge(branch.GetOutputPort("pass"), target.GetInputPort("input"));
            Assert.AreEqual(1, failEdge.Count);
            Assert.IsFalse(failEdge.Ambiguous);
            Assert.AreEqual(0, passEdge.Count);

            var inferred = EdgeInference.Find(branch, target, null, out var ambiguous);
            Assert.AreEqual(2, inferred.Count);
            Assert.IsTrue(ambiguous);

            var reported = EdgeInference.Find(branch, target, branch.GetOutputPort("fail"), out ambiguous);
            Assert.AreEqual(1, reported.Count);
            Assert.IsFalse(ambiguous);
        }

        [Test]
        public void SubGraphRecord_HasParentAndOrigin()
        {
            GraphDebugHooks.Observer = _tracer;
            var child = Flow(out _, out _, Rec("ChildR"));
            SubGraphNode sub = null;
            var parent = Flow(out _, out _, g =>
            {
                sub = Add<SubGraphNode>(g, "Sub");
                sub.subGraph = child;
                return sub;
            });
            var parser = Parse(parent);

            parser.Continue().GetAwaiter().GetResult();

            var main = _tracer.InstancesOf(parent).Single();
            var childRecord = _tracer.InstancesOf(child).Single();
            Assert.AreSame(main, childRecord.Parent);
            Assert.AreSame(sub, childRecord.Origin);
            StringAssert.StartsWith("Sub-graph via Sub", childRecord.Label);
            Assert.AreEqual("Main", main.Label);
        }

        #endregion

        #region Gate

        [Test]
        public void Breakpoint_HoldsContinueUntilContinue()
        {
            GraphDebugHooks.Observer = _tracer;
            var graph = Flow(out _, out _, Rec("R"));
            GraphBreakpoints.SetEnabled(Find(graph, "R"), true);
            var parser = Parse(graph);

            var task = parser.Continue();

            Assert.AreEqual(UniTaskStatus.Pending, task.Status);
            Assert.IsEmpty(TestRecordNode.Log, "the node must not trigger before the gate opens");
            Assert.IsTrue(_tracer.InstancesOf(graph).Single().IsPausedAtGate);

            GraphDebugGate.Continue(parser.DebugId);

            Assert.AreEqual(UniTaskStatus.Succeeded, task.Status);
            CollectionAssert.AreEqual(new[] { "R" }, TestRecordNode.Log);
        }

        [Test]
        public void RemovingTheBreakpoint_ReleasesTheHeldRecord()
        {
            GraphDebugHooks.Observer = _tracer;
            var graph = Flow(out _, out _, Rec("R"));
            var node = Find(graph, "R");
            GraphBreakpoints.SetEnabled(node, true);
            var task = Parse(graph).Continue();
            Assert.AreEqual(UniTaskStatus.Pending, task.Status);

            GraphBreakpoints.Remove(node);

            Assert.AreEqual(UniTaskStatus.Succeeded, task.Status);
        }

        [Test]
        public void Step_AffectsOnlyTheSameRecord()
        {
            GraphDebugHooks.Observer = _tracer;
            var graph = Flow(out _, out _, Rec("R1"), Rec("R2"));
            GraphBreakpoints.SetEnabled(Find(graph, "R1"), true);
            var parserA = Parse(graph);
            var parserB = Parse(graph);
            var taskA = parserA.Continue();
            var taskB = parserB.Continue();
            Assert.AreEqual(UniTaskStatus.Pending, taskA.Status);
            Assert.AreEqual(UniTaskStatus.Pending, taskB.Status);

            GraphDebugGate.Step(parserA.DebugId);
            Assert.AreEqual(UniTaskStatus.Succeeded, taskA.Status);

            var nextA = parserA.Continue();
            Assert.AreEqual(UniTaskStatus.Pending, nextA.Status, "A steps to R2 and breaks there");
            _tracer.TryGetRecord(parserA.DebugId, out var recordA);
            _tracer.TryGetRecord(parserB.DebugId, out var recordB);
            Assert.AreEqual(GateBreakReason.Step, recordA.PauseReason);
            Assert.AreEqual("R2", recordA.ActiveNode.name);
            Assert.AreEqual(GateBreakReason.Breakpoint, recordB.PauseReason);
            Assert.AreEqual(UniTaskStatus.Pending, taskB.Status, "B stays held at its breakpoint");

            GraphDebugGate.ContinueAll();
            Assert.AreEqual(UniTaskStatus.Succeeded, nextA.Status);
            Assert.AreEqual(UniTaskStatus.Succeeded, taskB.Status);
        }

        [Test]
        public void SuppressBreaks_SkipsTheGate()
        {
            GraphDebugHooks.Observer = _tracer;
            var graph = Flow(out _, out _, Rec("R"));
            GraphBreakpoints.SetEnabled(Find(graph, "R"), true);
            var parser = Parse(graph);

            UniTask task;
            using (GraphDebugHooks.SuppressBreaks())
            {
                task = parser.Continue();
            }

            Assert.AreEqual(UniTaskStatus.Succeeded, task.Status);
        }

        [Test]
        public void Cancellation_ReportsCancelledAndThrows()
        {
            GraphDebugHooks.Observer = _tracer;
            var graph = Flow(out _, out _, Rec("R"));
            GraphBreakpoints.SetEnabled(Find(graph, "R"), true);
            var parser = Parse(graph);
            using var cts = new CancellationTokenSource();
            var task = parser.Continue(cts.Token);
            Assert.AreEqual(UniTaskStatus.Pending, task.Status);

            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() => task.GetAwaiter().GetResult());
            Assert.AreEqual(GraphEndReason.Cancelled, _tracer.InstancesOf(graph).Single().EndReason);
        }

        [Test]
        public void GraphCleared_EndsTheRecordsAndCancelsTheHeldFlow()
        {
            GraphDebugHooks.Observer = _tracer;
            var graph = Flow(out _, out _, Rec("R"));
            GraphBreakpoints.SetEnabled(Find(graph, "R"), true);
            var task = Parse(graph).Continue();
            Assert.AreEqual(UniTaskStatus.Pending, task.Status);

            graph.ClearTransientState();

            var record = _tracer.InstancesOf(graph).Single();
            Assert.IsFalse(record.IsAlive);
            Assert.AreEqual(GraphEndReason.GraphInactive, record.EndReason);
            Assert.AreEqual(UniTaskStatus.Canceled, task.Status);
        }

        #endregion

        #region Visual state

        [Test]
        public void VisualState_Table()
        {
            GraphDebugHooks.Observer = _tracer;
            var graph = Flow(out var start, out _, Pass("P"), Rec("R1"), Rec("R2"));
            var r1 = Find(graph, "R1");
            var r2 = Find(graph, "R2");
            var parser = Parse(graph);
            var id = parser.DebugId;

            parser.Continue().GetAwaiter().GetResult(); // Start -> P -> R1

            Assert.AreEqual(NodeVisualState.Visited, Resolve(start, true, false));
            Assert.AreEqual(NodeVisualState.Visited, Resolve(Find(graph, "P"), true, false));
            Assert.AreEqual(NodeVisualState.Active, Resolve(r1, true, false));
            Assert.AreEqual(NodeVisualState.NotVisited, Resolve(r2, true, false));
            Assert.AreEqual(NodeVisualState.Idle, Resolve(r2, false, false), "Edit Mode shows no run visuals");
            Assert.AreEqual(NodeVisualState.NotVisited, Resolve(r2, false, true), "unless the trace is shown");
            Assert.AreEqual(NodeVisualState.Active, NodeVisualStateResolver.Resolve(r1, _tracer, id, true, false));
            Assert.AreEqual(NodeVisualState.NotVisited, NodeVisualStateResolver.Resolve(r1, _tracer, id + 100, true, false));

            GraphBreakpoints.SetEnabled(r2, true);
            var task = parser.Continue();
            Assert.AreEqual(NodeVisualState.PausedHere, Resolve(r2, true, false));
            GraphDebugGate.Continue(id);
            Assert.AreEqual(UniTaskStatus.Succeeded, task.Status);
            Assert.AreEqual(NodeVisualState.Active, Resolve(r2, true, false));
        }

        [Test]
        public void VisualState_NoTraceIsIdle()
        {
            var graph = Flow(out var start, out _);

            Assert.AreEqual(NodeVisualState.Idle, NodeVisualStateResolver.Resolve(start, _tracer, null, true, true));
            Assert.IsNotNull(graph);
        }

        [Test]
        public void VisualState_DisabledAsyncStart()
        {
            GraphDebugHooks.Observer = _tracer;
            var graph = Flow(out _, out _);
            var asyncStart = Add<TestAsyncStartNode>(graph, "Async");
            asyncStart.activeStart = new BoolReference(false);
            Parse(graph);

            Assert.AreEqual(NodeVisualState.Disabled, Resolve(asyncStart, true, false));
        }

        [Test]
        public void StartAndAsyncStartNodes_AreNeverNotVisited()
        {
            GraphDebugHooks.Observer = _tracer;
            var graph = Flow(out var start, out _);
            var asyncStart = Add<TestAsyncStartNode>(graph, "Async");
            var r1 = Add<TestRecordNode>(graph, "R1");
            var r2 = Add<TestRecordNode>(graph, "R2");
            var asyncEnd = Add<EndNode>(graph, "AsyncEnd");
            Link(asyncStart.GetOutputPort("start"), r1.GetInputPort("input"));
            Chain(r1, r2, asyncEnd);
            Parse(graph);
            Assert.AreNotEqual(NodeVisualState.NotVisited, Resolve(start, true, false), "Start is positioned on");

            asyncStart.Fire();

            Assert.AreNotEqual(NodeVisualState.NotVisited, Resolve(asyncStart, true, false));
            Assert.AreNotEqual(NodeVisualState.NotVisited, Resolve(r1, true, false), "first async node is positioned on, never entered");
            Assert.AreNotEqual(NodeVisualState.NotVisited, Resolve(r2, true, false));
            var flow = _tracer.InstancesOf(graph).First(r => r.Origin == asyncStart);
            StringAssert.StartsWith("Async from Async", flow.Label);
            Assert.AreEqual(1, _tracer.GetEdge(asyncStart.GetOutputPort("start"), r1.GetInputPort("input")).Count);
        }

        private NodeVisualState Resolve(Node node, bool playing, bool showTrace) =>
            NodeVisualStateResolver.Resolve(node, _tracer, null, playing, showTrace);

        #endregion

        #region Breakpoint store

        private const string TempFolder = "Assets/__GraphDebuggerTests__";

        [Test]
        public void BreakpointStore_RoundTripsThroughTheGlobalObjectId()
        {
            AssetDatabase.CreateFolder("Assets", "__GraphDebuggerTests__");
            try
            {
                var graph = NewGraph();
                var node = Add<TestRecordNode>(graph, "Persisted");
                AssetDatabase.CreateAsset(graph, TempFolder + "/Persist.asset");
                AssetDatabase.AddObjectToAsset(node, graph);
                AssetDatabase.SaveAssets();
                var changes = 0;
                GraphBreakpoints.Changed += OnChanged;

                GraphBreakpoints.Toggle(node);
                GraphBreakpoints.InvalidateCache(); // forces a read from the stored GlobalObjectId

                Assert.IsTrue(GraphBreakpoints.Has(node));
                Assert.IsTrue(GraphBreakpoints.ShouldBreak(node));
                CollectionAssert.AreEqual(new[] { node }, GraphBreakpoints.In(graph).ToArray());

                GraphBreakpoints.SetEnabled(node, false);
                GraphBreakpoints.InvalidateCache();
                Assert.IsTrue(GraphBreakpoints.Has(node));
                Assert.IsFalse(GraphBreakpoints.ShouldBreak(node));

                GraphBreakpoints.Toggle(node);
                GraphBreakpoints.InvalidateCache();
                Assert.IsFalse(GraphBreakpoints.Has(node));
                Assert.GreaterOrEqual(changes, 3);
                GraphBreakpoints.Changed -= OnChanged;

                void OnChanged() => changes++;
            }
            finally
            {
                GraphBreakpoints.ResetSession();
                AssetDatabase.DeleteAsset(TempFolder);
                _graphs.Clear();
            }
        }

        #endregion

        private sealed class FakeObserver : IGraphRuntimeObserver
        {
            public readonly List<string> Events = new();

            public void OnParserCreated(GraphParserBase parser, BaseNode startNode, BaseNode origin, GraphParserBase parent) =>
                Events.Add($"created:{startNode.name}");

            public void OnResolvingNext(GraphParserBase parser, BaseNode from) => Events.Add($"resolve:{from.name}");

            public void OnEdgeTaken(GraphParserBase parser, BaseNode from, BaseNode to, NodePort reportedPort) =>
                Events.Add($"edge:{from.name}>{to.name}");

            public UniTask WaitAtGate(GraphParserBase parser, BaseNode node, CancellationToken cancellationToken)
            {
                Events.Add($"gate:{node.name}");
                return UniTask.CompletedTask;
            }

            public void OnNodeEntered(GraphParserBase parser, BaseNode node) => Events.Add($"enter:{node.name}");
            public void OnTriggerCompleted(GraphParserBase parser, BaseNode node) => Events.Add($"done:{node.name}");
            public void OnParserEnded(GraphParserBase parser, GraphEndReason reason) => Events.Add($"ended:{reason}");
            public void OnRepositioned(GraphParserBase parser, BaseNode node) => Events.Add($"repositioned:{node.name}");
            public void OnGraphCleared(GraphBase graph) => Events.Add("cleared");
            public void OnRunStarted(Object runOwner) => Events.Add("run");
        }
    }
}
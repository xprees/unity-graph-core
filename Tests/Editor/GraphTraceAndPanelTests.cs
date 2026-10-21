using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using XNode;
using Xprees.Graph.Core.Base;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Debugging;
using Xprees.Graph.Core.Editor.Debugger;
using Xprees.Graph.Core.Editor.Graph;
using Xprees.Graph.Core.Editor.Toolbar;
using Xprees.Variables.Aggregations;
using Xprees.Variables.Primitive;
using Object = UnityEngine.Object;

namespace Xprees.Graph.Core.Tests
{
    public class GraphTraceAndPanelTests
    {
        private sealed class FakeIds : ITraceObjectIds
        {
            private readonly Dictionary<Object, string> _ids = new();
            private readonly Dictionary<string, Object> _objects = new();

            public void Register(Object obj, string id)
            {
                _ids[obj] = id;
                _objects[id] = obj;
            }

            public void Forget(string id) => _objects.Remove(id);

            public string IdOf(Object obj) => obj != null && _ids.TryGetValue(obj, out var id) ? id : null;

            public Object Resolve(string id) => _objects.TryGetValue(id, out var obj) ? obj : null;
        }

        private readonly List<Object> _objects = new();
        private readonly List<GraphParserBase> _parsers = new();
        private IGraphRuntimeObserver _previousObserver;
        private string _tempPath;

        [SetUp]
        public void SetUp()
        {
            _previousObserver = GraphDebugHooks.Observer;
            GraphDebugHooks.Observer = null;
            GraphDebugHooks.ResetStatics();
            _tempPath = Path.Combine(Path.GetTempPath(), $"cf-trace-test-{Guid.NewGuid():N}.{GraphTraceFile.Extension}");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var parser in _parsers) parser.Dispose();
            foreach (var obj in _objects) Object.DestroyImmediate(obj);
            _objects.Clear();
            _parsers.Clear();
            GraphDebugHooks.ResetStatics();
            GraphDebugHooks.Observer = _previousObserver;
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
        }

        #region Helpers

        private T Create<T>() where T : ScriptableObject
        {
            var obj = ScriptableObject.CreateInstance<T>();
            _objects.Add(obj);
            return obj;
        }

        /// Start -> R1 -> R2 -> End
        private GraphBase RunnableGraph(out FakeIds ids)
        {
            var graph = Create<GraphBase>();
            var start = graph.AddNode<StartNode>();
            start.name = "Start";
            var r1 = graph.AddNode<TestRecordNode>();
            r1.name = "R1";
            var r2 = graph.AddNode<TestRecordNode>();
            r2.name = "R2";
            var end = graph.AddNode<EndNode>();
            end.name = "End";

            start.GetOutputPort(nameof(StartNode.start)).Connect(r1.GetInputPort("input"));
            r1.GetOutputPort("output").Connect(r2.GetInputPort("input"));
            r2.GetOutputPort("output").Connect(end.GetInputPort(nameof(EndNode.end)));

            ids = new FakeIds();
            ids.Register(graph, "graph" + graph.GetInstanceID());
            foreach (var node in graph.nodes) ids.Register(node, "node" + node.GetInstanceID());
            return graph;
        }

        private void RunToEnd(GraphBase graph, GraphRuntimeTracer tracer)
        {
            GraphDebugHooks.Observer = tracer;
            var parser = new GraphParserBase(graph, null);
            _parsers.Add(parser);
            while (!parser.Ended) parser.Continue().GetAwaiter().GetResult();
            GraphDebugHooks.Observer = null;
        }

        private static Node Named(GraphBase graph, string name) => graph.nodes.First(n => n.name == name);

        #endregion

        #region Trace file

        [Test]
        public void TraceFile_RoundTrip_RestoresRecordsNodesEdgesAndHistory()
        {
            var graph = RunnableGraph(out var ids);
            var source = new GraphRuntimeTracer();
            RunToEnd(graph, source);
            Assert.IsTrue(source.HasTrace);

            GraphTraceFile.Save(source, _tempPath, ids);
            var loaded = new GraphRuntimeTracer();
            Assert.IsTrue(GraphTraceFile.Load(loaded, _tempPath, ids));

            Assert.IsTrue(loaded.IsLoadedTrace);
            Assert.AreEqual(Path.GetFileName(_tempPath), loaded.TraceSourceName);
            Assert.AreEqual(source.Instances.Count, loaded.Instances.Count);
            Assert.AreEqual(source.History.Count, loaded.History.Count);

            foreach (var node in graph.nodes)
            {
                var expected = source.GetNode(node);
                var actual = loaded.GetNode(node);
                Assert.AreEqual(expected.VisitCount, actual.VisitCount, node.name);
                Assert.AreEqual(expected.LastPort?.fieldName, actual.LastPort?.fieldName, node.name);
            }

            var from = Named(graph, "R1");
            var to = Named(graph, "R2");
            var edge = loaded.GetEdge(from.GetOutputPort("output"), to.GetInputPort("input"));
            Assert.AreEqual(source.GetEdge(from.GetOutputPort("output"), to.GetInputPort("input")).Count, edge.Count);
            Assert.Greater(edge.Count, 0);

            var record = loaded.Instances.Single();
            Assert.IsFalse(record.IsAlive);
            Assert.AreSame(graph, record.Graph);
            Assert.IsTrue(record.HasVisited((BaseNode) from));
        }

        [Test]
        public void TraceFile_LoadedTrace_FeedsTheVisualStateResolver()
        {
            var graph = RunnableGraph(out var ids);
            var source = new GraphRuntimeTracer();
            RunToEnd(graph, source);
            GraphTraceFile.Save(source, _tempPath, ids);
            var loaded = new GraphRuntimeTracer();
            GraphTraceFile.Load(loaded, _tempPath, ids);

            var r1 = Named(graph, "R1");
            Assert.AreEqual(NodeVisualState.Visited, NodeVisualStateResolver.Resolve(r1, loaded, null, false, true));
            Assert.AreEqual(NodeVisualState.Idle, NodeVisualStateResolver.Resolve(r1, loaded, null, false, false));
        }

        [Test]
        public void TraceFile_MissingNode_IsSkippedWithOneWarning()
        {
            var graph = RunnableGraph(out var ids);
            var source = new GraphRuntimeTracer();
            RunToEnd(graph, source);
            GraphTraceFile.Save(source, _tempPath, ids);

            var r2 = Named(graph, "R2");
            ids.Forget(ids.IdOf(r2));
            var loaded = new GraphRuntimeTracer();

            LogAssert.Expect(LogType.Warning, new Regex("no longer exist"));
            Assert.IsTrue(GraphTraceFile.Load(loaded, _tempPath, ids));

            Assert.AreEqual(0, loaded.GetNode(r2).VisitCount);
            Assert.Greater(loaded.GetNode(Named(graph, "R1")).VisitCount, 0);
        }

        [Test]
        public void TraceFile_UnsupportedVersion_IsRejected()
        {
            File.WriteAllText(_tempPath, "{\"version\":99}");
            LogAssert.Expect(LogType.Warning, new Regex("unsupported version"));

            Assert.IsFalse(GraphTraceFile.Load(new GraphRuntimeTracer(), _tempPath, new FakeIds()));
        }

        [Test]
        public void TraceFile_MissingFile_ReturnsFalse() =>
            Assert.IsFalse(GraphTraceFile.Load(new GraphRuntimeTracer(), _tempPath, new FakeIds()));

        [Test]
        public void Clear_ForgetsTheLoadedTraceName()
        {
            var graph = RunnableGraph(out var ids);
            var source = new GraphRuntimeTracer();
            RunToEnd(graph, source);
            GraphTraceFile.Save(source, _tempPath, ids);
            var loaded = new GraphRuntimeTracer();
            GraphTraceFile.Load(loaded, _tempPath, ids);

            loaded.Clear();

            Assert.IsFalse(loaded.IsLoadedTrace);
            Assert.IsNull(loaded.TraceSourceName);
        }

        #endregion

        #region History filter & texts

        [Test]
        public void FilterHistory_ByGraphAndRecord_KeepsOrderAndNewest()
        {
            var graphA = RunnableGraph(out _);
            var graphB = RunnableGraph(out _);
            var tracer = new GraphRuntimeTracer();
            RunToEnd(graphA, tracer);
            RunToEnd(graphB, tracer);

            var all = DebuggerText.FilterHistory(tracer.History, null, null);
            var onlyA = DebuggerText.FilterHistory(tracer.History, graphA, null);
            Assert.AreEqual(tracer.History.Count, all.Count);
            Assert.IsTrue(onlyA.Count > 0 && onlyA.Count < all.Count);
            Assert.IsTrue(onlyA.All(e => e.Graph == graphA));

            var recordId = tracer.InstancesOf(graphB).Single().Id;
            var onlyRecord = DebuggerText.FilterHistory(tracer.History, graphB, recordId);
            Assert.IsTrue(onlyRecord.All(e => e.RecordId == recordId));
            Assert.IsEmpty(DebuggerText.FilterHistory(tracer.History, graphA, recordId));

            var newest = DebuggerText.FilterHistory(tracer.History, graphB, null, 2);
            Assert.AreEqual(2, newest.Count);
            Assert.AreEqual(onlyRecord[^1].Node, newest[^1].Node);
            Assert.AreEqual(onlyRecord[^2].Node, newest[0].Node);
        }

        [Test]
        public void NodeState_ComposesTheParts()
        {
            Assert.AreEqual("running 2.5 s  |  ×3  |  → pass  |  hello",
                DebuggerText.NodeState(GraphParserPhase.Triggering, 12.5, 10, 3, "pass", "hello"));
            Assert.AreEqual("×2", DebuggerText.NodeState(null, 50, 10, 2, null, null));
            Assert.AreEqual(string.Empty, DebuggerText.NodeState(null, 0, 0, 0, null, null));
        }

        [Test]
        public void EdgeTooltip_Formats()
        {
            Assert.AreEqual("not taken", DebuggerText.EdgeTooltip(0, 0, 0, 5, null));
            Assert.AreEqual("taken 3×, last 2.0 s ago (#2 Main)", DebuggerText.EdgeTooltip(3, 3, 10, 12, "#2 Main"));
            StringAssert.EndsWith("[inferred]", DebuggerText.EdgeTooltip(1, 0, 10, 12, null));
        }

        [TestCase(0.0, 0.0, 1f)]
        [TestCase(0.0, 0.25, 0.5f)]
        [TestCase(0.0, 0.5, 0f)]
        [TestCase(0.0, 5.0, 0f)]
        public void FlashAmount_FadesOut(double changedAt, double now, float expected) =>
            Assert.AreEqual(expected, LiveValues.FlashAmount(now, changedAt), 0.001f);

        #endregion

        #region Variables

        [TestCase(typeof(bool), true)]
        [TestCase(typeof(int), true)]
        [TestCase(typeof(float), true)]
        [TestCase(typeof(string), true)]
        [TestCase(typeof(LogType), true)]
        [TestCase(typeof(Vector3), false)]
        [TestCase(typeof(Object), false)]
        [TestCase(typeof(List<int>), false)]
        [TestCase(null, false)]
        public void IsEditableType_PrimitivesAndEnumsOnly(Type type, bool expected) =>
            Assert.AreEqual(expected, VariableAccess.IsEditableType(type));

        [Test]
        public void VariableAccess_ReadsAndWritesThroughCurrentValue()
        {
            var variable = Create<IntVariable>();
            var notified = 0;
            variable.onValueChanged += _ => notified++;

            Assert.IsTrue(VariableAccess.IsEditable(variable));
            Assert.IsTrue(VariableAccess.TryWrite(variable, 7));
            Assert.IsTrue(VariableAccess.TryRead(variable, out var value));

            Assert.AreEqual(7, value);
            Assert.AreEqual(7, variable.CurrentValue);
            Assert.AreEqual(1, notified);
        }

        [Test]
        public void VariableAccess_AggregationIsReadOnly()
        {
            var aggregation = Create<BoolAggregation>();

            Assert.IsFalse(VariableAccess.IsEditable(aggregation));
            Assert.IsFalse(VariableAccess.TryWrite(aggregation, true));
        }

        [Test]
        public void CollectUsage_ListsReferencedVariablesAndIgnoresInlinedReferences()
        {
            var graph = Create<GraphBase>();
            var first = graph.AddNode<TestVariableNode>();
            var second = graph.AddNode<TestVariableNode>();

            var shared = Create<IntVariable>();
            shared.name = "Shared";
            var other = Create<IntVariable>();
            other.name = "Other";
            var listed = Create<IntVariable>();
            listed.name = "Listed";

            first.reference.useInlined = false;
            first.reference.variable = shared;
            first.direct = other;
            second.reference.useInlined = false;
            second.reference.variable = shared;
            second.many.Add(listed);

            var usage = GraphDebugPanel.CollectUsage(graph);

            CollectionAssert.AreEqual(new[] { "Listed", "Other", "Shared" }, usage.Select(p => p.Key.name));
            Assert.AreEqual(2, usage.Single(p => p.Key == shared).Value.Count);

            first.reference.useInlined = true;
            usage = GraphDebugPanel.CollectUsage(graph);
            Assert.AreEqual(1, usage.Single(p => p.Key == shared).Value.Count);
        }

        #endregion

        #region Edge hit test

        [Test]
        public void EdgeHitTest_StraightPolyline_UsesSegmentDistance()
        {
            var distance = EdgeHitTest.Distance(new Vector2(50, 10), new Vector2(0, 0), new Vector2(100, 0), null, false, 7f);
            Assert.AreEqual(10f, distance, 0.001f);

            var reroutes = new List<Vector2> { new(50, 50) };
            Assert.AreEqual(0f, EdgeHitTest.Distance(new Vector2(50, 50), new Vector2(0, 0), new Vector2(100, 0), reroutes, false, 7f), 0.001f);
        }

        [Test]
        public void EdgeHitTest_Curve_PassesThroughItsEndsAndMidpoint()
        {
            var from = new Vector2(0, 0);
            var to = new Vector2(200, 100);

            Assert.Less(EdgeHitTest.Distance(from, from, to, null, true, 7f), 0.5f);
            Assert.Less(EdgeHitTest.Distance(to, from, to, null, true, 7f), 0.5f);
            // A symmetric S-curve crosses the centre between its end points
            Assert.Less(EdgeHitTest.Distance((from + to) * 0.5f, from, to, null, true, 7f), 1.5f);
            Assert.Greater(EdgeHitTest.Distance(new Vector2(100, 400), from, to, null, true, 7f), 100f);
        }

        #endregion

    }
}
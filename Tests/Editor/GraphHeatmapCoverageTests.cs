using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using XNode;
using Xprees.Graph.Core.Base;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Debugging;
using Xprees.Graph.Core.Editor.Debugger;
using Xprees.Graph.Core.Nodes.Debug;

namespace Xprees.Graph.Core.Tests
{
    public class GraphHeatmapCoverageTests
    {
        private sealed class FakeIds : ITraceObjectIds
        {
            public string IdOf(Object obj) => obj ? "id" + obj.GetInstanceID() : null;

            public Object Resolve(string id) => null;
        }

        private readonly List<Object> _objects = new();
        private IGraphRuntimeObserver _previousObserver;

        [SetUp]
        public void SetUp()
        {
            _previousObserver = GraphDebugHooks.Observer;
            GraphDebugHooks.Observer = null;
            GraphDebugHooks.ResetStatics();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _objects) Object.DestroyImmediate(obj);
            _objects.Clear();
            GraphDebugHooks.ResetStatics();
            GraphDebugHooks.Observer = _previousObserver;
        }

        /// Start -> R1 -> R2 -> End, plus an orphan R3 (unreachable), a note and a group.
        private GraphBase BuildGraph()
        {
            var graph = ScriptableObject.CreateInstance<GraphBase>();
            _objects.Add(graph);
            var start = graph.AddNode<StartNode>();
            start.name = "Start";
            var r1 = graph.AddNode<TestRecordNode>();
            r1.name = "R1";
            var r2 = graph.AddNode<TestRecordNode>();
            r2.name = "R2";
            var end = graph.AddNode<EndNode>();
            end.name = "End";
            graph.AddNode<TestRecordNode>().name = "Orphan";
            graph.AddNode<NoteNode>().name = "Note";
            graph.AddNode<NodeGroup>().name = "Group";

            start.GetOutputPort(nameof(StartNode.start)).Connect(r1.GetInputPort("input"));
            r1.GetOutputPort("output").Connect(r2.GetInputPort("input"));
            r2.GetOutputPort("output").Connect(end.GetInputPort(nameof(EndNode.end)));
            return graph;
        }

        private static void RunToEnd(GraphBase graph, GraphRuntimeTracer tracer, List<GraphParserBase> parsers)
        {
            GraphDebugHooks.Observer = tracer;
            var parser = new GraphParserBase(graph, null);
            parsers.Add(parser);
            while (!parser.Ended) parser.Continue().GetAwaiter().GetResult();
            GraphDebugHooks.Observer = null;
        }

        private static Node Named(GraphBase graph, string name) => graph.nodes.First(n => n.name == name);

        #region Heat math

        [Test]
        public void Normalize_Linear_IsRatioOfMax()
        {
            Assert.AreEqual(0f, GraphHeatmap.Normalize(0, 10, false));
            Assert.AreEqual(0.5f, GraphHeatmap.Normalize(5, 10, false), 0.0001f);
            Assert.AreEqual(1f, GraphHeatmap.Normalize(10, 10, false));
            Assert.AreEqual(1f, GraphHeatmap.Normalize(20, 10, false));
        }

        [Test]
        public void Normalize_Log_LiftsSmallCountsAndKeepsEnds()
        {
            var linear = GraphHeatmap.Normalize(2, 100, false);
            var log = GraphHeatmap.Normalize(2, 100, true);
            Assert.Greater(log, linear);
            Assert.AreEqual(0f, GraphHeatmap.Normalize(0, 100, true));
            Assert.AreEqual(1f, GraphHeatmap.Normalize(100, 100, true));
            Assert.Less(GraphHeatmap.Normalize(10, 100, true), GraphHeatmap.Normalize(50, 100, true));
        }

        [Test]
        public void Normalize_NoMax_IsZero() => Assert.AreEqual(0f, GraphHeatmap.Normalize(3, 0, true));

        [Test]
        public void EdgeThickness_SpansRange()
        {
            Assert.AreEqual(GraphHeatmap.MinEdgeThickness, GraphHeatmap.EdgeThickness(0f));
            Assert.AreEqual(GraphHeatmap.MaxEdgeThickness, GraphHeatmap.EdgeThickness(1f));
            Assert.AreEqual(GraphHeatmap.MaxEdgeThickness, GraphHeatmap.EdgeThickness(7f));
        }

        [Test]
        public void BuildScales_UsesMaxPerGraph()
        {
            var graph = BuildGraph();
            var tracer = new GraphRuntimeTracer();
            var parsers = new List<GraphParserBase>();
            RunToEnd(graph, tracer, parsers);
            RunToEnd(graph, tracer, parsers);
            foreach (var parser in parsers) parser.Dispose();

            var scales = GraphHeatmap.BuildScales(tracer);
            Assert.IsTrue(scales.TryGetValue(graph, out var scale));
            Assert.AreEqual(tracer.GetNode(Named(graph, "R1")).VisitCount, scale.MaxNodeVisits);
            Assert.GreaterOrEqual(scale.MaxNodeVisits, 1);
            Assert.GreaterOrEqual(scale.MaxEdgeCount, 1);
        }

        #endregion

        #region Coverage

        [Test]
        public void Reachable_ExcludesOrphansNotesAndGroups()
        {
            var graph = BuildGraph();
            var names = GraphCoverage.ReachableNodes(graph).Select(n => n.name).OrderBy(n => n).ToArray();
            CollectionAssert.AreEqual(new[] { "End", "R1", "R2", "Start" }, names);
        }

        [Test]
        public void Reachable_NoStart_FallsBackToUnconnectedInputs()
        {
            var graph = ScriptableObject.CreateInstance<GraphBase>();
            _objects.Add(graph);
            var a = graph.AddNode<TestRecordNode>();
            var b = graph.AddNode<TestRecordNode>();
            a.GetOutputPort("output").Connect(b.GetInputPort("input"));

            Assert.AreEqual(2, GraphCoverage.ReachableNodes(graph).Count);
        }

        [Test]
        public void Compute_CountsVisitedOverReachable()
        {
            var graph = BuildGraph();
            var visited = new HashSet<Node> { Named(graph, "Start"), Named(graph, "R1"), Named(graph, "Orphan"), Named(graph, "Note") };

            var result = GraphCoverage.Compute(graph, visited.Contains);

            Assert.AreEqual(2, result.Visited);
            Assert.AreEqual(4, result.Total);
            Assert.AreEqual(50f, result.Percent, 0.0001f);
        }

        [Test]
        public void Compute_EmptyGraph_IsZeroPercent() => Assert.AreEqual(0f, new CoverageResult(0, 0).Percent);

        [Test]
        public void Coverage_FromRealRun_IsFull_AndCumulativeAddsEarlierRuns()
        {
            var graph = BuildGraph();
            var tracer = new GraphRuntimeTracer();
            var parsers = new List<GraphParserBase>();
            RunToEnd(graph, tracer, parsers);
            foreach (var parser in parsers) parser.Dispose();
            var ids = new FakeIds();

            var current = GraphCoverageService.Compute(graph, tracer, null, ids);
            Assert.AreEqual(current.Total, current.Visited);

            var empty = new GraphRuntimeTracer();
            Assert.AreEqual(0, GraphCoverageService.Compute(graph, empty, null, ids).Visited);

            var data = new CoverageData();
            Assert.IsTrue(GraphCoverageService.Accumulate(tracer, ids, data));
            var cumulative = GraphCoverageService.Compute(graph, empty, data, ids);
            Assert.AreEqual(current.Visited, cumulative.Visited);
            Assert.IsFalse(GraphCoverageService.Accumulate(tracer, ids, data), "second identical run adds nothing");
        }

        [Test]
        public void Accumulate_SkipsLoadedTraces()
        {
            var graph = BuildGraph();
            var tracer = new GraphRuntimeTracer();
            var parsers = new List<GraphParserBase>();
            RunToEnd(graph, tracer, parsers);
            foreach (var parser in parsers) parser.Dispose();
            tracer.IsLoadedTrace = true;

            Assert.IsFalse(GraphCoverageService.Accumulate(tracer, new FakeIds(), new CoverageData()));
        }

        [Test]
        public void CoverageData_UnionAndJsonRoundTrip()
        {
            var data = new CoverageData();
            Assert.IsTrue(data.Add("g1", new[] { "a", "b" }));
            Assert.IsTrue(data.Add("g1", new[] { "b", "c" }));
            Assert.IsFalse(data.Add("g1", new[] { "a" }));
            Assert.IsTrue(data.Add("g2", new[] { "x" }));

            var restored = JsonUtility.FromJson<CoverageData>(JsonUtility.ToJson(data));

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, restored.NodesOf("g1").ToArray());
            Assert.IsTrue(restored.Contains("g2", "x"));
            Assert.IsFalse(restored.Contains("g2", "a"));

            restored.Clear();
            Assert.IsTrue(restored.IsEmpty);
        }

        #endregion

    }
}
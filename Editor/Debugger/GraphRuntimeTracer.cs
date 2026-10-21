using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using XNode;
using Xprees.Graph.Core.Base;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Debugging;
using Xprees.Graph.Core.Nodes.Debug;
using Object = UnityEngine.Object;

namespace Xprees.Graph.Core.Editor.Debugger
{
    /// Records what the graph parsers do (instances, visited nodes, taken edges, history) and holds them at breakpoints.
    /// Registered as <see cref="GraphDebugHooks.Observer"/> while the master toggle is on.
    public sealed class GraphRuntimeTracer : IGraphRuntimeObserver
    {
        private const int HistoryCapacity = 2000;
        private const int MaxDeadRecords = 1000;

        private static GraphRuntimeTracer _instance;

        private readonly Dictionary<int, ParserRecord> _records = new();
        private readonly Queue<int> _deadRecordIds = new();
        private readonly Dictionary<Node, NodeTrace> _nodes = new();
        private readonly Dictionary<EdgeKey, EdgeTrace> _edges = new();
        private readonly HistoryBuffer _history = new(HistoryCapacity);

        public static GraphRuntimeTracer Instance => _instance ??= new GraphRuntimeTracer();

        /// Incremented on every change, the UI repaints when it differs from the last seen value.
        public int Version { get; private set; }

        public IReadOnlyCollection<ParserRecord> Instances => _records.Values;

        public IReadOnlyList<HistoryEntry> History => _history;

        /// A read-only trace loaded from a file.
        public bool IsLoadedTrace { get; internal set; }

        /// File name of the loaded trace, null for a live run.
        public string TraceSourceName { get; internal set; }

        /// Name of the object that started the run (the scenario), null when unknown.
        public string RunName { get; internal set; }

        /// Time.timeAsDouble when the run stopped (Play Mode exit) or was saved. Times of a stopped run are relative to it.
        public double EndTime { get; internal set; }

        /// The "now" that ages in the UI are measured against: the game clock while a live run goes on, else the end of the recorded run.
        public double ReferenceTime => Application.isPlaying && !IsLoadedTrace ? Time.timeAsDouble : EndTime;

        /// True when anything was recorded since the last clear.
        public bool HasTrace => _records.Count > 0 || _nodes.Count > 0;

        /// A break was hit, the UI opens the graph window and focuses the node.
        public event Action<ParserRecord, BaseNode> BreakHit;

        public GraphRuntimeTracer()
        {
            GraphBreakpoints.Changed += ReleaseRemovedBreakpoints;
        }

        public IEnumerable<ParserRecord> InstancesOf(GraphBase graph)
        {
            foreach (var record in _records.Values)
            {
                if (record.Graph == graph) yield return record;
            }
        }

        public bool TryGetRecord(int id, out ParserRecord record) => _records.TryGetValue(id, out record);

        /// Never null, a node that was not seen returns an empty trace.
        public NodeTrace GetNode(Node node) => node != null && _nodes.TryGetValue(node, out var trace) ? trace : NodeTrace.Empty;

        /// Never null, an edge that was not taken returns an empty trace.
        public EdgeTrace GetEdge(NodePort output, NodePort input) =>
            output != null && input != null && _edges.TryGetValue(new EdgeKey(output, input), out var trace) ? trace : EdgeTrace.Empty;

        /// Clears visited nodes, edges, history and ended instances. Instances that are still running stay (they re-mark their current node).
        public void Clear()
        {
            _nodes.Clear();
            _edges.Clear();
            _history.Clear();
            _deadRecordIds.Clear();
            IsLoadedTrace = false;
            TraceSourceName = null;
            RunName = null;
            EndTime = 0;

            var dead = new List<int>();
            foreach (var pair in _records)
            {
                if (!pair.Value.IsAlive) dead.Add(pair.Key);
            }

            foreach (var id in dead) _records.Remove(id);

            foreach (var record in _records.Values)
            {
                record.ClearVisited();
                if (record.ActiveNode == null) continue;
                record.MarkVisited(record.ActiveNode);
                TraceOf(record.ActiveNode).AddActive(record);
            }

            Version++;
        }

        /// Full wipe including running instances, used when a Play Mode session starts.
        internal void Reset()
        {
            _records.Clear();
            Clear();
        }

        /// Play Mode is exiting: nothing runs any more, the data stays inspectable.
        internal void EndAllRunning(GraphEndReason reason)
        {
            EndTime = Time.timeAsDouble;
            foreach (var record in _records.Values)
            {
                if (record.IsAlive) EndRecord(record, reason);
            }

            Version++;
        }

        /// A gate was released by <see cref="GraphDebugGate"/>.
        internal void OnReleased(ParserRecord record, bool step)
        {
            record.IsPausedAtGate = false;
            record.PauseReason = GateBreakReason.None;
            record.StepPending = step;
            record.Phase = GraphParserPhase.AtGate;
            Version++;
        }

        #region Trace file access

        internal IEnumerable<KeyValuePair<Node, NodeTrace>> NodeTraces => _nodes;

        internal IEnumerable<KeyValuePair<EdgeKey, EdgeTrace>> EdgeTraces => _edges;

        /// Wipes everything and returns the tracer to a blank state before a trace file is imported.
        internal void BeginImport()
        {
            Reset();
            IsLoadedTrace = false;
        }

        internal ParserRecord ImportRecord(
            int id,
            string label,
            GraphBase graph,
            ParserRecord parent,
            BaseNode origin,
            BaseNode activeNode,
            GraphEndReason endReason
        )
        {
            var record = new ParserRecord(this, id, label, graph, parent, origin)
            {
                ActiveNode = activeNode,
                Phase = GraphParserPhase.Ended,
                EndReason = endReason,
                IsAlive = false,
            };
            _records[id] = record;
            return record;
        }

        internal void ImportNode(Node node, int visitCount, double lastEnteredTime, NodePort lastPort)
        {
            var trace = TraceOf(node);
            trace.VisitCount = visitCount;
            trace.LastEnteredTime = lastEnteredTime;
            trace.LastPort = lastPort;
        }

        internal void ImportEdge(NodePort output, NodePort input, int count, int exactCount, double lastTime, ParserRecord lastRecord)
        {
            _edges[new EdgeKey(output, input)] = new EdgeTrace
                { Count = count, ExactCount = exactCount, LastTime = lastTime, LastRecord = lastRecord };
        }

        internal void ImportHistory(in HistoryEntry entry) => _history.Add(entry);

        internal void EndImport(string sourceName, string runName, double endTime)
        {
            IsLoadedTrace = true;
            TraceSourceName = sourceName;
            RunName = runName;
            EndTime = endTime;
            Version++;
        }

        #endregion

        #region IGraphRuntimeObserver

        public void OnParserCreated(GraphParserBase parser, BaseNode startNode, BaseNode origin, GraphParserBase parent)
        {
            var parentRecord = parent != null ? GetOrCreate(parent) : null;
            var record = CreateRecord(parser, parentRecord, origin);

            record.ActiveNode = startNode;
            record.Phase = GraphParserPhase.Positioned;
            if (startNode != null)
            {
                var startTrace = TraceOf(startNode);
                startTrace.VisitCount++;
                startTrace.LastEnteredTime = Time.timeAsDouble;
                startTrace.AddActive(record);
                record.MarkVisited(startNode);
            }

            NodePort edgeOutput = null;
            NodePort edgeInput = null;
            var ambiguous = false;
            if (origin != null)
            {
                // The origin is entered by its parent parser (sub-graph) or by an event (async start), only the latter is a new visit.
                if (origin != startNode && parentRecord?.ActiveNode != origin)
                {
                    var originTrace = TraceOf(origin);
                    originTrace.VisitCount++;
                    originTrace.LastEnteredTime = Time.timeAsDouble;
                }

                record.MarkVisited(origin);
                if (startNode != null && origin != startNode)
                {
                    RecordEdge(record, origin, startNode, null, out edgeOutput, out edgeInput, out ambiguous);
                }
            }

            if (startNode != null)
                _history.Add(new HistoryEntry(Time.timeAsDouble, record, record.Graph, startNode, edgeOutput, edgeInput, ambiguous));
            Version++;
        }

        public void OnResolvingNext(GraphParserBase parser, BaseNode from)
        {
            var record = GetOrCreate(parser);
            Revive(record);
            record.Phase = GraphParserPhase.ResolvingNext;
            SetActive(record, from);
            Version++;
        }

        public void OnEdgeTaken(GraphParserBase parser, BaseNode from, BaseNode to, NodePort reportedPort)
        {
            var record = GetOrCreate(parser);
            RecordEdge(record, from, to, reportedPort, out var output, out var input, out var ambiguous);
            record.LastEdgeOutput = output;
            record.LastEdgeInput = input;
            record.LastEdgeAmbiguous = ambiguous;
            if (from != null) TraceOf(from).LastPort = ambiguous ? null : output;

            SetActive(record, to);
            record.Phase = GraphParserPhase.AtGate;
            Version++;
        }

        public UniTask WaitAtGate(GraphParserBase parser, BaseNode node, CancellationToken cancellationToken)
        {
            if (GraphDebugHooks.BreaksSuppressed || node == null) return UniTask.CompletedTask;

            var record = GetOrCreate(parser);
            var reason = GateBreakReason.None;
            if (GraphBreakpoints.ShouldBreak(node)) reason = GateBreakReason.Breakpoint;
            else if (record.StepPending) reason = GateBreakReason.Step;
            else if (node is PausePointNode { active: true }) reason = GateBreakReason.PausePoint;

            if (reason == GateBreakReason.None) return UniTask.CompletedTask;

            return HoldAtGate(record, node, reason, cancellationToken);
        }

        public void OnNodeEntered(GraphParserBase parser, BaseNode node)
        {
            var record = GetOrCreate(parser);
            Revive(record);
            SetActive(record, node);
            record.Phase = GraphParserPhase.Triggering;

            var trace = TraceOf(node);
            trace.VisitCount++;
            trace.LastEnteredTime = Time.timeAsDouble;
            record.MarkVisited(node);

            _history.Add(new HistoryEntry(Time.timeAsDouble, record, record.Graph, node, record.LastEdgeOutput, record.LastEdgeInput,
                record.LastEdgeAmbiguous));
            record.LastEdgeOutput = null;
            record.LastEdgeInput = null;
            Version++;
        }

        public void OnTriggerCompleted(GraphParserBase parser, BaseNode node)
        {
            var record = GetOrCreate(parser);
            record.Phase = GraphParserPhase.Positioned;
            Version++;
        }

        public void OnParserEnded(GraphParserBase parser, GraphEndReason reason)
        {
            EndRecord(GetOrCreate(parser), reason);
            Version++;
        }

        public void OnRepositioned(GraphParserBase parser, BaseNode node)
        {
            var record = GetOrCreate(parser);
            SetActive(record, node);
            record.Phase = GraphParserPhase.Positioned;
            Version++;
        }

        public void OnGraphCleared(GraphBase graph)
        {
            foreach (var record in _records.Values)
            {
                if (record.Graph != graph || !record.IsAlive) continue;

                EndRecord(record, GraphEndReason.GraphInactive);
                GraphDebugGate.Cancel(record.Id);
            }

            Version++;
        }

        public void OnRunStarted(Object runOwner)
        {
            Clear();
            RunName = runOwner ? runOwner.name : null;
        }

        #endregion

        private async UniTask HoldAtGate(ParserRecord record, BaseNode node, GateBreakReason reason, CancellationToken cancellationToken)
        {
            Revive(record);
            SetActive(record, node);
            record.Phase = GraphParserPhase.AtGate;
            record.IsPausedAtGate = true;
            record.PauseReason = reason;
            record.StepPending = false;
            Version++;
            BreakHit?.Invoke(record, node);

            if (Application.isPlaying && GraphDebuggerSettings.FreezeWorldOnBreak) EditorApplication.isPaused = true;

            try
            {
                await GraphDebugGate.Wait(record, cancellationToken);
            }
            finally
            {
                record.IsPausedAtGate = false;
                record.PauseReason = GateBreakReason.None;
                Version++;
            }
        }

        /// A breakpoint that was removed or disabled lets the records it holds go.
        private void ReleaseRemovedBreakpoints()
        {
            foreach (var record in new List<ParserRecord>(_records.Values))
            {
                if (!record.IsPausedAtGate || record.PauseReason != GateBreakReason.Breakpoint) continue;
                if (!GraphBreakpoints.ShouldBreak(record.ActiveNode)) GraphDebugGate.Continue(record);
            }
        }

        private ParserRecord GetOrCreate(GraphParserBase parser)
        {
            if (_records.TryGetValue(parser.DebugId, out var existing)) return existing;

            // A parser that started before the tracer was switched on.
            var record = CreateRecord(parser, null, null);
            record.ActiveNode = parser.CurrentNode;
            record.Phase = GraphParserPhase.Positioned;
            if (parser.CurrentNode != null)
            {
                record.MarkVisited(parser.CurrentNode);
                TraceOf(parser.CurrentNode).AddActive(record);
            }

            return record;
        }

        private ParserRecord CreateRecord(GraphParserBase parser, ParserRecord parent, BaseNode origin)
        {
            var id = parser.DebugId;
            var record = new ParserRecord(this, id, AutoLabel(id, parent, origin), parser.DebugGraph, parent, origin)
            {
                Parser = new WeakReference<GraphParserBase>(parser)
            };
            _records[id] = record;
            return record;
        }

        private static string AutoLabel(int id, ParserRecord parent, BaseNode origin)
        {
            if (origin is AsyncStartBaseNode) return $"Async from {origin.name} #{id}";
            if (origin is SubGraphNode) return $"Sub-graph via {origin.name} #{id}";
            if (origin != null) return $"From {origin.name} #{id}";
            return parent == null ? "Main" : $"Instance #{id}";
        }

        private void EndRecord(ParserRecord record, GraphEndReason reason)
        {
            if (!record.IsAlive) return;

            record.IsAlive = false;
            record.EndReason = reason;
            record.Phase = GraphParserPhase.Ended;
            record.IsPausedAtGate = false;
            record.Parser = null;
            if (record.ActiveNode != null) TraceOf(record.ActiveNode).RemoveActive(record);

            _deadRecordIds.Enqueue(record.Id);
            while (_deadRecordIds.Count > MaxDeadRecords)
            {
                var oldest = _deadRecordIds.Dequeue();
                if (_records.TryGetValue(oldest, out var old) && !old.IsAlive) _records.Remove(oldest);
            }
        }

        /// A parser that continues after it reported an end (cancelled flow resumed) is running again.
        private void Revive(ParserRecord record)
        {
            if (record.IsAlive) return;

            record.IsAlive = true;
            record.EndReason = null;
        }

        private void SetActive(ParserRecord record, BaseNode node)
        {
            if (record.ActiveNode == node) return;

            if (record.ActiveNode != null) TraceOf(record.ActiveNode).RemoveActive(record);
            record.ActiveNode = node;
            if (node != null) TraceOf(node).AddActive(record);
        }

        private NodeTrace TraceOf(Node node)
        {
            if (!_nodes.TryGetValue(node, out var trace))
            {
                trace = new NodeTrace();
                _nodes[node] = trace;
            }

            return trace;
        }

        private void RecordEdge(
            ParserRecord record,
            BaseNode from,
            BaseNode to,
            NodePort reported,
            out NodePort firstOutput,
            out NodePort firstInput,
            out bool ambiguous
        )
        {
            firstOutput = null;
            firstInput = null;
            var outputs = EdgeInference.Find(from, to, reported, out ambiguous);
            foreach (var output in outputs)
            {
                var connectionCount = output.ConnectionCount;
                for (var i = 0; i < connectionCount; i++)
                {
                    var input = output.GetConnection(i);
                    if (input == null || input.node != to) continue;

                    var key = new EdgeKey(output, input);
                    if (!_edges.TryGetValue(key, out var edge))
                    {
                        edge = new EdgeTrace();
                        _edges[key] = edge;
                    }

                    edge.Count++;
                    if (!ambiguous) edge.ExactCount++;
                    edge.LastTime = Time.timeAsDouble;
                    edge.LastRecord = record;

                    if (firstOutput != null) continue;
                    firstOutput = output;
                    firstInput = input;
                }
            }
        }
    }
}
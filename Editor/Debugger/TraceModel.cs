using System;
using System.Collections;
using System.Collections.Generic;
using XNode;
using Xprees.Graph.Core.Base;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Debugging;

namespace Xprees.Graph.Core.Editor.Debugger
{
    public enum GateBreakReason
    {
        None,
        Breakpoint,
        Step,
        PausePoint
    }

    /// One parser instance seen by the tracer (a scenario run, an async flow, a sub-graph, a dialog, a chat).
    public sealed class ParserRecord
    {
        private readonly string _autoLabel;
        private readonly HashSet<BaseNode> _visited = new();

        public int Id { get; }
        public GraphBase Graph { get; }
        public ParserRecord Parent { get; }
        public BaseNode Origin { get; }
        public BaseNode ActiveNode { get; internal set; }
        public GraphParserPhase Phase { get; internal set; }
        public GraphEndReason? EndReason { get; internal set; }
        public bool IsAlive { get; internal set; } = true;
        public bool IsPausedAtGate { get; internal set; }
        public GateBreakReason PauseReason { get; internal set; }
        public WeakReference<GraphParserBase> Parser { get; internal set; }

        /// Nodes this instance went through (including the node it started on).
        public IReadOnlyCollection<BaseNode> VisitedNodes => _visited;

        internal GraphRuntimeTracer Owner { get; }
        internal bool StepPending { get; set; }
        internal NodePort LastEdgeOutput { get; set; }
        internal NodePort LastEdgeInput { get; set; }
        internal bool LastEdgeAmbiguous { get; set; }

        /// A name set by the project (SetDebugName) wins over the label derived from the origin.
        public string Label
        {
            get
            {
                if (Parser != null && Parser.TryGetTarget(out var parser) && !string.IsNullOrEmpty(parser.DebugName)) return parser.DebugName;
                return _autoLabel;
            }
        }

        internal ParserRecord(GraphRuntimeTracer owner, int id, string autoLabel, GraphBase graph, ParserRecord parent, BaseNode origin)
        {
            Owner = owner;
            Id = id;
            _autoLabel = autoLabel;
            Graph = graph;
            Parent = parent;
            Origin = origin;
        }

        internal bool MarkVisited(BaseNode node) => _visited.Add(node);

        public bool HasVisited(BaseNode node) => _visited.Contains(node);

        internal void ClearVisited() => _visited.Clear();

        public override string ToString() => $"#{Id} {Label}";
    }

    /// Run statistics of one node.
    public sealed class NodeTrace
    {
        internal static readonly NodeTrace Empty = new();

        private readonly List<ParserRecord> _activeIn = new();

        public int VisitCount { get; internal set; }

        /// Time.timeAsDouble of the last entry.
        public double LastEnteredTime { get; internal set; }

        /// Records currently positioned on this node.
        public IReadOnlyList<ParserRecord> ActiveIn => _activeIn;

        /// Output port taken when the flow last left the node, null when unknown or ambiguous.
        public NodePort LastPort { get; internal set; }

        internal void AddActive(ParserRecord record)
        {
            if (!_activeIn.Contains(record)) _activeIn.Add(record);
        }

        internal void RemoveActive(ParserRecord record) => _activeIn.Remove(record);
    }

    /// Run statistics of one connection.
    public sealed class EdgeTrace
    {
        internal static readonly EdgeTrace Empty = new();

        public int Count { get; internal set; }
        public int ExactCount { get; internal set; }
        public double LastTime { get; internal set; }
        public ParserRecord LastRecord { get; internal set; }

        /// True when every traversal was only inferred and shared with another connection of the same node.
        public bool Ambiguous => Count > 0 && ExactCount == 0;
    }

    public readonly struct HistoryEntry
    {
        public double Time { get; }
        public int RecordId { get; }
        public ParserRecord Record { get; }
        public GraphBase Graph { get; }
        public BaseNode Node { get; }
        public NodePort EdgeOutput { get; }
        public NodePort EdgeInput { get; }
        public bool EdgeAmbiguous { get; }

        public HistoryEntry(
            double time,
            ParserRecord record,
            GraphBase graph,
            BaseNode node,
            NodePort edgeOutput,
            NodePort edgeInput,
            bool edgeAmbiguous
        )
        {
            Time = time;
            Record = record;
            RecordId = record?.Id ?? 0;
            Graph = graph;
            Node = node;
            EdgeOutput = edgeOutput;
            EdgeInput = edgeInput;
            EdgeAmbiguous = edgeAmbiguous;
        }
    }

    internal readonly struct EdgeKey : IEquatable<EdgeKey>
    {
        public readonly NodePort Output;
        public readonly NodePort Input;

        public EdgeKey(NodePort output, NodePort input)
        {
            Output = output;
            Input = input;
        }

        public bool Equals(EdgeKey other) => ReferenceEquals(Output, other.Output) && ReferenceEquals(Input, other.Input);
        public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
        public override int GetHashCode() => (Output?.GetHashCode() ?? 0) * 397 ^ (Input?.GetHashCode() ?? 0);
    }

    /// Oldest-first view over a fixed-size ring of history entries.
    public sealed class HistoryBuffer : IReadOnlyList<HistoryEntry>
    {
        private readonly HistoryEntry[] _items;
        private int _start;

        public int Count { get; private set; }

        public HistoryBuffer(int capacity) => _items = new HistoryEntry[capacity];

        public HistoryEntry this[int index]
        {
            get
            {
                if ((uint) index >= (uint) Count) throw new ArgumentOutOfRangeException(nameof(index));
                return _items[(_start + index) % _items.Length];
            }
        }

        internal void Add(in HistoryEntry entry)
        {
            if (Count < _items.Length)
            {
                _items[(_start + Count) % _items.Length] = entry;
                Count++;
                return;
            }

            _items[_start] = entry;
            _start = (_start + 1) % _items.Length;
        }

        internal void Clear()
        {
            Array.Clear(_items, 0, _items.Length);
            _start = 0;
            Count = 0;
        }

        public IEnumerator<HistoryEntry> GetEnumerator()
        {
            for (var i = 0; i < Count; i++) yield return this[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
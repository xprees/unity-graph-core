using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using XNode;
using Xprees.Graph.Core.Base;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Debugging;
using Object = UnityEngine.Object;

namespace Xprees.Graph.Core.Editor.Debugger
{
    /// Maps graph and node assets to stable text ids and back. Tests plug in a fake, the editor uses GlobalObjectId.
    public interface ITraceObjectIds
    {
        /// Null when the object has no persistent id (not saved yet).
        string IdOf(Object obj);

        /// Null when the object no longer exists.
        Object Resolve(string id);
    }

    public sealed class GlobalObjectTraceIds : ITraceObjectIds
    {
        public static readonly GlobalObjectTraceIds Instance = new();

        public string IdOf(Object obj)
        {
            if (!obj) return null;

            var id = GlobalObjectId.GetGlobalObjectIdSlow(obj);
            // identifierType 0 means the object is not an asset (yet).
            return id.identifierType == 0 ? null : id.ToString();
        }

        public Object Resolve(string id) =>
            !string.IsNullOrEmpty(id) && GlobalObjectId.TryParse(id, out var globalId)
                ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId)
                : null;
    }

    /// Saves and loads a recorded run (nodes and graphs as ids, JSON), like Profiler captures. A loaded trace is read-only.
    public static class GraphTraceFile
    {
        public const int Version = 1;
        public const string Extension = "graphtrace";

        /// Library/GraphDebug/LastRun.graphtrace, written when Play Mode exits. Not in VCS.
        public static string LastRunPath =>
            Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty, "Library", "GraphDebug", "LastRun." + Extension);

        /// Folder the Save As / Load dialogs open in: Temp/graph-debugger (created on demand). Unity empties Temp when the Editor closes,
        /// so save somewhere else to keep a trace.
        public static string PickerDirectory
        {
            get
            {
                var path = Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty, "Temp", "graph-debugger");
                Directory.CreateDirectory(path);
                return path;
            }
        }

        #region File data

        [Serializable]
        public sealed class TraceData
        {
            public int version;
            public string runName;
            public string savedAt;
            public double endTime;
            public List<string> objects = new();
            public List<RecordData> records = new();
            public List<NodeData> nodes = new();
            public List<EdgeData> edges = new();
            public List<HistoryData> history = new();
        }

        [Serializable]
        public sealed class RecordData
        {
            public int id;
            public string label;
            public int graph = -1;
            public int parent = -1;
            public int origin = -1;
            public int activeNode = -1;
            public int endReason = -1;
            public List<int> visited = new();
        }

        [Serializable]
        public sealed class NodeData
        {
            public int node;
            public int visits;
            public double lastEntered;
            public string lastPort;
        }

        [Serializable]
        public sealed class EdgeData
        {
            public int outNode;
            public string outPort;
            public int inNode;
            public string inPort;
            public int count;
            public int exact;
            public double last;
            public int lastRecord = -1;
        }

        [Serializable]
        public sealed class HistoryData
        {
            public double time;
            public int record;
            public int graph = -1;
            public int node = -1;
            public int outNode = -1;
            public string outPort;
            public int inNode = -1;
            public string inPort;
            public bool ambiguous;
        }

        #endregion

        public static void Save(GraphRuntimeTracer tracer, string path) => Save(tracer, path, GlobalObjectTraceIds.Instance);

        public static void Save(GraphRuntimeTracer tracer, string path, ITraceObjectIds ids)
        {
            var json = JsonUtility.ToJson(Build(tracer, ids), true);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, json);
        }

        /// Writes the last run file when something was recorded. Never throws, a failing auto-save must not disturb leaving Play Mode.
        public static void SaveLastRun(GraphRuntimeTracer tracer)
        {
            if (tracer == null || !tracer.HasTrace || tracer.IsLoadedTrace) return;

            try
            {
                Save(tracer, LastRunPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Graph debugger: could not save the last run trace: {exception.Message}");
            }
        }

        public static bool Load(GraphRuntimeTracer tracer, string path) => Load(tracer, path, GlobalObjectTraceIds.Instance);

        /// Replaces the tracer content with the file. Nodes that no longer exist are skipped, with one warning.
        public static bool Load(GraphRuntimeTracer tracer, string path, ITraceObjectIds ids)
        {
            if (tracer == null || !File.Exists(path)) return false;

            TraceData data;
            try
            {
                data = JsonUtility.FromJson<TraceData>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Graph debugger: could not read trace '{path}': {exception.Message}");
                return false;
            }

            if (data == null || data.version != Version)
            {
                Debug.LogWarning($"Graph debugger: trace '{path}' has an unsupported version ({data?.version}).");
                return false;
            }

            Apply(tracer, data, ids, Path.GetFileName(path));
            return true;
        }

        public static TraceData Build(GraphRuntimeTracer tracer, ITraceObjectIds ids)
        {
            var data = new TraceData
            {
                version = Version,
                runName = tracer.RunName,
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                endTime = tracer.ReferenceTime,
            };

            var table = new Dictionary<Object, int>();

            int Index(Object obj)
            {
                if (!obj) return -1;
                if (table.TryGetValue(obj, out var index)) return index;

                var id = ids.IdOf(obj);
                if (id == null) return -1;

                index = data.objects.Count;
                data.objects.Add(id);
                table[obj] = index;
                return index;
            }

            foreach (var record in tracer.Instances)
            {
                var recordData = new RecordData
                {
                    id = record.Id,
                    label = record.Label,
                    graph = Index(record.Graph),
                    parent = record.Parent?.Id ?? -1,
                    origin = Index(record.Origin),
                    activeNode = Index(record.ActiveNode),
                    endReason = record.EndReason.HasValue ? (int) record.EndReason.Value : (int) GraphEndReason.Cancelled,
                };
                foreach (var node in record.VisitedNodes)
                {
                    var index = Index(node);
                    if (index >= 0) recordData.visited.Add(index);
                }

                data.records.Add(recordData);
            }

            foreach (var pair in tracer.NodeTraces)
            {
                var index = Index(pair.Key);
                if (index < 0) continue;

                data.nodes.Add(new NodeData
                {
                    node = index,
                    visits = pair.Value.VisitCount,
                    lastEntered = pair.Value.LastEnteredTime,
                    lastPort = pair.Value.LastPort?.fieldName,
                });
            }

            foreach (var pair in tracer.EdgeTraces)
            {
                var outNode = Index(pair.Key.Output?.node);
                var inNode = Index(pair.Key.Input?.node);
                if (outNode < 0 || inNode < 0) continue;

                data.edges.Add(new EdgeData
                {
                    outNode = outNode,
                    outPort = pair.Key.Output.fieldName,
                    inNode = inNode,
                    inPort = pair.Key.Input.fieldName,
                    count = pair.Value.Count,
                    exact = pair.Value.ExactCount,
                    last = pair.Value.LastTime,
                    lastRecord = pair.Value.LastRecord?.Id ?? -1,
                });
            }

            foreach (var entry in tracer.History)
            {
                var node = Index(entry.Node);
                if (node < 0) continue;

                data.history.Add(new HistoryData
                {
                    time = entry.Time,
                    record = entry.RecordId,
                    graph = Index(entry.Graph),
                    node = node,
                    outNode = Index(entry.EdgeOutput?.node),
                    outPort = entry.EdgeOutput?.fieldName,
                    inNode = Index(entry.EdgeInput?.node),
                    inPort = entry.EdgeInput?.fieldName,
                    ambiguous = entry.EdgeAmbiguous,
                });
            }

            return data;
        }

        public static void Apply(GraphRuntimeTracer tracer, TraceData data, ITraceObjectIds ids, string sourceName)
        {
            var resolved = new Object[data.objects.Count];
            var resolvedDone = new bool[data.objects.Count];
            var skipped = 0;

            Object Get(int index)
            {
                if (index < 0 || index >= resolved.Length) return null;
                if (resolvedDone[index]) return resolved[index];

                resolvedDone[index] = true;
                resolved[index] = ids.Resolve(data.objects[index]);
                if (!resolved[index]) skipped++;
                return resolved[index];
            }

            T GetAs<T>(int index) where T : Object => Get(index) as T;

            NodePort PortOf(int nodeIndex, string portName) =>
                string.IsNullOrEmpty(portName) ? null : GetAs<Node>(nodeIndex)?.GetPort(portName);

            tracer.BeginImport();

            var records = new Dictionary<int, ParserRecord>();
            data.records.Sort((a, b) => a.id.CompareTo(b.id));
            foreach (var recordData in data.records)
            {
                var graph = GetAs<GraphBase>(recordData.graph);
                if (!graph) continue;

                records.TryGetValue(recordData.parent, out var parent);
                var reason = Enum.IsDefined(typeof(GraphEndReason), recordData.endReason)
                    ? (GraphEndReason) recordData.endReason
                    : GraphEndReason.Cancelled;
                var record = tracer.ImportRecord(recordData.id, recordData.label, graph, parent, GetAs<BaseNode>(recordData.origin),
                    GetAs<BaseNode>(recordData.activeNode), reason);
                foreach (var visited in recordData.visited)
                {
                    if (GetAs<BaseNode>(visited) is { } node) record.MarkVisited(node);
                }

                records[record.Id] = record;
            }

            foreach (var nodeData in data.nodes)
            {
                var node = GetAs<Node>(nodeData.node);
                if (node) tracer.ImportNode(node, nodeData.visits, nodeData.lastEntered, PortOf(nodeData.node, nodeData.lastPort));
            }

            foreach (var edgeData in data.edges)
            {
                var output = PortOf(edgeData.outNode, edgeData.outPort);
                var input = PortOf(edgeData.inNode, edgeData.inPort);
                if (output == null || input == null) continue;

                records.TryGetValue(edgeData.lastRecord, out var lastRecord);
                tracer.ImportEdge(output, input, edgeData.count, edgeData.exact, edgeData.last, lastRecord);
            }

            foreach (var historyData in data.history)
            {
                var node = GetAs<BaseNode>(historyData.node);
                if (!node) continue;

                records.TryGetValue(historyData.record, out var record);
                var entry = new HistoryEntry(historyData.time, record, GetAs<GraphBase>(historyData.graph) ?? node.graph as GraphBase, node,
                    PortOf(historyData.outNode, historyData.outPort), PortOf(historyData.inNode, historyData.inPort), historyData.ambiguous);
                tracer.ImportHistory(entry);
            }

            // Resolve every object that was never touched, so the warning counts all missing ones
            for (var i = 0; i < resolved.Length; i++) Get(i);

            tracer.EndImport(sourceName, data.runName, data.endTime);
            if (skipped > 0)
                Debug.LogWarning($"Graph debugger: {skipped} node(s) or graph(s) of trace '{sourceName}' no longer exist and were skipped.");
        }
    }
}
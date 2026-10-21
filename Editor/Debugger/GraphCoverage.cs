using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XNode;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Nodes.Debug;
using Object = UnityEngine.Object;

namespace Xprees.Graph.Core.Editor.Debugger
{
    public readonly struct CoverageResult
    {
        public readonly int Visited;
        public readonly int Total;

        public CoverageResult(int visited, int total)
        {
            Visited = visited;
            Total = total;
        }

        public float Percent => Total <= 0 ? 0f : 100f * Visited / Total;
    }

    /// Which reachable nodes of a graph a run visited. Pure over the graph and a visited predicate, so it is testable without a tracer.
    public static class GraphCoverage
    {
        /// Notes and groups never run, they do not count.
        public static bool IsCountable(Node node) => node != null && node is not NoteNode && node is not NodeGroup;

        /// Nodes reachable from the Start and AsyncStart nodes along output connections. A graph with neither starts from the nodes without a connected input.
        public static HashSet<Node> ReachableNodes(NodeGraph graph)
        {
            var result = new HashSet<Node>();
            if (graph == null) return result;

            var queue = new Queue<Node>();
            foreach (var node in graph.nodes)
            {
                if (node is StartNode or AsyncStartBaseNode) Enqueue(node, result, queue);
            }

            if (queue.Count == 0)
            {
                foreach (var node in graph.nodes)
                {
                    if (IsCountable(node) && !HasConnectedInput(node)) Enqueue(node, result, queue);
                }
            }

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                foreach (var port in node.Outputs)
                {
                    for (var i = 0; i < port.ConnectionCount; i++)
                    {
                        var target = port.GetConnection(i)?.node;
                        if (target != null && target.graph == graph) Enqueue(target, result, queue);
                    }
                }
            }

            result.RemoveWhere(node => !IsCountable(node));
            return result;
        }

        public static CoverageResult Compute(NodeGraph graph, Func<Node, bool> isVisited)
        {
            var reachable = ReachableNodes(graph);
            var visited = 0;
            foreach (var node in reachable)
            {
                if (isVisited(node)) visited++;
            }

            return new CoverageResult(visited, reachable.Count);
        }

        private static void Enqueue(Node node, HashSet<Node> seen, Queue<Node> queue)
        {
            if (seen.Add(node)) queue.Enqueue(node);
        }

        private static bool HasConnectedInput(Node node)
        {
            foreach (var port in node.Inputs)
            {
                if (port.IsConnected) return true;
            }

            return false;
        }
    }

    /// Visited node ids per graph id, the persisted data of the cumulative coverage. Serializable so it round-trips through JSON.
    [Serializable]
    public sealed class CoverageData
    {
        [Serializable]
        public sealed class GraphEntry
        {
            public string graph;
            public List<string> nodes = new();
        }

        public List<GraphEntry> graphs = new();

        public bool IsEmpty => graphs.Count == 0;

        public IReadOnlyList<string> NodesOf(string graphId)
        {
            var entry = Find(graphId);
            return entry != null ? entry.nodes : Array.Empty<string>();
        }

        public bool Contains(string graphId, string nodeId) => Find(graphId)?.nodes.Contains(nodeId) == true;

        /// Union: returns true when something new was added.
        public bool Add(string graphId, IEnumerable<string> nodeIds)
        {
            if (string.IsNullOrEmpty(graphId)) return false;

            var entry = Find(graphId);
            var changed = false;
            foreach (var id in nodeIds)
            {
                if (string.IsNullOrEmpty(id)) continue;

                if (entry == null)
                {
                    entry = new GraphEntry { graph = graphId };
                    graphs.Add(entry);
                    changed = true;
                }

                if (entry.nodes.Contains(id)) continue;

                entry.nodes.Add(id);
                changed = true;
            }

            return changed;
        }

        public void Clear() => graphs.Clear();

        private GraphEntry Find(string graphId)
        {
            foreach (var entry in graphs)
            {
                if (entry.graph == graphId) return entry;
            }

            return null;
        }
    }

    /// Accumulates the nodes a run visited into the persisted coverage and answers coverage queries, with or without the persisted union.
    public static class GraphCoverageService
    {
        private static readonly CachingIds Ids = new();
        private static readonly Dictionary<NodeGraph, CacheEntry> Cache = new();

        private static int _cacheVersion = -1;
        private static bool _cacheCumulative;
        private static int _storeVersion;

        /// Copies what the tracer recorded into the persisted store. A loaded trace is not a play-test of the current content, so it is skipped.
        public static bool Accumulate(GraphRuntimeTracer tracer, ITraceObjectIds ids, CoverageData data)
        {
            if (tracer == null || tracer.IsLoadedTrace) return false;

            var perGraph = new Dictionary<NodeGraph, List<string>>();
            foreach (var pair in tracer.NodeTraces)
            {
                var node = pair.Key;
                if (node == null || node.graph == null || pair.Value.VisitCount <= 0 || !GraphCoverage.IsCountable(node)) continue;

                var id = ids.IdOf(node);
                if (id == null) continue;

                if (!perGraph.TryGetValue(node.graph, out var list)) perGraph[node.graph] = list = new List<string>();
                list.Add(id);
            }

            var changed = false;
            foreach (var pair in perGraph)
            {
                var graphId = ids.IdOf(pair.Key);
                if (graphId != null) changed |= data.Add(graphId, pair.Value);
            }

            return changed;
        }

        /// Editor entry point: Accumulate into UserSettings/GraphCoverage.asset.
        public static void Accumulate(GraphRuntimeTracer tracer)
        {
            var store = GraphCoverageStore.instance;
            if (!Accumulate(tracer, GlobalObjectTraceIds.Instance, store.Data)) return;

            store.Persist();
            _storeVersion++;
        }

        public static void Reset()
        {
            GraphCoverageStore.instance.ResetAll();
            _storeVersion++;
            Cache.Clear();
            _cacheVersion = -1;
        }

        public static bool HasPersisted => !GraphCoverageStore.instance.Data.IsEmpty;

        /// Coverage of a graph, cached per tracer version. Null when nothing was recorded.
        public static CoverageResult? Get(NodeGraph graph, GraphRuntimeTracer tracer, bool cumulative)
        {
            if (graph == null || tracer == null || !tracer.HasTrace && !cumulative) return null;

            var key = tracer.Version * 31 + _storeVersion;
            if (key != _cacheVersion || cumulative != _cacheCumulative)
            {
                Cache.Clear();
                _cacheVersion = key;
                _cacheCumulative = cumulative;
            }

            if (Cache.TryGetValue(graph, out var entry)) return entry.Result;

            var result = Compute(graph, tracer, cumulative ? GraphCoverageStore.instance.Data : null, Ids);
            Cache[graph] = new CacheEntry { Result = result };
            return result;
        }

        /// Pure given the ids: visited now, or (with persisted data) visited in an earlier run.
        public static CoverageResult Compute(NodeGraph graph, GraphRuntimeTracer tracer, CoverageData persisted, ITraceObjectIds ids)
        {
            var graphId = persisted != null ? ids.IdOf(graph) : null;
            return GraphCoverage.Compute(graph, node =>
            {
                if (tracer.GetNode(node).VisitCount > 0) return true;
                if (persisted == null || graphId == null) return false;

                return persisted.Contains(graphId, ids.IdOf(node));
            });
        }

        /// GlobalObjectId lookups are slow, the ids of nodes do not change while the editor runs.
        private sealed class CachingIds : ITraceObjectIds
        {
            private readonly Dictionary<Object, string> _ids = new();

            public string IdOf(Object obj)
            {
                if (!obj) return null;
                if (_ids.TryGetValue(obj, out var id)) return id;

                id = GlobalObjectTraceIds.Instance.IdOf(obj);
                if (id != null) _ids[obj] = id;
                return id;
            }

            public Object Resolve(string id) => GlobalObjectTraceIds.Instance.Resolve(id);
        }

        private struct CacheEntry
        {
            public CoverageResult Result;
        }
    }

    /// Persisted coverage across runs: the union of visited node ids per graph (GlobalObjectId strings). Per user, not in VCS.
    [FilePath("UserSettings/GraphCoverage.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class GraphCoverageStore : ScriptableSingleton<GraphCoverageStore>
    {
        [SerializeField] private CoverageData data = new();

        public CoverageData Data => data ??= new CoverageData();

        public void Persist() => Save(true);

        public void ResetAll()
        {
            Data.Clear();
            Save(true);
        }
    }
}
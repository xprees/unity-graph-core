using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XNode;

namespace Xprees.Graph.Core.Editor.Debugger
{
    /// Editor-only breakpoints on graph nodes. Stored per user in UserSettings (never in the graph asset), keyed by the node GlobalObjectId.
    /// Nodes without a persistent id (not saved yet) keep a session-only breakpoint.
    public static class GraphBreakpoints
    {
        private static Dictionary<Node, BreakpointState> _resolved;
        private static readonly Dictionary<Node, BreakpointState> Transient = new();
        private static readonly Dictionary<Node, string> IdCache = new();

        public static event Action Changed;

        public static bool Has(Node node) => node != null && TryGet(node, out _);

        public static bool IsEnabled(Node node) => node != null && TryGet(node, out var state) && state.enabled;

        /// False for nodes that are not saved yet (their breakpoint is session-only).
        public static bool HasPersistentId(Node node) => node != null && IdOf(node) != null;

        /// Hot path of the debugger gate, a dictionary lookup after the first call.
        public static bool ShouldBreak(Node node) => IsEnabled(node);

        /// Adds an enabled breakpoint, or removes it when there is one already.
        public static void Toggle(Node node)
        {
            if (node == null) return;

            if (Has(node)) Remove(node);
            else SetEnabled(node, true);
        }

        /// Creates the breakpoint if it is missing.
        public static void SetEnabled(Node node, bool enabled)
        {
            if (node == null) return;

            EnsureResolved();
            if (TryGet(node, out var state))
            {
                if (state.enabled == enabled) return;
                state.enabled = enabled;
                Persist(node, state);
            }
            else
            {
                Persist(node, new BreakpointState { enabled = enabled });
            }

            Changed?.Invoke();
        }

        public static void Remove(Node node)
        {
            if (node == null) return;

            EnsureResolved();
            var removed = Transient.Remove(node);
            if (_resolved.Remove(node))
            {
                removed = true;
                var id = IdOf(node);
                if (id != null) GraphBreakpointStore.instance.Remove(id);
            }

            if (removed) Changed?.Invoke();
        }

        /// All breakpoints (enabled or not) in the graph.
        public static IEnumerable<Node> In(NodeGraph graph)
        {
            if (graph == null) yield break;

            EnsureResolved();
            foreach (var node in graph.nodes)
            {
                if (node != null && TryGet(node, out _)) yield return node;
            }
        }

        /// Drops the resolved cache, the next query reads the store again.
        internal static void InvalidateCache() => _resolved = null;

        /// Removes the session-only breakpoints and the cache.
        internal static void ResetSession()
        {
            Transient.Clear();
            IdCache.Clear();
            _resolved = null;
        }

        private static bool TryGet(Node node, out BreakpointState state)
        {
            if (Transient.TryGetValue(node, out state)) return true;

            EnsureResolved();
            return _resolved.TryGetValue(node, out state);
        }

        private static void Persist(Node node, BreakpointState state)
        {
            var id = IdOf(node);
            if (id == null)
            {
                Transient[node] = state;
                return;
            }

            _resolved[node] = state;
            GraphBreakpointStore.instance.Set(id, state.enabled);
        }

        private static void EnsureResolved()
        {
            if (_resolved != null) return;

            _resolved = new Dictionary<Node, BreakpointState>();
            foreach (var entry in GraphBreakpointStore.instance.Entries)
            {
                if (!GlobalObjectId.TryParse(entry.id, out var globalId)) continue;
                if (GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) is Node node)
                {
                    _resolved[node] = new BreakpointState { enabled = entry.enabled };
                }
            }
        }

        private static string IdOf(Node node)
        {
            if (IdCache.TryGetValue(node, out var cached)) return cached;

            var id = GlobalObjectId.GetGlobalObjectIdSlow(node);
            // identifierType 0 means the object is not an asset (yet).
            var text = id.identifierType == 0 ? null : id.ToString();
            if (text != null) IdCache[node] = text;
            return text;
        }

        private sealed class BreakpointState
        {
            public bool enabled;
        }
    }

    [FilePath("UserSettings/GraphDebugBreakpoints.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class GraphBreakpointStore : ScriptableSingleton<GraphBreakpointStore>
    {
        [SerializeField] private List<Entry> entries = new();

        public IReadOnlyList<Entry> Entries => entries;

        public void Set(string id, bool enabled)
        {
            var index = entries.FindIndex(e => e.id == id);
            if (index >= 0) entries[index] = new Entry { id = id, enabled = enabled };
            else entries.Add(new Entry { id = id, enabled = enabled });
            Save(true);
        }

        public void Remove(string id)
        {
            if (entries.RemoveAll(e => e.id == id) > 0) Save(true);
        }

        [Serializable]
        public struct Entry
        {
            public string id;
            public bool enabled;
        }
    }
}
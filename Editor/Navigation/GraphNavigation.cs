using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XNode;
using XNodeEditor;
using Xprees.Graph.Core.Editor.Graph;
using Object = UnityEngine.Object;

namespace Xprees.Graph.Core.Editor.Navigation
{
    /// Parent chain of graphs opened in the node editor (Scenario > Setup SubGraph > Dialog ...).
    /// Entering a graph referenced by a node pushes it, navigating to a crumb cuts the chain,
    /// opening an unrelated graph (e.g. from the Project window) starts a new chain.
    /// Persisted in SessionState, so it survives domain reloads.
    public static class GraphNavigation
    {
        private const string sessionKey = "Xprees.Graph.Navigation.Chain";

        public readonly struct Crumb
        {
            public readonly NodeGraph graph;

            /// Node in the parent graph this graph was entered from (null for the root).
            public readonly Node enteredFrom;

            public Crumb(NodeGraph graph, Node enteredFrom)
            {
                this.graph = graph;
                this.enteredFrom = enteredFrom;
            }
        }

        [Serializable]
        private class SerializedChain
        {
            public List<string> graphs = new();
            public List<string> enteredFrom = new();
        }

        private static List<Crumb> chain;

        public static event Action OnChainChanged;

        public static IReadOnlyList<Crumb> Chain => chain ??= Load();

        /// Opens the child graph referenced by the node and pushes it to the chain.
        public static void EnterGraph(Node fromNode, NodeGraph child)
        {
            if (!child) return;

            var current = Chain;
            var parent = fromNode ? fromNode.graph : null;
            var list = current.ToList();

            // Keep the chain only if we are entering from its tail, otherwise the parent becomes a new root
            if (list.Count <= 0 || list[^1].graph != parent)
            {
                list.Clear();
                if (parent) list.Add(new Crumb(parent, null));
            }

            list.Add(new Crumb(child, fromNode));
            SetChain(list);

            NodeEditorWindow.Open(child);
        }

        /// Opens the graph of the crumb and cuts off the crumbs after it. Focuses the node the child was entered from.
        public static void NavigateTo(int index)
        {
            var list = Chain.ToList();
            if (index < 0 || index >= list.Count) return;

            var target = list[index];
            var enteredFrom = index + 1 < list.Count ? list[index + 1].enteredFrom : null;
            SetChain(list.Take(index + 1).ToList());

            if (enteredFrom && enteredFrom.graph == target.graph) GraphBaseEditor.FocusNode(enteredFrom);
            else NodeEditorWindow.Open(target.graph);
        }

        /// Goes one level up in the chain (to the parent graph). Returns false when the current graph has no parent.
        public static bool GoToParent()
        {
            var count = Chain.Count;
            if (count < 2) return false;

            NavigateTo(count - 2);
            return true;
        }

        /// Called whenever the node editor shows a graph. Keeps the chain consistent with graphs opened by other means.
        public static void OnGraphShown(NodeGraph graph)
        {
            if (!graph) return;

            var list = Chain.ToList();
            var index = list.FindIndex(c => c.graph == graph);
            if (index == list.Count - 1 && index >= 0) return; // Already the current crumb

            if (index >= 0)
            {
                SetChain(list.Take(index + 1).ToList()); // Navigated back to an ancestor
                return;
            }

            // Opened from elsewhere (e.g. double-click on the asset in the Project window) while the parent graph was shown:
            // when the current graph references it, treat it as going down into a child
            var referencingNode = list.Count > 0 ? GraphReferences.FindReferencingNode(list[^1].graph, graph) : null;
            if (referencingNode)
            {
                list.Add(new Crumb(graph, referencingNode));
                SetChain(list);
                return;
            }

            SetChain(new List<Crumb> { new(graph, null) }); // Unrelated graph -> new root
        }

        #region Persistence

        private static void SetChain(List<Crumb> newChain)
        {
            chain = newChain;
            Save(newChain);
            OnChainChanged?.Invoke();
        }

        private static void Save(List<Crumb> list)
        {
            var data = new SerializedChain();
            foreach (var crumb in list)
            {
                data.graphs.Add(ToId(crumb.graph));
                data.enteredFrom.Add(ToId(crumb.enteredFrom));
            }

            SessionState.SetString(sessionKey, JsonUtility.ToJson(data));
        }

        private static List<Crumb> Load()
        {
            var result = new List<Crumb>();
            var json = SessionState.GetString(sessionKey, null);
            if (string.IsNullOrEmpty(json)) return result;

            var data = JsonUtility.FromJson<SerializedChain>(json);
            for (var i = 0; i < data.graphs.Count; i++)
            {
                var graph = FromId<NodeGraph>(data.graphs[i]);
                if (!graph) break; // Deleted graph breaks the chain from here on

                var enteredFrom = i < data.enteredFrom.Count ? FromId<Node>(data.enteredFrom[i]) : null;
                result.Add(new Crumb(graph, enteredFrom));
            }

            return result;
        }

        private static string ToId(Object obj) => obj ? GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString() : string.Empty;

        private static T FromId<T>(string id) where T : Object
        {
            if (string.IsNullOrEmpty(id) || !GlobalObjectId.TryParse(id, out var globalId)) return null;
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) as T;
        }

        #endregion

    }
}
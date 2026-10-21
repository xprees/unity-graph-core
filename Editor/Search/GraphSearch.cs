using System;
using System.Collections.Generic;
using UnityEditor;
using XNode;
using Xprees.Graph.Core.Editor.Navigation;
using Object = UnityEngine.Object;

namespace Xprees.Graph.Core.Editor.Search
{
    public enum GraphSearchMatchKind
    {
        Name,
        Type,
        Text,
        Reference,
    }

    public readonly struct GraphSearchResult
    {
        public readonly Node Node;
        public readonly GraphSearchMatchKind Kind;

        /// The text that matched (node name, type name, a string field value or a referenced asset name).
        public readonly string Matched;

        public NodeGraph Graph => Node ? Node.graph : null;

        public GraphSearchResult(Node node, GraphSearchMatchKind kind, string matched)
        {
            Node = node;
            Kind = kind;
            Matched = matched;
        }
    }

    /// Pure search over a graph and the graphs it references (sub-graphs, dialogs). Matches node name, type name, note text,
    /// string fields and names of referenced assets, case-insensitive. One result per node, the first match wins.
    public static class GraphSearch
    {
        private const int maxPreviewLength = 80;

        public static bool IsMatch(string text, string query) =>
            !string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(query) && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        /// Root graph first, then referenced graphs breadth first. Every graph is visited once, so reference cycles end.
        public static List<GraphSearchResult> Find(NodeGraph root, string query)
        {
            var results = new List<GraphSearchResult>();
            if (!root || string.IsNullOrWhiteSpace(query)) return results;

            query = query.Trim();
            var visited = new HashSet<NodeGraph> { root };
            var queue = new Queue<NodeGraph>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var graph = queue.Dequeue();
                foreach (var node in graph.nodes)
                {
                    if (!node) continue;

                    if (TryMatch(node, query, out var result)) results.Add(result);

                    foreach (var child in GraphReferences.GetReferencedGraphs(node))
                    {
                        if (child && visited.Add(child)) queue.Enqueue(child);
                    }
                }
            }

            return results;
        }

        public static bool TryMatch(Node node, string query, out GraphSearchResult result)
        {
            if (IsMatch(node.name, query))
            {
                result = new GraphSearchResult(node, GraphSearchMatchKind.Name, node.name);
                return true;
            }

            var typeName = node.GetType().Name;
            if (IsMatch(typeName, query))
            {
                result = new GraphSearchResult(node, GraphSearchMatchKind.Type, typeName);
                return true;
            }

            return TryMatchFields(node, query, out result);
        }

        private static bool TryMatchFields(Node node, string query, out GraphSearchResult result)
        {
            result = default;
            using var serialized = new SerializedObject(node);
            var property = serialized.GetIterator();
            var enterChildren = true;
            while (property.Next(enterChildren))
            {
                // Ports hold generated field names, the graph reference would match every node
                enterChildren = property.propertyPath != "ports";
                if (property.propertyPath == "graph" || property.propertyPath == "m_Script") continue;

                if (property.propertyType == SerializedPropertyType.String)
                {
                    if (!IsMatch(property.stringValue, query)) continue;

                    result = new GraphSearchResult(node, GraphSearchMatchKind.Text, Preview(property.stringValue));
                    return true;
                }

                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;

                var reference = property.objectReferenceValue;
                if (!reference || !IsMatch(reference.name, query)) continue;

                result = new GraphSearchResult(node, GraphSearchMatchKind.Reference, ReferenceLabel(reference));
                return true;
            }

            return false;
        }

        private static string ReferenceLabel(Object reference) => reference.name;

        private static string Preview(string text)
        {
            var line = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
            return line.Length <= maxPreviewLength ? line : line.Substring(0, maxPreviewLength) + "...";
        }
    }
}
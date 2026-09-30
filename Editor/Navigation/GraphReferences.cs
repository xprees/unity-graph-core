using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using XNode;

namespace Xprees.Graph.Core.Editor.Navigation
{
    /// Finds graphs referenced by nodes (e.g. SubGraphNode.subGraph, dialog nodes) - the "parent > child" relation of graphs.
    public static class GraphReferences
    {
        private const BindingFlags fieldFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private readonly static Dictionary<Type, FieldInfo[]> graphFieldsCache = new();

        /// Graphs referenced by the node (other than the graph the node lives in).
        public static IEnumerable<NodeGraph> GetReferencedGraphs(Node node)
        {
            if (!node) yield break;

            foreach (var field in GetGraphFields(node.GetType()))
            {
                if (field.GetValue(node) is NodeGraph graph && graph && graph != node.graph) yield return graph;
            }
        }

        /// First node of the parent graph referencing the child graph. Null if the parent doesn't reference it.
        public static Node FindReferencingNode(NodeGraph parent, NodeGraph child)
        {
            if (!parent || !child) return null;

            return parent.nodes.FirstOrDefault(node => node && GetReferencedGraphs(node).Contains(child));
        }

        private static FieldInfo[] GetGraphFields(Type nodeType)
        {
            if (graphFieldsCache.TryGetValue(nodeType, out var fields)) return fields;

            var list = new List<FieldInfo>();
            for (var type = nodeType; type != null && type != typeof(Node); type = type.BaseType)
            {
                list.AddRange(type.GetFields(fieldFlags).Where(f =>
                    typeof(NodeGraph).IsAssignableFrom(f.FieldType) && (f.IsPublic || f.IsDefined(typeof(SerializeField)))));
            }

            return graphFieldsCache[nodeType] = list.ToArray();
        }
    }
}
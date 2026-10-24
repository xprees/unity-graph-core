using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XNode;
using Object = UnityEngine.Object;

namespace Xprees.Graph.Core.Editor.Flow
{
    /// Selects the nodes connected to a node: its whole connected component, or only what follows it (this -> ... -> end).
    /// Selecting is not a graph change, so there is nothing to undo.
    public static class FlowSelection
    {
        /// Every node reachable from <paramref name="start"/> through <paramref name="neighbours"/>, start first, each node once.
        /// Cycles are fine: a node already seen is not visited again.
        public static List<T> Reach<T>(T start, Func<T, IEnumerable<T>> neighbours) where T : class
        {
            var result = new List<T>();
            if (start == null) return result;

            var seen = new HashSet<T> { start };
            var queue = new Queue<T>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                result.Add(node);
                foreach (var next in neighbours(node))
                {
                    if (next != null && seen.Add(next)) queue.Enqueue(next);
                }
            }

            return result;
        }

        /// The node and everything connected to it on either side (inputs and outputs, any depth).
        public static List<Node> Connected(Node start) => Reach(start, node => Linked(node, outputs: true, inputs: true));

        /// The node and every node it leads to: this -> ... -> end (outputs only, any depth).
        public static List<Node> Following(Node start) => Reach(start, node => Linked(node, outputs: true, inputs: false));

        private static IEnumerable<Node> Linked(Node node, bool outputs, bool inputs)
        {
            foreach (var port in node.Ports)
            {
                if (port.IsOutput && !outputs || port.IsInput && !inputs) continue;

                foreach (var connection in port.GetConnections())
                {
                    if (connection != null && connection.node && connection.node.graph == node.graph) yield return connection.node;
                }
            }
        }

        /// Replaces the selection with the nodes, the clicked one stays the active object. Returns how many nodes were selected.
        public static int Select(Node active, IReadOnlyList<Node> nodes)
        {
            if (nodes == null || nodes.Count == 0) return 0;

            Selection.objects = nodes.Cast<Object>().ToArray();
            Selection.activeObject = active ? active : nodes[0];
            return nodes.Count;
        }

        public static void SelectConnected(Node node, EditorWindow window) =>
            Notify(window, "Connected nodes selected", Select(node, Connected(node)));

        public static void SelectFollowing(Node node, EditorWindow window) =>
            Notify(window, "Following nodes selected", Select(node, Following(node)));

        private static void Notify(EditorWindow window, string text, int count)
        {
            if (!window || count <= 0) return;

            window.ShowNotification(new GUIContent($"{text}: {count}"), 1.0);
            window.Repaint();
        }
    }
}
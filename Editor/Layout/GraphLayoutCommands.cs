using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XNode;
using XNodeEditor;
using Xprees.Graph.Core.Base.Nodes;
using Object = UnityEngine.Object;

namespace Xprees.Graph.Core.Editor.Layout
{
    /// Entry points of the layout commands (toolbar, shortcut, context menu, MCP). Builds a <see cref="GraphLayoutModel"/> from the graph,
    /// runs <see cref="GraphLayoutEngine"/> and writes the result back as one Undo step.
    public static class GraphLayoutCommands
    {
        /// Runs the command on the window's selection. An empty selection only shows a notification.
        public static void Run(NodeEditorWindow window, LayoutCommand command)
        {
            if (!window || !window.graph) return;

            var selection = Selection.objects.OfType<Node>().Where(n => n && n.graph == window.graph).ToList();
            if (selection.Count == 0)
            {
                window.ShowNotification(new GUIContent("Select nodes or a group first"), 1.5);
                return;
            }

            var changed = Apply(window.graph, selection, command, NodeSizeEstimator.ForWindow(window));
            ForgetPortPositions(window, changed);
            if (changed.Count == 0) window.ShowNotification(new GUIContent("Already arranged"), 1.0);
            window.Repaint();
        }

        /// Applies the command to the selected nodes / groups of the graph as ONE Undo step. Returns the nodes that moved or were resized.
        /// Does not save the asset.
        public static IReadOnlyList<Node> Apply(NodeGraph graph, IEnumerable<Node> selection, LayoutCommand command, Func<Node, Vector2> sizeOf)
        {
            if (!graph) return Array.Empty<Node>();

            sizeOf ??= NodeSizeEstimator.Estimate;
            var model = BuildModel(graph, sizeOf, out var nodes);
            var ids = new Dictionary<Node, int>();
            for (var i = 0; i < nodes.Count; i++) ids[nodes[i]] = i;

            var selected = (selection ?? Enumerable.Empty<Node>()).Where(n => n && ids.ContainsKey(n)).Select(n => ids[n]).ToList();
            var result = GraphLayoutEngine.Run(model, selected, command);
            if (result.Count == 0) return Array.Empty<Node>();

            var changed = result.Keys.OrderBy(i => i).Select(i => nodes[i]).ToList();
            var undoName = LayoutCommandNames.UndoName(command);
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            Undo.RecordObjects(changed.Cast<Object>().Append(graph).ToArray(), undoName);

            foreach (var pair in result)
            {
                var node = nodes[pair.Key];
                var rect = pair.Value;
                node.position = rect.position;
                if (node is NodeGroup group)
                {
                    group.width = Mathf.CeilToInt(rect.width);
                    group.height = Mathf.CeilToInt(rect.height - model.Nodes[pair.Key].GroupChrome);
                }

                EditorUtility.SetDirty(node);
            }

            EditorUtility.SetDirty(graph);
            Undo.CollapseUndoOperations(undoGroup);
            return changed;
        }

        /// The layout model of a whole graph. Ids are indices into <paramref name="nodes"/> (the graph's non-null nodes in order).
        public static GraphLayoutModel BuildModel(NodeGraph graph, Func<Node, Vector2> sizeOf, out List<Node> nodes)
        {
            nodes = graph.nodes.Where(n => n).ToList();
            var model = new GraphLayoutModel();
            var ids = new Dictionary<Node, int>();
            foreach (var node in nodes)
            {
                var size = sizeOf(node);
                var layoutNode = model.Add(new Rect(node.position, size), KindOf(node), node);
                if (node is StartNode) layoutNode.EntryPriority = 0;
                if (node is NodeGroup group && size.y > group.height) layoutNode.GroupChrome = size.y - group.height;
                ids[node] = layoutNode.Id;
            }

            foreach (var node in nodes)
            {
                var port = 0;
                foreach (var output in node.Outputs)
                {
                    foreach (var connection in output.GetConnections())
                    {
                        if (connection?.node && ids.TryGetValue(connection.node, out var to)) model.Connect(ids[node], to, port);
                    }

                    port++;
                }
            }

            return model;
        }

        private static LayoutNodeKind KindOf(Node node) => node switch
        {
            NodeGroup => LayoutNodeKind.Group,
            StartNode or AsyncStartBaseNode => LayoutNodeKind.Entry,
            _ => LayoutNodeKind.Node,
        };

        /// xNode keeps port positions of nodes it did not draw since; drop them so the edges of moved nodes are re-seeded at the new place.
        private static void ForgetPortPositions(NodeEditorWindow window, IReadOnlyList<Node> moved)
        {
            foreach (var node in moved)
            {
                foreach (var port in node.Ports) window.portConnectionPoints.Remove(port);
            }
        }
    }
}
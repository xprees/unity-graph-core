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
    /// "Group selected nodes": wraps the selected nodes in a new group sized to fit them (padding around, title above).
    /// Optional next to the layout commands: it never moves a node, it only adds the group frame. One undo step.
    public static class GraphGroupSelection
    {
        public const string MenuLabel = "Group selected nodes";

        /// Groups the window's selection. Nothing selected only shows a notification.
        public static void Run(NodeEditorWindow window)
        {
            if (!window || !window.graph) return;

            var selection = Selection.objects.OfType<Node>().Where(n => n && n.graph == window.graph).ToList();
            if (selection.Count == 0)
            {
                window.ShowNotification(new GUIContent("Select the nodes to group first"), 1.5);
                return;
            }

            var group = Create(window.graph, selection, NodeSizeEstimator.ForWindow(window));
            if (group) Selection.objects = new Object[] { group };
            window.Repaint();
        }

        /// Adds a <see cref="NodeGroup"/> around <paramref name="nodes"/> and returns it (null when there is nothing to wrap).
        /// Does not save the asset.
        public static NodeGroup Create(NodeGraph graph, IEnumerable<Node> nodes, Func<Node, Vector2> sizeOf = null, string title = "Group")
        {
            if (!graph) return null;

            sizeOf ??= NodeSizeEstimator.Estimate;
            var members = (nodes ?? Enumerable.Empty<Node>()).Where(n => n && n.graph == graph).Distinct().ToList();
            var fitted = GroupFit.Fit(members.Select(n => new Rect(n.position, sizeOf(n))));
            if (!fitted.HasValue) return null;

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(MenuLabel);
            Undo.RecordObject(graph, MenuLabel);

            var group = graph.AddNode<NodeGroup>();
            group.name = UniqueName(graph, title);
            if (EditorUtility.IsPersistent(graph)) AssetDatabase.AddObjectToAsset(group, graph);
            Undo.RegisterCreatedObjectUndo(group, MenuLabel);

            var rect = fitted.Value;
            group.position = rect.position;
            group.width = Mathf.CeilToInt(rect.width);
            group.height = Mathf.CeilToInt(rect.height - LayoutGeometry.GroupChrome);
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(graph);
            Undo.CollapseUndoOperations(undoGroup);
            return group;
        }

        private static string UniqueName(NodeGraph graph, string baseName)
        {
            var name = baseName;
            for (var i = 2; graph.nodes.Any(n => n && n.name == name); i++) name = $"{baseName} {i}";
            return name;
        }
    }
}
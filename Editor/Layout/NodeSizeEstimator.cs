using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using XNode;
using XNodeEditor;
using Xprees.Graph.Core.Attributes;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Editor.Layout
{
    /// Node sizes for the layout. Width is computed like the node editors do (resizable width, [NodeWidth], 208 default, group width).
    /// Height comes from the graph window's measured sizes when the node was drawn there, else it is estimated from the node's fields:
    /// 44 px (title + padding) + 21 px per visible field row (lists: one row per element + 2). Groups: body height + 50 px.
    public static class NodeSizeEstimator
    {
        private const float defaultWidth = 208f;
        private const float baseHeight = 44f;
        private const float rowHeight = 21f;

        /// Sizes measured by the window, falling back to estimates. Selected nodes are never LOD-collapsed, their measured height is exact.
        /// Unselected nodes may be drawn collapsed at low zoom, their height is never smaller than the estimate (bigger is safe for the
        /// no-overlap rule).
        public static Func<Node, Vector2> ForWindow(NodeEditorWindow window) => node =>
        {
            var estimate = Estimate(node);
            if (!window || node is NodeGroup || !window.nodeSizes.TryGetValue(node, out var measured)) return estimate;

            return new Vector2(estimate.x, Selection.Contains(node) ? measured.y : Mathf.Max(measured.y, estimate.y));
        };

        /// Size without a graph window (MCP tools, tests).
        public static Vector2 Estimate(Node node)
        {
            if (!node) return new Vector2(defaultWidth, baseHeight + rowHeight);
            if (node is NodeGroup group) return new Vector2(group.width, group.height + LayoutGeometry.GroupChrome);

            return new Vector2(Width(node), baseHeight + rowHeight * Mathf.Max(1, CountRows(node)));
        }

        public static float Width(Node node)
        {
            if (node is NodeGroup group) return group.width;

            var type = node.GetType();
            var resizable = type.GetCustomAttribute<NodeResizableWidthAttribute>(true);
            if (resizable is { isResizable: true } && node is BaseNode baseNode) return Mathf.Max(baseNode.width, resizable.minWidth);

            var width = type.GetCustomAttribute<Node.NodeWidthAttribute>(true);
            return width?.width ?? defaultWidth;
        }

        private static int CountRows(Node node)
        {
            using var serialized = new SerializedObject(node);
            var property = serialized.GetIterator();
            var rows = 0;
            var enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.name is "m_Script" or "graph" or "position" or "ports") continue;

                rows += property.isArray && property.propertyType != SerializedPropertyType.String ? property.arraySize + 2 : 1;
            }

            return rows;
        }
    }
}
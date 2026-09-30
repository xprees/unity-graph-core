using System.Linq;
using UnityEditor;
using UnityEngine;
using XNode;
using Xprees.Graph.Core.Editor.Navigation;

namespace Xprees.Graph.Core.Editor.Nodes
{
    /// Nodes referencing other graphs (e.g. SubGraphNode.subGraph, dialog nodes) can be entered
    /// by double-clicking their header or with the "Open" button - it pushes a breadcrumb.
    public partial class BaseNodeEditor
    {
        private const double doubleClickTime = 0.3;
        private const float openButtonIconSize = 14f;

        private static Node lastHeaderClickNode;
        private static double lastHeaderClickTime;

        /// xNode uses the MouseDown on the header (selection), so the double click is detected from two MouseUps.
        private void HandleHeaderDoubleClickToEnterGraph(Rect headerRect)
        {
            var e = Event.current;
            if (e.type != EventType.MouseUp || e.button != 0 || !headerRect.Contains(e.mousePosition)) return;

            var now = EditorApplication.timeSinceStartup;
            var isDoubleClick = lastHeaderClickNode == target && now - lastHeaderClickTime <= doubleClickTime;
            lastHeaderClickNode = target;
            lastHeaderClickTime = now;
            if (!isDoubleClick) return;

            lastHeaderClickNode = null;
            var graph = GraphReferences.GetReferencedGraphs(target).FirstOrDefault();
            if (graph) EnterGraphDelayed(graph);
        }

        private void DrawEnterGraphButtons()
        {
            foreach (var graph in GraphReferences.GetReferencedGraphs(target))
            {
                var content = EditorGUIUtility.IconContent("d_forward");
                content.text = $" Open {graph.name}";
                content.tooltip = "Open the graph and add it to the breadcrumbs.";

                var previousIconSize = EditorGUIUtility.GetIconSize();
                EditorGUIUtility.SetIconSize(new Vector2(openButtonIconSize, openButtonIconSize));
                var clicked = GUILayout.Button(content);
                EditorGUIUtility.SetIconSize(previousIconSize);

                if (clicked) EnterGraphDelayed(graph);
            }
        }

        /// Switching the graph in the middle of the node editor's GUI pass would break its layout.
        private void EnterGraphDelayed(NodeGraph graph)
        {
            var node = target;
            EditorApplication.delayCall += () => GraphNavigation.EnterGraph(node, graph);
        }
    }
}
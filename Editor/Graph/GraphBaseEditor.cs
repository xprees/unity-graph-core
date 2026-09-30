using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XNode;
using XNodeEditor;
using Xprees.Graph.Core.Base;

namespace Xprees.Graph.Core.Editor.Graph
{
    [CustomNodeGraphEditor(typeof(GraphBase))]
    public class GraphBaseEditor : NodeGraphEditor
    {
        // Rough node layout used to estimate port positions of nodes that were not drawn yet
        private const float estimatedHeaderHeight = 30f;
        private const float estimatedPortLineHeight = 20f;

        private const float highlightDuration = 2f;
        private const float highlightThickness = 3f;
        private const float highlightPadding = 6f;
        private const float maxFocusZoom = 1.5f;
        private readonly static Color highlightColor = new(1f, 0.8f, 0.1f);

        private static int lastTargetHash;

        private static Node highlightedNode;
        private static double highlightStartTime;

        /// Ports we already requested a repaint for. Ports never drawn by their node editor would otherwise repaint forever.
        private readonly HashSet<NodePort> _seededPorts = new();

        public override void OnGUI()
        {
            base.OnGUI();

            if (Event.current.type == EventType.Repaint)
            {
                EnsureConnectionPointsCached();
                DrawHighlight();
            }

            var currentTargetHash = target.GetHashCode();
            if (lastTargetHash == currentTargetHash) return;
            lastTargetHash = currentTargetHash;

            OnGraphChange();
        }

        protected virtual void OnGraphChange() => ResetViewPositionOnSwitchIfNoNodesVisible();

        private void ResetViewPositionOnSwitchIfNoNodesVisible() => NodeEditorWindow.current.Home();

        /// Opens the node's graph, selects the node, centers the viewport on it and briefly highlights it.
        public static void FocusNode(Node node)
        {
            if (!node || !node.graph) return;

            var window = NodeEditorWindow.Open(node.graph);
            Selection.activeObject = node;

            // Zoom in when the view is zoomed out too far to read the node (higher value = further out)
            if (window.zoom > maxFocusZoom) window.zoom = maxFocusZoom;

            var size = window.nodeSizes.TryGetValue(node, out var cachedSize) ? cachedSize : new Vector2(200, 100);
            window.panOffset = -(node.position + size * 0.5f);

            highlightedNode = node;
            highlightStartTime = EditorApplication.timeSinceStartup;
            window.Repaint();
        }

        /// Pulsing outline around the focused node, fading out after a while.
        private void DrawHighlight()
        {
            if (!highlightedNode || !window || highlightedNode.graph != target) return;

            var elapsed = (float) (EditorApplication.timeSinceStartup - highlightStartTime);
            if (elapsed > highlightDuration)
            {
                highlightedNode = null;
                return;
            }

            var size = window.nodeSizes.TryGetValue(highlightedNode, out var cachedSize) ? cachedSize : new Vector2(200, 100);
            var rect = window.GridToWindowRect(new Rect(highlightedNode.position, size));
            rect.xMin -= highlightPadding;
            rect.yMin -= highlightPadding;
            rect.xMax += highlightPadding;
            rect.yMax += highlightPadding;

            var pulse = 0.5f + 0.5f * Mathf.Cos(elapsed * Mathf.PI * 3f);
            var fade = 1f - elapsed / highlightDuration;
            var color = highlightColor;
            color.a = Mathf.Lerp(0.4f, 1f, pulse) * fade;

            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, highlightThickness), color);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMax - highlightThickness, rect.width, highlightThickness), color);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, highlightThickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - highlightThickness, rect.yMin, highlightThickness, rect.height), color);

            window.Repaint(); // Keep animating until the highlight fades out
        }

        /// xNode draws connections before nodes, using port positions cached by the previous repaint,
        /// and it never caches ports of nodes culled outside the view. That leaves edges invisible right after
        /// opening a graph and for edges leading off-screen. Seed estimated positions for such ports
        /// (xNode overwrites them once the node is drawn) and repaint once, so the edges get drawn.
        private void EnsureConnectionPointsCached()
        {
            if (!window || !target) return;

            var connectionPoints = window.portConnectionPoints;
            var needsRepaint = false;

            foreach (var node in target.nodes)
            {
                if (!node) continue;

                var inputIndex = 0;
                var outputIndex = 0;
                foreach (var port in node.Ports)
                {
                    var index = port.IsInput ? inputIndex++ : outputIndex++;
                    if (!port.IsConnected || connectionPoints.ContainsKey(port)) continue;

                    var x = port.IsInput ? 0f : NodeEditor.GetEditor(node, window).GetWidth();
                    var y = estimatedHeaderHeight + (index + 0.5f) * estimatedPortLineHeight;
                    if (window.nodeSizes.TryGetValue(node, out var size)) y = Mathf.Min(y, size.y);

                    var center = node.position + new Vector2(x, y);
                    connectionPoints[port] = new Rect(center.x - 8, center.y - 8, 16, 16);
                    if (_seededPorts.Add(port)) needsRepaint = true;
                }
            }

            if (needsRepaint) window.Repaint();
        }
    }
}
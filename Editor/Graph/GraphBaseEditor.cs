using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using XNode;
using XNodeEditor;
using Xprees.Graph.Core.Base;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Editor.Debugger;
using Xprees.Graph.Core.Editor.Minimap;
using Xprees.Graph.Core.Editor.Navigation;
using Xprees.Graph.Core.Editor.NodeMenu;
using Xprees.Graph.Core.Editor.Nodes;
using Xprees.Graph.Core.Editor.Search;
using Xprees.Graph.Core.Editor.Toolbar;

namespace Xprees.Graph.Core.Editor.Graph
{
    [CustomNodeGraphEditor(typeof(GraphBase))]
    public class GraphBaseEditor : NodeGraphEditor
    {
        // Rough node layout used to estimate port positions of nodes that were not drawn yet
        private const float estimatedHeaderHeight = 30f;
        private const float estimatedPortLineHeight = 20f;
        private readonly static Vector2 defaultNodeSize = new(200, 100);

        private const float highlightDuration = 2f;
        private const float highlightThickness = 3f;
        private const float highlightPadding = 6f;
        private const float maxFocusZoom = 1.5f;
        private const float frameAllMargin = 1.15f;
        private readonly static Color highlightColor = new(1f, 0.8f, 0.1f);

        private static int lastTargetHash;

        private static Node highlightedNode;
        private static double highlightStartTime;

        /// Node to focus once its graph is shown - graph switches reset the view, so focusing has to wait for it.
        private static Node pendingFocusNode;

        /// Ports we already requested a repaint for. Ports never drawn by their node editor would otherwise repaint forever.
        private readonly HashSet<NodePort> _seededPorts = new();

        public override void OnCreate()
        {
            base.OnCreate();
            GuardedNodeEditors.EnsureInstalled();
        }

        public override void OnGUI()
        {
            using (GraphDebugMarkers.OnGui.Auto()) OnGuiInternal();
        }

        private void OnGuiInternal()
        {
            // Typing into the debug panel must not trigger xNode shortcuts (Delete, F, Ctrl+V ...)
            var e = Event.current;
            if ((e.type == EventType.KeyDown || e.type == EventType.KeyUp)
                && (GraphDebugPanel.HasKeyboardFocus(window) || GraphSearchOverlay.HasKeyboardFocus(window))) e.Use();

            base.OnGUI();

            // A node editor that threw while dimmed must not leave its GUI.color behind
            if (GUI.color != Color.white) GUI.color = Color.white;

            GraphEditorToolbar.EnsureAttached(window, target);
            GraphDebugPanel.EnsureAttached(window);
            GraphMinimap.EnsureAttached(window);
            FilterCopyBuffer();
            HandleGroupBodyRightClick();
            HandleBreakpointDotClick();

            if (Event.current.type == EventType.Repaint)
            {
                using (GraphDebugMarkers.ConnectionPoints.Auto()) EnsureConnectionPointsCached();
                DrawHighlight();
                using (GraphDebugMarkers.Overlays.Auto()) GraphDebugRenderer.DrawOverlays(window, target);
            }

            using (GraphDebugMarkers.EdgeTooltip.Auto()) GraphEdgeTooltip.Handle(window, target);

            var currentTargetHash = target.GetHashCode();
            if (lastTargetHash == currentTargetHash) return;
            lastTargetHash = currentTargetHash;

            OnGraphChange();
        }

        #region Debugger visuals

        // xNode asks for these on every event and for every connection (also with a null input while dragging), so they only read caches.
        public override Gradient GetNoodleGradient(NodePort output, NodePort input) =>
            IsHovered(output, input)
                ? base.GetNoodleGradient(output, input)
                : GraphDebugRenderer.GetEdgeStyle(this, output, input)?.Gradient ?? base.GetNoodleGradient(output, input);

        public override float GetNoodleThickness(NodePort output, NodePort input) =>
            GraphDebugRenderer.GetEdgeStyle(this, output, input)?.Thickness ?? base.GetNoodleThickness(output, input);

        public override NoodleStroke GetNoodleStroke(NodePort output, NodePort input)
        {
            var style = GraphDebugRenderer.GetEdgeStyle(this, output, input);
            return style != null && style.Stroke == NoodleStroke.Dashed ? NoodleStroke.Dashed : base.GetNoodleStroke(output, input);
        }

        private Rect _viewGrid;
        private Vector2 _viewPan;
        private float _viewZoom;
        private Vector2 _viewSize;

        /// A curvy connection that cannot reach the view is drawn straight (one draw call instead of one per few pixels), see <see cref="NoodleCulling"/>.
        public override NoodlePath GetNoodlePath(NodePort output, NodePort input)
        {
            var path = base.GetNoodlePath(output, input);
            if (path != NoodlePath.Curvy || !GraphLod.Enabled || !window || output == null || input == null) return path;

            var points = window.portConnectionPoints;
            if (!points.TryGetValue(output, out var from) || !points.TryGetValue(input, out var to)) return path;
            if (HasReroutes(output, input)) return path;

            return NoodleCulling.CanSkipCurve(ViewInGrid(), from.center, to.center) ? NoodlePath.Straight : path;
        }

        private static bool HasReroutes(NodePort output, NodePort input)
        {
            for (var i = 0; i < output.ConnectionCount; i++)
            {
                if (output.GetConnection(i) == input) return output.GetReroutePoints(i).Count > 0;
            }

            return false;
        }

        private Rect ViewInGrid()
        {
            var size = window.position.size;
            if (_viewPan != window.panOffset || _viewZoom != window.zoom || _viewSize != size)
            {
                _viewPan = window.panOffset;
                _viewZoom = window.zoom;
                _viewSize = size;
                var a = window.WindowToGridPosition(Vector2.zero);
                var b = window.WindowToGridPosition(size);
                _viewGrid = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            }

            return _viewGrid;
        }

        public override Color GetPortColor(NodePort port)
        {
            var color = base.GetPortColor(port);
            if (port?.node != null && GraphDebugRenderer.GetState(port.node) == NodeVisualState.NotVisited) color.a *= 0.45f;
            return color;
        }

        private bool IsHovered(NodePort output, NodePort input) =>
            window && input != null && (window.hoveredPort == output || window.hoveredPort == input);

        #endregion

        #region Node type restrictions & group right-click

        // xNode pastes from one static buffer. Keep the copied nodes and expose only the ones this graph allows,
        // so [AllowOnlyInGraphs] / [ExcludeFromGraphs] can't be bypassed by copy & paste.
        private static Node[] copiedNodes;
        private static Node[] filteredBuffer;

        private void FilterCopyBuffer()
        {
            var buffer = NodeEditorWindow.copyBuffer;
            if (buffer == null || buffer.Length == 0) return;

            if (!ReferenceEquals(buffer, filteredBuffer)) copiedNodes = buffer; // New copy made by the user

            var allowed = copiedNodes.Where(node => node && NodeTypeFilter.IsAllowed(node.GetType(), target)).ToArray();
            if (allowed.Length == copiedNodes.Length)
            {
                filteredBuffer = copiedNodes;
            }
            else
            {
                filteredBuffer = allowed;
            }

            NodeEditorWindow.copyBuffer = filteredBuffer;
        }

        private Vector2 _rightMouseDownPosition;

        /// xNode opens no menu when right-clicking inside a node, so the body of a group (which usually contains nothing else)
        /// offers no way to create nodes. Open the create popup there, unless the click lands on another node, a port or a title.
        private void HandleGroupBodyRightClick()
        {
            var e = Event.current;
            if (e.button != 1) return;

            if (e.rawType == EventType.MouseDown)
            {
                _rightMouseDownPosition = e.mousePosition;
                return;
            }

            // Right mouse drag pans the view - only a click opens the popup
            if (e.rawType != EventType.MouseUp || (e.mousePosition - _rightMouseDownPosition).sqrMagnitude > 16f) return;
            if (window.hoveredPort != null || !IsInsideGroupBody(e.mousePosition)) return;

            NodeCreationPopup.Show(this, target, window, window.WindowToGridPosition(e.mousePosition), new Rect(e.mousePosition, Vector2.zero));
            e.Use();
        }

        private Node _dotPressedNode;
        private Node _dotHoverNode;
        private Vector2 _dotPressedMouse;
        private int[] _dotPressedSelection;
        private bool _dotPressedModifier;

        /// Toggles a breakpoint when the dot in a node header is clicked. Handled here, from the raw mouse events after xNode's controls,
        /// because the per-node header GUI sees a MouseDown xNode already used and shares the band with the title label and its description.
        /// xNode's controls run first and select the node (Ctrl/Shift toggles the selection), a click on the dot puts the selection back.
        private void HandleBreakpointDotClick()
        {
            var e = Event.current;
            if (e.type == EventType.Layout) DotSelectionGuard.Snapshot();

            if (e.rawType == EventType.MouseMove)
            {
                var hover = FindDotNodeAt(e.mousePosition);
                if (hover != _dotHoverNode)
                {
                    _dotHoverNode = hover;
                    window.Repaint();
                }

                return;
            }

            if (e.button != 0) return;

            if (e.rawType == EventType.MouseDown)
            {
                _dotPressedNode = FindDotNodeAt(e.mousePosition);
                _dotPressedMouse = e.mousePosition;
                _dotPressedModifier = e.alt || e.control || e.shift;
                _dotPressedSelection = _dotPressedNode ? DotSelectionGuard.LastSnapshot : null;
                return;
            }

            if (e.rawType != EventType.MouseUp || !_dotPressedNode) return;

            var pressed = _dotPressedNode;
            _dotPressedNode = null;
            if (!BreakpointDotLayout.IsClick(_dotPressedMouse, e.mousePosition) || FindDotNodeAt(e.mousePosition) != pressed) return;

            DotSelectionGuard.Restore(_dotPressedSelection);
            // The modifier counts when held at press OR release: letting go of Alt a moment before the mouse button must not turn it into a plain click
            ApplyDotClick(pressed, _dotPressedModifier || e.alt || e.control || e.shift);
            e.Use();
        }

        private void ApplyDotClick(Node node, bool altOrControl)
        {
            var action = BreakpointDotLayout.ResolveClick(GraphBreakpoints.Has(node), altOrControl);
            switch (action)
            {
                case DotClickAction.Add:
                    GraphBreakpoints.SetEnabled(node, true);
                    break;
                case DotClickAction.Remove:
                    GraphBreakpoints.Remove(node);
                    break;
                case DotClickAction.ToggleEnabled:
                    GraphBreakpoints.SetEnabled(node, !GraphBreakpoints.IsEnabled(node));
                    break;
            }

            var message = BreakpointDotLayout.FeedbackMessage(action, GraphBreakpoints.IsEnabled(node), node.name,
                GraphDebuggerSettings.Enabled);
            window.ShowNotification(new GUIContent(message), 1.5);
            window.Repaint();
        }

        /// The topmost node whose breakpoint dot area contains the window position. A node on top of the dot hides it.
        private Node FindDotNodeAt(Vector2 windowPosition)
        {
            if (!window || !target) return null;

            var nodes = target.nodes;
            for (var i = nodes.Count - 1; i >= 0; i--)
            {
                var node = nodes[i];
                if (node is not BaseNode || !window.nodeSizes.TryGetValue(node, out var size)) continue; // Groups draw no dot and lie below

                var origin = window.GridToWindowPosition(node.position);
                if (!new Rect(origin, size / window.zoom).Contains(windowPosition)) continue;

                return BreakpointDotLayout.WindowHit(origin, size.x, window.zoom).Contains(windowPosition) ? node : null;
            }

            return null;
        }

        private bool IsInsideGroupBody(Vector2 windowPosition)
        {
            var insideGroup = false;
            foreach (var node in target.nodes)
            {
                if (!node) continue;

                var size = window.nodeSizes.TryGetValue(node, out var nodeSize) ? nodeSize : Vector2.zero;
                var rect = window.GridToWindowRect(new Rect(node.position, size));
                if (!rect.Contains(windowPosition)) continue;

                if (node is not NodeGroup) return false; // Clicked a regular node

                var titleHeight = 30f / window.zoom; // Same header height xNode uses for its title menu
                if (windowPosition.y < rect.y + titleHeight) return false;
                insideGroup = true;
            }

            return insideGroup;
        }

        #endregion

        /// Right-click on the grid (and dropping a dragged connection on empty space) opens the searchable node tree
        /// instead of xNode's flat GenericMenu. The menu itself stays empty, so nothing else pops up.
        public override void AddContextMenuItems(GenericMenu menu)
        {
            var mouse = Event.current.mousePosition;
            ReleaseDraggedPort();
            NodeCreationPopup.Show(this, target, window, window.WindowToGridPosition(mouse), new Rect(mouse, Vector2.zero));
        }

        private readonly static FieldInfo draggedOutputField =
            typeof(NodeEditorWindow).GetField("draggedOutput", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly static FieldInfo draggedOutputTargetField =
            typeof(NodeEditorWindow).GetField("draggedOutputTarget", BindingFlags.Instance | BindingFlags.NonPublic);

        /// xNode clears its dragged connection only after AddContextMenuItems returns. The popup opening inside it interrupts the
        /// mouse-up handling (the window loses focus), so the connection kept following the mouse after the node was created.
        /// The auto-connect source is a separate field and stays intact.
        private void ReleaseDraggedPort()
        {
            draggedOutputField?.SetValue(window, null);
            draggedOutputTargetField?.SetValue(window, null);
        }

        protected virtual void OnGraphChange()
        {
            GraphNavigation.OnGraphShown(target);
            GraphEditorToolbar.Refresh(window, target);

            if (pendingFocusNode && pendingFocusNode.graph == target)
            {
                var node = pendingFocusNode;
                pendingFocusNode = null;
                FocusNodeInWindow(window, node);
                return;
            }

            FrameAll(window);
        }

        #region View

        /// Centers the view on all nodes and zooms out so they fit into the window.
        public static void FrameAll(NodeEditorWindow window)
        {
            if (!window || !window.graph) return;

            var hasNodes = false;
            var bounds = new Rect();
            foreach (var node in window.graph.nodes)
            {
                if (!node) continue;

                var rect = new Rect(node.position, GetNodeSize(window, node));
                bounds = hasNodes
                    ? Rect.MinMaxRect(
                        Mathf.Min(bounds.xMin, rect.xMin), Mathf.Min(bounds.yMin, rect.yMin),
                        Mathf.Max(bounds.xMax, rect.xMax), Mathf.Max(bounds.yMax, rect.yMax))
                    : rect;
                hasNodes = true;
            }

            if (!hasNodes)
            {
                window.panOffset = Vector2.zero;
                window.zoom = 1f;
                return;
            }

            // Zoom is a scale of the visible grid area (higher value = further out)
            var viewSize = window.position.size - new Vector2(0, GraphEditorToolbar.Height);
            window.zoom = Mathf.Max(bounds.width / viewSize.x, bounds.height / viewSize.y) * frameAllMargin;
            window.panOffset = -bounds.center;
        }

        /// Opens the node's graph, selects the node, centers the viewport on it and briefly highlights it.
        public static void FocusNode(Node node)
        {
            if (!node || !node.graph) return;

            var window = NodeEditorWindow.Open(node.graph);
            if (lastTargetHash != node.graph.GetHashCode())
            {
                // The graph switch resets the view on the next GUI pass - focus after it
                pendingFocusNode = node;
                Selection.activeObject = node;
                return;
            }

            FocusNodeInWindow(window, node);
        }

        private static void FocusNodeInWindow(NodeEditorWindow window, Node node)
        {
            Selection.activeObject = node;

            if (window.zoom > maxFocusZoom) window.zoom = maxFocusZoom;
            window.panOffset = -(node.position + GetNodeSize(window, node) * 0.5f);

            highlightedNode = node;
            highlightStartTime = EditorApplication.timeSinceStartup;
            window.Repaint();
        }

        private static Vector2 GetNodeSize(NodeEditorWindow window, Node node) =>
            window.nodeSizes.TryGetValue(node, out var size) ? size : defaultNodeSize;

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

            var rect = window.GridToWindowRect(new Rect(highlightedNode.position, GetNodeSize(window, highlightedNode)));
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

        #endregion

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
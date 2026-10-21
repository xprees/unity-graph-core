using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XNode;
using XNodeEditor;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Editor.Debugger;

namespace Xprees.Graph.Core.Editor.Graph
{
    /// Pure geometry of the connection hit test, so it is testable without a window.
    public static class EdgeHitTest
    {
        private const int curveSteps = 24;

        /// Distance from the mouse to a connection drawn as xNode's curve (no reroutes) or as a polyline through the points. Window space.
        public static float Distance(Vector2 mouse, Vector2 from, Vector2 to, IReadOnlyList<Vector2> reroutes, bool curvy, float reach)
        {
            if (curvy && (reroutes == null || reroutes.Count == 0)) return CurveDistance(mouse, from, to, reach);

            var best = float.MaxValue;
            var previous = from;
            if (reroutes != null)
            {
                for (var i = 0; i < reroutes.Count; i++)
                {
                    best = Mathf.Min(best, SegmentDistance(mouse, previous, reroutes[i]));
                    previous = reroutes[i];
                }
            }

            return Mathf.Min(best, SegmentDistance(mouse, previous, to));
        }

        /// xNode's single-segment noodle: tangents of half the distance, horizontal at both ends.
        private static float CurveDistance(Vector2 mouse, Vector2 from, Vector2 to, float reach)
        {
            var offset = Vector2.Distance(from, to) * 0.5f;
            var c1 = from + Vector2.right * offset;
            var c2 = to + Vector2.left * offset;

            var min = Vector2.Min(Vector2.Min(from, to), Vector2.Min(c1, c2)) - Vector2.one * reach;
            var max = Vector2.Max(Vector2.Max(from, to), Vector2.Max(c1, c2)) + Vector2.one * reach;
            if (mouse.x < min.x || mouse.y < min.y || mouse.x > max.x || mouse.y > max.y) return float.MaxValue;

            var best = float.MaxValue;
            var previous = from;
            for (var i = 1; i <= curveSteps; i++)
            {
                var next = Bezier(from, c1, c2, to, i / (float) curveSteps);
                best = Mathf.Min(best, SegmentDistance(mouse, previous, next));
                previous = next;
            }

            return best;
        }

        private static Vector2 Bezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            var u = 1f - t;
            return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
        }

        public static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lengthSqr = ab.sqrMagnitude;
            if (lengthSqr < 0.0001f) return Vector2.Distance(point, a);

            var t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSqr);
            return Vector2.Distance(point, a + ab * t);
        }
    }

    /// Hover tooltip of a connection (taken N times, last time, instance). Own hit test, because xNode has none for noodles.
    /// The text is only rebuilt and the window repainted when the hovered connection changes.
    internal static class GraphEdgeTooltip
    {
        private const float hitDistance = 7f;
        private const float tooltipOffset = 16f;

        private static NodeEditorWindow _window;
        private static NodePort _output;
        private static NodePort _input;
        private static Vector2 _anchor;
        private static GUIStyle _style;
        private static readonly GUIContent Content = new();
        private static readonly List<Vector2> RerouteBuffer = new();

        public static void Handle(NodeEditorWindow window, NodeGraph graph)
        {
            if (!window || !graph) return;

            if (!GraphDebugRenderer.IsActive)
            {
                Forget(window);
                return;
            }

            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseMove:
                case EventType.MouseDrag:
                    Update(window, graph, e.mousePosition);
                    break;
                case EventType.MouseLeaveWindow:
                    Forget(window);
                    break;
                case EventType.Repaint:
                    Draw(window);
                    break;
            }
        }

        private static void Forget(NodeEditorWindow window)
        {
            if (_output == null) return;

            _output = null;
            _input = null;
            _window = null;
            if (window) window.Repaint();
        }

        private static void Update(NodeEditorWindow window, NodeGraph graph, Vector2 mouse)
        {
            NodePort output = null;
            NodePort input = null;
            if (window.hoveredPort == null && !IsOverNode(window, graph, mouse)) Find(window, graph, mouse, out output, out input);

            var changed = output != _output || input != _input || window != _window;
            if (!changed) return;

            _output = output;
            _input = input;
            _window = output != null ? window : null;
            _anchor = mouse;
            window.Repaint();
        }

        private static bool IsOverNode(NodeEditorWindow window, NodeGraph graph, Vector2 mouse)
        {
            foreach (var node in graph.nodes)
            {
                if (!node || node is NodeGroup) continue;
                if (!window.nodeSizes.TryGetValue(node, out var size)) continue;
                if (window.GridToWindowRect(new Rect(node.position, size)).Contains(mouse)) return true;
            }

            return false;
        }

        private static void Find(NodeEditorWindow window, NodeGraph graph, Vector2 mouse, out NodePort foundOutput, out NodePort foundInput)
        {
            foundOutput = null;
            foundInput = null;
            var best = hitDistance;
            var points = window.portConnectionPoints;

            foreach (var node in graph.nodes)
            {
                if (!node) continue;

                foreach (var output in node.Outputs)
                {
                    if (!points.TryGetValue(output, out var fromRect)) continue;

                    var from = window.GridToWindowPosition(fromRect.center);
                    for (var k = 0; k < output.ConnectionCount; k++)
                    {
                        var input = output.GetConnection(k);
                        if (input == null || !points.TryGetValue(input, out var toRect)) continue;

                        RerouteBuffer.Clear();
                        foreach (var reroute in output.GetReroutePoints(k)) RerouteBuffer.Add(window.GridToWindowPosition(reroute));

                        var curvy = window.graphEditor.GetNoodlePath(output, input) == NoodlePath.Curvy;
                        var distance = EdgeHitTest.Distance(mouse, from, window.GridToWindowPosition(toRect.center), RerouteBuffer, curvy,
                            hitDistance);
                        if (distance >= best) continue;

                        best = distance;
                        foundOutput = output;
                        foundInput = input;
                    }
                }
            }
        }

        private static void Draw(NodeEditorWindow window)
        {
            if (_output == null || _window != window) return;

            var tracer = GraphRuntimeTracer.Instance;
            var edge = tracer.GetEdge(_output, _input);
            Content.text = DebuggerText.EdgeTooltip(edge.Count, edge.ExactCount, edge.LastTime, tracer.ReferenceTime,
                edge.LastRecord?.ToString());

            _style ??= new GUIStyle(EditorStyles.miniLabel)
            {
                padding = new RectOffset(6, 6, 3, 3),
                normal = { textColor = Color.white },
            };

            var size = _style.CalcSize(Content);
            var rect = new Rect(_anchor + new Vector2(tooltipOffset, tooltipOffset), size);
            var bounds = window.position.size;
            rect.x = Mathf.Min(rect.x, bounds.x - rect.width - 4f);
            rect.y = Mathf.Min(rect.y, bounds.y - rect.height - 4f);

            EditorGUI.DrawRect(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2), new Color(0.05f, 0.05f, 0.05f, 0.95f));
            EditorGUI.DrawRect(rect, new Color(0.14f, 0.14f, 0.14f, 0.97f));
            GUI.Label(rect, Content, _style);
        }
    }
}
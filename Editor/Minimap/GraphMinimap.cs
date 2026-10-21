using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XNode;
using XNodeEditor;
using Xprees.Graph.Core.Editor.Debugger;
using Object = UnityEngine.Object;

namespace Xprees.Graph.Core.Editor.Minimap
{
    /// Bottom-right overview of the whole graph (UI Toolkit, Painter2D): node rects (colored by debug state while a run is shown),
    /// the visible viewport, click or drag pans the window. Works with the debugger off.
    public static class GraphMinimap
    {
        private const string minimapName = "xprees-graph-minimap";
        private const string visibleKey = "Xprees.GraphMinimap.Visible";
        private const string debugPanelName = "xprees-graph-debug-panel";
        private const float mapWidth = 220f;
        private const float mapHeight = 150f;
        private const float margin = 8f;
        private const float padding = 6f;
        private const long refreshIntervalMs = 100;
        private readonly static Vector2 defaultNodeSize = new(200, 100);

        private readonly static Color background = new(0.12f, 0.12f, 0.12f, 0.85f);
        private readonly static Color idleColor = new(0.6f, 0.6f, 0.6f);
        private readonly static Color visitedColor = new(0.30f, 0.75f, 1f);
        private readonly static Color notVisitedColor = new(0.35f, 0.35f, 0.35f);
        private readonly static Color selectedColor = new(1f, 0.85f, 0.2f);
        private readonly static Color viewportColor = new(1f, 1f, 1f, 0.9f);

        public static bool Visible
        {
            get => EditorPrefs.GetBool(visibleKey, true);
            set => EditorPrefs.SetBool(visibleKey, value);
        }

        private sealed class State
        {
            public NodeEditorWindow Window;
            public bool Dragging;
            public int Signature;
            public readonly List<Rect> Rects = new();
            public readonly HashSet<Object> Selected = new();
        }

        /// Adds the minimap to the window when missing, shows or hides it by the toggle.
        public static void EnsureAttached(NodeEditorWindow window)
        {
            if (!window) return;

            var root = window.rootVisualElement;
            var minimap = root.Q(minimapName);
            if (minimap == null)
            {
                minimap = Create(window);
                root.Add(minimap);
            }

            var display = Visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (minimap.style.display != display) minimap.style.display = display;
        }

        /// Toolbar toggle that shows and hides the minimap.
        public static ToolbarToggle CreateToolbarToggle(NodeEditorWindow window)
        {
            var toggle = new ToolbarToggle
            {
                text = "Map",
                tooltip =
                    "Minimap.\nShows the whole graph in a corner (colored by debug state during a run). Click or drag it to move the view.",
                value = Visible
            };
            toggle.RegisterValueChangedCallback(evt =>
            {
                Visible = evt.newValue;
                EnsureAttached(window);
            });
            return toggle;
        }

        private static VisualElement Create(NodeEditorWindow window)
        {
            var state = new State { Window = window };
            var minimap = new VisualElement { name = minimapName, userData = state };
            minimap.style.position = Position.Absolute;
            minimap.style.bottom = margin;
            minimap.style.right = margin;
            minimap.style.width = mapWidth;
            minimap.style.height = mapHeight;
            minimap.style.borderTopWidth = minimap.style.borderBottomWidth = minimap.style.borderLeftWidth = minimap.style.borderRightWidth = 1;
            minimap.style.borderTopColor = minimap.style.borderBottomColor =
                minimap.style.borderLeftColor = minimap.style.borderRightColor = new Color(0.05f, 0.05f, 0.05f);
            minimap.generateVisualContent += ctx => Paint(ctx, state, minimap);

            minimap.RegisterCallback<PointerDownEvent>(evt =>
            {
                state.Dragging = true;
                minimap.CapturePointer(evt.pointerId);
                PanTo(state, minimap, evt.localPosition);
                evt.StopPropagation();
            });
            minimap.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!state.Dragging) return;

                PanTo(state, minimap, evt.localPosition);
                evt.StopPropagation();
            });
            minimap.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!state.Dragging) return;

                state.Dragging = false;
                minimap.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            });

            minimap.schedule.Execute(() => Tick(state, minimap)).Every(refreshIntervalMs);
            return minimap;
        }

        private static void Tick(State state, VisualElement minimap)
        {
            var window = state.Window;
            if (!window || minimap.style.display == DisplayStyle.None) return;

            using var marker = GraphDebugMarkers.MinimapTick.Auto();
            // Stay left of the debug panel instead of hiding behind it
            var panel = window.rootVisualElement.Q(debugPanelName);
            var right = margin + (panel != null && panel.style.display != DisplayStyle.None ? panel.layout.width : 0f);
            if (!Mathf.Approximately(minimap.style.right.value.value, right)) minimap.style.right = right;

            var signature = ComputeSignature(window);
            if (signature == state.Signature) return;

            state.Signature = signature;
            minimap.MarkDirtyRepaint();
        }

        /// Cheap change detector: view, node positions, selection and the recorded run.
        private static int ComputeSignature(NodeEditorWindow window)
        {
            var hash = new HashCode();
            hash.Add(window.panOffset);
            hash.Add(window.zoom);
            hash.Add(window.position.size);
            hash.Add(Selection.objects.Length);
            hash.Add(GraphDebugRenderer.IsActive ? GraphRuntimeTracer.Instance.Version : -1);
            if (window.graph)
            {
                foreach (var node in window.graph.nodes)
                {
                    if (node) hash.Add(node.position);
                }
            }

            return hash.ToHashCode();
        }

        private static MinimapTransform BuildTransform(State state, Vector2 mapSize)
        {
            state.Rects.Clear();
            var graph = state.Window ? state.Window.graph : null;
            if (graph)
            {
                foreach (var node in graph.nodes)
                {
                    if (node) state.Rects.Add(NodeRect(state.Window, node));
                }
            }

            return new MinimapTransform(MinimapTransform.Bounds(state.Rects), mapSize, padding);
        }

        private static Rect NodeRect(NodeEditorWindow window, Node node) =>
            new(node.position, window.nodeSizes.TryGetValue(node, out var size) ? size : defaultNodeSize);

        private static void PanTo(State state, VisualElement minimap, Vector2 local)
        {
            var window = state.Window;
            if (!window || !window.graph) return;

            var transform = BuildTransform(state, minimap.contentRect.size);
            window.panOffset = MinimapTransform.PanOffsetFor(transform.ToGrid(local));
            window.Repaint();
            minimap.MarkDirtyRepaint();
        }

        private static void Paint(MeshGenerationContext ctx, State state, VisualElement minimap)
        {
            var window = state.Window;
            if (!window || !window.graph) return;

            using var marker = GraphDebugMarkers.MinimapPaint.Auto();
            var size = minimap.contentRect.size;
            var transform = BuildTransform(state, size);
            var painter = ctx.painter2D;

            FillRect(painter, new Rect(Vector2.zero, size), background);

            state.Selected.Clear();
            foreach (var selected in Selection.objects) state.Selected.Add(selected);

            var nodes = window.graph.nodes;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (!node) continue;

                var rect = transform.ToMap(NodeRect(window, node));
                rect.width = Mathf.Max(rect.width, 2f);
                rect.height = Mathf.Max(rect.height, 2f);
                FillRect(painter, rect, state.Selected.Contains(node) ? selectedColor : ColorOf(node));
            }

            var viewport = transform.ToMap(MinimapTransform.ViewportInGrid(window.panOffset, window.zoom, window.position.size));
            StrokeRect(painter, ClampTo(viewport, size), viewportColor);
        }

        private static Color ColorOf(Node node)
        {
            return GraphDebugRenderer.GetState(node) switch
            {
                NodeVisualState.Active => GraphDebugRenderer.ActiveNodeColor,
                NodeVisualState.PausedHere => GraphDebugRenderer.PausedNodeColor,
                NodeVisualState.Visited => visitedColor,
                NodeVisualState.NotVisited => notVisitedColor,
                NodeVisualState.Disabled => notVisitedColor,
                _ => idleColor,
            };
        }

        private static Rect ClampTo(Rect rect, Vector2 size) =>
            Rect.MinMaxRect(Mathf.Clamp(rect.xMin, 0, size.x), Mathf.Clamp(rect.yMin, 0, size.y), Mathf.Clamp(rect.xMax, 0, size.x),
                Mathf.Clamp(rect.yMax, 0, size.y));

        private static void FillRect(Painter2D painter, Rect rect, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            Path(painter, rect);
            painter.Fill();
        }

        private static void StrokeRect(Painter2D painter, Rect rect, Color color)
        {
            painter.strokeColor = color;
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            Path(painter, rect);
            painter.Stroke();
        }

        private static void Path(Painter2D painter, Rect rect)
        {
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
        }
    }
}
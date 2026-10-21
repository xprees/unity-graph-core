using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XNode;
using XNodeEditor;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Editor.Graph;
using Xprees.Graph.Core.Editor.Navigation;

namespace Xprees.Graph.Core.Editor.Debugger
{
    public enum EdgeVisual
    {
        Untaken,
        Taken,
        TakenAmbiguous,
        Active
    }

    /// Glue between the tracer model and the xNode GUI: cached node/edge visuals, run overlays (outlines, instance badges)
    /// and the Follow behaviour. Everything is cached per tracer Version, so the per-node and per-edge queries do not allocate per call.
    [InitializeOnLoad]
    public static class GraphDebugRenderer
    {
        public static readonly Color AccentColor = new(0.30f, 0.75f, 1f);
        public static readonly Color ActiveEdgeColor = new(0.75f, 0.95f, 1f);
        public static readonly Color ActiveNodeColor = new(0.35f, 0.9f, 0.5f);
        public static readonly Color PausedNodeColor = new(1f, 0.25f, 0.25f);

        private const float DimmedAlpha = 0.45f;
        private const float OutlineThickness = 3f;
        private const float OutlinePadding = 5f;
        private const float BadgeHeight = 16f;
        private const int MaxBadgesPerNode = 3;
        private const float BaseThickness = 5f;

        private static readonly Dictionary<Node, NodeVisualState> StateCache = new();
        private static readonly Dictionary<EdgeKey, EdgeStyle> EdgeCache = new();
        private static readonly Dictionary<Node, float> HeatCache = new();
        private static readonly Dictionary<int, BaseNode> LastActive = new();
        private static readonly List<KeyValuePair<int, BaseNode>> currentActive = new();
        private static readonly GUIContent BadgeContent = new();

        private static GUIStyle _badgeStyle;
        private static int _cacheVersion = -1;
        private static int? _cacheFilter;
        private static bool _cachePlaying;
        private static bool _cacheShowTrace;
        private static bool _cacheEnabled;
        private static bool _cacheHeatmap;
        private static bool _cacheHeatLog;
        private static Dictionary<NodeGraph, HeatScale> _scales;
        private static int _followedVersion = -1;
        private static int? _instanceFilter;

        static GraphDebugRenderer()
        {
            EditorApplication.update -= FollowTick;
            EditorApplication.update += FollowTick;

            GraphDebuggerSettings.Changed -= RepaintAll;
            GraphDebuggerSettings.Changed += RepaintAll;
            GraphBreakpoints.Changed -= RepaintAll;
            GraphBreakpoints.Changed += RepaintAll;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            var tracer = GraphRuntimeTracer.Instance;
            tracer.BreakHit -= OnBreakHit;
            tracer.BreakHit += OnBreakHit;
        }

        /// Limits the run visuals to one parser record (null = all). Applies to the graph the record runs on.
        public static int? InstanceFilter
        {
            get => _instanceFilter;
            set
            {
                if (_instanceFilter == value) return;

                _instanceFilter = value;
                RepaintAll();
            }
        }

        /// True when run visuals are drawn: the debugger is on, something was recorded, and Play Mode runs or the trace is shown.
        public static bool IsActive =>
            GraphDebuggerSettings.Enabled && (Application.isPlaying || GraphDebuggerSettings.ShowTraceInEditMode)
                                          && GraphRuntimeTracer.Instance.HasTrace;

        public static void RepaintAll()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<NodeEditorWindow>())
            {
                if (window) window.Repaint();
            }
        }

        /// Pure: how a traversed connection is drawn.
        public static EdgeVisual Classify(int count, bool ambiguous, bool leadsToActiveNode)
        {
            if (count <= 0) return EdgeVisual.Untaken;
            if (leadsToActiveNode) return EdgeVisual.Active;
            return ambiguous ? EdgeVisual.TakenAmbiguous : EdgeVisual.Taken;
        }

        #region Node visuals

        /// Idle when the run visuals are off.
        public static NodeVisualState GetState(Node node)
        {
            if (node == null || !IsActive) return NodeVisualState.Idle;

            ValidateCache();
            if (StateCache.TryGetValue(node, out var state)) return state;

            var tracer = GraphRuntimeTracer.Instance;
            state = NodeVisualStateResolver.Resolve(node, tracer, FilterFor(node.graph, tracer), Application.isPlaying,
                GraphDebuggerSettings.ShowTraceInEditMode);
            StateCache[node] = state;
            return state;
        }

        /// The instance filter as it applies to a graph: only graphs the filtered record runs on are limited.
        internal static int? FilterFor(NodeGraph graph) => FilterFor(graph, GraphRuntimeTracer.Instance);

        private static int? FilterFor(NodeGraph graph, GraphRuntimeTracer tracer)
        {
            if (!_instanceFilter.HasValue) return null;
            return tracer.TryGetRecord(_instanceFilter.Value, out var record) && record.Graph == graph ? _instanceFilter : null;
        }

        /// Heatmap on and the run visuals are drawn.
        public static bool HeatmapActive => GraphDebuggerSettings.Heatmap && IsActive;

        /// Heat 0..1 of a node the run went through (visit count against the graph maximum), -1 when the heatmap does not apply to it.
        public static float GetHeat(Node node)
        {
            if (node == null || !HeatmapActive) return -1f;

            ValidateCache();
            if (HeatCache.TryGetValue(node, out var heat)) return heat;

            heat = -1f;
            var count = GraphRuntimeTracer.Instance.GetNode(node).VisitCount;
            if (count > 0 && node.graph != null)
                heat = GraphHeatmap.Normalize(count, ScaleOf(node.graph).MaxNodeVisits, GraphDebuggerSettings.HeatmapLogScale);
            HeatCache[node] = heat;
            return heat;
        }

        private static HeatScale ScaleOf(NodeGraph graph)
        {
            _scales ??= GraphHeatmap.BuildScales(GraphRuntimeTracer.Instance);
            return _scales.TryGetValue(graph, out var scale) ? scale : default;
        }

        private static void ValidateCache()
        {
            var version = GraphRuntimeTracer.Instance.Version;
            var playing = Application.isPlaying;
            var showTrace = GraphDebuggerSettings.ShowTraceInEditMode;
            var enabled = GraphDebuggerSettings.Enabled;
            var heatmap = GraphDebuggerSettings.Heatmap;
            var heatLog = GraphDebuggerSettings.HeatmapLogScale;
            if (version == _cacheVersion && heatmap == _cacheHeatmap && heatLog == _cacheHeatLog && _cacheFilter == _instanceFilter
                && playing == _cachePlaying && showTrace == _cacheShowTrace && enabled == _cacheEnabled) return;

            _cacheVersion = version;
            _cacheFilter = _instanceFilter;
            _cachePlaying = playing;
            _cacheShowTrace = showTrace;
            _cacheEnabled = enabled;
            _cacheHeatmap = heatmap;
            _cacheHeatLog = heatLog;
            _scales = null;
            StateCache.Clear();
            EdgeCache.Clear();
            HeatCache.Clear();
        }

        #endregion

        #region Edge visuals

        /// Style of a traversed/untaken connection, null when the run visuals are off (use the xNode default).
        internal static EdgeStyle GetEdgeStyle(NodeGraphEditor editor, NodePort output, NodePort input)
        {
            if (output == null || input == null || !IsActive) return null;

            using var marker = GraphDebugMarkers.EdgeStyle.Auto();
            ValidateCache();
            var key = new EdgeKey(output, input);
            if (EdgeCache.TryGetValue(key, out var style)) return style;

            var tracer = GraphRuntimeTracer.Instance;
            var edge = tracer.GetEdge(output, input);
            var record = edge.LastRecord;
            var leadsToActive = record is { IsAlive: true } && record.ActiveNode == input.node &&
                                (!_instanceFilter.HasValue || record.Id == _instanceFilter.Value);
            var visual = Classify(edge.Count, edge.Ambiguous, leadsToActive);

            var from = editor.GetTypeColor(output.ValueType);
            var to = editor.GetTypeColor(input.ValueType);
            var heat = -1f;
            if (GraphDebuggerSettings.Heatmap && visual is EdgeVisual.Taken or EdgeVisual.TakenAmbiguous)
            {
                heat = GraphHeatmap.Normalize(edge.Count, ScaleOf(output.node.graph).MaxEdgeCount, GraphDebuggerSettings.HeatmapLogScale);
            }

            style = BuildEdgeStyle(visual, from, to, heat);
            EdgeCache[key] = style;
            return style;
        }

        private static EdgeStyle BuildEdgeStyle(EdgeVisual visual, Color from, Color to, float heat)
        {
            var gradient = new Gradient();
            var thickness = BaseThickness;
            var stroke = NoodleStroke.Full;

            switch (visual)
            {
                case EdgeVisual.Untaken:
                    from.a = to.a = 0.3f;
                    thickness = 4f;
                    break;
                case EdgeVisual.Taken:
                    from = Color.Lerp(from, AccentColor, 0.75f);
                    to = Color.Lerp(to, AccentColor, 0.75f);
                    thickness = 7f;
                    break;
                case EdgeVisual.TakenAmbiguous:
                    from = Color.Lerp(from, AccentColor, 0.6f);
                    to = Color.Lerp(to, AccentColor, 0.6f);
                    thickness = 6f;
                    stroke = NoodleStroke.Dashed;
                    break;
                case EdgeVisual.Active:
                    from = Color.Lerp(from, ActiveEdgeColor, 0.9f);
                    to = ActiveEdgeColor;
                    thickness = 9f;
                    break;
            }

            if (heat >= 0f)
            {
                var heatColor = GraphHeatmap.Evaluate(heat);
                from = Color.Lerp(from, heatColor, 0.85f);
                to = Color.Lerp(to, heatColor, 0.85f);
                thickness = GraphHeatmap.EdgeThickness(heat);
            }

            gradient.SetKeys(
                new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                new[] { new GradientAlphaKey(from.a, 0f), new GradientAlphaKey(to.a, 1f) });
            return new EdgeStyle(gradient, thickness, stroke);
        }

        internal sealed class EdgeStyle
        {
            public readonly Gradient Gradient;
            public readonly float Thickness;
            public readonly NoodleStroke Stroke;

            public EdgeStyle(Gradient gradient, float thickness, NoodleStroke stroke)
            {
                Gradient = gradient;
                Thickness = thickness;
                Stroke = stroke;
            }
        }

        #endregion

        #region Overlays

        /// Outlines and instance badges of the nodes that currently run. Call from a Repaint of the graph window, after the nodes were drawn.
        public static void DrawOverlays(NodeEditorWindow window, NodeGraph graph)
        {
            if (!window || !graph || !IsActive) return;

            var tracer = GraphRuntimeTracer.Instance;
            var view = new Rect(Vector2.zero, window.position.size);
            var animate = false;

            foreach (var node in graph.nodes)
            {
                if (!node) continue;

                var state = GetState(node);
                if (state != NodeVisualState.Active && state != NodeVisualState.PausedHere) continue;
                if (!window.nodeSizes.TryGetValue(node, out var size)) continue;

                var rect = window.GridToWindowRect(new Rect(node.position, size));
                if (!rect.Overlaps(view)) continue;

                var paused = state == NodeVisualState.PausedHere;
                var color = paused ? PausedNodeColor : ActiveNodeColor;
                if (paused)
                {
                    var pulse = 0.5f + 0.5f * Mathf.Cos((float) EditorApplication.timeSinceStartup * Mathf.PI * 3f);
                    color.a = Mathf.Lerp(0.45f, 1f, pulse);
                    animate = true;
                }

                DrawOutline(rect, color);
                DrawBadges(rect, tracer.GetNode(node), color);
            }

            if (animate) window.Repaint();
        }

        private static void DrawOutline(Rect rect, Color color)
        {
            rect.xMin -= OutlinePadding;
            rect.yMin -= OutlinePadding;
            rect.xMax += OutlinePadding;
            rect.yMax += OutlinePadding;

            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, OutlineThickness), color);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMax - OutlineThickness, rect.width, OutlineThickness), color);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, OutlineThickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - OutlineThickness, rect.yMin, OutlineThickness, rect.height), color);
        }

        private static void DrawBadges(Rect nodeRect, NodeTrace trace, Color color)
        {
            _badgeStyle ??= new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(5, 5, 0, 0),
                normal = { textColor = Color.white },
            };

            var drawn = 0;
            var hidden = 0;
            var y = nodeRect.yMin - OutlinePadding - BadgeHeight - 2f;
            var records = trace.ActiveIn;
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                if (!record.IsAlive || _instanceFilter.HasValue && record.Id != _instanceFilter.Value) continue;

                if (drawn >= MaxBadgesPerNode)
                {
                    hidden++;
                    continue;
                }

                BadgeContent.text = trace.VisitCount > 1 && drawn == 0
                    ? $"#{record.Id} {record.Label}  x{trace.VisitCount}"
                    : $"#{record.Id} {record.Label}";
                DrawBadge(new Vector2(nodeRect.xMin, y), color, record.IsPausedAtGate);
                y -= BadgeHeight + 2f;
                drawn++;
            }

            if (hidden <= 0) return;

            BadgeContent.text = $"+{hidden} more";
            DrawBadge(new Vector2(nodeRect.xMin, y), color, false);
        }

        private static void DrawBadge(Vector2 position, Color color, bool paused)
        {
            var width = _badgeStyle.CalcSize(BadgeContent).x;
            var rect = new Rect(position.x, position.y, width, BadgeHeight);
            var background = paused ? PausedNodeColor : color * 0.7f;
            background.a = 0.95f;
            EditorGUI.DrawRect(rect, background);
            GUI.Label(rect, BadgeContent, _badgeStyle);
        }

        #endregion

        #region Follow & break

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            LastActive.Clear();
            _followedVersion = -1;
            RepaintAll();
        }

        private static void OnBreakHit(ParserRecord record, BaseNode node) => ShowNode(record, node);

        private static void FollowTick()
        {
            if (!GraphDebuggerSettings.Enabled) return;

            var tracer = GraphRuntimeTracer.Instance;
            if (tracer.Version == _followedVersion) return;

            using var marker = GraphDebugMarkers.FollowTick.Auto();
            _followedVersion = tracer.Version;
            if (!Application.isPlaying || tracer.IsLoadedTrace) return;

            // The active nodes are remembered whether or not Follow is on, so switching it on never jumps to an old position.
            currentActive.Clear();
            foreach (var record in tracer.Instances)
            {
                if (record.IsAlive && record.ActiveNode != null)
                    currentActive.Add(new KeyValuePair<int, BaseNode>(record.Id, record.ActiveNode));
            }

            var movedId = FollowPolicy.FindMoved(LastActive, currentActive, _instanceFilter);
            if (LastActive.Count > 4000) LastActive.Clear();
            if (!movedId.HasValue || !GraphDebuggerSettings.Follow || !tracer.TryGetRecord(movedId.Value, out var moved)) return;

            ShowNode(moved, moved.ActiveNode);
        }

        /// Opens the node's graph (also when no window shows it), keeps the breadcrumbs and focuses the node. A graph that is not in the
        /// trail yet (dialog, chat, a flow started elsewhere) is pushed on top of it, so Back / Alt+Up returns to where the user was.
        public static void ShowNode(ParserRecord record, BaseNode node)
        {
            if (!node || !node.graph) return;

            switch (GraphNavigation.PlanShow(GraphNavigation.Chain, node.graph, out var index))
            {
                case GraphNavigation.ShowKind.GoBack:
                    GraphNavigation.NavigateTo(index);
                    break;
                case GraphNavigation.ShowKind.Push:
                    var origin = record?.Origin;
                    var tail = GraphNavigation.Chain[^1].graph;
                    var enteredFrom = origin && origin.graph == tail && ReferencesGraph(origin, node.graph)
                        ? origin
                        : GraphReferences.FindReferencingNode(tail, node.graph);
                    GraphNavigation.Push(node.graph, enteredFrom);
                    break;
            }

            GraphBaseEditor.FocusNode(node);
        }

        private static bool ReferencesGraph(Node node, NodeGraph graph)
        {
            foreach (var referenced in GraphReferences.GetReferencedGraphs(node))
            {
                if (referenced == graph) return true;
            }

            return false;
        }

        #endregion

    }
}
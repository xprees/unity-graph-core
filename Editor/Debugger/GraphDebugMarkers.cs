using Unity.Profiling;

namespace Xprees.Graph.Core.Editor.Debugger
{
    /// Profiler markers of the graph editor and debugger hot paths (names start with "GraphEd."), so a Profiler capture shows what a repaint costs.
    internal static class GraphDebugMarkers
    {
        public static readonly ProfilerMarker OnGui = new("GraphEd.GraphBaseEditor.OnGUI");
        public static readonly ProfilerMarker ConnectionPoints = new("GraphEd.EnsureConnectionPointsCached");
        public static readonly ProfilerMarker Overlays = new("GraphEd.DrawOverlays");
        public static readonly ProfilerMarker EdgeTooltip = new("GraphEd.EdgeTooltip");
        public static readonly ProfilerMarker EdgeStyle = new("GraphEd.GetEdgeStyle");
        public static readonly ProfilerMarker NodeHeader = new("GraphEd.Node.OnHeaderGUI");
        public static readonly ProfilerMarker NodeBody = new("GraphEd.Node.OnBodyGUI");
        public static readonly ProfilerMarker BodyFields = new("GraphEd.Node.DrawBodyFields");
        public static readonly ProfilerMarker CollapsedBody = new("GraphEd.Node.DrawCollapsedBody");
        public static readonly ProfilerMarker EnterButtons = new("GraphEd.Node.DrawEnterGraphButtons");
        public static readonly ProfilerMarker Resizing = new("GraphEd.Node.HandleResizing");
        public static readonly ProfilerMarker NodeAttributes = new("GraphEd.Node.GetTargetAssignedAttribute");
        public static readonly ProfilerMarker StateLine = new("GraphEd.Node.BuildRuntimeStateLine");
        public static readonly ProfilerMarker LiveValue = new("GraphEd.Node.DrawLiveValue");
        public static readonly ProfilerMarker MinimapTick = new("GraphEd.Minimap.Tick");
        public static readonly ProfilerMarker MinimapPaint = new("GraphEd.Minimap.Paint");
        public static readonly ProfilerMarker FollowTick = new("GraphEd.FollowTick");
    }
}
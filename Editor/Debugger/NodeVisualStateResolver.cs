using XNode;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Editor.Debugger
{
    public enum NodeVisualState
    {
        Idle,
        NotVisited,
        Visited,
        Active,
        PausedHere,
        Disabled
    }

    /// Decides how a node is drawn while debugging. Pure, so it is testable without any window.
    public static class NodeVisualStateResolver
    {
        /// Idle means no run visuals: in Edit Mode unless the trace is shown, or when nothing was recorded.
        /// The instance filter limits Active, PausedHere and Visited to one record.
        public static NodeVisualState Resolve(Node node, GraphRuntimeTracer tracer, int? instanceFilter, bool playing, bool showTrace)
        {
            if (node == null || tracer == null) return NodeVisualState.Idle;
            if (!playing && !showTrace) return NodeVisualState.Idle;
            if (!tracer.HasTrace) return NodeVisualState.Idle;

            var trace = tracer.GetNode(node);
            var active = false;
            foreach (var record in trace.ActiveIn)
            {
                if (!record.IsAlive || instanceFilter.HasValue && record.Id != instanceFilter.Value) continue;
                if (record.IsPausedAtGate) return NodeVisualState.PausedHere;
                active = true;
            }

            if (active) return NodeVisualState.Active;
            if (node is AsyncStartBaseNode asyncStart && !asyncStart.activeStart) return NodeVisualState.Disabled;

            return IsVisited(node, tracer, trace, instanceFilter) ? NodeVisualState.Visited : NodeVisualState.NotVisited;
        }

        private static bool IsVisited(Node node, GraphRuntimeTracer tracer, NodeTrace trace, int? instanceFilter)
        {
            if (!instanceFilter.HasValue) return trace.VisitCount > 0;
            if (!tracer.TryGetRecord(instanceFilter.Value, out var record)) return false;

            return node is BaseNode baseNode && record.HasVisited(baseNode);
        }
    }
}
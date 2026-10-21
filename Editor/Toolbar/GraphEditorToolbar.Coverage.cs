using XNodeEditor;
using Xprees.Graph.Core.Editor.Debugger;

namespace Xprees.Graph.Core.Editor.Toolbar
{
    /// Coverage text of the debugger status label (the heatmap and coverage options live in the trace menu).
    public static partial class GraphEditorToolbar
    {
        /// Appends "Coverage 7/10 (70%)" for the shown graph to the status text.
        private static string WithCoverage(string status, NodeEditorWindow window, GraphRuntimeTracer tracer)
        {
            if (!window || !window.graph) return status;

            var cumulative = GraphDebuggerSettings.ShowCumulativeCoverage;
            var result = GraphCoverageService.Get(window.graph, tracer, cumulative);
            if (result is not { Total: > 0 } coverage) return status;

            var text = $"Coverage{(cumulative ? " (all runs)" : string.Empty)} {coverage.Visited}/{coverage.Total} ({coverage.Percent:0}%)";
            return string.IsNullOrEmpty(status) ? text : $"{status} | {text}";
        }
    }
}
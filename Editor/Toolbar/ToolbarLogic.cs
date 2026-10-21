using System.Collections.Generic;

namespace Xprees.Graph.Core.Editor.Toolbar
{
    /// Actions of the trace dropdown (eye + chevron compound control) of the debugger toolbar.
    public enum TraceMenuAction
    {
        None,
        ClearTrace,
        ToggleHeatmap,
        ToggleHeatmapLogScale,
        ToggleCumulativeCoverage,
        ResetCoverage,
        SaveTraceAs,
        LoadTrace,
        LoadLastRun,
    }

    /// One row of the trace dropdown. A separator has action None and an empty path. Path uses '/' for submenus.
    public readonly struct TraceMenuEntry
    {
        public readonly TraceMenuAction Action;
        public readonly string Path;
        public readonly bool Enabled;
        public readonly bool Checked;

        public TraceMenuEntry(TraceMenuAction action, string path, bool enabled, bool isChecked)
        {
            Action = action;
            Path = path;
            Enabled = enabled;
            Checked = isChecked;
        }

        public bool IsSeparator => Action == TraceMenuAction.None;
    }

    /// Inputs the trace dropdown depends on, gathered by the toolbar so the model stays pure.
    public readonly struct TraceMenuState
    {
        public readonly bool HasTrace;
        public readonly bool IsPlaying;
        public readonly bool LastRunExists;
        public readonly bool Heatmap;
        public readonly bool HeatmapLogScale;
        public readonly bool CumulativeCoverage;

        public TraceMenuState(bool hasTrace, bool isPlaying, bool lastRunExists, bool heatmap, bool heatmapLogScale, bool cumulativeCoverage)
        {
            HasTrace = hasTrace;
            IsPlaying = isPlaying;
            LastRunExists = lastRunExists;
            Heatmap = heatmap;
            HeatmapLogScale = heatmapLogScale;
            CumulativeCoverage = cumulativeCoverage;
        }
    }

    /// Pure menu model and narrow-mode decision of the graph toolbar (no UI Toolkit, testable in EditMode).
    public static class ToolbarLogic
    {
        /// Window width below which the toolbar shows icons only.
        public const float NarrowWidth = 900f;

        /// Extra width needed to leave narrow mode again, so the layout does not flicker around the threshold.
        public const float NarrowHysteresis = 40f;

        public static bool IsNarrow(float width, bool wasNarrow) => width < (wasNarrow ? NarrowWidth + NarrowHysteresis : NarrowWidth);

        public static List<TraceMenuEntry> BuildTraceMenu(TraceMenuState state)
        {
            // A loaded trace is read-only and a live run records into the same tracer, so loading waits for Play Mode to end
            var canLoad = !state.IsPlaying;
            return new List<TraceMenuEntry>
            {
                new(TraceMenuAction.ClearTrace, "Clear trace", true, false),
                new(TraceMenuAction.None, string.Empty, true, false),
                new(TraceMenuAction.ToggleHeatmap, "Heatmap", true, state.Heatmap),
                new(TraceMenuAction.ToggleHeatmapLogScale, "Coverage/Heatmap log scale", true, state.HeatmapLogScale),
                new(TraceMenuAction.ToggleCumulativeCoverage, "Coverage/Show cumulative coverage", true, state.CumulativeCoverage),
                new(TraceMenuAction.None, "Coverage/", true, false),
                new(TraceMenuAction.ResetCoverage, "Coverage/Reset coverage", true, false),
                new(TraceMenuAction.None, string.Empty, true, false),
                new(TraceMenuAction.SaveTraceAs, "Save trace as...", state.HasTrace, false),
                new(TraceMenuAction.LoadTrace, "Load trace...", canLoad, false),
                new(TraceMenuAction.LoadLastRun, "Load last run", canLoad && state.LastRunExists, false),
            };
        }
    }
}
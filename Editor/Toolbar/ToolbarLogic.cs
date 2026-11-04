using System;
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

        /// Which breadcrumbs of a chain are shown. Visible: the first (if <see cref="ShowFirst"/>), an ellipsis item
        /// for every crumb between it and <see cref="TailStart"/> (if any is hidden), then crumbs TailStart..last.
        public readonly struct BreadcrumbLayout
        {
            public readonly bool ShowFirst;
            public readonly int TailStart;
            public readonly bool TruncateLast;
            private readonly int _count;

            public BreadcrumbLayout(int count, bool showFirst, int tailStart, bool truncateLast)
            {
                _count = count;
                ShowFirst = showFirst;
                TailStart = tailStart;
                TruncateLast = truncateLast;
            }

            public int HiddenFrom => ShowFirst ? 1 : 0;
            public bool HasEllipsis => TailStart > HiddenFrom && _count > 0;
        }

        /// Greedy collapse: full chain, else first + ellipsis + as many trailing crumbs as fit, else ellipsis + trailing crumbs,
        /// else ellipsis + the last crumb cut short. Widths include the separator chrome of each item.
        public static BreadcrumbLayout ComputeBreadcrumbs(IReadOnlyList<float> widths, float ellipsisWidth, float available)
        {
            var n = widths.Count;
            if (n == 0) return new BreadcrumbLayout(0, false, 0, false);

            var total = 0f;
            for (var i = 0; i < n; i++) total += widths[i];
            if (total <= available) return new BreadcrumbLayout(n, true, 1, false);
            if (n == 1) return new BreadcrumbLayout(n, true, 1, true);

            // Hidden crumbs are always replaced by an ellipsis item, so the first is only kept when it fits next to it
            var withFirst = widths[0] + ellipsisWidth;
            var tail = GreedyTail(widths, withFirst, available);
            if (tail >= 2) return new BreadcrumbLayout(n, true, tail, false);

            tail = GreedyTail(widths, ellipsisWidth, available);
            if (tail >= 0) return new BreadcrumbLayout(n, false, Math.Max(tail, 1), false);

            return new BreadcrumbLayout(n, false, n - 1, true);
        }

        /// First index of the trailing run that fits after <paramref name="used"/>, or -1 when not even the last crumb fits.
        private static int GreedyTail(IReadOnlyList<float> widths, float used, float available)
        {
            var start = widths.Count;
            while (start > 0 && used + widths[start - 1] <= available)
            {
                used += widths[start - 1];
                start--;
            }

            return start == widths.Count ? -1 : start;
        }

        /// Keeps <paramref name="keep"/> characters of the name around a middle ellipsis ("bigLaunch_…nooping").
        public static string MiddleEllipsis(string text, int keep)
        {
            if (string.IsNullOrEmpty(text) || keep >= text.Length) return text;
            if (keep <= 0) return "…";

            var head = (keep + 1) / 2;
            var tailLength = keep - head;
            return text.Substring(0, head) + "…" + text.Substring(text.Length - tailLength);
        }

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
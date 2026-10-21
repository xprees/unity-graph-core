using System;
using UnityEditor;

namespace Xprees.Graph.Core.Editor.Debugger
{
    /// Editor-wide switches of the graph debugger, stored in EditorPrefs. With Enabled off the tracer is not registered and nothing is recorded.
    public static class GraphDebuggerSettings
    {
        private const string Prefix = "Xprees.GraphDebugger.";
        private const string EnabledKey = Prefix + "Enabled";
        private const string FreezeKey = Prefix + "FreezeWorldOnBreak";
        private const string FollowKey = Prefix + "Follow";
        private const string HeatmapKey = Prefix + "Heatmap";
        private const string HeatmapLogKey = Prefix + "HeatmapLogScale";
        private const string CumulativeKey = Prefix + "ShowCumulativeCoverage";
        private const string ShowTraceKey = Prefix + "ShowTraceInEditMode";

        private static bool? _enabled;
        private static bool? _freezeWorldOnBreak;
        private static bool? _follow;
        private static bool? _heatmap;
        private static bool? _showTraceInEditMode;
        private static bool? _heatmapLogScale;
        private static bool? _showCumulativeCoverage;

        public static event Action Changed;

        public static bool Enabled
        {
            get => Get(ref _enabled, EnabledKey, false);
            set => Set(ref _enabled, EnabledKey, value);
        }

        /// Pauses the Editor when a break is hit, so the world does not keep running while the flow is held.
        public static bool FreezeWorldOnBreak
        {
            get => Get(ref _freezeWorldOnBreak, FreezeKey, true);
            set => Set(ref _freezeWorldOnBreak, FreezeKey, value);
        }

        public static bool Follow
        {
            get => Get(ref _follow, FollowKey, false);
            set => Set(ref _follow, FollowKey, value);
        }

        public static bool Heatmap
        {
            get => Get(ref _heatmap, HeatmapKey, false);
            set => Set(ref _heatmap, HeatmapKey, value);
        }

        public static bool HeatmapLogScale
        {
            get => Get(ref _heatmapLogScale, HeatmapLogKey, true);
            set => Set(ref _heatmapLogScale, HeatmapLogKey, value);
        }

        /// Coverage counts the nodes any earlier run visited (UserSettings/GraphCoverage.asset), not only the current trace.
        public static bool ShowCumulativeCoverage
        {
            get => Get(ref _showCumulativeCoverage, CumulativeKey, false);
            set => Set(ref _showCumulativeCoverage, CumulativeKey, value);
        }

        public static bool ShowTraceInEditMode
        {
            get => Get(ref _showTraceInEditMode, ShowTraceKey, false);
            set => Set(ref _showTraceInEditMode, ShowTraceKey, value);
        }

        private static bool Get(ref bool? cache, string key, bool defaultValue)
        {
            cache ??= EditorPrefs.GetBool(key, defaultValue);
            return cache.Value;
        }

        private static void Set(ref bool? cache, string key, bool value)
        {
            if (cache == value) return;

            cache = value;
            EditorPrefs.SetBool(key, value);
            Changed?.Invoke();
        }
    }
}
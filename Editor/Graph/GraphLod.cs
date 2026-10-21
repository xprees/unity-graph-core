using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using XNodeEditor;

namespace Xprees.Graph.Core.Editor.Graph
{
    /// Zoomed-out level of detail: when the view is zoomed out past the threshold, nodes draw as cards (header, ports, state visuals)
    /// without their field bodies. Selected nodes always draw in full. xNode draws every visible node body with IMGUI, which is the
    /// main cost of a big graph seen from far away.
    public static class GraphLod
    {
        public const float DefaultThreshold = 1.6f;
        private const string enabledKey = "Xprees.GraphLod.Enabled";
        private const string thresholdKey = "Xprees.GraphLod.Threshold";

        public static event Action Changed;

        // EditorPrefs is a registry read on Windows, the per-node path reads these cached values
        private static bool? _enabled;
        private static float? _threshold;

        public static bool Enabled
        {
            get => _enabled ??= EditorPrefs.GetBool(enabledKey, true);
            set
            {
                if (Enabled == value) return;

                _enabled = value;
                EditorPrefs.SetBool(enabledKey, value);
                Changed?.Invoke();
                NodeEditorWindow.RepaintAll();
            }
        }

        /// xNode zoom is the scale of the visible grid (1 = 100 %, higher = further out).
        public static float Threshold
        {
            get => _threshold ??= EditorPrefs.GetFloat(thresholdKey, DefaultThreshold);
            set
            {
                _threshold = value;
                EditorPrefs.SetFloat(thresholdKey, value);
                Changed?.Invoke();
                NodeEditorWindow.RepaintAll();
            }
        }

        /// Pure rule: a node draws as a card when the level of detail is on, the view is zoomed out past the threshold and the node is not selected.
        public static bool ShouldCollapse(bool enabled, float zoom, float threshold, bool selected) => enabled && !selected && zoom > threshold;

        public static ToolbarToggle CreateToolbarToggle()
        {
            var toggle = new ToolbarToggle
            {
                text = "LOD",
                tooltip =
                    "Zoomed-out level of detail.\nWhen zoomed out, nodes draw as cards without their field bodies (selected nodes stay full). Makes big graphs much faster to draw.",
                value = Enabled,
            };
            INotifyValueChangedExtensions.RegisterValueChangedCallback(toggle, evt => Enabled = evt.newValue);
            return toggle;
        }
    }
}
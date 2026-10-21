using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Xprees.Variables.Base;

namespace Xprees.Graph.Core.Editor.Debugger
{
    /// Live variable values shown in node bodies: samples the runtime value, detects changes and drives the change flash.
    /// Only active while playing with the debugger on, so nothing here runs otherwise.
    public static class LiveValues
    {
        public const double FlashDuration = 0.5;
        private const double SampleInterval = 0.05;
        private const double DrawnRecentlyWindow = 1.0;

        private struct Entry
        {
            public string Text;
            public double ChangedAt;
            public double SampledAt;
        }

        private static readonly Dictionary<VariableBaseSO, Entry> Entries = new();
        private static double _lastDrawTime = double.NegativeInfinity;

        /// Live values are drawn: the debugger is on, the game runs and the trace is not a loaded (read-only) one.
        public static bool IsActive => GraphDebuggerSettings.Enabled && Application.isPlaying && !GraphRuntimeTracer.Instance.IsLoadedTrace;

        /// A node body drew a live value recently, so the window keeps repainting to show changes.
        public static bool NeedsRepaint => IsActive && EditorApplication.timeSinceStartup - _lastDrawTime < DrawnRecentlyWindow;

        /// Something live was drawn, the window keeps repainting for a moment.
        public static void Touch() => _lastDrawTime = EditorApplication.timeSinceStartup;

        public static void Reset()
        {
            Entries.Clear();
            _lastDrawTime = double.NegativeInfinity;
        }

        /// 1 right after a change, fading to 0 over <see cref="FlashDuration"/>.
        public static float FlashAmount(double now, double changedAt)
        {
            var elapsed = now - changedAt;
            if (elapsed < 0 || elapsed >= FlashDuration) return 0f;

            return (float) (1.0 - elapsed / FlashDuration);
        }

        /// Current value text and the flash amount of a variable, null when the value cannot be read.
        public static string Sample(VariableBaseSO variable, out float flash)
        {
            flash = 0f;
            if (!variable) return null;

            var now = EditorApplication.timeSinceStartup;
            _lastDrawTime = now;

            Entries.TryGetValue(variable, out var entry);
            var known = entry.Text != null;
            if (known && now - entry.SampledAt < SampleInterval)
            {
                flash = FlashAmount(now, entry.ChangedAt);
                return entry.Text;
            }

            if (!VariableAccess.TryRead(variable, out var value)) return null;

            var text = VariableAccess.Format(value);
            if (known && text != entry.Text) entry.ChangedAt = now;
            entry.Text = text;
            entry.SampledAt = now;
            Entries[variable] = entry;

            flash = FlashAmount(now, entry.ChangedAt);
            return text;
        }
    }
}
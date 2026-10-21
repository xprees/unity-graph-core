using System.Collections.Generic;
using System.Globalization;
using System.Text;
using XNode;
using Xprees.Graph.Core.Debugging;

namespace Xprees.Graph.Core.Editor.Debugger
{
    /// Pure text builders of the debugger UI (node state line, edge tooltip, history rows), testable without any window.
    public static class DebuggerText
    {
        /// Generic runtime-state line under a node title: phase and time in node (only while a flow sits on it), visits, last port, provider text.
        public static string NodeState(
            GraphParserPhase? activePhase,
            double now,
            double enteredTime,
            int visits,
            string lastPortName,
            string providerText
        )
        {
            var builder = new StringBuilder();
            if (activePhase.HasValue)
            {
                Append(builder, $"{PhaseText(activePhase.Value)} {Seconds(now - enteredTime)}");
            }

            if (visits > 0) Append(builder, $"×{visits}");
            if (!string.IsNullOrEmpty(lastPortName)) Append(builder, $"→ {lastPortName}");
            if (!string.IsNullOrEmpty(providerText)) Append(builder, providerText);
            return builder.ToString();
        }

        /// Tooltip of a connection: how often and when it was taken, and by whom.
        public static string EdgeTooltip(int count, int exactCount, double lastTime, double now, string recordLabel)
        {
            if (count <= 0) return "not taken";

            var text = $"taken {count}×, last {Seconds(now - lastTime)} ago";
            if (!string.IsNullOrEmpty(recordLabel)) text += $" ({recordLabel})";
            if (exactCount <= 0) text += " [inferred]";
            return text;
        }

        public static string HistoryRow(in HistoryEntry entry, string nodeName, double now)
        {
            var edge = entry.EdgeOutput != null
                ? $"  via {entry.EdgeOutput.fieldName}{(entry.EdgeAmbiguous ? "?" : string.Empty)}"
                : string.Empty;
            return $"{Seconds(now - entry.Time)} ago  #{entry.RecordId}  {nodeName}{edge}";
        }

        /// Entries of one graph, optionally of one parser record, oldest first. The newest <paramref name="maxCount"/> are kept.
        public static List<HistoryEntry> FilterHistory(
            IReadOnlyList<HistoryEntry> history,
            NodeGraph graph,
            int? recordId,
            int maxCount = int.MaxValue
        )
        {
            var result = new List<HistoryEntry>();
            if (history == null) return result;

            for (var i = history.Count - 1; i >= 0 && result.Count < maxCount; i--)
            {
                var entry = history[i];
                if (graph != null && entry.Graph != graph) continue;
                if (recordId.HasValue && entry.RecordId != recordId.Value) continue;
                result.Add(entry);
            }

            result.Reverse();
            return result;
        }

        /// Shortens text to at most maxChars characters by replacing its middle with an ellipsis, so the start and the end stay readable.
        public static string MiddleEllipsis(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars) return text;
            if (maxChars <= 1) return "…";

            var keep = maxChars - 1;
            var head = (keep + 1) / 2;
            var tail = keep / 2;
            return text.Substring(0, head) + "…" + text.Substring(text.Length - tail);
        }

        private static string PhaseText(GraphParserPhase phase) => phase switch
        {
            GraphParserPhase.Triggering => "running",
            GraphParserPhase.ResolvingNext => "resolving",
            GraphParserPhase.AtGate => "held",
            GraphParserPhase.Ended => "ended",
            _ => "idle",
        };

        private static string Seconds(double seconds) => seconds < 0 ? "0.0 s" : seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";

        private static void Append(StringBuilder builder, string part)
        {
            if (builder.Length > 0) builder.Append("  |  ");
            builder.Append(part);
        }
    }
}
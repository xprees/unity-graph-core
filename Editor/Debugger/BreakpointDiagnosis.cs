using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using XNode;
using Xprees.Graph.Core.Base;
using Xprees.Graph.Core.Debugging;

namespace Xprees.Graph.Core.Editor.Debugger
{
    /// Why a breakpoint would or would not stop a run. Pure decision on a snapshot, so it is unit tested.
    public static class BreakpointDiagnosis
    {
        public readonly struct Inputs
        {
            public readonly bool DebuggerEnabled;
            public readonly bool HasBreakpoint;
            public readonly bool BreakpointEnabled;
            public readonly bool BreaksSuppressed;
            public readonly bool HasPersistentId;
            public readonly bool GraphRunning;

            public Inputs(
                bool debuggerEnabled,
                bool hasBreakpoint,
                bool breakpointEnabled,
                bool breaksSuppressed,
                bool hasPersistentId,
                bool graphRunning
            )
            {
                DebuggerEnabled = debuggerEnabled;
                HasBreakpoint = hasBreakpoint;
                BreakpointEnabled = breakpointEnabled;
                BreaksSuppressed = breaksSuppressed;
                HasPersistentId = hasPersistentId;
                GraphRunning = graphRunning;
            }
        }

        public enum Verdict
        {
            WouldFire,
            DebuggerOff,
            NoBreakpoint,
            BreakpointDisabled,
            BreaksSuppressed,
        }

        /// Blocking reasons in the order the tracer applies them. Not saved / not running never block, they are notes.
        public static Verdict Evaluate(in Inputs inputs)
        {
            if (!inputs.DebuggerEnabled) return Verdict.DebuggerOff;
            if (!inputs.HasBreakpoint) return Verdict.NoBreakpoint;
            if (!inputs.BreakpointEnabled) return Verdict.BreakpointDisabled;
            if (inputs.BreaksSuppressed) return Verdict.BreaksSuppressed;
            return Verdict.WouldFire;
        }

        public static string Explain(Verdict verdict) => verdict switch
        {
            Verdict.WouldFire => "The breakpoint WOULD fire when a parser enters this node.",
            Verdict.DebuggerOff => "Debugger is off - turn on Debug in the toolbar.",
            Verdict.NoBreakpoint => "This node has no breakpoint.",
            Verdict.BreakpointDisabled =>
                "The breakpoint is disabled - Alt+click (or Ctrl/Shift+click) the dot or use Enable Breakpoint in the node menu.",
            Verdict.BreaksSuppressed => "Breaks are suppressed right now (run from node or a similar tool holds them off).",
            _ => string.Empty,
        };

        public static List<string> Notes(in Inputs inputs)
        {
            var notes = new List<string>();
            if (inputs.HasBreakpoint && !inputs.HasPersistentId)
            {
                notes.Add("Node is not saved yet (no persistent id): the breakpoint lasts for this session only. Save the graph asset.");
            }

            if (!inputs.GraphRunning) notes.Add("No parser runs this graph right now: it fires once a scenario or dialog executes this node.");
            return notes;
        }

        public static Inputs Capture(Node node)
        {
            var graphRunning = false;
            if (node != null && node.graph is GraphBase graph)
            {
                foreach (var _ in GraphRuntimeTracer.Instance.InstancesOf(graph))
                {
                    graphRunning = true;
                    break;
                }
            }

            return new Inputs(GraphDebuggerSettings.Enabled, GraphBreakpoints.Has(node), GraphBreakpoints.IsEnabled(node),
                GraphDebugHooks.BreaksSuppressed, GraphBreakpoints.HasPersistentId(node), graphRunning);
        }

        public static string Report(Node node)
        {
            var inputs = Capture(node);
            var builder = new StringBuilder();
            builder.Append("[Breakpoint dry run] ").Append(node ? node.name : "(no node)").Append(": ").Append(Explain(Evaluate(inputs)));
            foreach (var note in Notes(inputs)) builder.Append("\n  - ").Append(note);
            return builder.ToString();
        }

        /// One-line hint for the dot tooltip when something blocks the breakpoint, or null when all is fine.
        public static string HoverHint(Node node)
        {
            var verdict = Evaluate(Capture(node));
            return verdict is Verdict.DebuggerOff or Verdict.BreakpointDisabled or Verdict.BreaksSuppressed ? Explain(verdict) : null;
        }

        [MenuItem("Tools/Graph/Breakpoints/Test selected node breakpoints (dry run)")]
        public static void DryRunSelection()
        {
            var nodes = Selection.GetFiltered<Node>(SelectionMode.Unfiltered);
            if (nodes.Length == 0)
            {
                Debug.Log("[Breakpoint dry run] Select a node in a graph first.");
                return;
            }

            foreach (var node in nodes) Debug.Log(Report(node), node);
        }
    }
}
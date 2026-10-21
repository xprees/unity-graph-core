using System.Linq;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using XNode;
using XNodeEditor;

namespace Xprees.Graph.Core.Editor.Debugger
{
    /// Rebindable shortcuts of the debugger (Edit > Shortcuts... > "Xprees/Graph"), active when the node editor window is focused.
    public static class GraphDebugShortcuts
    {
        public const string ToggleBreakpointId = "Xprees/Graph/Toggle Breakpoint";

        [Shortcut(ToggleBreakpointId, typeof(NodeEditorWindow), KeyCode.F9)]
        private static void ToggleBreakpoint() => ToggleOn(Selection.GetFiltered<Node>(SelectionMode.Unfiltered));

        /// Adds a breakpoint to all nodes when any of them has none, otherwise removes them all.
        public static void ToggleOn(Node[] nodes)
        {
            if (nodes == null || nodes.Length == 0) return;

            var addAll = nodes.Any(node => node && !GraphBreakpoints.Has(node));
            foreach (var node in nodes)
            {
                if (!node) continue;

                if (addAll) GraphBreakpoints.SetEnabled(node, true);
                else GraphBreakpoints.Remove(node);
            }
        }
    }
}
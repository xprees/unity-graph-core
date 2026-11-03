using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using XNodeEditor;

namespace Xprees.Graph.Core.Editor.Layout
{
    /// Rebindable shortcut of the layout commands (Edit > Shortcuts... > "Xprees/Graph"), active when the node editor window is focused.
    /// Shift+L, not Shift+A: xNode's own key handling reacts to A with any modifier (select all / deselect all).
    public static class GraphLayoutShortcuts
    {
        public const string ArrangeId = "Xprees/Graph/Arrange Selection";

        [Shortcut(ArrangeId, typeof(NodeEditorWindow), KeyCode.L, ShortcutModifiers.Shift)]
        private static void Arrange(ShortcutArguments args) =>
            GraphLayoutCommands.Run(args.context as NodeEditorWindow ?? EditorWindow.focusedWindow as NodeEditorWindow, LayoutCommand.Arrange);

        /// Second binding of the same command, Ctrl+Alt+L (Cmd+Alt+L on macOS), next to Shift+L. Rebindable on its own.
        public const string ArrangeAltId = "Xprees/Graph/Arrange Selection (Ctrl+Alt+L)";

        [Shortcut(ArrangeAltId, typeof(NodeEditorWindow), KeyCode.L, ShortcutModifiers.Action | ShortcutModifiers.Alt)]
        private static void ArrangeAlt(ShortcutArguments args) => Arrange(args);

        /// Current bindings for menu labels, e.g. "Shift+L, Ctrl+Alt+L", or empty when both are unbound.
        public static string ArrangeBinding
        {
            get
            {
                var bindings = new List<string>();
                foreach (var id in new[] { ArrangeId, ArrangeAltId })
                {
                    try
                    {
                        var binding = ShortcutManager.instance.GetShortcutBinding(id).ToString();
                        if (binding != "None") bindings.Add(binding);
                    }
                    catch (ArgumentException)
                    {
                        // Not registered (yet): nothing to show
                    }
                }

                return string.Join(", ", bindings);
            }
        }
    }
}
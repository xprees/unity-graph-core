using System;
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

        /// Current binding for menu labels, e.g. "Shift+L", or empty when unbound.
        public static string ArrangeBinding
        {
            get
            {
                try
                {
                    var binding = ShortcutManager.instance.GetShortcutBinding(ArrangeId).ToString();
                    return binding == "None" ? string.Empty : binding;
                }
                catch (ArgumentException)
                {
                    return string.Empty;
                }
            }
        }
    }
}
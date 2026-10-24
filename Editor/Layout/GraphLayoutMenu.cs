using UnityEditor;
using UnityEngine;
using XNodeEditor;

namespace Xprees.Graph.Core.Editor.Layout
{
    /// The layout commands as GenericMenu items (node context menu, toolbar dropdown).
    public static class GraphLayoutMenu
    {
        /// Adds every layout command under <paramref name="prefix"/> (e.g. "Layout/"). The window is captured now, menus run later.
        public static void AddItems(GenericMenu menu, NodeEditorWindow window, string prefix)
        {
            var binding = GraphLayoutShortcuts.ArrangeBinding;
            foreach (var command in LayoutCommandNames.All)
            {
                var path = LayoutCommandNames.MenuPath(command);
                if (command == LayoutCommand.Arrange && binding.Length > 0) path += $"  ({binding})";

                var captured = command;
                menu.AddItem(new GUIContent(prefix + path), false, () => GraphLayoutCommands.Run(window, captured));
                if (command == LayoutCommand.Arrange) menu.AddSeparator(prefix);
            }
        }
    }
}
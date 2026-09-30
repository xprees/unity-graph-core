using UnityEditor.ShortcutManagement;
using UnityEngine;
using XNodeEditor;

namespace Xprees.Graph.Core.Editor.Navigation
{
    /// Rebindable shortcuts of the graph navigation (Edit > Shortcuts... > "Xprees/Graph").
    public static class GraphNavigationShortcuts
    {
        public const string GoToParentId = "Xprees/Graph/Go To Parent Graph";

        /// Goes back up to the parent graph of the breadcrumbs. Active only when the node editor window is focused.
        [Shortcut(GoToParentId, typeof(NodeEditorWindow), KeyCode.UpArrow, ShortcutModifiers.Alt)]
        private static void GoToParent() => GraphNavigation.GoToParent();
    }
}
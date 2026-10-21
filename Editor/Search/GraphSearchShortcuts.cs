using UnityEditor.ShortcutManagement;
using UnityEngine;
using XNodeEditor;

namespace Xprees.Graph.Core.Editor.Search
{
    /// Rebindable shortcuts of the graph search (Edit > Shortcuts... > "Xprees/Graph"), active when the node editor window is focused.
    public static class GraphSearchShortcuts
    {
        public const string FindId = "Xprees/Graph/Find In Graph";
        public const string FindNextId = "Xprees/Graph/Find Next";
        public const string FindPreviousId = "Xprees/Graph/Find Previous";

        [Shortcut(FindId, typeof(NodeEditorWindow), KeyCode.F, ShortcutModifiers.Action)]
        private static void Find(ShortcutArguments args) => GraphSearchOverlay.Show(args.context as NodeEditorWindow);

        [Shortcut(FindNextId, typeof(NodeEditorWindow), KeyCode.F3)]
        private static void FindNext(ShortcutArguments args) => GraphSearchOverlay.Step(args.context as NodeEditorWindow, 1);

        [Shortcut(FindPreviousId, typeof(NodeEditorWindow), KeyCode.F3, ShortcutModifiers.Shift)]
        private static void FindPrevious(ShortcutArguments args) => GraphSearchOverlay.Step(args.context as NodeEditorWindow, -1);
    }
}
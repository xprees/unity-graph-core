using System.Linq;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using XNode;
using XNodeEditor;
using Xprees.Graph.Core.Editor.Graph;

namespace Xprees.Graph.Core.Editor.Bookmarks
{
    /// Rebindable shortcuts of the node bookmarks (Edit > Shortcuts... > "Xprees/Graph"): Ctrl+Shift+1..9 sets, Ctrl+1..9 jumps.
    /// Contextual to the node editor window, so they win over the global Ctrl+1..9 window layout shortcuts while it is focused.
    public static class GraphBookmarkShortcuts
    {
        private const ShortcutModifiers setModifiers = ShortcutModifiers.Action | ShortcutModifiers.Shift;

        [Shortcut("Xprees/Graph/Set Bookmark 1", typeof(NodeEditorWindow), KeyCode.Alpha1, setModifiers)]
        private static void Set1(ShortcutArguments a) => SetBookmark(a, 1);

        [Shortcut("Xprees/Graph/Set Bookmark 2", typeof(NodeEditorWindow), KeyCode.Alpha2, setModifiers)]
        private static void Set2(ShortcutArguments a) => SetBookmark(a, 2);

        [Shortcut("Xprees/Graph/Set Bookmark 3", typeof(NodeEditorWindow), KeyCode.Alpha3, setModifiers)]
        private static void Set3(ShortcutArguments a) => SetBookmark(a, 3);

        [Shortcut("Xprees/Graph/Set Bookmark 4", typeof(NodeEditorWindow), KeyCode.Alpha4, setModifiers)]
        private static void Set4(ShortcutArguments a) => SetBookmark(a, 4);

        [Shortcut("Xprees/Graph/Set Bookmark 5", typeof(NodeEditorWindow), KeyCode.Alpha5, setModifiers)]
        private static void Set5(ShortcutArguments a) => SetBookmark(a, 5);

        [Shortcut("Xprees/Graph/Set Bookmark 6", typeof(NodeEditorWindow), KeyCode.Alpha6, setModifiers)]
        private static void Set6(ShortcutArguments a) => SetBookmark(a, 6);

        [Shortcut("Xprees/Graph/Set Bookmark 7", typeof(NodeEditorWindow), KeyCode.Alpha7, setModifiers)]
        private static void Set7(ShortcutArguments a) => SetBookmark(a, 7);

        [Shortcut("Xprees/Graph/Set Bookmark 8", typeof(NodeEditorWindow), KeyCode.Alpha8, setModifiers)]
        private static void Set8(ShortcutArguments a) => SetBookmark(a, 8);

        [Shortcut("Xprees/Graph/Set Bookmark 9", typeof(NodeEditorWindow), KeyCode.Alpha9, setModifiers)]
        private static void Set9(ShortcutArguments a) => SetBookmark(a, 9);

        [Shortcut("Xprees/Graph/Go To Bookmark 1", typeof(NodeEditorWindow), KeyCode.Alpha1, ShortcutModifiers.Action)]
        private static void Go1(ShortcutArguments a) => JumpTo(a, 1);

        [Shortcut("Xprees/Graph/Go To Bookmark 2", typeof(NodeEditorWindow), KeyCode.Alpha2, ShortcutModifiers.Action)]
        private static void Go2(ShortcutArguments a) => JumpTo(a, 2);

        [Shortcut("Xprees/Graph/Go To Bookmark 3", typeof(NodeEditorWindow), KeyCode.Alpha3, ShortcutModifiers.Action)]
        private static void Go3(ShortcutArguments a) => JumpTo(a, 3);

        [Shortcut("Xprees/Graph/Go To Bookmark 4", typeof(NodeEditorWindow), KeyCode.Alpha4, ShortcutModifiers.Action)]
        private static void Go4(ShortcutArguments a) => JumpTo(a, 4);

        [Shortcut("Xprees/Graph/Go To Bookmark 5", typeof(NodeEditorWindow), KeyCode.Alpha5, ShortcutModifiers.Action)]
        private static void Go5(ShortcutArguments a) => JumpTo(a, 5);

        [Shortcut("Xprees/Graph/Go To Bookmark 6", typeof(NodeEditorWindow), KeyCode.Alpha6, ShortcutModifiers.Action)]
        private static void Go6(ShortcutArguments a) => JumpTo(a, 6);

        [Shortcut("Xprees/Graph/Go To Bookmark 7", typeof(NodeEditorWindow), KeyCode.Alpha7, ShortcutModifiers.Action)]
        private static void Go7(ShortcutArguments a) => JumpTo(a, 7);

        [Shortcut("Xprees/Graph/Go To Bookmark 8", typeof(NodeEditorWindow), KeyCode.Alpha8, ShortcutModifiers.Action)]
        private static void Go8(ShortcutArguments a) => JumpTo(a, 8);

        [Shortcut("Xprees/Graph/Go To Bookmark 9", typeof(NodeEditorWindow), KeyCode.Alpha9, ShortcutModifiers.Action)]
        private static void Go9(ShortcutArguments a) => JumpTo(a, 9);

        private static void SetBookmark(ShortcutArguments args, int slot)
        {
            var window = args.context as NodeEditorWindow;
            if (!window || !window.graph) return;

            var node = Selection.GetFiltered<Node>(SelectionMode.Unfiltered).FirstOrDefault(n => n && n.graph == window.graph);
            if (!node)
            {
                window.ShowNotification(new GUIContent("Select a node to bookmark"), 1.5);
                return;
            }

            var done = GraphBookmarks.Set(window.graph, slot, node);
            window.ShowNotification(new GUIContent(done ? $"Bookmark {slot} set: {node.name}" : "Save the graph asset to bookmark nodes"), 1.5);
        }

        private static void JumpTo(ShortcutArguments args, int slot)
        {
            var window = args.context as NodeEditorWindow;
            if (!window || !window.graph) return;

            var node = GraphBookmarks.Get(window.graph, slot);
            if (!node)
            {
                window.ShowNotification(new GUIContent($"Bookmark {slot} is empty"), 1.5);
                return;
            }

            GraphBaseEditor.FocusNode(node);
        }
    }
}
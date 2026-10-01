using System.Linq;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using XNode;
using XNodeEditor;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Editor.Refactor
{
    /// Entry points of the "Extract to SubGraph" refactor: node context menu and shortcut.
    public static class SubGraphExtraction
    {
        public const string ShortcutId = "Xprees/Graph/Extract To SubGraph";
        private const string menuLabel = "Extract to SubGraph...";

        /// Adds the menu item when a refactorable selection exists (2+ nodes or a group). Call from the context menu of a node.
        public static void AddMenuItem(GenericMenu menu, Node clicked)
        {
            if (!clicked || !clicked.graph) return;

            var selection = GetSelectedNodes(clicked.graph);
            if (!selection.Contains(clicked)) return; // The menu belongs to a node outside of the selection

            if (selection.Length < 2 && selection.All(n => n is not NodeGroup)) return;

            menu.AddSeparator("");
            menu.AddItem(new GUIContent(menuLabel), false, () => Begin(clicked.graph, selection));
        }

        public static void Begin(NodeGraph graph, Node[] selection)
        {
            // Opening a window from the middle of the node editor's GUI pass is not safe
            EditorApplication.delayCall += () => SubGraphExtractionWindow.Open(graph, selection);
        }

        public static Node[] GetSelectedNodes(NodeGraph graph) =>
            Selection.objects.OfType<Node>().Where(n => n && n.graph == graph).ToArray();

        [Shortcut(ShortcutId, typeof(NodeEditorWindow), KeyCode.G, ShortcutModifiers.Action | ShortcutModifiers.Alt)]
        private static void ExtractFromShortcut()
        {
            var window = NodeEditorWindow.current;
            if (!window || !window.graph) return;

            var selection = GetSelectedNodes(window.graph);
            if (selection.Length == 0) return;

            Begin(window.graph, selection);
        }
    }
}
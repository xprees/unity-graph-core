using UnityEditor;
using UnityEngine;
using Xprees.Graph.Core.Editor.Flow;

namespace Xprees.Graph.Core.Editor.Nodes
{
    /// Node context menu entries that select connected nodes.
    public partial class BaseNodeEditor
    {
        private void AddFlowSelectionContextMenuItems(GenericMenu menu)
        {
            var node = target;
            var editorWindow = window;
            menu.AddItem(new GUIContent("Select Connected Nodes"), false, () => FlowSelection.SelectConnected(node, editorWindow));
            menu.AddItem(new GUIContent("Select Following Nodes (this -> end)"), false,
                () => FlowSelection.SelectFollowing(node, editorWindow));
            menu.AddSeparator(string.Empty);
        }
    }
}
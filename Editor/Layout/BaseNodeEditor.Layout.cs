using UnityEditor;
using Xprees.Graph.Core.Editor.Layout;

namespace Xprees.Graph.Core.Editor.Nodes
{
    /// Node context menu entries of the layout commands (they act on the whole selection).
    public partial class BaseNodeEditor
    {
        private void AddLayoutContextMenuItems(GenericMenu menu)
        {
            menu.AddSeparator(string.Empty);
            GraphLayoutMenu.AddItems(menu, window, "Layout/");
        }
    }
}
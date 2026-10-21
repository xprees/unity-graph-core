using UnityEditor;

namespace Xprees.Graph.Core.Editor.Graph
{
    /// xNode selects the node under the mouse in its controls, before the breakpoint dot handler of the graph editor runs. This keeps the
    /// selection from the last Layout event (every mouse event is preceded by one), so a click on the dot can undo that selection change
    /// (Ctrl/Shift+click toggles it too). A drag from the dot keeps xNode's selection, the node is dragged as usual.
    internal static class DotSelectionGuard
    {
        /// Selection as of the last Layout event, before the current mouse event reached xNode.
        public static int[] LastSnapshot { get; private set; } = new int[0];

        /// Call on the Layout event of every graph editor OnGUI.
        public static void Snapshot() => LastSnapshot = Selection.instanceIDs;

        public static void Restore(int[] ids)
        {
            if (ids != null) Selection.instanceIDs = ids;
        }
    }
}
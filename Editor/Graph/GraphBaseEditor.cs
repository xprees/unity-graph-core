using XNodeEditor;
using Xprees.Graph.Core.Base;

namespace Xprees.Graph.Core.Editor.Graph
{
    [CustomNodeGraphEditor(typeof(GraphBase))]
    public class GraphBaseEditor : NodeGraphEditor
    {
        private static int lastTargetHash;

        public override void OnGUI()
        {
            base.OnGUI();

            var currentTargetHash = target.GetHashCode();
            if (lastTargetHash == currentTargetHash) return;
            lastTargetHash = currentTargetHash;

            OnGraphChange();
        }

        protected virtual void OnGraphChange() => ResetViewPositionOnSwitchIfNoNodesVisible();

        private void ResetViewPositionOnSwitchIfNoNodesVisible() => NodeEditorWindow.current.Home();
    }
}
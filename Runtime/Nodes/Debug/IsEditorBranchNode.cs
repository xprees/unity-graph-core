using Xprees.Graph.Core.Attributes;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Nodes.Debug
{
    /// This node has branch functionality based on whether the application is running in the Unity Editor.
    [NodeDescription("Branches based on whether the application is running in the Unity Editor.")]
    [NodeTint("#00aaff")]
    [NodeResizableWidth(250, 1000)]
    [CreateNodeMenu("Debug/IsEditor Branch")]
    public class IsEditorBranchNode : BranchBaseNode
    {
        protected override bool GetConditionResult()
        {
#if UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }
}
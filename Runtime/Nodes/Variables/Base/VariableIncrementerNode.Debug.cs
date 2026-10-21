#if UNITY_EDITOR
using Xprees.Graph.Core.Debugging;
using Xprees.Graph.Core.Nodes.DebugState;

namespace Xprees.Graph.Core.Nodes.Variables.Base
{
    public abstract partial class VariableIncrementerBaseNode<T> : IGraphNodeDebugState
    {
        public string GetDebugState(in NodeDebugContext ctx)
        {
            if (variable == null) return "no variable";

            var by = incrementBy == null ? "none" : NodeDebugFormat.Val(incrementBy.Value);
            return $"{variable.name} = {NodeDebugFormat.Val(variable.CurrentValue)}, increments by {by}";
        }
    }
}
#endif
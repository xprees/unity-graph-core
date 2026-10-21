#if UNITY_EDITOR
using Xprees.Graph.Core.Debugging;
using Xprees.Graph.Core.Nodes.DebugState;

namespace Xprees.Graph.Core.Nodes.Wait.Variables.Base
{
    public abstract partial class WaitUntilVariableValueBaseNode<T> : IGraphNodeDebugState
    {
        public string GetDebugState(in NodeDebugContext ctx)
        {
            if (variable == null) return "no variable";

            var expected = value == null ? "none" : NodeDebugFormat.Val(value.Value);
            return $"{variable.name} = {NodeDebugFormat.Val(variable.CurrentValue)}, expected {expected}";
        }
    }

    public abstract partial class WaitForVariableChangedBaseNode<T> : IGraphNodeDebugState
    {
        public string GetDebugState(in NodeDebugContext ctx) =>
            variable == null ? "no variable" : $"watching {variable.name} = {NodeDebugFormat.Val(variable.CurrentValue)}";
    }
}
#endif
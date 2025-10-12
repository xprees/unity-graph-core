using Xprees.Graph.Core.Attributes;
using Xprees.Graph.Core.Nodes.Variables.Base;

namespace Xprees.Graph.Core.Nodes.Variables.Incrementing
{
    [NodeResizableWidth(300)]
    [CreateNodeMenu("Variables/Int Increment")]
    public class IntIncrementerNode : VariableIncrementerBaseNode<int>
    {
        protected override int GetDefaultIncrementValue() => 1;

        protected override int GetIncrementedValue(int currentValue, int increment) => currentValue + increment;
    }
}
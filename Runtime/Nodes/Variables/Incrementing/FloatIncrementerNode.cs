using Xprees.Graph.Core.Attributes;
using Xprees.Graph.Core.Nodes.Variables.Base;

namespace Xprees.Graph.Core.Nodes.Variables.Incrementing
{
    [NodeResizableWidth(300)]
    [CreateNodeMenu("Variables/Float Increment")]
    public class FloatIncrementerNode : VariableIncrementerBaseNode<float>
    {
        protected override float GetDefaultIncrementValue() => 1;

        protected override float GetIncrementedValue(float currentValue, float increment) => currentValue + increment;
    }
}
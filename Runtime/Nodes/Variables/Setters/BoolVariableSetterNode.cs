using Xprees.Graph.Core.Attributes;
using Xprees.Graph.Core.Nodes.Variables.Base;

namespace Xprees.Graph.Core.Nodes.Variables.Setters
{
    [NodeResizableWidth(300)]
    [CreateNodeMenu("Variables/Bool Setter")]
    public class BoolVariableSetterNode : VariablesSetterBaseNode<bool>
    {
    }
}
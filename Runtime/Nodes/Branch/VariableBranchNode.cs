using Xprees.Graph.Core.Base.Nodes;
using Xprees.Variables.Primitive;

namespace Xprees.Graph.Core.Nodes.Branch
{
    [CreateNodeMenu("Branch/VariableBranch")]
    public class VariableBranchNode : BranchBaseNode
    {
        public BoolVariable boolVariable;

        protected override bool GetConditionResult() => boolVariable.CurrentValue;

        public override void ResetState()
        {
            base.ResetState();
            boolVariable?.ResetState();
        }
    }
}
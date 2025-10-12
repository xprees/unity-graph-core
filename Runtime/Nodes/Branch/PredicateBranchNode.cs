using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Nodes.Branch.SerializedCallbacks;

namespace Xprees.Graph.Core.Nodes.Branch
{
    [CreateNodeMenu("Branch/PredicateBranch")]
    public class PredicateBranchNode : BranchBaseNode
    {
        public SerializablePredicate predicate;

        protected override bool GetConditionResult() => predicate.Invoke();
    }
}
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Tests
{
    public class TestBranchNode : BranchBaseNode
    {
        public bool result;

        protected override bool GetConditionResult() => result;
    }
}
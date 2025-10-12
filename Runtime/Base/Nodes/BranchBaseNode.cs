using System.Threading;
using Cysharp.Threading.Tasks;
using Xprees.Graph.Core.Attributes;

namespace Xprees.Graph.Core.Base.Nodes
{
    [NodeResizableWidth(300)]
    public abstract class BranchBaseNode : BaseNode, IPassthroughNode
    {
        [Input] public GraphConnection input;

        [Output(connectionType = ConnectionType.Override)]
        public GraphConnection pass;

        [Output(connectionType = ConnectionType.Override)]
        public GraphConnection fail;

        protected override UniTask<BaseNode> GetNextNode(CancellationToken cancellationToken = default)
        {
            var port = GetOutputPort(GetConditionResult() ? nameof(pass) : nameof(fail));
            return new UniTask<BaseNode>(port.Connection?.node as BaseNode);
        }

        protected abstract bool GetConditionResult();
    }
}
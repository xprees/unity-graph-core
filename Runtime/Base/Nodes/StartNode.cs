using System.Threading;
using Cysharp.Threading.Tasks;

namespace Xprees.Graph.Core.Base.Nodes
{
    [NodeTint("#03a345")]
    [NodeWidth(150)]
    [DisallowMultipleNodes]
    [CreateNodeMenu("Start", -1)]
    public class StartNode : BaseNode, IPassthroughNode
    {
        [Output(connectionType = ConnectionType.Override)]
        public GraphConnection start;

        protected override UniTask<BaseNode> GetNextNode(CancellationToken cancellationToken = default) =>
            new(GetOutputPort(nameof(start)).Connection.node as BaseNode);
    }
}
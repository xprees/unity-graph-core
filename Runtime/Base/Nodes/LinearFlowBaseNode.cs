using System.Threading;
using Cysharp.Threading.Tasks;

namespace Xprees.Graph.Core.Base.Nodes
{
    /// BaseNode with single input and single output and implemented GetNextNode method.
    public abstract class LinearFlowBaseNode : BaseNode
    {
        [Input] public GraphConnection input;

        [Output(connectionType = ConnectionType.Override)]
        public GraphConnection output;

        protected override UniTask<BaseNode> GetNextNode(CancellationToken cancellationToken = default) =>
            new(GetOutputPort(nameof(output)).Connection.node as BaseNode);
    }
}
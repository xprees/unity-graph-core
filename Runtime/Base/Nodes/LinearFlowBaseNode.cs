using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Xprees.Graph.Core.Base.Nodes
{
    /// BaseNode with single input and single output and implemented GetNextNode method.
    public abstract class LinearFlowBaseNode : BaseNode
    {
        [Input] public GraphConnection input;

        [Output(connectionType = ConnectionType.Override)]
        public GraphConnection output;

        protected override UniTask<BaseNode> GetNextNode(CancellationToken cancellationToken = default)
        {
            var connectionNode = GetOutputPort(nameof(output))?.Connection?.node as BaseNode;
#if UNITY_EDITOR
            if (connectionNode == null)
            {
                Debug.LogError($"Output port of node {name} from {graph.name} graph is not connected to any node.", this);
            }

#endif
            return new UniTask<BaseNode>(connectionNode);
        }
    }
}
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Xprees.Graph.Core.Base.Nodes
{
    [NodeTint("#009C8B")]
    [NodeWidth(350)]
    [CreateNodeMenu("SubGraph", 0)]
    public class SubGraphNode : LinearFlowBaseNode, IPassthroughNode, ITraverseGraphMixin, ICancellableFlowOwner
    {
        [Header("SubGraph")]
        public GraphBase subGraph;

        protected override UniTask Trigger(CancellationToken cancellationToken = default) =>
            (this as ITraverseGraphMixin).TraverseGraph(subGraph, cancellationToken: cancellationToken);

        public override void ResetState()
        {
            base.ResetState();
        }

        public void CancelFlows()
        {
            subGraph?.CancelActiveFlows();
        }
    }
}
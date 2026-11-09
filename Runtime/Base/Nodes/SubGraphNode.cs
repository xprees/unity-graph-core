using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Xprees.Core;

namespace Xprees.Graph.Core.Base.Nodes
{
    [NodeTint("#009C8B")]
    [NodeWidth(350)]
    [CreateNodeMenu("SubGraph", 0)]
    public partial class SubGraphNode : LinearFlowBaseNode, IPassthroughNode, ITraverseGraphMixin, ICancellableFlowOwner,
        IRuntimeStateOwner
    {
        [Header("SubGraph")]
        public GraphBase subGraph;

        /// Run from node: the next Trigger starts the sub-graph at this node (and triggers it) instead of the Start node. Consumed once.
        [NonSerialized] private BaseNode _oneShotStart;

        /// Makes the next Trigger start the sub-graph at the given node, which must be part of the sub-graph. Pass null to clear.
        public void SetOneShotStart(BaseNode node) => _oneShotStart = node;

        protected override UniTask Trigger(CancellationToken cancellationToken = default)
        {
            var start = _oneShotStart;
            _oneShotStart = null;
            return (this as ITraverseGraphMixin).TraverseGraph(subGraph, start, cancellationToken, start != null);
        }

        public void CancelFlows()
        {
            subGraph?.CancelActiveFlows();
        }

        /// The reset of the parent graph is the only thing that reaches the nodes of a sub-graph.
        public void ClearTransientState()
        {
            if (subGraph) subGraph.ClearTransientState();
        }
    }
}
#if UNITY_EDITOR
using System;
using Xprees.Graph.Core.Debugging;

namespace Xprees.Graph.Core.Base.Nodes
{
    public abstract partial class AsyncStartBaseNode : IGraphNodeDebugState
    {
        [NonSerialized] private int _liveFlows;

        /// Editor-only count of flows currently running from this start node.
        public int LiveFlowCount => _liveFlows;

        // Explicit so concrete start nodes can still provide their own public GetDebugState.
        string IGraphNodeDebugState.GetDebugState(in NodeDebugContext ctx)
        {
            bool active = activeStart;
            return $"activeStart: {active}, live flows: {_liveFlows}";
        }

        /// Increments the live flow counter for the lifetime of one Trigger call.
        private readonly struct LiveFlowScope : IDisposable
        {
            private readonly AsyncStartBaseNode _node;

            public LiveFlowScope(AsyncStartBaseNode node)
            {
                _node = node;
                node._liveFlows++;
            }

            public void Dispose() => _node._liveFlows--;
        }
    }
}
#endif
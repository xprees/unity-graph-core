using System.Collections.Generic;
using XNode;
using Xprees.Core;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Debugging;

namespace Xprees.Graph.Core.Base
{
    [StatefulLifetime(StateLifetime.Scenario)]
    [RequireNode(typeof(StartNode), typeof(EndNode))]
    public class GraphBase : NodeGraph, IRuntimeStateOwner
    {
        private readonly HashSet<GraphParserBase> _activeParsers = new();
        public bool IsActive => _activeParsers.Count > 0;

        /// Sub-graphs cascade flow cancellation back into GraphBase, so a shared or cyclic
        /// sub-graph would otherwise recurse without end.
        private bool _isCancellingFlows;

        /// Call this method before your start working with the graph.
        /// Activates and initializes the graph, if it already wasn't initialized.
        public void Activate(GraphParserBase parserCtx)
        {
            if (_activeParsers.Count <= 0)
            {
                Initialize();
            }

            _activeParsers.Add(parserCtx);
        }

        /// Deactivates graph and uninitializes if no other parsers are running on this graph.
        public void Deactivate(GraphParserBase parserCtx)
        {
            _activeParsers.Remove(parserCtx);

            if (_activeParsers.Count <= 0)
            {
                Uninitialize();
            }
        }

        /// Initialize all components, which will be needed for active state.
        /// Activates only with first parser!
        protected virtual void Initialize()
        {
        }

        /// Deinitializes all components used in graph.
        /// Deactivates with last active parser!
        protected virtual void Uninitialize()
        {
        }

        /// Same guard as <see cref="_isCancellingFlows"/>, for the transient state cascade.
        private bool _isClearingTransientState;

        /// Clears all transient in-flight parser sessions from this graph and the transient state of its nodes and sub-graphs.
        /// Nodes are not part of the state registry (they are stateless for StateSnapshotService), so the graph is the only one that
        /// reaches them. Without it a node keeps its flags from the previous run, e.g. a wait that already saw its event.
        public virtual void ClearTransientState()
        {
            if (_isClearingTransientState) return;

            _activeParsers.Clear();
#if UNITY_EDITOR
            GraphDebugHooks.Observer?.OnGraphCleared(this);
#endif
            if (nodes == null) return;

            try
            {
                _isClearingTransientState = true;
                foreach (var node in nodes)
                {
                    if (node is IRuntimeStateOwner stateOwner) stateOwner.ClearTransientState();
                }
            }
            finally
            {
                _isClearingTransientState = false;
            }
        }

        /// Cancels every asynchronous flow still in flight in this graph and its sub-graphs.
        /// Async start nodes run their own parser, so stopping the main parser does not stop them.
        public void CancelActiveFlows()
        {
            if (_isCancellingFlows || nodes == null) return;

            try
            {
                _isCancellingFlows = true;
                foreach (var node in nodes)
                {
                    if (node is ICancellableFlowOwner flowOwner) flowOwner.CancelFlows();
                }
            }
            finally
            {
                _isCancellingFlows = false;
            }
        }
    }
}
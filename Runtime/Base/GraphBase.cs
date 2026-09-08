using System.Collections.Generic;
using XNode;
using Xprees.Core;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Base
{
    [StatefulLifetime(StateLifetime.Scenario)]
    [RequireNode(typeof(StartNode), typeof(EndNode))]
    public class GraphBase : NodeGraph, IResettable
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

        /// Resets all temporary data on graph to default state.
        public virtual void ResetState()
        {
            _activeParsers.Clear();
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
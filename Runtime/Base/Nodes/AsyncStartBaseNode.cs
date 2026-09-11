using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Xprees.Core;
using Xprees.Graph.Core.Attributes;
using Xprees.Variables.Reference.Primitive;

namespace Xprees.Graph.Core.Base.Nodes
{
    /// This node is used to react to the asynchronous events and start the graph asynchronous execution to the main graph flow.
    /// We allow multiple async start points per graph, in contrast to StartNode, which must be exactly one.
    [NodeResizableWidth(200)]
    [NodeTint("#03a345")]
    public abstract class AsyncStartBaseNode : BaseNode, IPassthroughNode, ITraverseGraphMixin, ICancellableFlowOwner, IRuntimeStateOwner
    {
        [Output(connectionType = ConnectionType.Override)]
        public GraphConnection start;

        [Tooltip("If true, the node is active it accepts the events and starts the graph execution."
                 + " If false, the node is inactive and does not accept the events.")]
        public BoolReference activeStart = new(true); // User-driven node activation

        /// Kill switch shared by every flow started from this node. It is never used to cancel a
        /// previous flow - the event this node listens to can legitimately fire while an earlier
        /// flow is still running, and those flows must not tear each other down.
        private CancellationTokenSource _nodeCts;

        /// Starts the graph execution from GetNextNode result or fallbacks to this node when the event is raised.
        protected override async sealed UniTask Trigger(CancellationToken cancellationToken = default)
        {
            if (!IsActive) return; // If the node itself is not active, we ignore the trigger -> in inactive graph.
            if (!activeStart) return; // If the user decided to disable the start node, we ignore the trigger.

            _nodeCts ??= new CancellationTokenSource();
            var nodeToken = _nodeCts.Token; // captured before awaiting - CancelFlows() may replace _nodeCts

            using var flowCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, nodeToken);
            var token = flowCts.Token;

            var startNode = await GetNextNode(token);
            await (this as ITraverseGraphMixin)
                .TraverseGraph(graph as GraphBase, startNode ?? this, token);
        }

        /// Starts the graph execution from this node when the event is raised.
        protected virtual void StartFlow() => Trigger().Forget();

        protected override UniTask<BaseNode> GetNextNode(CancellationToken cancellationToken = default)
        {
            // If the node is inactive, we return null to stop the graph execution.
            if (!activeStart) return UniTask.FromResult<BaseNode>(null);

            return UniTask.FromResult(GetOutputPort(nameof(start)).Connection?.node as BaseNode);
        }

        #region Housekeeping

        protected override void Init()
        {
            base.Init();
            SetupEvents();
        }

        protected virtual void OnDisable()
        {
            CleanupEvents();
            CancelFlows();
        }

        /// Use this method to set up any event subscriptions or listeners (called in Init aka onEnable)
        protected virtual void SetupEvents()
        {
            // Subscribe to events here if needed.
        }

        /// Use this method to clean up any event subscriptions or listeners (usually called onDisable)
        protected virtual void CleanupEvents()
        {
            // Unsubscribe from events here if needed.
        }

        public virtual void ClearTransientState()
        {
            CancelFlows();
        }

        public override void ResetState()
        {
            ClearTransientState();
        }

        /// Cancels every flow currently in flight from this node. The next trigger lazily
        /// creates a fresh kill switch, so the node stays usable afterwards.
        public void CancelFlows()
        {
            if (_nodeCts == null) return;

            _nodeCts.Cancel();
            _nodeCts.Dispose();
            _nodeCts = null;
        }

        #endregion

    }
}
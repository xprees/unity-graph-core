using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Xprees.Graph.Core.Attributes;
using Xprees.Variables.Reference.Primitive;

namespace Xprees.Graph.Core.Base.Nodes
{
    /// This node is used to react to the asynchronous events and start the graph asynchronous execution to main graph flow.
    /// We allow multiple async start points per graph, in contrast to StartNode which must be exactly one.
    [NodeResizableWidth(200)]
    [NodeTint("#03a345")]
    public abstract class AsyncStartBaseNode : BaseNode, IPassthroughNode, ITraverseGraphMixin
    {
        [Output(connectionType = ConnectionType.Override)]
        public GraphConnection start;

        [Tooltip("If true, the node is active it accepts the events and starts the graph execution."
                 + " If false, the node is inactive and does not accept the events.")]
        public BoolReference activeStart = new(true); // User driven node activation

        private CancellationTokenSource _cts;

        /// Starts the graph execution from GetNextNode result or fallbacks to this node when the event is raised.
        protected override async sealed UniTask Trigger(CancellationToken cancellationToken = default)
        {
            if (!IsActive) return; // If the node itself is not active, we ignore the trigger -> in inactive graph.
            if (!activeStart) return; // If the user decided to disable the start node, we ignore the trigger.

            SetupCts();
            var startNode = await GetNextNode(_cts.Token);
            await (this as ITraverseGraphMixin)
                .TraverseGraph(graph as GraphBase, startNode ?? this, _cts.Token);
            CleanupCts();
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
            CleanupCts();
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

        public override void BackupStartState()
        {
            base.BackupStartState();
            activeStart?.BackupStartState();
        }

        public override void ResetState()
        {
            base.ResetState();
            activeStart?.ResetState();
            CleanupCts();
        }

        private void SetupCts()
        {
            CleanupCts();
            _cts = new CancellationTokenSource();
        }

        private void CleanupCts()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        #endregion

    }
}
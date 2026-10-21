using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using XNode;
using Xprees.Graph.Core.Debugging;

namespace Xprees.Graph.Core.Base.Nodes
{
    public abstract class BaseNode : Node
    {

        #region Editor properties

        [HideInInspector]
        [SerializeField] public int width; // Serialize to save the width of the node.

        #endregion

        /// Returns true if node is in active graph. If false, node should not do anything.
        protected bool IsActive => (graph as GraphBase)?.IsActive ?? false;

        /// Move to next node and trigger it.
        /// Returns next node or null, if there is no next node or node is not active.
        public async UniTask<BaseNode> MoveNext(CancellationToken cancellationToken = default)
        {
            if (!IsActive) return null; // Node is not active, do not trigger it and don't move to next node.

            var next = await ResolveNextInternal(cancellationToken);
            if (next) await next.TriggerInternal(cancellationToken);

            return next;
        }

        /// Resolves the next node, keeps the active graph guard of <see cref="MoveNext"/>. Lets the parser observe the steps of MoveNext separately.
        internal UniTask<BaseNode> ResolveNextInternal(CancellationToken cancellationToken)
        {
            if (!IsActive) return UniTask.FromResult<BaseNode>(null);

#if UNITY_EDITOR
            GraphDebugHooks.ClearPendingPort(this); // look-ahead callers leave stale reports behind
#endif
            return GetNextNode(cancellationToken);
        }

        internal UniTask TriggerInternal(CancellationToken cancellationToken) => Trigger(cancellationToken);

        /// Returns the node connected to the port and reports the port to the graph debugger. Use it in GetNextNode of branching nodes.
        protected BaseNode FollowPort(NodePort port)
        {
            GraphDebugHooks.ReportPort(this, port);
            return port?.Connection?.node as BaseNode;
        }

        public override object GetValue(NodePort port) => null; // xNode needs this to be implemented.

        /// Override to implement logic, invoked on node enter.
        /// <param name="cancellationToken"></param>
        protected virtual UniTask Trigger(CancellationToken cancellationToken = default) => UniTask.CompletedTask;

        /// Override to implement logic for getting the next node.
        /// Can be asynchronous if needed, so you can await operations (wait for user input etc.) before returning the next node.
        protected abstract UniTask<BaseNode> GetNextNode(CancellationToken cancellationToken = default);
    }
}
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using XNode;
using Xprees.Core;

namespace Xprees.Graph.Core.Base.Nodes
{
    public abstract class BaseNode : Node, IResettable
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

            var next = await GetNextNode(cancellationToken);
            if (next != null)
            {
                await next.Trigger(cancellationToken);
            }

            return next;
        }

        public override object GetValue(NodePort port) => null; // xNode needs this to be implemented.

        /// Override to implement logic, invoked on node enter.
        /// <param name="cancellationToken"></param>
        protected virtual UniTask Trigger(CancellationToken cancellationToken = default) => UniTask.CompletedTask;

        /// Override to implement logic for getting the next node.
        /// Can be asynchronous if needed, so you can await operations (wait for user input etc.) before returning the next node.
        protected abstract UniTask<BaseNode> GetNextNode(CancellationToken cancellationToken = default);

        protected override void Init()
        {
            base.Init();
            BackupStartState();
        }

        /// Use it to back-up start state when needed.
        /// Called when the node is initialized.
        public virtual void BackupStartState()
        {
        }

        public virtual void ResetState()
        {
        }
    }
}
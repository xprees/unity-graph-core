using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Xprees.Core;
using Xprees.Graph.Core.Attributes;
using Xprees.Variables.Reference.Primitive;

namespace Xprees.Graph.Core.Base.Nodes
{
    /// This node is used to reset the state of the provided objects to their initial state when triggered.
    /// It can be used to reset the state of any object that implements the `IResettable` interface.
    [NodeDescription("Reset the state of the provided objects to their initial state when triggered.")]
    [NodeResizableWidth(200)]
    [NodeTint("#777ffe")]
    [CreateNodeMenu("State/Reset State")]
    public class ResetStateNode : LinearFlowBaseNode, IPassthroughNode
    {
        public BoolReference activate = new(true);

        [Header("Resettable Objects")]
        [Tooltip("Objects to reset when the node is triggered. They must implement the IResettable interface.")]
        [SerializeField]
        private ScriptableObject[] objectsToReset;

        protected override UniTask Trigger(CancellationToken cancellationToken = default)
        {
            if (!activate.Value) return UniTask.CompletedTask;
            if (objectsToReset == null || objectsToReset.Length == 0) return UniTask.CompletedTask;

            // Reset the state of each object that implements IResettable
            foreach (var scriptableObject in objectsToReset)
            {
                if (scriptableObject is IResettable resettable)
                {
                    resettable.ResetState();
                }
            }

            return UniTask.CompletedTask;
        }

        public override void BackupStartState()
        {
            base.BackupStartState();
            activate?.BackupStartState();
        }

        public override void ResetState()
        {
            base.ResetState();
            activate?.ResetState();
        }
    }
}
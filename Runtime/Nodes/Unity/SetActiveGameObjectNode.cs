using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.RuntimeAnchors;
using Xprees.Variables.Reference.Primitive;

namespace Xprees.Graph.Core.Nodes.Unity
{
    [CreateNodeMenu("Unity/SetActive gameObject")]
    public class SetActiveGameObjectNode : LinearFlowBaseNode, IPassthroughNode
    {
        [Header("GameObject")]
        public GameObjectAnchor gameObjectAnchor;

        public BoolReference active = new();

        protected override UniTask Trigger(CancellationToken cancellationToken = default)
        {
            if (gameObjectAnchor != null) gameObjectAnchor.Value.SetActive(active.Value);

            return UniTask.CompletedTask;
        }

        public override void BackupStartState()
        {
            base.BackupStartState();
            active?.BackupStartState();
        }

        public override void ResetState()
        {
            gameObjectAnchor?.ResetState();
            active?.ResetState();
        }
    }
}
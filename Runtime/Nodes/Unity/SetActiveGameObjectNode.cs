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
            // The object can be gone already (shredded, destroyed with its scene): nothing to switch then
            var target = gameObjectAnchor ? gameObjectAnchor.Value : null;
            if (target) target.SetActive(active.Value);

            return UniTask.CompletedTask;
        }
    }
}
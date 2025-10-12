using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Xprees.Graph.Core.Attributes;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.RuntimeAnchors;
using Xprees.Variables.Reference.Primitive;

namespace Xprees.Graph.Core.Nodes.Unity
{
    [NodeDescription("This node spawns a prefab at a transform specified by spawnAnchor.")]
    [NodeTint("#062F51")]
    [NodeResizableWidth(350)]
    [CreateNodeMenu("Unity/Spawn Prefab")]
    public class SpawnPrefabNode : LinearFlowBaseNode, IPassthroughNode
    {
        [Header("Spawning")]
        [SerializeField] private TransformAnchor spawnPositionAnchor;

        [SerializeField] private GameObject prefab;

        [Space]
        [Tooltip("If true, the prefab will only be spawned once.")]
        [SerializeField] private bool spawnOnlyOnce = false;

        [Tooltip("Reference to a bool variable that will be set to true when the prefab is spawned.")]
        [SerializeField] private BoolReference prefabSpawned = new(false);


        protected override UniTask Trigger(CancellationToken cancellationToken = default)
        {
            if (prefab == null || spawnPositionAnchor == null || !spawnPositionAnchor.isSet) return base.Trigger(cancellationToken);
            if (spawnOnlyOnce && prefabSpawned) return base.Trigger(cancellationToken); // Prevents spawning multiple times

            var t = spawnPositionAnchor.Value;
            Instantiate(prefab, t.position, t.rotation, t);
            prefabSpawned.Value = true;

            return base.Trigger(cancellationToken);
        }

        public override void BackupStartState()
        {
            base.BackupStartState();
            prefabSpawned?.BackupStartState();
        }

        public override void ResetState()
        {
            base.ResetState();
            prefabSpawned?.ResetState();
            spawnPositionAnchor?.ResetState(); // Reset the anchor state
        }
    }
}
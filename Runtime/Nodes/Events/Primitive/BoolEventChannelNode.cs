using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Xprees.Graph.Core.Nodes.Events.Base;
using Xprees.Variables.Reference.Primitive;

namespace Xprees.Graph.Core.Nodes.Events.Primitive
{
    [NodeWidth(350)]
    [CreateNodeMenu("Events/Primitive/Bool Event")]
    public class BoolEventChannelNode : EventChannelBaseNode<bool>
    {
        [Header("Value")]
        public BoolReference valueReference = new();

        protected override UniTask<bool> GetEventData(CancellationToken cancellationToken = default) => new(valueReference.Value);

        public override void BackupStartState()
        {
            base.BackupStartState();
            valueReference?.BackupStartState();
        }

        public override void ResetState()
        {
            base.ResetState();
            valueReference?.ResetState();
        }
    }
}
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Xprees.Graph.Core.Nodes.Events.Base;
using Xprees.Variables.Reference.Primitive;

namespace Xprees.Graph.Core.Nodes.Events.Primitive
{
    [NodeWidth(350)]
    [CreateNodeMenu("Events/Primitive/Int Event")]
    public class IntEventChannelNode : EventChannelBaseNode<int>
    {
        [Header("Value to send")]
        public IntReference valueReference = new();

        protected override UniTask<int> GetEventData(CancellationToken cancellationToken = default) => new(valueReference.Value);

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
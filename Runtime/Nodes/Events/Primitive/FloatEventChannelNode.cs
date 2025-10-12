using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Xprees.Graph.Core.Nodes.Events.Base;
using Xprees.Variables.Reference.Primitive;

namespace Xprees.Graph.Core.Nodes.Events.Primitive
{
    [NodeWidth(350)]
    [CreateNodeMenu("Events/Primitive/Float Event")]
    public class FloatEventChannelNode : EventChannelBaseNode<float>
    {
        [Header("Value to send")]
        public FloatReference valueReference = new(0);

        protected override UniTask<float> GetEventData(CancellationToken cancellationToken = default) => new(valueReference.Value);

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
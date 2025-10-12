using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Xprees.Graph.Core.Nodes.Events.Base;
using Xprees.Variables.Reference.Primitive;

namespace Xprees.Graph.Core.Nodes.Events.Primitive
{
    [NodeWidth(350)]
    [CreateNodeMenu("Events/Primitive/String Event")]
    public class StringEventChannelNode : EventChannelBaseNode<string>
    {
        [Header("Value to send")]
        public StringReference text = new();

        protected override UniTask<string> GetEventData(CancellationToken cancellationToken = default) => new(text?.Value);

        public override void BackupStartState()
        {
            base.BackupStartState();
            text?.BackupStartState();
        }

        public override void ResetState()
        {
            base.ResetState();
            text?.ResetState();
        }
    }
}
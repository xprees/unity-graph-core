using System.Threading;
using Cysharp.Threading.Tasks;
using Xprees.Events.ScriptableObjects.Base;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Nodes.Events.Primitive
{
    [NodeTint("#5e6cae")]
    [NodeWidth(300)]
    [CreateNodeMenu("Events/Void Event")]
    public class VoidEventChannelNode : LinearFlowBaseNode, IPassthroughNode
    {
        public VoidEventChannelSO eventChannel;

        protected override UniTask Trigger(CancellationToken cancellationToken = default)
        {
            if (eventChannel != null) eventChannel.RaiseEvent();

            return UniTask.CompletedTask;
        }

        public override void ResetState()
        {
            base.ResetState();
            eventChannel?.ResetState();
        }
    }
}
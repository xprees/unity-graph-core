using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Xprees.Events.ScriptableObjects.Base;
using Xprees.Graph.Core.Attributes;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Nodes.Events.Base
{
    [NodeResizableWidth(300)]
    public abstract class EventChannelBaseNode<T> : LinearFlowBaseNode, IPassthroughNode
    {
        public EventChannelBaseSO<T> eventChannel;

        protected override async UniTask Trigger(CancellationToken cancellationToken = default)
        {
            if (eventChannel == null) return;
            eventChannel.RaiseEvent(await GetEventData(cancellationToken));
        }

        protected abstract UniTask<T> GetEventData(CancellationToken cancellationToken = default);

        public override void ResetState()
        {
            base.ResetState();
            eventChannel?.ResetState();
        }
    }

    [NodeResizableWidth(300)]
    public abstract class EventChannelBaseNode<T1, T2> : LinearFlowBaseNode, IPassthroughNode
    {
        public EventChannelBaseSO<T1, T2> eventChannel;

        protected override async UniTask Trigger(CancellationToken cancellationToken = default)
        {
            if (eventChannel == null) return;
            var (t1, t2) = await GetEventData(cancellationToken);
            eventChannel.RaiseEvent(t1, t2);
        }

        protected abstract UniTask<Tuple<T1, T2>> GetEventData(CancellationToken cancellationToken = default);

        public override void ResetState()
        {
            base.ResetState();
            eventChannel?.ResetState();
        }
    }

    [NodeResizableWidth(300)]
    public abstract class EventChannelBaseNode<T1, T2, T3> : LinearFlowBaseNode, IPassthroughNode
    {
        public EventChannelBaseSO<T1, T2, T3> eventChannel;

        protected override async UniTask Trigger(CancellationToken cancellationToken = default)
        {
            if (eventChannel == null) return;
            var (t1, t2, t3) = await GetEventData(cancellationToken);
            eventChannel.RaiseEvent(t1, t2, t3);
        }

        protected abstract UniTask<Tuple<T1, T2, T3>> GetEventData(CancellationToken cancellationToken = default);

        public override void ResetState()
        {
            base.ResetState();
            eventChannel?.ResetState();
        }
    }
}
using Xprees.Events.ScriptableObjects.Base;
using Xprees.Graph.Core.Attributes;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Nodes.Start
{
    [NodeDescription("Starts the flow when a Void event is raised.")]
    [CreateNodeMenu("Async Start/Void Event Start", order = -1)]
    public class VoidEventAsyncStartNode : AsyncStartBaseNode
    {
        public VoidEventChannelSO eventChannel;

        protected override void SetupEvents()
        {
            base.SetupEvents();
            if (eventChannel) eventChannel.onEventRaised += StartFlow;
        }

        protected override void CleanupEvents()
        {
            base.CleanupEvents();
            if (eventChannel) eventChannel.onEventRaised -= StartFlow;
        }

        public override void ResetState()
        {
            base.ResetState();
            eventChannel?.ResetState();
        }

#if UNITY_EDITOR
        private void OnValidate() => SetBetterNodeName();

        private void SetBetterNodeName()
        {
            const string defaultNodeName = "Void Event Async Start";
            if (name != defaultNodeName) return;
            if (!eventChannel) return;

            name = $"On {eventChannel.name}";
        }
#endif
    }
}
using UnityEngine;
using Xprees.Events.ScriptableObjects.Base;
using Xprees.Graph.Core.Attributes;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Nodes.Wait.Events
{
    [NodeDescription("Wait for a void event to be raised. "
                     + "This node will trigger when the specified event is raised, allowing the graph to continue execution.")]
    [CreateNodeMenu("Wait/Wait on Void Event")]
    public class WaitOnVoidEventNode : WaitOnEventBaseNode
    {
        [Header("Listens to")]
        public VoidEventChannelSO eventChannel;

        protected override void SetupEvents()
        {
            base.SetupEvents();
            if (eventChannel) eventChannel.onEventRaised += OnEventRaised;
        }

        protected override void CleanupEvents()
        {
            base.CleanupEvents();
            if (eventChannel) eventChannel.onEventRaised -= OnEventRaised;
        }

        private new void OnEventRaised() => base.OnEventRaised();

        public override void ResetState()
        {
            base.ResetState();
            eventChannel?.ResetState();
        }
    }
}
using UnityEngine;
using Xprees.Events.ScriptableObjects.Unity;
using Xprees.Graph.Core.Attributes;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Nodes.Start
{
    [NodeDescription("Starts the flow when a GameObject event is raised.")]
    [CreateNodeMenu("Async Start/GameObject Event Start", order = -1)]
    public class GameObjectEventAsyncStartNode : AsyncStartBaseNode
    {
        public GameObjectEventChannelSO eventChannel;

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

        private void OnEventRaised(GameObject _) => StartFlow();

        public override void ResetState()
        {
            base.ResetState();
            if (eventChannel) eventChannel?.ResetState();
        }

#if UNITY_EDITOR
        private void OnValidate() => SetBetterNodeName();

        private void SetBetterNodeName()
        {
            const string defaultNodeName = "Game Object Event Async Start";
            if (name != defaultNodeName) return;
            if (!eventChannel) return;

            name = $"On {eventChannel.name}";
        }
#endif

    }
}
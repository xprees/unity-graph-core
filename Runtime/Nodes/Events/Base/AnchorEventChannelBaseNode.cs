using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Xprees.RuntimeAnchors.Base;
using Object = UnityEngine.Object;

namespace Xprees.Graph.Core.Nodes.Events.Base
{
    [NodeTint("#7B2351")]
    [NodeWidth(300)]
    public abstract class AnchorEventChannelBaseNode<T> : EventChannelBaseNode<T> where T : Object
    {
        [Tooltip("The Object to pass to the event channel. If Anchor left empty null will be passed.")]
        public RuntimeAnchorBase<T> anchor;

        [Header("Settings")]
        [Min(0f)]
        [Tooltip("Wait for amount of time if anchor is not set. If 0 will not wait.")]
        public float waitForAnchorSetupIfNotReady = 1f;

        protected override async UniTask<T> GetEventData(CancellationToken cancellationToken = default)
        {
            if (anchor == null) return null;

            if (IsAnchorSet() || waitForAnchorSetupIfNotReady <= 0) return anchor.Value;

            try
            {
                await UniTask
                    .WaitUntil(IsAnchorSet, cancellationToken: cancellationToken)
                    .Timeout(TimeSpan.FromSeconds(waitForAnchorSetupIfNotReady));
            }
            catch (Exception e) when (e is OperationCanceledException or TimeoutException)
            {
            }

            return anchor.Value;
        }

        private bool IsAnchorSet() => anchor.isSet;

        public override void ResetState()
        {
            base.ResetState();
            anchor?.ResetState();
        }
    }
}
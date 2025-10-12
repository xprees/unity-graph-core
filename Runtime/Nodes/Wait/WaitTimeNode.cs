using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Xprees.Graph.Core.Attributes;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Variables.Reference.Primitive;

namespace Xprees.Graph.Core.Nodes.Wait
{
    [NodeDescription("Wait for a specified amount of time before proceeding.")]
    [NodeResizableWidth(250)]
    [CreateNodeMenu("Wait/Wait for Time")]
    public class WaitTimeNode : WaitBaseNode
    {
        [Header("Settings")]
        [Tooltip("Wait for x second time before moving on")]
        public FloatReference timeToWait = new(5f);

        // TODO show progress in the NodeEditor for debugging purposes
        protected override UniTask Wait(CancellationToken cancellationToken = default)
        {
            return UniTask.Delay(TimeSpan.FromSeconds(timeToWait), cancellationToken: cancellationToken);
        }

        public override void BackupStartState()
        {
            base.BackupStartState();
            timeToWait?.BackupStartState();
        }

        public override void ResetState()
        {
            base.ResetState();
            timeToWait?.ResetState();
        }
    }
}
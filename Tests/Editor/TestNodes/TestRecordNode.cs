using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Tests
{
    /// Linear test node that logs its Trigger, can throw and can hold the flow.
    public class TestRecordNode : LinearFlowBaseNode
    {
        public static readonly List<string> Log = new();

        public bool throwOnTrigger;

        protected override UniTask Trigger(CancellationToken cancellationToken = default)
        {
            Log.Add(name);
            if (throwOnTrigger) throw new InvalidOperationException("test");

            return UniTask.CompletedTask;
        }
    }
}
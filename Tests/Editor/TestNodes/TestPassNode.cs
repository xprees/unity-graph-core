using System.Threading;
using Cysharp.Threading.Tasks;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Tests
{
    public class TestPassNode : LinearFlowBaseNode, IPassthroughNode
    {
        protected override UniTask Trigger(CancellationToken cancellationToken = default)
        {
            TestRecordNode.Log.Add(name);
            return UniTask.CompletedTask;
        }
    }
}
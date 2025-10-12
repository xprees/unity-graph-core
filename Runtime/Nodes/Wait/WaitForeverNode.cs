using System.Threading;
using Cysharp.Threading.Tasks;
using Xprees.Graph.Core.Attributes;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Nodes.Wait
{
    [NodeDescription("Waits forever until canceled.")]
    [NodeResizableWidth(200)]
    [NodeTint("#511915")]
    [CreateNodeMenu("Wait/Wait Forever")]
    public class WaitForeverNode : WaitBaseNode
    {
        protected override UniTask Wait(CancellationToken cancellationToken = default) => UniTask.WaitUntilCanceled(CancellationToken.None);
    }
}
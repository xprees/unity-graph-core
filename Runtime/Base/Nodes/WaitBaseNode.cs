using System.Threading;
using Cysharp.Threading.Tasks;
using Xprees.Graph.Core.Attributes;

namespace Xprees.Graph.Core.Base.Nodes
{
    /// This is the base class for all linearFlow waiting nodes that block the execution until a certain condition is met or a time period has passed.
    [NodeTint("#7B3A27")]
    [NodeResizableWidth(300)]
    public abstract class WaitBaseNode : LinearFlowBaseNode, IPassthroughNode
    {
        protected override async sealed UniTask Trigger(CancellationToken cancellationToken = default) =>
            await Wait(cancellationToken);

        /// This method is called when the node is triggered, it should block the execution until the waiting period is done.
        protected abstract UniTask Wait(CancellationToken cancellationToken = default);
    }
}
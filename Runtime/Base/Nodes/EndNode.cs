using System.Threading;
using Cysharp.Threading.Tasks;
using Xprees.Graph.Core.Attributes;

namespace Xprees.Graph.Core.Base.Nodes
{
    [NodeTint("#b40d1b")]
    [NodeWidth(125)]
    [CreateNodeMenu("End", -1)]
    [PinnedNode(order = 0)]
    public class EndNode : BaseNode
    {
        [Input] public GraphConnection end;

        protected override UniTask<BaseNode> GetNextNode(CancellationToken cancellationToken = default) => new(this);
    }
}
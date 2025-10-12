using System.Threading;
using Cysharp.Threading.Tasks;

namespace Xprees.Graph.Core.Base.Nodes
{
    [NodeTint("#b40d1b")]
    [NodeWidth(125)]
    [CreateNodeMenu("End", -1)]
    public class EndNode : BaseNode
    {
        [Input] public GraphConnection end;

        protected override UniTask<BaseNode> GetNextNode(CancellationToken cancellationToken = default) => new(this);
    }
}
using System.Threading;
using Cysharp.Threading.Tasks;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Base
{
    /// Traverses the graph and awaits for each node to finish.
    public interface ITraverseGraphMixin
    {
        /// Starts traversing through the graph from the specified start node or find start node automatically by default.
        /// With triggerStartNode the start node is triggered once before the walk moves on from it.
        async UniTask TraverseGraph(
            GraphBase graph,
            BaseNode startNode = null,
            CancellationToken cancellationToken = default,
            bool triggerStartNode = false
        )
        {
            using var graphParser = new GraphParserBase(graph, startNode, this as BaseNode, triggerStartNode && startNode != null);

            while (!graphParser.Ended)
            {
                await graphParser.Continue(cancellationToken);
            }
        }
    }
}
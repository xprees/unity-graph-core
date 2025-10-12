using System.Threading;
using Cysharp.Threading.Tasks;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Base
{
    /// Traverses the graph and awaits for each node to finish.
    public interface ITraverseGraphMixin
    {
        /// Starts traversing through the graph from the specified start node or find start node automatically by default.
        async UniTask TraverseGraph(GraphBase graph, BaseNode startNode = null, CancellationToken cancellationToken = default)
        {
            using var graphParser = new GraphParserBase(graph, startNode);

            while (!graphParser.Ended)
            {
                await graphParser.Continue(cancellationToken);
            }
        }
    }
}
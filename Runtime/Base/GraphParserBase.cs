using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Xprees.Core;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Base
{
    /// Any <see cref="GraphBase"/> can traversed by a <see cref="GraphParserBase"/>.
    /// Each instance of parser keeps its state of current walkthrough. Making it possible to have multiple walkthroughes of the same graph.
    public class GraphParserBase : IResettable, IDisposable
    {
        private bool IsCurrentNodeNotSet => CurrentNode == null;

        public BaseNode CurrentNode { get; private set; } = null;

        public bool Ended => CurrentNode is EndNode || IsCurrentNodeNotSet;

        protected GraphBase Graph { get; }

        /// Start node can be overridden by passing it as a parameter.
        public GraphParserBase(GraphBase graph, BaseNode startNode = null)
        {
            Graph = graph;
            if (startNode != null)
            {
                CheckIfStartNodeIsInGraph(startNode);
                CurrentNode = startNode;
                Graph.Activate(this);
                return;
            }

            ResetGraphToStartNode();
            Graph.Activate(this);
        }

        private void CheckIfStartNodeIsInGraph(BaseNode startNode)
        {
            if (Graph.nodes.Contains(startNode)) return;
            throw new ArgumentException($"{nameof(startNode)}: {startNode.name} is not part of the graph: {Graph.name}!");
        }

        /// Moves to the next node in the graph, if possible, and awaits for it to finish.
        /// If the next node is a <see cref="IPassthroughNode"/>, it will continue to the next node until a non-passthrough node is found or parsing ends.
        public async UniTask Continue(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                if (Ended) return;

                CurrentNode = await CurrentNode.MoveNext(cancellationToken);
                if (CurrentNode is IPassthroughNode) continue;

                return;
            }
        }

        protected void ResetGraphToStartNode() => CurrentNode = GetStartNode();

        private BaseNode GetStartNode()
        {
            foreach (var node in Graph.nodes)
            {
                if (node is StartNode startNode)
                {
                    return startNode;
                }
            }

            throw new Exception($"No {nameof(StartNode)} found! Do you have one in the graph: {Graph.name}?");
        }

        public void ResetState() => ResetGraphToStartNode();

        public void Dispose() => Graph.Deactivate(this);
    }
}
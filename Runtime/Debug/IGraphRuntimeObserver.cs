#if UNITY_EDITOR
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using XNode;
using Xprees.Graph.Core.Base;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Debugging
{
    public enum GraphParserPhase
    {
        Positioned,
        ResolvingNext,
        AtGate,
        Triggering,
        Ended
    }

    public enum GraphEndReason
    {
        EndNode,
        NoConnection,
        GraphInactive,
        Cancelled,
        Faulted,
        Disposed
    }

    /// Receives the execution events of every <see cref="GraphParserBase"/>. Editor only, set through <see cref="GraphDebugHooks.Observer"/>.
    public interface IGraphRuntimeObserver
    {
        void OnParserCreated(GraphParserBase parser, BaseNode startNode, BaseNode origin, GraphParserBase parent);
        void OnResolvingNext(GraphParserBase parser, BaseNode from);

        /// Port null means the node did not report the port it followed, the observer has to infer it.
        void OnEdgeTaken(GraphParserBase parser, BaseNode from, BaseNode to, NodePort reportedPort);

        /// Must return a completed task when the flow is not breaking, so synchronous graphs stay synchronous.
        UniTask WaitAtGate(GraphParserBase parser, BaseNode node, CancellationToken cancellationToken);

        void OnNodeEntered(GraphParserBase parser, BaseNode node);
        void OnTriggerCompleted(GraphParserBase parser, BaseNode node);
        void OnParserEnded(GraphParserBase parser, GraphEndReason reason);

        /// The restore path (SetCurrentNode) moved the parser without running any node.
        void OnRepositioned(GraphParserBase parser, BaseNode node);

        /// ClearTransientState wipes the parsers of the graph without disposing them.
        void OnGraphCleared(GraphBase graph);

        /// A scenario (re)started, the observer resets visited nodes, edges and history.
        void OnRunStarted(Object runOwner);
    }

    /// Implemented by nodes that can describe their runtime state for the debugger.
    public interface IGraphNodeDebugState
    {
        string GetDebugState(in NodeDebugContext ctx);
    }

    public readonly struct NodeDebugContext
    {
        public GraphParserBase Parser { get; }

        /// Time.timeAsDouble when the node was entered.
        public double EnteredTime { get; }

        public int VisitCount { get; }
        public NodePort LastPort { get; }

        public NodeDebugContext(GraphParserBase parser, double enteredTime, int visitCount, NodePort lastPort)
        {
            Parser = parser;
            EnteredTime = enteredTime;
            VisitCount = visitCount;
            LastPort = lastPort;
        }
    }
}
#endif
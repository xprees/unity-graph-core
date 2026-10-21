using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Debugging;

namespace Xprees.Graph.Core.Base
{
    /// Any <see cref="GraphBase"/> can traversed by a <see cref="GraphParserBase"/>.
    /// Each instance of parser keeps its state of current walkthrough. Making it possible to have multiple walkthroughs of the same graph.
    public class GraphParserBase : IDisposable
    {
        private bool IsCurrentNodeNotSet => CurrentNode == null;

        public BaseNode CurrentNode { get; private set; } = null;

        public bool Ended => CurrentNode is EndNode || IsCurrentNodeNotSet;

        protected GraphBase Graph { get; }

#if UNITY_EDITOR
        /// Unique id of this parser in the current Play Mode session, for the graph debugger.
        public int DebugId { get; }

        /// Label shown by the graph debugger, empty means it derives one from the origin.
        public string DebugName { get; set; }

        public GraphBase DebugGraph => Graph;

        private bool _debugCreated;
        private bool _debugStartGateDone;
        private bool _debugEndReported;
#endif

        private bool _triggerStartPending;
        private IReadOnlyList<BaseNode> _prelude;

        /// Start node can be overridden by passing it as a parameter.
        public GraphParserBase(GraphBase graph, BaseNode startNode = null) : this(graph, startNode, null)
        {
        }

        /// Starts at the given node and triggers it once on the first <see cref="Continue"/> (the plain constructor only moves on from it).
        /// The prelude nodes (first one is only the place the walk begins, not triggered) are entered and triggered in order before the start node.
        public GraphParserBase(GraphBase graph, BaseNode startNode, bool triggerStartNode, IReadOnlyList<BaseNode> prelude = null)
            : this(graph, startNode, null, triggerStartNode, prelude)
        {
        }

        /// The origin is the node that created this parser (sub-graph or async start node), for the graph debugger.
        internal GraphParserBase(
            GraphBase graph,
            BaseNode startNode,
            BaseNode origin,
            bool triggerStartNode = false,
            IReadOnlyList<BaseNode> prelude = null
        )
        {
            Graph = graph;
#if UNITY_EDITOR
            DebugId = GraphDebugHooks.NextDebugId();
#endif
            if (startNode != null)
            {
                CheckIfStartNodeIsInGraph(startNode);
                CurrentNode = startNode;
                _triggerStartPending = triggerStartNode;
                if (prelude != null && prelude.Count > 0)
                {
                    foreach (var node in prelude) CheckIfStartNodeIsInGraph(node);
                    _prelude = prelude;
                }
            }
            else
            {
                ResetGraphToStartNode();
            }

            Graph.Activate(this);
#if UNITY_EDITOR
            _debugCreated = true;
            GraphDebugHooks.Observer?.OnParserCreated(this, CurrentNode, origin, GraphDebugHooks.TriggeringParser);
#endif
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
#if UNITY_EDITOR
            var observer = GraphDebugHooks.Observer;
            if (observer != null)
            {
                await ContinueObserved(observer, cancellationToken);
                return;
            }
#endif
            if (_prelude != null || _triggerStartPending) await RunPendingStart(cancellationToken);

            while (true)
            {
                if (Ended) return;

                var nextNode = await CurrentNode.MoveNext(cancellationToken);
                CurrentNode = nextNode;
                // Passthrough nodes are meant to be "invisible" to parser. aka. Parser shouldn't stay in them waiting for Continue.
                if (CurrentNode is IPassthroughNode) continue;

                return;
            }
        }

        /// Prelude nodes, then the start node, are triggered once before the walk goes on from the start node.
        private async UniTask RunPendingStart(CancellationToken cancellationToken)
        {
            var prelude = _prelude;
            var triggerStart = _triggerStartPending;
            _prelude = null;
            _triggerStartPending = false;
            if (!Graph.IsActive) return;

            if (prelude != null)
            {
                for (var i = 1; i < prelude.Count; i++)
                {
                    await prelude[i].TriggerInternal(cancellationToken);
                }
            }

            if (triggerStart && !Ended) await CurrentNode.TriggerInternal(cancellationToken);
        }

        protected void ResetGraphToStartNode() => SetCurrentNode(GetStartNode());

        /// Directly repositions the parser to a specific node without triggering
        /// any node logic, side effects, or async awaits.
        /// Use this to restore state from recorded history.
        protected void SetCurrentNode(BaseNode node)
        {
            CurrentNode = node;
#if UNITY_EDITOR
            if (_debugCreated) GraphDebugHooks.Observer?.OnRepositioned(this, node);
#endif
        }

        private BaseNode GetStartNode()
        {
            foreach (var node in Graph.nodes)
            {
                if (node is StartNode startNode) return startNode;
            }

            throw new Exception($"No {nameof(StartNode)} found! Do you have one in the graph: {Graph.name}?");
        }

        public void Dispose()
        {
            Graph.Deactivate(this);
#if UNITY_EDITOR
            ReportEnded(GraphDebugHooks.Observer, GraphEndReason.Disposed);
#endif
        }

#if UNITY_EDITOR
        /// Same walk as <see cref="BaseNode.MoveNext"/> but split into steps the observer can see and hold.
        private async UniTask ContinueObserved(IGraphRuntimeObserver observer, CancellationToken cancellationToken)
        {
            if (!Ended) _debugEndReported = false;

            try
            {
                if (!_debugStartGateDone)
                {
                    _debugStartGateDone = true;
                    if (!Ended)
                    {
                        await RunPendingPreludeObserved(observer, cancellationToken);
                        await observer.WaitAtGate(this, CurrentNode, cancellationToken);
                    }
                }

                if (_triggerStartPending)
                {
                    _triggerStartPending = false;
                    if (!Ended && Graph.IsActive)
                    {
                        observer.OnNodeEntered(this, CurrentNode);
                        await TriggerWithAmbientParent(CurrentNode, cancellationToken);
                        observer.OnTriggerCompleted(this, CurrentNode);
                    }
                }

                while (true)
                {
                    if (Ended)
                    {
                        ReportEnded(observer, CurrentNode is EndNode ? GraphEndReason.EndNode : GraphEndReason.NoConnection);
                        return;
                    }

                    var from = CurrentNode;
                    observer.OnResolvingNext(this, from);
                    var next = await from.ResolveNextInternal(cancellationToken);
                    if (next == null)
                    {
                        CurrentNode = null;
                        ReportEnded(observer, Graph.IsActive ? GraphEndReason.NoConnection : GraphEndReason.GraphInactive);
                        return;
                    }

                    observer.OnEdgeTaken(this, from, next, GraphDebugHooks.TakePendingPort(from));
                    await observer.WaitAtGate(this, next, cancellationToken);
                    observer.OnNodeEntered(this, next);
                    await TriggerWithAmbientParent(next, cancellationToken);
                    observer.OnTriggerCompleted(this, next);

                    CurrentNode = next;
                    if (next is EndNode)
                    {
                        ReportEnded(observer, GraphEndReason.EndNode);
                        return;
                    }

                    if (next is IPassthroughNode) continue;

                    return;
                }
            }
            catch (OperationCanceledException)
            {
                ReportEnded(observer, GraphEndReason.Cancelled);
                throw;
            }
            catch (Exception)
            {
                ReportEnded(observer, GraphEndReason.Faulted);
                throw;
            }
        }

        private async UniTask RunPendingPreludeObserved(IGraphRuntimeObserver observer, CancellationToken cancellationToken)
        {
            var prelude = _prelude;
            _prelude = null;
            if (prelude == null || !Graph.IsActive) return;

            for (var i = 1; i < prelude.Count; i++)
            {
                var node = prelude[i];
                observer.OnEdgeTaken(this, prelude[i - 1], node, null);
                await observer.WaitAtGate(this, node, cancellationToken);
                observer.OnNodeEntered(this, node);
                await TriggerWithAmbientParent(node, cancellationToken);
                observer.OnTriggerCompleted(this, node);
            }
        }

        /// The synchronous part of a Trigger creates child parsers (sub-graph, async start), they find this parser as their parent.
        private UniTask TriggerWithAmbientParent(BaseNode node, CancellationToken cancellationToken)
        {
            GraphDebugHooks.PushTriggering(this);
            try
            {
                return node.TriggerInternal(cancellationToken);
            }
            finally
            {
                GraphDebugHooks.PopTriggering();
            }
        }

        private void ReportEnded(IGraphRuntimeObserver observer, GraphEndReason reason)
        {
            if (observer == null || _debugEndReported) return;

            _debugEndReported = true;
            observer.OnParserEnded(this, reason);
        }
#endif
    }
}
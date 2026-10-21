using System;
using System.Diagnostics;
using UnityEngine;
using XNode;
using Xprees.Graph.Core.Base;
using Xprees.Graph.Core.Base.Nodes;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using System.Collections.Generic;
#endif

namespace Xprees.Graph.Core.Debugging
{
    /// Entry point of the graph debugger for runtime code. Everything is editor only, in player builds the calls are stripped.
    /// With no <see cref="Observer"/> set the whole cost is a null check.
    public static class GraphDebugHooks
    {
#if UNITY_EDITOR
        private static readonly List<GraphParserBase> TriggerStack = new();
        private static readonly Dictionary<BaseNode, NodePort> PendingPorts = new();
        private static readonly SuppressScope SharedSuppressScope = new();
        private static int _suppressCount;
        private static int _lastDebugId;

        /// Set by the editor tracer when the master toggle is on.
        public static IGraphRuntimeObserver Observer { get; set; }

        /// Parser whose node Trigger is running right now (top of the ambient stack), valid in the synchronous part of the Trigger.
        public static GraphParserBase TriggeringParser => TriggerStack.Count > 0 ? TriggerStack[^1] : null;

        public static bool BreaksSuppressed => _suppressCount > 0;

        /// Runtime code asks the editor to enable the debugger and open a graph. The editor tracer subscribes.
        public static event System.Action<Object, bool> DebugSessionRequested;

        public static void RequestDebugSession(Object graph, bool follow) => DebugSessionRequested?.Invoke(graph, follow);

        /// Breakpoints and pause points do not hold the flow inside the scope (for code that needs a synchronous Continue).
        public static IDisposable SuppressBreaks()
        {
            _suppressCount++;
            return SharedSuppressScope;
        }

        internal static int NextDebugId() => ++_lastDebugId;

        internal static void PushTriggering(GraphParserBase parser) => TriggerStack.Add(parser);

        internal static void PopTriggering()
        {
            if (TriggerStack.Count > 0) TriggerStack.RemoveAt(TriggerStack.Count - 1);
        }

        internal static void ClearPendingPort(BaseNode node) => PendingPorts.Remove(node);

        internal static NodePort TakePendingPort(BaseNode node)
        {
            if (!PendingPorts.Remove(node, out var port)) return null;
            return port;
        }

        /// Domain reload is off in this project, so the statics are reset explicitly.
        internal static void ResetStatics()
        {
            TriggerStack.Clear();
            PendingPorts.Clear();
            _suppressCount = 0;
            _lastDebugId = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnSubsystemRegistration() => ResetStatics();

        private sealed class SuppressScope : IDisposable
        {
            public void Dispose()
            {
                if (_suppressCount > 0) _suppressCount--;
            }
        }
#endif

        /// Announces a (re)start of a run, the tracer resets its visited nodes, edges and history.
        [Conditional("UNITY_EDITOR")]
        public static void BeginRun(Object runOwner)
        {
#if UNITY_EDITOR
            Observer?.OnRunStarted(runOwner);
#endif
        }

        /// A node reports the output port it followed, right before returning the next node. Use BaseNode.FollowPort.
        [Conditional("UNITY_EDITOR")]
        public static void ReportPort(BaseNode node, NodePort port)
        {
#if UNITY_EDITOR
            if (Observer == null || node == null) return;
            PendingPorts[node] = port;
#endif
        }

        [Conditional("UNITY_EDITOR")]
        public static void SetDebugName(GraphParserBase parser, string name)
        {
#if UNITY_EDITOR
            if (parser != null) parser.DebugName = name;
#endif
        }
    }
}
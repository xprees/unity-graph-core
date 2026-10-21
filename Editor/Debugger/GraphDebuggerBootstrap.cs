using UnityEditor;
using UnityEngine;
using XNode;
using XNodeEditor;
using Xprees.Graph.Core.Debugging;

namespace Xprees.Graph.Core.Editor.Debugger
{
    /// Registers the tracer as the graph observer while the master toggle is on and resets the static debugger state around Play Mode.
    /// Domain reload is off in this project, so nothing here relies on a fresh domain.
    [InitializeOnLoad]
    internal static class GraphDebuggerBootstrap
    {
        static GraphDebuggerBootstrap()
        {
            GraphDebuggerSettings.Changed += ApplyEnabled;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            GraphDebugHooks.DebugSessionRequested -= OnDebugSessionRequested;
            GraphDebugHooks.DebugSessionRequested += OnDebugSessionRequested;
            ApplyEnabled();
        }

        private static void ApplyEnabled()
        {
            var tracer = GraphRuntimeTracer.Instance;
            if (GraphDebuggerSettings.Enabled)
            {
                GraphDebugHooks.Observer = tracer;
                return;
            }

            if (GraphDebugHooks.Observer == tracer) GraphDebugHooks.Observer = null;
            GraphDebugGate.ReleaseAll();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                // EnteredPlayMode fires after the first scenes ran Awake/OnEnable, so the statics are reset before that.
                case PlayModeStateChange.ExitingEditMode:
                    GraphDebugGate.CancelAll();
                    GraphDebugHooks.ResetStatics();
                    GraphBreakpoints.InvalidateCache();
                    GraphRuntimeTracer.Instance.Reset();
                    LiveValues.Reset();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    GraphDebugGate.CancelAll();
                    GraphRuntimeTracer.Instance.EndAllRunning(GraphEndReason.Cancelled);
                    if (GraphDebuggerSettings.Enabled) GraphTraceFile.SaveLastRun(GraphRuntimeTracer.Instance);
                    if (GraphDebuggerSettings.Enabled) GraphCoverageService.Accumulate(GraphRuntimeTracer.Instance);
                    break;
            }
        }

        private static void OnDebugSessionRequested(Object graph, bool follow)
        {
            GraphDebuggerSettings.Enabled = true;
            GraphDebuggerSettings.Follow = follow;
            if (graph is NodeGraph nodeGraph) NodeEditorWindow.Open(nodeGraph);
        }
    }
}
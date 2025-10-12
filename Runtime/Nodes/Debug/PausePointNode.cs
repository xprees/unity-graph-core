using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Xprees.Graph.Core.Base.Nodes;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Xprees.Graph.Core.Nodes.Debug
{
    [NodeTint("#cd0e1e")]
    [CreateNodeMenu("Debug/Pause Point")]
    public class PausePointNode : LinearFlowBaseNode, IPassthroughNode
    {
        [Tooltip("If true, the graph will pause in the editor when this node is reached.")]
        public bool active = true;

        protected override UniTask Trigger(CancellationToken cancellationToken = default)
        {
#if UNITY_EDITOR
            if (active)
            {
                // This will pause the execution of the graph until resumed in the editor.
                EditorApplication.isPaused = true;

                var window = EditorWindow.focusedWindow ?? EditorWindow.mouseOverWindow;
                window?.ShowNotification(new GUIContent($"Hit Pause Point in {graph.name}"));
            }
#endif
            return UniTask.CompletedTask;
        }

    }
}
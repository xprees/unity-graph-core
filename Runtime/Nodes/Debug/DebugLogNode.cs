using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Xprees.Graph.Core.Attributes;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Nodes.Debug
{
    [NodeTint("#676916")]
    [NodeResizableWidth(200)]
    [CreateNodeMenu("Debug/Log")]
    public class DebugLogNode : LinearFlowBaseNode, IPassthroughNode
    {
        [TextArea]
        public string message;

        public LogType logType = LogType.Log;

        protected override UniTask Trigger(CancellationToken cancellationToken = default)
        {
            UnityEngine.Debug.LogFormat(logType, LogOption.None, graph, message);
            return UniTask.CompletedTask;
        }
    }
}
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Events;
using Xprees.Graph.Core.Base;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Nodes.Events.Unity
{
    [NodeTint("#5e6cae")]
    [NodeWidth(325)]
    [CreateNodeMenu("Events/Unity/UnityEvent")]
    public class UnityEventNode : BaseNode, IPassthroughNode
    {
        public UnityEvent triggerEvent;

        [Input] public GraphConnection input;

        [Output(connectionType = ConnectionType.Override)]
        public GraphConnection output;

        protected override UniTask Trigger(CancellationToken cancellationToken = default)
        {
            triggerEvent?.Invoke();
            return UniTask.CompletedTask;
        }

        protected override UniTask<BaseNode> GetNextNode(CancellationToken cancellationToken = default) =>
            new(GetOutputPort(nameof(output)).Connection.node as BaseNode);
    }
}
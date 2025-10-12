using System.Threading;
using Cysharp.Threading.Tasks;
using Xprees.Graph.Core.Attributes;

namespace Xprees.Graph.Core.Nodes.Wait.Variables.Base
{
    /// Base Node which will wait until variable value has changed or canceled.
    [NodeDescription("Node waits until variable values has changed or canceled")]
    public abstract class WaitForVariableChangedBaseNode<T> : VariableWaitBaseNode<T>
    {
        protected override UniTask Wait(CancellationToken cancellationToken = default) =>
            UniTask.WaitUntilValueChanged(variable, value => value.CurrentValue, cancellationToken: cancellationToken);
    }
}
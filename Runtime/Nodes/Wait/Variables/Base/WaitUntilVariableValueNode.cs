using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Xprees.Variables.Base;

namespace Xprees.Graph.Core.Nodes.Wait.Variables.Base
{
    public abstract class WaitUntilVariableValueBaseNode<T> : VariableWaitBaseNode<T>
    {
        [Header("Expected Value")]
        [SerializeField] protected ReferenceBase<T> value = new();

        protected override async UniTask Wait(CancellationToken cancellationToken = default) =>
            await UniTask.WaitUntil(VariableHasMetValueCondition, cancellationToken: cancellationToken);

        /// Override this method to change the value condition check. Default is Equals().
        protected virtual bool VariableHasMetValueCondition() => value.Value.Equals(variable.CurrentValue);
    }
}
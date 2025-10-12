using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Xprees.Graph.Core.Attributes;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Variables.Base;

namespace Xprees.Graph.Core.Nodes.Variables.Base
{
    [NodeDescription("Increment a variable by a specified value on node Trigger.")]
    [NodeResizableWidth(300)]
    public abstract class VariableIncrementerBaseNode<T> : LinearFlowBaseNode, IPassthroughNode
    {
        [Header("Variable incrementing")]
        public ReferenceBase<T> incrementBy = new();

        public VariableBaseSO<T> variable;

        protected override void Init()
        {
            base.Init();
            SetDefaultIncrementValueIfNotSet();
        }

        private void SetDefaultIncrementValueIfNotSet()
        {
            if (incrementBy != null) return;

            var defaultIncrementValue = GetDefaultIncrementValue();
            if (defaultIncrementValue == null)
            {
                incrementBy = new();
                return;
            }

            incrementBy = new(defaultIncrementValue);
        }

        protected virtual T GetDefaultIncrementValue() => default;

        protected abstract T GetIncrementedValue(T currentValue, T increment);

        protected override UniTask Trigger(CancellationToken cancellationToken = default)
        {
            if (variable != null) variable.CurrentValue = GetIncrementedValue(variable.CurrentValue, incrementBy.Value);

            return UniTask.CompletedTask;
        }

        public override void ResetState()
        {
            variable?.ResetState();
            SetDefaultIncrementValueIfNotSet();
        }
    }
}
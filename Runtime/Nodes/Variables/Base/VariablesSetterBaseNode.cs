using System.Threading;
using Cysharp.Threading.Tasks;
using Xprees.Graph.Core.Attributes;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Variables.Base;

namespace Xprees.Graph.Core.Nodes.Variables.Base
{
    [NodeDescription("Sets a variable to a value on trigger.")]
    [NodeTint("#693805")]
    [NodeResizableWidth(300)]
    public abstract class VariablesSetterBaseNode<T> : LinearFlowBaseNode, IPassthroughNode
    {
        public ReferenceBase<T> valueToSet = new();
        public VariableBaseSO<T> variable;

        protected override UniTask Trigger(CancellationToken cancellationToken = default)
        {
            if (variable != null) variable.CurrentValue = GetValueToSet();

            return UniTask.CompletedTask;
        }

        protected virtual T GetValueToSet() => valueToSet.Value;

        public override void BackupStartState()
        {
            base.BackupStartState();
            valueToSet?.BackupStartState();
        }

        public override void ResetState()
        {
            variable?.ResetState();
            valueToSet?.ResetState();
        }
    }
}
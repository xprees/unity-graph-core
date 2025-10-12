using UnityEngine;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Nodes.Variables;
using Xprees.Variables.Primitive;
using Xprees.Variables.Reference.Primitive;

namespace Xprees.Graph.Core.Nodes.Branch
{
    [CreateNodeMenu("Branch/Int Variable Branch")]
    public class IntVariableBranchNode : BranchBaseNode
    {
        [Header("Values to compare")]
        [Tooltip("The variable that will be used to manage flow.")]
        public IntVariable intVariable;

        [Tooltip("The value that will be compared to the variable.")]
        public IntReference compareValue = new();

        [Header("Value comparison method")]
        public NumberComparisonMethod method = NumberComparisonMethod.Equals;

        protected override bool GetConditionResult() =>
            method.CompareValues(intVariable.CurrentValue, compareValue.Value);

        public override void BackupStartState()
        {
            base.BackupStartState();
            compareValue?.BackupStartState();
        }

        public override void ResetState()
        {
            base.ResetState();
            intVariable?.ResetState();
            compareValue?.ResetState();
        }
    }
}
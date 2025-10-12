using UnityEngine;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Nodes.Variables;
using Xprees.Variables.Primitive;
using Xprees.Variables.Reference.Primitive;

namespace Xprees.Graph.Core.Nodes.Branch
{
    [CreateNodeMenu("Branch/String Variable Branch")]
    public class StringBranchNode : BranchBaseNode
    {
        [Header("Values to compare")]
        [Tooltip("The variable that will be used to manage flow.")]
        public StringVariable stringVariable;

        [Tooltip("The value that will be compared to the variable.")]
        public StringReference compareValue = new();

        [Header("Value comparison method")]
        public StringComparisonMethod method = StringComparisonMethod.Equals;

        protected override bool GetConditionResult() =>
            method.CompareValues(stringVariable.CurrentValue, compareValue.Value);

        public override void BackupStartState()
        {
            base.BackupStartState();
            compareValue?.BackupStartState();
        }

        public override void ResetState()
        {
            base.ResetState();
            stringVariable?.ResetState();
            compareValue?.ResetState();
        }
    }
}
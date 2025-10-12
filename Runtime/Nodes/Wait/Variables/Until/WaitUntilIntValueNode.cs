using UnityEngine;
using Xprees.Graph.Core.Nodes.Variables;
using Xprees.Graph.Core.Nodes.Wait.Variables.Base;

namespace Xprees.Graph.Core.Nodes.Wait.Variables.Until
{
    [CreateNodeMenu("Wait/Variables/Wait until/Int has value")]
    public class WaitUntilIntValueNode : WaitUntilVariableValueBaseNode<int>
    {
        [Header("Value comparison method")]
        [SerializeField] private NumberComparisonMethod method = NumberComparisonMethod.Equals;

        protected override bool VariableHasMetValueCondition() =>
            method.CompareValues(variable.CurrentValue, value.Value);
    }
}
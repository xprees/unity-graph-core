using UnityEngine;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Nodes.Variables;

namespace Xprees.Graph.Core.Nodes.Wait.Events
{
    [NodeTint("#68427B")]
    [CreateNodeMenu("Wait/Wait on Int Event")]
    public class WaitOnIntEventNode : WaitOnEventBaseNode<int>
    {
        [Tooltip("If true, the node will accept any value, otherwise it will only accept value if it fulfills the comparison value")]
        public bool acceptAnyValue;

        [Header("Value requirements")]
        public int comparisonValue;

        public NumberComparisonMethod comparisonMethod;

        protected override bool CanMoveOn() =>
            base.CanMoveOn()
            && (acceptAnyValue || comparisonMethod.CompareValues(eventData, comparisonValue));
    }
}
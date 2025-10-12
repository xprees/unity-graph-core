using UnityEngine;

namespace Xprees.Graph.Core.Nodes.Variables
{
    public enum NumberComparisonMethod
    {
        Equals = 0,
        NotEquals,

        GreaterThan,
        GreaterThanOrEquals,

        LessThan,
        LessThanOrEquals,
    }

    public static class NumberComparisonMethodExtensions
    {
        public static bool CompareValues(this NumberComparisonMethod method, int value, int comparisonValue) =>
            method switch
            {
                NumberComparisonMethod.Equals => value == comparisonValue,
                NumberComparisonMethod.NotEquals => value != comparisonValue,

                NumberComparisonMethod.GreaterThan => value > comparisonValue,
                NumberComparisonMethod.GreaterThanOrEquals => value >= comparisonValue,

                NumberComparisonMethod.LessThan => value < comparisonValue,
                NumberComparisonMethod.LessThanOrEquals => value <= comparisonValue,

                _ => false,
            };

        public static bool CompareValues(this NumberComparisonMethod method, float value, float comparisonValue) =>
            method switch
            {
                NumberComparisonMethod.Equals => Mathf.Approximately(value, comparisonValue),
                NumberComparisonMethod.NotEquals => !Mathf.Approximately(value, comparisonValue),

                NumberComparisonMethod.GreaterThan => value > comparisonValue,
                NumberComparisonMethod.GreaterThanOrEquals => value >= comparisonValue,

                NumberComparisonMethod.LessThan => value < comparisonValue,
                NumberComparisonMethod.LessThanOrEquals => value <= comparisonValue,

                _ => false,
            };
    }

}
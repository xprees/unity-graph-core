namespace Xprees.Graph.Core.Nodes.Variables
{
    public enum StringComparisonMethod
    {
        Equals = 0,
        NotEquals,

        Contains,

        StartsWith,
        EndsWith,
    }

    public static class StringComparisonMethodExtensions
    {
        public static bool CompareValues(this StringComparisonMethod method, string value, string comparisonValue) =>
            method switch
            {
                StringComparisonMethod.Equals => value == comparisonValue,
                StringComparisonMethod.NotEquals => value != comparisonValue,

                StringComparisonMethod.Contains => value.Contains(comparisonValue),

                StringComparisonMethod.StartsWith => value.StartsWith(comparisonValue),
                StringComparisonMethod.EndsWith => value.EndsWith(comparisonValue),

                _ => false,
            };
    }

}
#if UNITY_EDITOR
using XNode;
using Xprees.Graph.Core.Debugging;

namespace Xprees.Graph.Core.Nodes.Branch
{
    public partial class IntVariableBranchNode : IGraphNodeDebugState
    {
        public string GetDebugState(in NodeDebugContext ctx) =>
            intVariable == null ? "no variable" : $"{intVariable.name} = {intVariable.CurrentValue} {method} {compareValue.Value}";
    }

    public partial class StringBranchNode : IGraphNodeDebugState
    {
        public string GetDebugState(in NodeDebugContext ctx) =>
            stringVariable == null
                ? "no variable"
                : $"{stringVariable.name} = \"{stringVariable.CurrentValue}\" {method} \"{compareValue.Value}\"";
    }

    public partial class VariableBranchNode : IGraphNodeDebugState
    {
        public string GetDebugState(in NodeDebugContext ctx) =>
            boolVariable == null ? "no variable" : $"{boolVariable.name} = {boolVariable.CurrentValue}";
    }

    public partial class StringSwitchNode : IGraphNodeDebugState
    {
        public string GetDebugState(in NodeDebugContext ctx)
        {
            var current = stringVariable == null ? "no variable" : $"\"{stringVariable.CurrentValue}\"";
            var matched = DescribeMatch(ctx.LastPort);
            return matched == null ? $"value {current}" : $"value {current} -> {matched}";
        }

        /// Names the case behind the port the node followed last, null when it is unknown.
        internal string DescribeMatch(NodePort port)
        {
            if (port == null || port.node != this) return null;
            if (port.fieldName == nameof(defaultCase)) return "default";

            const string prefix = nameof(cases) + " ";
            if (!port.fieldName.StartsWith(prefix) || !int.TryParse(port.fieldName.Substring(prefix.Length), out var index)) return null;
            if (cases == null || index < 0 || index >= cases.Count) return $"case {index}";

            return $"case {index} \"{cases[index].Value}\"";
        }
    }
}
#endif
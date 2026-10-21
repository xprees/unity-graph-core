using System.Collections.Generic;
using XNode;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Editor.Debugger
{
    /// Works out which output port of a node led to the next node.
    public static class EdgeInference
    {
        /// A reported port that connects to the target wins. Otherwise the output ports of the source connected to the target are the candidates:
        /// one is exact, more are ambiguous (all returned), none returns an empty list.
        public static IReadOnlyList<NodePort> Find(BaseNode from, BaseNode to, NodePort reported, out bool ambiguous)
        {
            ambiguous = false;
            var result = new List<NodePort>(2);
            if (from == null || to == null) return result;

            if (reported != null && reported.IsOutput && reported.node == from && ConnectsTo(reported, to))
            {
                result.Add(reported);
                return result;
            }

            foreach (var output in from.Outputs)
            {
                if (ConnectsTo(output, to)) result.Add(output);
            }

            ambiguous = result.Count > 1;
            return result;
        }

        private static bool ConnectsTo(NodePort output, Node target)
        {
            var connectionCount = output.ConnectionCount;
            for (var i = 0; i < connectionCount; i++)
            {
                if (output.GetConnection(i)?.node == target) return true;
            }

            return false;
        }
    }
}
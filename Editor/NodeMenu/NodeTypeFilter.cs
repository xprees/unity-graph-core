using System;
using System.Linq;
using XNode;
using Xprees.Graph.Core.Attributes;

namespace Xprees.Graph.Core.Editor.NodeMenu
{
    /// Decides which node types a graph accepts according to <see cref="AllowOnlyInGraphsAttribute"/> and <see cref="ExcludeFromGraphsAttribute"/>.
    /// Without attributes a node is allowed in every graph.
    public static class NodeTypeFilter
    {
        public static bool IsAllowed(Type nodeType, NodeGraph graph) => graph == null || IsAllowed(nodeType, graph.GetType());

        public static bool IsAllowed(Type nodeType, Type graphType)
        {
            var exclude = (ExcludeFromGraphsAttribute) Attribute.GetCustomAttribute(nodeType, typeof(ExcludeFromGraphsAttribute), true);
            if (exclude != null && Matches(exclude.graphTypes, graphType)) return false;

            var allowOnly = (AllowOnlyInGraphsAttribute) Attribute.GetCustomAttribute(nodeType, typeof(AllowOnlyInGraphsAttribute), true);
            return allowOnly == null || Matches(allowOnly.graphTypes, graphType);
        }

        /// Human readable reason for the validator.
        public static string DescribeRestriction(Type nodeType, Type graphType)
        {
            var exclude = (ExcludeFromGraphsAttribute) Attribute.GetCustomAttribute(nodeType, typeof(ExcludeFromGraphsAttribute), true);
            if (exclude != null && Matches(exclude.graphTypes, graphType))
                return $"{nodeType.Name} is excluded from {graphType.Name} ({nameof(ExcludeFromGraphsAttribute)}).";

            var allowOnly = (AllowOnlyInGraphsAttribute) Attribute.GetCustomAttribute(nodeType, typeof(AllowOnlyInGraphsAttribute), true);
            var allowed = allowOnly == null ? "" : string.Join(", ", allowOnly.graphTypes.Select(t => t.Name));
            return $"{nodeType.Name} is only allowed in: {allowed} ({nameof(AllowOnlyInGraphsAttribute)}).";
        }

        private static bool Matches(Type[] listedTypes, Type graphType) =>
            listedTypes != null && listedTypes.Any(t => t != null && t.IsAssignableFrom(graphType));
    }
}
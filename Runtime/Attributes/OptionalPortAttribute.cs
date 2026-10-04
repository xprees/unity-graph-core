using System;

namespace Xprees.Graph.Core.Attributes
{
    /// Marks an [Output] port whose connection is optional: the graph validator doesn't warn that the flow ends here when it is left unconnected.
    [AttributeUsage(AttributeTargets.Field)]
    public class OptionalPortAttribute : Attribute
    {
    }
}
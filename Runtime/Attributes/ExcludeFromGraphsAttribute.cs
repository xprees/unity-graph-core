using System;

namespace Xprees.Graph.Core.Attributes
{
    /// Hides the node from the "create node" menu of the listed graph types (and their subclasses).
    /// Inherited by derived node classes unless they declare their own.
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class ExcludeFromGraphsAttribute : Attribute
    {
        public readonly Type[] graphTypes;

        public ExcludeFromGraphsAttribute(params Type[] graphTypes)
        {
            this.graphTypes = graphTypes;
        }
    }
}
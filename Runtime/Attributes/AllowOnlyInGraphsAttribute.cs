using System;

namespace Xprees.Graph.Core.Attributes
{
    /// Restricts the node to the listed graph types (and their subclasses): it is offered in the "create node" menu only there.
    /// Nodes without this attribute are available in every graph. Inherited by derived node classes unless they declare their own.
    /// <see cref="ExcludeFromGraphsAttribute"/> still wins over it.
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class AllowOnlyInGraphsAttribute : Attribute
    {
        public readonly Type[] graphTypes;

        public AllowOnlyInGraphsAttribute(params Type[] graphTypes)
        {
            this.graphTypes = graphTypes;
        }
    }
}
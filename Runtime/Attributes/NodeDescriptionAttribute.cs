using System;

namespace Xprees.Graph.Core.Attributes
{
    /// This attribute is used to provide a description for a node class which will show as a tooltip on node header.
    [AttributeUsage(AttributeTargets.Class)]
    public class NodeDescriptionAttribute : Attribute
    {
        public readonly string description;

        public NodeDescriptionAttribute(string description)
        {
            this.description = description;
        }
    }
}
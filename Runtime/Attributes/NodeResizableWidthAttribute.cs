using System;

namespace Xprees.Graph.Core.Attributes
{
    /// This attribute is used to mark a baseNode inherited nodes as with resizable width in the graph editor.
    [AttributeUsage(AttributeTargets.Class)]
    public class NodeResizableWidthAttribute : Attribute
    {
        public readonly bool isResizable;
        public readonly int minWidth;
        public readonly int maxWidth;

        public NodeResizableWidthAttribute(int minWidth = 100, int maxWidth = 500, bool resizable = true)
        {
            isResizable = resizable;
            this.minWidth = minWidth;
            this.maxWidth = maxWidth;
        }
    }
}
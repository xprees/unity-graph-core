using System;

namespace Xprees.Graph.Core.Attributes
{
    /// Lists the node in the "Pinned" section at the top of the editor's create-node tree (in addition to its normal place).
    /// Lower <see cref="order"/> is listed first. Not inherited by derived node classes.
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class PinnedNodeAttribute : Attribute
    {
        public int order;
    }
}
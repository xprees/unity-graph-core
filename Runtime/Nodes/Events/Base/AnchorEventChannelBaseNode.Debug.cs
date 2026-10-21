#if UNITY_EDITOR
using Xprees.Graph.Core.Debugging;
using Xprees.Graph.Core.Nodes.DebugState;

namespace Xprees.Graph.Core.Nodes.Events.Base
{
    public abstract partial class AnchorEventChannelBaseNode<T> : IGraphNodeDebugState
    {
        public string GetDebugState(in NodeDebugContext ctx) =>
            anchor == null ? "no anchor, passes null" : $"anchor {NodeDebugFormat.Obj(anchor)}: {(anchor.isSet ? "set" : "not set")}";
    }
}
#endif
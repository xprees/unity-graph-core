#if UNITY_EDITOR
using Xprees.Graph.Core.Debugging;
using Xprees.Graph.Core.Nodes.DebugState;

namespace Xprees.Graph.Core.Base.Nodes
{
    public abstract partial class WaitOnEventBaseNode : IGraphNodeDebugState
    {
        /// Accepting / triggerActivated / raised part shared by every event wait.
        protected string DebugEventFlags()
        {
            bool accepting = acceptingEvents;
            bool onlyAfterTrigger = triggerActivated;
            return $"accepting: {accepting}, triggerActivated: {onlyAfterTrigger}, raised: {isEventRaised}";
        }

        // Explicit so concrete nodes can still provide their own public GetDebugState.
        string IGraphNodeDebugState.GetDebugState(in NodeDebugContext ctx) => DebugEventFlags();
    }

    public abstract partial class WaitOnEventBaseNode<T> : IGraphNodeDebugState
    {
        string IGraphNodeDebugState.GetDebugState(in NodeDebugContext ctx) =>
            $"channel {NodeDebugFormat.Obj(eventChannel)}, {DebugEventFlags()}, data {NodeDebugFormat.Val(eventData)}";
    }

    public abstract partial class WaitOnEventBaseNode<T1, T2> : IGraphNodeDebugState
    {
        string IGraphNodeDebugState.GetDebugState(in NodeDebugContext ctx) =>
            $"channel {NodeDebugFormat.Obj(eventChannel)}, {DebugEventFlags()}, data {NodeDebugFormat.Val(eventData)}";
    }

    public abstract partial class WaitOnEventBaseNode<T1, T2, T3> : IGraphNodeDebugState
    {
        string IGraphNodeDebugState.GetDebugState(in NodeDebugContext ctx) =>
            $"channel {NodeDebugFormat.Obj(eventChannel)}, {DebugEventFlags()}, data {NodeDebugFormat.Val(eventData)}";
    }
}
#endif
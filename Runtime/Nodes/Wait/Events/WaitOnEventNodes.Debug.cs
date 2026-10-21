#if UNITY_EDITOR
using Xprees.Graph.Core.Debugging;
using Xprees.Graph.Core.Nodes.DebugState;

namespace Xprees.Graph.Core.Nodes.Wait.Events
{
    public partial class WaitOnVoidEventNode : IGraphNodeDebugState
    {
        public string GetDebugState(in NodeDebugContext ctx) => $"channel {NodeDebugFormat.Obj(eventChannel)}, raised: {isEventRaised}";
    }

    public partial class WaitOnIntEventNode : IGraphNodeDebugState
    {
        public string GetDebugState(in NodeDebugContext ctx) =>
            $"channel {NodeDebugFormat.Obj(eventChannel)}, raised: {isEventRaised}, last value {eventData}, expects {(acceptAnyValue ? "any" : $"{comparisonMethod} {comparisonValue}")}";
    }

    public partial class WaitOnGameObjectEventNode : IGraphNodeDebugState
    {
        public string GetDebugState(in NodeDebugContext ctx) =>
            $"channel {NodeDebugFormat.Obj(eventChannel)}, raised: {isEventRaised}, data {NodeDebugFormat.Obj(eventData)}";
    }
}
#endif
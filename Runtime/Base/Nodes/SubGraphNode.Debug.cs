#if UNITY_EDITOR
using Xprees.Graph.Core.Debugging;
using Xprees.Graph.Core.Nodes.DebugState;

namespace Xprees.Graph.Core.Base.Nodes
{
    public partial class SubGraphNode : IGraphNodeDebugState
    {
        public string GetDebugState(in NodeDebugContext ctx) => $"sub-graph {NodeDebugFormat.Obj(subGraph)}";
    }
}
#endif
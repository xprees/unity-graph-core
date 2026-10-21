#if UNITY_EDITOR
using Xprees.Graph.Core.Debugging;
using Xprees.Graph.Core.Nodes.DebugState;

namespace Xprees.Graph.Core.Nodes.Wait
{
    public partial class WaitForeverNode : IGraphNodeDebugState
    {
        public string GetDebugState(in NodeDebugContext ctx) => $"waiting forever, {NodeDebugFormat.Num(NodeDebugFormat.Elapsed(ctx))}s so far";
    }
}
#endif
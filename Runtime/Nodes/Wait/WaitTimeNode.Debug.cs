#if UNITY_EDITOR
using Xprees.Graph.Core.Debugging;
using Xprees.Graph.Core.Nodes.DebugState;

namespace Xprees.Graph.Core.Nodes.Wait
{
    public partial class WaitTimeNode : IGraphNodeDebugState
    {
        public string GetDebugState(in NodeDebugContext ctx) => FormatRemaining(timeToWait.Value, NodeDebugFormat.Elapsed(ctx));

        internal static string FormatRemaining(float total, double elapsed)
        {
            var remaining = elapsed >= total ? 0d : total - elapsed;
            return $"remaining {NodeDebugFormat.Num(remaining)}s of {NodeDebugFormat.Num(total)}s";
        }
    }
}
#endif
namespace Xprees.Graph.Core.Base
{
    /// Marker for graph types that host <see cref="Nodes.AsyncStartBaseNode"/>s (e.g. scenarios).
    /// Async start nodes are offered in the create-node menu only in graphs implementing it.
    public interface IAllowAsyncStartNodes
    {
    }
}
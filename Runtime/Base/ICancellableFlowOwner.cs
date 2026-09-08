namespace Xprees.Graph.Core.Base
{
    /// Implemented by nodes that can have asynchronous graph flows in flight outside of the
    /// main parser walk (async start nodes, sub-graphs). Lets a graph tear those flows down
    /// when the scenario stops, instead of leaving them running against shared graph state.
    public interface ICancellableFlowOwner
    {
        /// Cancels every asynchronous flow this object still has in flight.
        void CancelFlows();
    }
}
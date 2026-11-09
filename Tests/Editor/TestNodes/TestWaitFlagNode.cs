using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Tests
{
    /// A wait-on-event node with its "event already happened" flag exposed.
    public class TestWaitFlagNode : WaitOnEventBaseNode
    {
        public bool Raised
        {
            get => isEventRaised;
            set => isEventRaised = value;
        }
    }
}
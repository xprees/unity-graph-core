using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Tests
{
    public class TestAsyncStartNode : AsyncStartBaseNode
    {
        public void Fire() => StartFlow();
    }
}
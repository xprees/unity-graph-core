using NUnit.Framework;
using UnityEngine;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Debugging;

namespace Xprees.Graph.Core.Tests
{
    public class BaseNodeDebugStateTests
    {
        private sealed class TestIntWaitNode : WaitOnEventBaseNode<int>
        {
        }

        private sealed class TestAsyncStartNode : AsyncStartBaseNode
        {
        }

        private static string State(IGraphNodeDebugState node) => node.GetDebugState(default);

        [Test]
        public void SubGraphNode_ReportsSubGraphName()
        {
            var node = ScriptableObject.CreateInstance<SubGraphNode>();
            try
            {
                StringAssert.Contains("none", State(node));
            }
            finally
            {
                Object.DestroyImmediate(node);
            }
        }

        [Test]
        public void WaitOnEventBaseNode_ReportsFlagsAndChannel()
        {
            var node = ScriptableObject.CreateInstance<TestIntWaitNode>();
            var start = ScriptableObject.CreateInstance<TestAsyncStartNode>();
            try
            {
                var text = State(node);
                StringAssert.Contains("channel none", text);
                StringAssert.Contains("accepting: True", text);
                StringAssert.Contains("raised: False", text);
                StringAssert.Contains("activeStart: True", State(start));
                StringAssert.Contains("live flows: 0", State(start));
            }
            finally
            {
                Object.DestroyImmediate(node);
                Object.DestroyImmediate(start);
            }
        }
    }
}
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XNode;
using Xprees.Graph.Core.Base;
using Xprees.Graph.Core.Debugging;
using Xprees.Graph.Core.Nodes.Branch;
using Xprees.Graph.Core.Nodes.Wait;
using Xprees.Variables.Primitive;
using Xprees.Variables.Reference.Primitive;

namespace Xprees.Graph.Core.Tests
{
    public class NodeDebugStateTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created) Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private T Create<T>() where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            _created.Add(instance);
            return instance;
        }

        [Test]
        public void WaitTime_FormatRemaining_CountsDownAndClampsAtZero()
        {
            Assert.AreEqual("remaining 3.0s of 5.0s", WaitTimeNode.FormatRemaining(5f, 2d));
            Assert.AreEqual("remaining 0.0s of 5.0s", WaitTimeNode.FormatRemaining(5f, 9d));
        }

        [Test]
        public void WaitTime_GetDebugState_UsesEnteredTimeOfTheContext()
        {
            var node = Create<WaitTimeNode>();
            node.timeToWait = new FloatReference(1000f);
            var ctx = new NodeDebugContext(null, Time.timeAsDouble - 10d, 1, null);

            var state = ((IGraphNodeDebugState) node).GetDebugState(ctx);

            StringAssert.StartsWith("remaining 99", state);
            StringAssert.EndsWith("of 1000.0s", state);
        }

        [Test]
        public void StringSwitch_DescribeMatch_NamesTheFollowedCase()
        {
            var node = Create<StringSwitchNode>();
            node.cases = new List<StringReference> { new("a"), new("b") };
            var casePort = node.AddDynamicOutput(typeof(GraphConnection), Node.ConnectionType.Override, Node.TypeConstraint.None, "cases 1");

            Assert.AreEqual("case 1 \"b\"", node.DescribeMatch(casePort));
            Assert.AreEqual("default", node.DescribeMatch(node.GetOutputPort(nameof(StringSwitchNode.defaultCase))));
            Assert.IsNull(node.DescribeMatch(null));
        }

        [Test]
        public void StringSwitch_GetDebugState_ShowsValueAndMatchedCase()
        {
            var node = Create<StringSwitchNode>();
            var variable = Create<StringVariable>();
            variable.CurrentValue = "b";
            node.stringVariable = variable;
            node.cases = new List<StringReference> { new("a"), new("b") };
            var casePort = node.AddDynamicOutput(typeof(GraphConnection), Node.ConnectionType.Override, Node.TypeConstraint.None, "cases 1");

            var matched = ((IGraphNodeDebugState) node).GetDebugState(new NodeDebugContext(null, 0d, 1, casePort));
            var unknown = ((IGraphNodeDebugState) node).GetDebugState(new NodeDebugContext(null, 0d, 1, null));

            Assert.AreEqual("value \"b\" -> case 1 \"b\"", matched);
            Assert.AreEqual("value \"b\"", unknown);
        }
    }
}
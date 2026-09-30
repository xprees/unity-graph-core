using System.Linq;
using NUnit.Framework;
using UnityEngine;
using XNode;
using Xprees.Graph.Core.Attributes;
using Xprees.Graph.Core.Editor.NodeMenu;
using Xprees.Graph.Core.Editor.Validation;

namespace Xprees.Graph.Core.Tests
{
    public class NodeTypeFilterTests
    {
        private class GraphA : NodeGraph
        {
        }

        private class GraphB : NodeGraph
        {
        }

        private class GraphAChild : GraphA
        {
        }

        private class FreeNode : Node
        {
            public override object GetValue(NodePort port) => null;
        }

        [AllowOnlyInGraphs(typeof(GraphA))]
        private class OnlyANode : Node
        {
            public override object GetValue(NodePort port) => null;
        }

        private class OnlyANodeDerived : OnlyANode
        {
        }

        [ExcludeFromGraphs(typeof(GraphB))]
        private class NotBNode : Node
        {
            public override object GetValue(NodePort port) => null;
        }

        [AllowOnlyInGraphs(typeof(GraphA), typeof(GraphB))]
        [ExcludeFromGraphs(typeof(GraphB))]
        private class ExcludeWinsNode : Node
        {
            public override object GetValue(NodePort port) => null;
        }

        [Test]
        public void NoAttribute_IsAllowedEverywhere()
        {
            Assert.IsTrue(NodeTypeFilter.IsAllowed(typeof(FreeNode), typeof(GraphA)));
            Assert.IsTrue(NodeTypeFilter.IsAllowed(typeof(FreeNode), typeof(GraphB)));
        }

        [Test]
        public void AllowOnly_IsAllowedOnlyInListedGraph()
        {
            Assert.IsTrue(NodeTypeFilter.IsAllowed(typeof(OnlyANode), typeof(GraphA)));
            Assert.IsFalse(NodeTypeFilter.IsAllowed(typeof(OnlyANode), typeof(GraphB)));
        }

        [Test]
        public void AllowOnly_MatchesSubclassesOfGraph()
        {
            Assert.IsTrue(NodeTypeFilter.IsAllowed(typeof(OnlyANode), typeof(GraphAChild)));
        }

        [Test]
        public void AllowOnly_IsInheritedByDerivedNodes()
        {
            Assert.IsTrue(NodeTypeFilter.IsAllowed(typeof(OnlyANodeDerived), typeof(GraphA)));
            Assert.IsFalse(NodeTypeFilter.IsAllowed(typeof(OnlyANodeDerived), typeof(GraphB)));
        }

        [Test]
        public void Exclude_HidesNodeOnlyInListedGraph()
        {
            Assert.IsFalse(NodeTypeFilter.IsAllowed(typeof(NotBNode), typeof(GraphB)));
            Assert.IsTrue(NodeTypeFilter.IsAllowed(typeof(NotBNode), typeof(GraphA)));
        }

        [Test]
        public void Exclude_WinsOverAllowOnly()
        {
            Assert.IsTrue(NodeTypeFilter.IsAllowed(typeof(ExcludeWinsNode), typeof(GraphA)));
            Assert.IsFalse(NodeTypeFilter.IsAllowed(typeof(ExcludeWinsNode), typeof(GraphB)));
        }

        [Test]
        public void Validator_WarnsAboutDisallowedNodeInGraph()
        {
            var graph = ScriptableObject.CreateInstance<GraphB>();
            try
            {
                graph.AddNode<OnlyANode>();
                graph.AddNode<FreeNode>();

                var issues = GraphValidator.Validate(graph);

                var disallowed = issues.Where(i => i.Node is OnlyANode && i.Message.Contains("not allowed")).ToList();
                Assert.AreEqual(1, disallowed.Count);
                Assert.AreEqual(GraphIssueSeverity.Warning, disallowed[0].Severity);
                Assert.IsFalse(issues.Any(i => i.Node is FreeNode && i.Message.Contains("not allowed")));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }
    }
}
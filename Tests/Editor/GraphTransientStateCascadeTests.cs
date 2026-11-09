using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Xprees.Graph.Core.Base;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Tests
{
    /// Nodes are stateless for StateSnapshotService, so a reset reaches them only through the graph. A node flag that survives the reset
    /// leaks into the next run (a wait passes at once because its event happened in the previous run).
    public class GraphTransientStateCascadeTests
    {
        private readonly List<GraphBase> _graphs = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var graph in _graphs) Object.DestroyImmediate(graph);
            _graphs.Clear();
        }

        private GraphBase NewGraph()
        {
            var graph = ScriptableObject.CreateInstance<GraphBase>();
            _graphs.Add(graph);
            return graph;
        }

        private static TestWaitFlagNode RaisedWait(GraphBase graph)
        {
            var node = graph.AddNode<TestWaitFlagNode>();
            node.Raised = true;
            return node;
        }

        private static SubGraphNode SubGraph(GraphBase parent, GraphBase child)
        {
            var node = parent.AddNode<SubGraphNode>();
            node.subGraph = child;
            return node;
        }

        [Test]
        public void ClearTransientState_ClearsTheNodesOfTheGraph()
        {
            var graph = NewGraph();
            var wait = RaisedWait(graph);

            graph.ClearTransientState();

            Assert.IsFalse(wait.Raised);
        }

        [Test]
        public void ClearTransientState_ReachesNodesOfNestedSubGraphs()
        {
            var root = NewGraph();
            var child = NewGraph();
            var grandChild = NewGraph();
            SubGraph(root, child);
            SubGraph(child, grandChild);
            var childWait = RaisedWait(child);
            var grandChildWait = RaisedWait(grandChild);

            root.ClearTransientState();

            Assert.IsFalse(childWait.Raised);
            Assert.IsFalse(grandChildWait.Raised);
        }

        [Test]
        public void ClearTransientState_CyclicSubGraphs_DoesNotRecurseForever()
        {
            var a = NewGraph();
            var b = NewGraph();
            SubGraph(a, b);
            SubGraph(b, a);
            var waitA = RaisedWait(a);
            var waitB = RaisedWait(b);

            Assert.DoesNotThrow(() => a.ClearTransientState());

            Assert.IsFalse(waitA.Raised);
            Assert.IsFalse(waitB.Raised);
        }

        [Test]
        public void ClearTransientState_EmptySubGraphReference_IsSkipped()
        {
            var graph = NewGraph();
            SubGraph(graph, null);
            var wait = RaisedWait(graph);

            Assert.DoesNotThrow(() => graph.ClearTransientState());

            Assert.IsFalse(wait.Raised);
        }
    }
}
using NUnit.Framework;
using UnityEngine;
using XNode;
using Xprees.Graph.Core.Editor.Debugger;

namespace Xprees.Graph.Core.Tests
{
    public class GraphDebugRendererTests
    {
        [TestCase(0, false, false, EdgeVisual.Untaken)]
        [TestCase(0, false, true, EdgeVisual.Untaken)]
        [TestCase(3, false, false, EdgeVisual.Taken)]
        [TestCase(3, true, false, EdgeVisual.TakenAmbiguous)]
        [TestCase(1, false, true, EdgeVisual.Active)]
        [TestCase(1, true, true, EdgeVisual.Active)]
        public void Classify_Table(int count, bool ambiguous, bool active, EdgeVisual expected) =>
            Assert.AreEqual(expected, GraphDebugRenderer.Classify(count, ambiguous, active));

        [Test]
        public void GetState_WithoutNodeIsIdle() => Assert.AreEqual(NodeVisualState.Idle, GraphDebugRenderer.GetState(null));

        [Test]
        public void ToggleOn_AddsToAllWhenAnyLacksThenRemovesAll()
        {
            GraphBreakpoints.ResetSession();
            var a = ScriptableObject.CreateInstance<TestPassNode>();
            var b = ScriptableObject.CreateInstance<TestPassNode>();
            try
            {
                GraphBreakpoints.SetEnabled(a, true);

                GraphDebugShortcuts.ToggleOn(new Node[] { a, b });
                Assert.IsTrue(GraphBreakpoints.Has(a));
                Assert.IsTrue(GraphBreakpoints.Has(b));

                GraphDebugShortcuts.ToggleOn(new Node[] { a, b });
                Assert.IsFalse(GraphBreakpoints.Has(a));
                Assert.IsFalse(GraphBreakpoints.Has(b));
            }
            finally
            {
                GraphBreakpoints.ResetSession();
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }
    }
}
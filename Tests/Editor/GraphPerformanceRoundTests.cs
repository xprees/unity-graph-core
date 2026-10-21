using NUnit.Framework;
using UnityEngine;
using Xprees.Graph.Core.Editor.Graph;

namespace Xprees.Graph.Core.Tests
{
    public class NoodleCullingTests
    {
        private static readonly Rect View = new(0, 0, 1000, 600);

        [Test]
        public void BothEndsFarLeft_Skips() =>
            Assert.IsTrue(NoodleCulling.CanSkipCurve(View, new Vector2(-3000, 100), new Vector2(-2500, 300)));

        [Test]
        public void BothEndsFarBelow_Skips() => Assert.IsTrue(NoodleCulling.CanSkipCurve(View, new Vector2(100, 2000), new Vector2(400, 2100)));

        [Test]
        public void EndInsideView_Draws() => Assert.IsFalse(NoodleCulling.CanSkipCurve(View, new Vector2(500, 300), new Vector2(5000, 300)));

        [Test]
        public void LongConnectionAcrossTheView_Draws() =>
            Assert.IsFalse(NoodleCulling.CanSkipCurve(View, new Vector2(-2000, 300), new Vector2(3000, 300)));

        [Test]
        public void TangentReach_KeepsCurveThatBulgesIntoView()
        {
            // Ends just outside to the left, but the horizontal tangents (half the distance) can bulge into the view
            Assert.IsFalse(NoodleCulling.CanSkipCurve(View, new Vector2(-400, 100), new Vector2(-30, 500)));
        }
    }

    public class GraphLodTests
    {
        [Test]
        public void ZoomedOut_NotSelected_Collapses() => Assert.IsTrue(GraphLod.ShouldCollapse(true, 2f, 1.6f, false));

        [Test]
        public void ZoomedIn_NeverCollapses() => Assert.IsFalse(GraphLod.ShouldCollapse(true, 1f, 1.6f, false));

        [Test]
        public void ExactlyAtThreshold_StaysFull() => Assert.IsFalse(GraphLod.ShouldCollapse(true, 1.6f, 1.6f, false));

        [Test]
        public void SelectedNode_StaysFull() => Assert.IsFalse(GraphLod.ShouldCollapse(true, 5f, 1.6f, true));

        [Test]
        public void Disabled_NeverCollapses() => Assert.IsFalse(GraphLod.ShouldCollapse(false, 5f, 1.6f, false));
    }
}
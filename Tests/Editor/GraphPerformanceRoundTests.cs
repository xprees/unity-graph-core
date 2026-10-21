using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Xprees.Graph.Core.Editor.Graph;
using Xprees.Graph.Core.Editor.Nodes;

namespace Xprees.Graph.Core.Tests
{
    public class NodeOrderGuardTests
    {
        private sealed class Item
        {
            public readonly string Name;
            public Item(string name) => Name = name;
        }

        private static string Names(List<Item> list) => string.Join(",", list.ConvertAll(i => i.Name));

        [Test]
        public void GroupMovedToFront_IsPutBack_OthersKeepOrder()
        {
            var a = new Item("a");
            var b = new Item("b");
            var c = new Item("c");
            var list = new List<Item> { a, b, c };

            // What the xNodeGroups editor does on Repaint
            list.Remove(b);
            list.Insert(0, b);

            Assert.IsTrue(NodeOrderGuard.RestoreIndex(list, b, 1));
            Assert.AreEqual("a,b,c", Names(list));
        }

        [Test]
        public void NothingMoved_NothingChanges()
        {
            var a = new Item("a");
            var b = new Item("b");
            var list = new List<Item> { a, b };

            Assert.IsFalse(NodeOrderGuard.RestoreIndex(list, b, 1));
            Assert.AreEqual("a,b", Names(list));
        }

        [Test]
        public void InvalidIndexOrMissingItem_IsIgnored()
        {
            var a = new Item("a");
            var list = new List<Item> { a };

            Assert.IsFalse(NodeOrderGuard.RestoreIndex(list, a, -1));
            Assert.IsFalse(NodeOrderGuard.RestoreIndex(list, a, 5));
            Assert.IsFalse(NodeOrderGuard.RestoreIndex(list, new Item("x"), 0));
        }

        [Test]
        public void RepeatedRepaints_WithManyGroups_NeverRotateTheList()
        {
            var items = new List<Item>();
            for (var i = 0; i < 12; i++) items.Add(new Item("g" + i));
            items.Add(new Item("n"));
            var before = Names(items);

            for (var repaint = 0; repaint < 50; repaint++)
            {
                for (var k = 0; k < 12; k++)
                {
                    var group = items[k];
                    var index = items.IndexOf(group);
                    items.Remove(group);
                    items.Insert(0, group);
                    NodeOrderGuard.RestoreIndex(items, group, index);
                }
            }

            Assert.AreEqual(before, Names(items));
        }
    }

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
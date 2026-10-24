using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Xprees.Graph.Core.Editor.Flow;

namespace Xprees.Graph.Core.Tests
{
    public class FlowSelectionTests
    {
        private sealed class N
        {
            public readonly string Name;
            public readonly List<N> Out = new();
            public readonly List<N> In = new();
            public N(string name) => Name = name;
        }

        private static N Link(N from, N to)
        {
            from.Out.Add(to);
            to.In.Add(from);
            return to;
        }

        private static string Names(IEnumerable<N> nodes) => string.Join(",", nodes.Select(n => n.Name).OrderBy(n => n));

        private static IEnumerable<N> Both(N n) => n.Out.Concat(n.In);

        [Test]
        public void Following_FromMiddle_IncludesOnlyDownstream()
        {
            var a = new N("A");
            var b = new N("B");
            var c = new N("C");
            var d = new N("D");
            Link(Link(Link(a, b), c), d);

            Assert.AreEqual("B,C,D", Names(FlowSelection.Reach(b, n => n.Out)));
        }

        [Test]
        public void Connected_FromMiddle_IncludesBothSidesAndBranches()
        {
            var a = new N("A");
            var b = new N("B");
            var c = new N("C");
            var side = new N("Side");
            var unrelated = new N("Unrelated");
            Link(a, b);
            Link(b, c);
            Link(side, b);

            var result = FlowSelection.Reach(b, Both);

            Assert.AreEqual("A,B,C,Side", Names(result));
            CollectionAssert.DoesNotContain(result, unrelated);
        }

        [Test]
        public void Reach_StartsWithTheStartNodeAndVisitsEachNodeOnce()
        {
            var a = new N("A");
            var b = new N("B");
            var c = new N("C");
            Link(a, b);
            Link(a, c);
            Link(b, c); // merge: C reachable twice

            var result = FlowSelection.Reach(a, n => n.Out);

            Assert.AreSame(a, result[0]);
            Assert.AreEqual(3, result.Count);
        }

        [Test]
        public void Reach_HandlesLoops()
        {
            var a = new N("A");
            var b = new N("B");
            Link(a, b);
            Link(b, a);

            Assert.AreEqual("A,B", Names(FlowSelection.Reach(a, n => n.Out)));
        }

        [Test]
        public void Reach_NullStart_IsEmpty() => Assert.IsEmpty(FlowSelection.Reach<N>(null, n => n.Out));
    }
}
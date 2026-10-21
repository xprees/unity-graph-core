using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Xprees.Graph.Core.Base;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Editor.Bookmarks;
using Xprees.Graph.Core.Editor.Minimap;
using Xprees.Graph.Core.Editor.Search;
using Xprees.Graph.Core.Nodes.Debug;

namespace Xprees.Graph.Core.Tests
{
    public class GraphSearchTests
    {
        private GraphBase _root;
        private GraphBase _child;

        [SetUp]
        public void SetUp()
        {
            _root = ScriptableObject.CreateInstance<GraphBase>();
            _root.name = "RootGraph";
            _child = ScriptableObject.CreateInstance<GraphBase>();
            _child.name = "ChildGraph";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_child);
        }

        [Test]
        public void MatchesNameTypeAndNoteText_CaseInsensitive()
        {
            var named = _root.AddNode<TestRecordNode>();
            named.name = "Open The Door";
            var note = _root.AddNode<NoteNode>();
            note.name = "Note";
            note.text = "Call the boss about the BADGE";
            _root.AddNode<TestPassNode>().name = "x";

            Assert.AreEqual(named, GraphSearch.Find(_root, "the door").Single().Node);
            Assert.AreEqual(GraphSearchMatchKind.Text, GraphSearch.Find(_root, "badge").Single().Kind);
            Assert.AreEqual(GraphSearchMatchKind.Type, GraphSearch.Find(_root, "TestPassNode").Single().Kind);
        }

        [Test]
        public void MatchesReferencedAssetName_AndOneResultPerNode()
        {
            var sub = _root.AddNode<SubGraphNode>();
            sub.name = "Enter";
            sub.subGraph = _child;

            var result = GraphSearch.Find(_root, "ChildGraph").Single();
            Assert.AreEqual(sub, result.Node);
            Assert.AreEqual(GraphSearchMatchKind.Reference, result.Kind);
        }

        [Test]
        public void EmptyQuery_FindsNothing() => Assert.IsEmpty(GraphSearch.Find(_root, "  "));

        [Test]
        public void SearchesSubGraphs_RootFirst()
        {
            var sub = _root.AddNode<SubGraphNode>();
            sub.subGraph = _child;
            _root.AddNode<TestRecordNode>().name = "Findme root";
            _child.AddNode<TestRecordNode>().name = "Findme child";

            var results = GraphSearch.Find(_root, "Findme");

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(_root, results[0].Graph);
            Assert.AreEqual(_child, results[1].Graph);
        }

        [Test]
        public void ReferenceCycle_Terminates_AndReportsEachNodeOnce()
        {
            _root.AddNode<SubGraphNode>().subGraph = _child;
            _child.AddNode<SubGraphNode>().subGraph = _root;
            _child.AddNode<TestRecordNode>().name = "Loop target";

            var results = GraphSearch.Find(_root, "Loop target");

            Assert.AreEqual(1, results.Count);
        }
    }

    public class GraphBookmarkTableTests
    {
        [Test]
        public void SetAndGet_RoundTrip_ThroughJson()
        {
            var table = new BookmarkTable();
            Assert.IsTrue(table.Set("guidA", 3, "node-1"));
            Assert.IsTrue(table.Set("guidB", 3, "node-2"));

            var copy = JsonUtility.FromJson<BookmarkTable>(JsonUtility.ToJson(table));

            Assert.IsTrue(copy.TryGet("guidA", 3, out var a));
            Assert.AreEqual("node-1", a);
            Assert.IsTrue(copy.TryGet("guidB", 3, out var b));
            Assert.AreEqual("node-2", b);
            Assert.IsFalse(copy.TryGet("guidA", 4, out _));
        }

        [Test]
        public void Set_ReplacesSlot_AndRemoveClearsIt()
        {
            var table = new BookmarkTable();
            table.Set("g", 1, "old");
            table.Set("g", 1, "new");

            Assert.AreEqual(1, table.Entries.Count);
            table.TryGet("g", 1, out var id);
            Assert.AreEqual("new", id);
            Assert.IsTrue(table.Remove("g", 1));
            Assert.IsFalse(table.TryGet("g", 1, out _));
        }

        [Test]
        public void InvalidInput_IsRejected()
        {
            var table = new BookmarkTable();

            Assert.IsFalse(table.Set("g", 0, "n"));
            Assert.IsFalse(table.Set("g", 10, "n"));
            Assert.IsFalse(table.Set("", 1, "n"));
            Assert.IsFalse(table.Set("g", 1, null));
        }
    }

    public class MinimapTransformTests
    {
        private readonly static Rect world = new(100, 200, 400, 200);

        [Test]
        public void ToMapAndBack_RoundTrips()
        {
            var transform = new MinimapTransform(world, new Vector2(220, 150), 6);
            var grid = new Vector2(321.5f, 287f);

            var back = transform.ToGrid(transform.ToMap(grid));

            Assert.AreEqual(grid.x, back.x, 0.001f);
            Assert.AreEqual(grid.y, back.y, 0.001f);
        }

        [Test]
        public void WorldFitsInsidePaddedMap_AndIsCentered()
        {
            var transform = new MinimapTransform(world, new Vector2(220, 150), 10);

            var min = transform.ToMap(world.min);
            var max = transform.ToMap(world.max);

            Assert.GreaterOrEqual(min.x, 10 - 0.001f);
            Assert.LessOrEqual(max.x, 210 + 0.001f);
            Assert.GreaterOrEqual(min.y, 10 - 0.001f);
            Assert.LessOrEqual(max.y, 140 + 0.001f);
            Assert.AreEqual(110f, (min.x + max.x) * 0.5f, 0.001f);
            Assert.AreEqual(75f, (min.y + max.y) * 0.5f, 0.001f);
        }

        [Test]
        public void ViewportIsCenteredOnNegativePanOffset_AndScaledByZoom()
        {
            var rect = MinimapTransform.ViewportInGrid(new Vector2(-300, -250), 2f, new Vector2(800, 600));

            Assert.AreEqual(new Vector2(300, 250), rect.center);
            Assert.AreEqual(new Vector2(1600, 1200), rect.size);
        }

        [Test]
        public void PanningToAMapPoint_CentersThatGridPoint()
        {
            var transform = new MinimapTransform(world, new Vector2(220, 150), 6);
            var clicked = transform.ToGrid(new Vector2(50, 40));

            var viewport = MinimapTransform.ViewportInGrid(MinimapTransform.PanOffsetFor(clicked), 1.5f, new Vector2(640, 480));

            Assert.AreEqual(clicked.x, viewport.center.x, 0.001f);
            Assert.AreEqual(clicked.y, viewport.center.y, 0.001f);
        }

        [Test]
        public void Bounds_OfNoRects_IsUnitRect_AndOfRects_IsUnion()
        {
            Assert.AreEqual(new Rect(0, 0, 1, 1), MinimapTransform.Bounds(new Rect[0]));
            Assert.AreEqual(Rect.MinMaxRect(0, -10, 150, 50),
                MinimapTransform.Bounds(new[] { new Rect(0, 0, 50, 50), new Rect(100, -10, 50, 20) }));
        }
    }
}
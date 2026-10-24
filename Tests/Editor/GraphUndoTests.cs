using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XNode;
using XNodeEditor;
using Xprees.Graph.Core.Base;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Editor.Graph;
using Xprees.Graph.Core.Editor.NodeMenu;
using Xprees.Graph.Core.Editor.Refactor;
using Xprees.Graph.Core.Editor.Validation;

namespace Xprees.Graph.Core.Tests
{
    /// Undo round trips of graph-altering editor operations on a saved graph asset (nodes are sub-assets, like in real graphs).
    /// Each operation runs in its own undo group, so a single PerformUndo reverts only the tested operation.
    public class GraphUndoTests
    {
        private const string folderName = "__GraphUndoTests";
        private const string folder = "Assets/" + folderName;
        private const string input = nameof(LinearFlowBaseNode.input);
        private const string output = nameof(LinearFlowBaseNode.output);

        private GraphBase _graph;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", folderName);

            _path = $"{folder}/UndoGraph.asset";
            _graph = ScriptableObject.CreateInstance<GraphBase>();
            AssetDatabase.CreateAsset(_graph, _path);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var path in AssetDatabase.FindAssets("", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath))
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path).Where(a => a)) Undo.ClearUndo(asset);
            }

            AssetDatabase.DeleteAsset(folder);
        }

        #region Helpers

        private T Add<T>(string name) where T : Node
        {
            var node = _graph.AddNode<T>();
            node.name = name;
            AssetDatabase.AddObjectToAsset(node, _graph);
            return node;
        }

        private Node Find(string name) => _graph.nodes.FirstOrDefault(n => n && n.name == name);

        /// Raw check on one side of an edge (NodePort.GetConnections would hide broken entries).
        private bool Lists(string from, string fromPort, string to, string toPort) =>
            PortConnectionAccessor.ListsConnectionTo(Find(from).GetPort(fromPort), Find(to).GetPort(toPort));

        private bool Linked(string from, string to) => Lists(from, output, to, input) && Lists(to, input, from, output);

        /// Setup made with xNode calls may leave Undo records, keep them out of the group under test.
        private static void EndSetup()
        {
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
        }

        private static void UndoOnce()
        {
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
        }

        /// A -> B -> C chain of pass nodes.
        private void BuildChain()
        {
            var a = Add<TestPassNode>("A");
            var b = Add<TestPassNode>("B");
            var c = Add<TestPassNode>("C");
            a.GetOutputPort(output).Connect(b.GetInputPort(input));
            b.GetOutputPort(output).Connect(c.GetInputPort(input));
            EndSetup();
        }

        #endregion

        [Test]
        public void Connect_OverrideOutput_UndoRestoresTheReplacedEdgeOnBothSides()
        {
            BuildChain();
            var d = Add<TestPassNode>("D");
            EndSetup();

            using (GraphUndo.Group("Connect"))
            {
                GraphUndo.Connect(Find("A").GetOutputPort(output), d.GetInputPort(input));
            }

            Assert.IsTrue(Linked("A", "D"));
            Assert.IsFalse(Lists("B", input, "A", output), "The Override output drops the old edge on the target node too");

            UndoOnce();

            Assert.IsTrue(Linked("A", "B"), "The replaced edge must come back on both nodes");
            Assert.IsFalse(Lists("D", input, "A", output));
            Assert.IsFalse(Lists("A", output, "D", input));
        }

        [Test]
        public void Disconnect_UndoRestoresBothSides()
        {
            BuildChain();

            using (GraphUndo.Group("Disconnect"))
            {
                GraphUndo.Disconnect(Find("A").GetOutputPort(output), Find("B").GetInputPort(input));
            }

            Assert.IsFalse(Lists("A", output, "B", input));
            Assert.IsFalse(Lists("B", input, "A", output));

            UndoOnce();

            Assert.IsTrue(Linked("A", "B"));
        }

        [Test]
        public void RemoveNode_UndoRestoresSubAssetAndConnections()
        {
            BuildChain();
            var count = _graph.nodes.Count;

            using (GraphUndo.Group("Remove B"))
            {
                GraphUndo.RemoveNode(_graph, Find("B"), "Remove B");
            }

            Assert.IsNull(Find("B"));
            Assert.AreEqual(count - 1, _graph.nodes.Count);
            Assert.AreEqual(0, Find("A").GetOutputPort(output).ConnectionCount);

            UndoOnce();

            var b = Find("B");
            Assert.IsNotNull(b);
            Assert.AreEqual(count, _graph.nodes.Count);
            // AssetDatabase.IsSubAsset reports false right after the undo, the object is back in the graph file though
            Assert.IsTrue(EditorUtility.IsPersistent(b));
            Assert.AreEqual(_path, AssetDatabase.GetAssetPath(b), "The node must be part of the graph asset again");
            Assert.IsTrue(Linked("A", "B"));
            Assert.IsTrue(Linked("B", "C"));
        }

        [Test]
        public void NodeCreationPopup_CreateNode_UndoRemovesTheNodeSubAsset()
        {
            var count = _graph.nodes.Count;
            EndSetup();

            var graphEditor = NodeGraphEditor.GetEditor(_graph, null);
            var node = NodeCreationPopup.CreateNode(graphEditor, null, typeof(TestPassNode), new Vector2(100, 50), autoConnect: false);

            Assert.IsTrue(node);
            Assert.AreEqual(count + 1, _graph.nodes.Count);
            Assert.IsTrue(AssetDatabase.IsSubAsset(node));

            UndoOnce();

            Assert.AreEqual(count, _graph.nodes.Count);
            Assert.IsFalse(node, "The created node must be destroyed");
            Assert.IsFalse(AssetDatabase.LoadAllAssetsAtPath(_path).OfType<TestPassNode>().Any(n => n));
        }

        [Test]
        public void FixAllSafe_AddBackLink_UndoRemovesTheBackLink()
        {
            var a = Add<TestPassNode>("A");
            var b = Add<TestPassNode>("B");
            PortConnectionAccessor.AddConnection(a.GetOutputPort(output), b.GetInputPort(input)); // One-sided link
            EndSetup();

            Assert.Greater(GraphValidator.FixAllSafe(_graph), 0);
            Assert.IsTrue(Linked("A", "B"));

            UndoOnce();

            Assert.IsTrue(Lists("A", output, "B", input));
            Assert.IsFalse(Lists("B", input, "A", output), "The added back-link must be removed again");
        }

        [Test]
        public void FixAllSafe_RemoveMissingNodes_UndoRestoresTheEntries()
        {
            Add<TestPassNode>("A");
            _graph.nodes.Add(null);
            EndSetup();
            var count = _graph.nodes.Count;

            Assert.Greater(GraphValidator.FixAllSafe(_graph), 0);
            Assert.AreEqual(count - 1, _graph.nodes.Count);

            UndoOnce();

            Assert.AreEqual(count, _graph.nodes.Count);
        }

        [Test]
        public void ApplyFix_IsOneUndoStep()
        {
            var a = Add<TestPassNode>("A");
            var b = Add<TestPassNode>("B");
            PortConnectionAccessor.AddConnection(a.GetOutputPort(output), b.GetInputPort(input));
            EndSetup();

            var issue = GraphValidator.Validate(_graph).First(i => i.IsSafelyFixable && i.Node == a);
            GraphValidator.ApplyFix(issue);
            Assert.IsTrue(Linked("A", "B"));

            UndoOnce();

            Assert.IsNotNull(Find("B"), $"Nodes after undo: {string.Join(", ", _graph.nodes.Select(n => n ? n.name : "null"))}");
            Assert.IsFalse(Lists("B", input, "A", output));
        }

        [Test]
        public void NestedGroups_CollapseIntoOneStep()
        {
            BuildChain();

            using (GraphUndo.Group("Outer"))
            {
                using (GraphUndo.Group("Inner 1")) GraphUndo.Disconnect(Find("A").GetOutputPort(output), Find("B").GetInputPort(input));
                using (GraphUndo.Group("Inner 2")) GraphUndo.Disconnect(Find("B").GetOutputPort(output), Find("C").GetInputPort(input));
            }

            UndoOnce();

            Assert.IsTrue(Linked("A", "B"));
            Assert.IsTrue(Linked("B", "C"), "Both inner edits must be reverted by one undo");
        }

        [Test]
        public void SubGraphExtraction_OneUndoTakesBackAllRecords_AndRestoresTheSourceGraph()
        {
            // Start -> A -> B -> C -> End, extract A + B
            BuildChain();
            var start = _graph.nodes.OfType<StartNode>().FirstOrDefault() ?? Add<StartNode>("Start");
            var end = _graph.nodes.OfType<EndNode>().FirstOrDefault() ?? Add<EndNode>("End");
            start.GetOutputPort(nameof(StartNode.start)).Connect(Find("A").GetInputPort(input));
            Find("C").GetOutputPort(output).Connect(end.GetInputPort(nameof(EndNode.end)));
            EditorUtility.SetDirty(_graph);
            AssetDatabase.SaveAssets();
            EndSetup();

            var getRecords = typeof(Undo).GetMethod("GetRecords", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public, null,
                new[] { typeof(List<string>), typeof(List<string>) }, null);
            if (getRecords == null) Assert.Ignore("Undo.GetRecords is not available in this Unity version.");

            List<string> UndoRecords()
            {
                var undo = new List<string>();
                getRecords.Invoke(null, new object[] { undo, new List<string>() });
                return undo;
            }

            var before = UndoRecords().Count;
            var result = SubGraphExtractionPlanner.Plan(_graph, new[] { Find("A"), Find("B") });
            Assert.IsTrue(result.CanExtract, string.Join("; ", result.errors));

            var created = SubGraphExtractor.Extract(result.plan, folder, "Extracted");
            Assert.IsTrue(created);
            Assert.IsNull(Find("A"));
            Assert.Greater(UndoRecords().Count, before);

            UndoOnce();

            // One Ctrl+Z must take back every record the extraction made (xNode's Connect records included), nothing may stay for a second one
            var left = UndoRecords();
            Assert.AreEqual(before, left.Count, $"Records left after one undo: {string.Join(" | ", left.Skip(before))}");

            _graph = AssetDatabase.LoadAssetAtPath<GraphBase>(_path);
            Assert.IsNotNull(Find("A"));
            Assert.IsNotNull(Find("B"));
            Assert.IsTrue(Linked("A", "B"));
            Assert.IsTrue(Linked("B", "C"));
            Assert.IsFalse(_graph.nodes.OfType<SubGraphNode>().Any(n => n));
        }
    }
}
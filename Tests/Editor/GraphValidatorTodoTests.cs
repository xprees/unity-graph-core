using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Xprees.Graph.Core.Base;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Editor.Validation;
using Xprees.Graph.Core.Nodes.Debug;

namespace Xprees.Graph.Core.Tests
{
    public class GraphValidatorTodoTests
    {
        private GraphBase _graph;

        [SetUp]
        public void SetUp() => _graph = ScriptableObject.CreateInstance<GraphBase>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        private static List<GraphIssue> Todos(GraphBase graph) =>
            GraphValidator.Validate(graph).Where(i => i.Severity == GraphIssueSeverity.Todo).ToList();

        [Test]
        public void NoteWithTodo_ReportsTodoIssue_NotProblem_NotFixable()
        {
            var note = _graph.AddNode<NoteNode>();
            note.name = "Note";
            note.text = "TODO: wire the reply\nsecond line";

            var todos = Todos(_graph);

            Assert.AreEqual(1, todos.Count);
            Assert.AreSame(note, todos[0].Node);
            Assert.AreEqual("TODO: wire the reply", todos[0].Message);
            Assert.IsFalse(todos[0].IsProblem);
            Assert.IsFalse(todos[0].IsFixable);
            Assert.IsFalse(todos[0].IsSafelyFixable);
            Assert.IsFalse(GraphValidator.Validate(_graph).Any(i => i.IsProblem));
        }

        [TestCase("fix this FIXME later")]
        [TestCase("Hack - temporary")]
        [TestCase("note\nsee todo below")]
        public void Markers_AreDetectedAtWordBoundary(string text)
        {
            var note = _graph.AddNode<NoteNode>();
            note.name = "Note";
            note.text = text;

            Assert.AreEqual(1, Todos(_graph).Count);
        }

        [TestCase("Remember the todos")]
        [TestCase("autodoc hackathon")]
        [TestCase("")]
        [TestCase(null)]
        public void NoMarker_NoIssue(string text)
        {
            var note = _graph.AddNode<NoteNode>();
            note.name = "Note";
            note.text = text;

            Assert.AreEqual(0, Todos(_graph).Count);
        }

        [Test]
        public void NodeNameAndGroupTitle_AreScanned()
        {
            var end = _graph.AddNode<EndNode>();
            end.name = "End - FIXME";
            var group = _graph.AddNode<NodeGroup>();
            group.name = "TODO cleanup";

            var todos = Todos(_graph);

            Assert.AreEqual(2, todos.Count);
            Assert.IsTrue(todos.All(i => !i.IsProblem && !i.IsFixable));
        }

        [Test]
        public void NoteWithMarkerInTextAndName_ReportsOnce()
        {
            var note = _graph.AddNode<NoteNode>();
            note.name = "TODO";
            note.text = "todo something";

            Assert.AreEqual(1, Todos(_graph).Count);
        }

        [Test]
        public void IsProblem_OnlyForErrorAndWarning()
        {
            GraphIssue Make(GraphIssueSeverity severity) => new(severity, _graph, null, null, "m");

            Assert.IsTrue(Make(GraphIssueSeverity.Error).IsProblem);
            Assert.IsTrue(Make(GraphIssueSeverity.Warning).IsProblem);
            Assert.IsFalse(Make(GraphIssueSeverity.Todo).IsProblem);
        }

        [Test]
        public void FixAllSafe_IgnoresTodoIssues()
        {
            var note = _graph.AddNode<NoteNode>();
            note.name = "Note";
            note.text = "TODO";

            Assert.AreEqual(0, GraphValidator.FixAllSafe(_graph));
        }
    }
}
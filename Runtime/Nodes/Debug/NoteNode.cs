using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NaughtyAttributes;
using UnityEngine;
using XNode;
using Xprees.Graph.Core.Attributes;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Nodes.Debug
{
    /// This node has no functionality, it's just a note.
    [NodeDescription("This node has no functionality, it's just a note. Not used in runtime.")]
    [NodeTint("#d1b100")]
    [NodeResizableWidth(250, 1000)]
    [CreateNodeMenu("Debug/Note")]
    public class NoteNode : BaseNode
    {
#if UNITY_EDITOR

        [Tooltip("Not used, just a note.")]
        [Label("")]
        [TextArea(2, 15)] public string text;
#endif

        public override object GetValue(NodePort port) => null;

        protected override UniTask<BaseNode> GetNextNode(CancellationToken cancellationToken = default) =>
            UniTask.FromResult<BaseNode>(null); // Not used in runtime, so no next node logic -> required by base class

#if UNITY_EDITOR
        private const string defaultNodeName = "Note";
        private const string todoNodeName = "TODO";

        public bool IsTodoNode => name == todoNodeName;

        private void OnValidate() => RenameAsTodoNodeIfStartsWithTodo();

        private void RenameAsTodoNodeIfStartsWithTodo()
        {
            if (name != defaultNodeName || name == todoNodeName) return;

            var isNotTodoNote = !(text?.Trim().StartsWith("todo", StringComparison.InvariantCultureIgnoreCase) ?? false);
            if (isNotTodoNote) return;

            name = todoNodeName;
        }
#endif
    }
}
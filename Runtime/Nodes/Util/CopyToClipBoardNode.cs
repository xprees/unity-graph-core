using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Xprees.Graph.Core.Attributes;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Variables.Reference.Primitive;

namespace Xprees.Graph.Core.Nodes.Util
{
    [NodeDescription("This node copies a text to users clipboard when triggered.")]
    [NodeResizableWidth(300)]
    [CreateNodeMenu("Util/Copy to clipboard")]
    public class CopyToClipBoardNode : LinearFlowBaseNode, IPassthroughNode
    {
        [Header("Data")]
        public StringReference textToCopy = new();

        protected override UniTask Trigger(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(textToCopy.Value)) return base.Trigger(cancellationToken);

            if (GUIUtility.systemCopyBuffer == textToCopy.Value) return base.Trigger(cancellationToken);

            GUIUtility.systemCopyBuffer = textToCopy.Value;
            return base.Trigger(cancellationToken);
        }

        public override void BackupStartState()
        {
            base.BackupStartState();
            textToCopy?.BackupStartState();
        }

        public override void ResetState()
        {
            base.ResetState();
            textToCopy?.ResetState();
        }
    }
}
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Xprees.Graph.Core.Attributes;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Variables.Reference.Primitive;

namespace Xprees.Graph.Core.Nodes.Util
{
    [NodeDescription("This node uses unity Application OpenURL to open an url in player's browser.")]
    [NodeResizableWidth(300)]
    [CreateNodeMenu("Util/Open URL")]
    public class OpenUrlNode : LinearFlowBaseNode, IPassthroughNode
    {
        [Header("Data")]
        [Tooltip("The url to open. See Application.OpenURL()")]
        public StringReference url = new();

        protected override UniTask Trigger(CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrEmpty(url.Value?.Trim())) Application.OpenURL(url.Value);
            return base.Trigger(cancellationToken);
        }

        public override void BackupStartState()
        {
            base.BackupStartState();
            url?.BackupStartState();
        }

        public override void ResetState()
        {
            base.ResetState();
            url?.ResetState();
        }
    }
}
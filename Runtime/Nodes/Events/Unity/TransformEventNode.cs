using UnityEngine;
using Xprees.Graph.Core.Nodes.Events.Base;

namespace Xprees.Graph.Core.Nodes.Events.Unity
{
    [CreateNodeMenu("Events/Unity/Transform Event")]
    public class TransformEventNode : AnchorEventChannelBaseNode<Transform>
    {
    }
}
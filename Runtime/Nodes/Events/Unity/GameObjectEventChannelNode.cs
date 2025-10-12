using UnityEngine;
using Xprees.Graph.Core.Nodes.Events.Base;

namespace Xprees.Graph.Core.Nodes.Events.Unity
{
    [CreateNodeMenu("Events/Unity/GameObject Event")]
    public class GameObjectEventChannelNode : AnchorEventChannelBaseNode<GameObject>
    {
    }
}
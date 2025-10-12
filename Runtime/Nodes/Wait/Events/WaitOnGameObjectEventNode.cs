using UnityEngine;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Nodes.Wait.Events
{
    [CreateNodeMenu("Wait/Wait on Any GameObjectEvent")]
    public class WaitOnGameObjectEventNode : WaitOnEventBaseNode<GameObject>
    {
    }
}
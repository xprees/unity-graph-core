using UnityEngine;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Variables.Base;

namespace Xprees.Graph.Core.Nodes.Wait.Variables.Base
{
    public abstract class VariableWaitBaseNode<T> : WaitBaseNode
    {
        [Header("Variable")]
        public VariableBaseSO<T> variable;

        public override void ResetState()
        {
            base.ResetState();
            variable?.ResetState();
        }
    }
}
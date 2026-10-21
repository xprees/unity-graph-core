using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Variables.Primitive;
using Xprees.Variables.Reference.Primitive;

namespace Xprees.Graph.Core.Tests
{
    /// Linear test node that references variables in the ways nodes do: a reference, a variable and a list of variables.
    public class TestVariableNode : LinearFlowBaseNode
    {
        public IntReference reference = new();
        public IntVariable direct;
        public List<IntVariable> many = new();

        protected override UniTask Trigger(CancellationToken cancellationToken = default) => UniTask.CompletedTask;
    }
}
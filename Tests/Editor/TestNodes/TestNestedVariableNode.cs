using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Variables.Primitive;

namespace Xprees.Graph.Core.Tests
{
    /// Linear test node that keeps variables inside a nested serializable class, alone and in a list (like the answers of a dialog node).
    public class TestNestedVariableNode : LinearFlowBaseNode
    {
        [Serializable]
        public class Item
        {
            public string label;
            public BoolVariable condition;
        }

        [Serializable]
        public class Group
        {
            public List<Item> items = new();
        }

        public Item single = new();
        public List<Item> list = new();
        public Group group = new();

        protected override UniTask Trigger(CancellationToken cancellationToken = default) => UniTask.CompletedTask;
    }
}
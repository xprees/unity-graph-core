// This file was originally forked from the Siccity/xNodeGroups (https://github.com/Siccity/xNodeGroups)

using System.Collections.Generic;
using UnityEngine;
using XNode;

namespace Xprees.Graph.Core.Base.Nodes
{
    [CreateNodeMenu("Group")]
    public class NodeGroup : Node
    {
        public int width = 400;
        public int height = 400;
        public Color color = new(1f, 1f, 1f, 0.1f);

        public override object GetValue(NodePort port)
        {
            return null;
        }

        /// Gets nodes in this group
        public List<Node> GetNodes()
        {
            var result = new List<Node>();
            foreach (var node in graph.nodes)
            {
                if (node == this) continue;
                if (node == null) continue;
                if (node.position.x < position.x) continue;
                if (node.position.y < position.y) continue;
                if (node.position.x > position.x + width) continue;
                if (node.position.y > position.y + height + 30) continue;
                result.Add(node);
            }

            return result;
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XNode;

namespace Xprees.Graph.Core.Editor.Refactor
{
    /// Everything needed to move a set of nodes into a new sub-graph. Produced by <see cref="SubGraphExtractionPlanner"/>, consumed by <see cref="SubGraphExtractor"/>.
    public class SubGraphExtractionPlan
    {
        public NodeGraph graph;

        /// Nodes that move to the new graph (selected nodes, group members and the groups themselves).
        public List<Node> members = new();

        /// Input port of a member that receives the flow from outside. It gets connected to the Start node of the new graph.
        public NodePort entryPort;

        /// Output ports outside of the selection feeding the entry port. They end up connected to the SubGraphNode.
        public List<NodePort> entrySources = new();

        /// Output ports of members leading outside. They get connected to the End node of the new graph.
        public List<NodePort> exitSources = new();

        /// Input port outside of the selection the flow continues in. Null when the extracted flow ends.
        public NodePort exitTarget;

        /// Every edge that crosses the selection boundary (output, input). They are cut when extracting.
        public List<(NodePort output, NodePort input)> crossingEdges = new();

        public string suggestedName = "SubGraph";

        public Vector2 TopLeft =>
            members.Count == 0 ? Vector2.zero : new Vector2(members.Min(n => n.position.x), members.Min(n => n.position.y));

        public Vector2 BottomRight =>
            members.Count == 0 ? Vector2.zero : new Vector2(members.Max(n => n.position.x), members.Max(n => n.position.y));

        public Vector2 Center => (TopLeft + BottomRight) * 0.5f;
    }

    public class SubGraphExtractionResult
    {
        public SubGraphExtractionPlan plan;
        public List<string> errors = new();
        public List<string> warnings = new();

        public bool CanExtract => plan != null && errors.Count == 0;
    }
}
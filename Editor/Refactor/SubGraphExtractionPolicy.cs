using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using XNode;
using Xprees.Graph.Core.Base;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Editor.Refactor
{
    /// Graph type specific rules of the sub-graph extraction (where the new asset goes, how it is set up).
    /// Games register their own policies for their graph types, e.g. from an [InitializeOnLoad] class.
    public interface ISubGraphExtractionPolicy
    {
        /// Larger priority wins when several policies apply to the same graph.
        int Priority { get; }

        bool AppliesTo(NodeGraph source);

        /// Project folder ("Assets/...") suggested for the new graph.
        string SuggestFolder(NodeGraph source);

        /// Sets up the new, not yet saved graph.
        void Configure(NodeGraph source, NodeGraph created);

        /// Called after both graphs were saved.
        void AfterExtract(NodeGraph source, NodeGraph created);

        /// Other assets the extraction changes (e.g. a baked registry of the source). Their files are restored by undo.
        /// Called before and after the extraction.
        IEnumerable<string> GetAffectedAssetPaths(NodeGraph source);

        /// The node that plays the new graph inside the source graph. It must derive from <see cref="LinearFlowBaseNode"/> (flow input / output).
        /// The default <see cref="SubGraphNode"/> runs the graph with a generic parser. A graph type whose nodes need the runtime of that graph
        /// (e.g. a dialog, shown by the dialog UI) returns its own node here, which also allows its graph specific nodes to be extracted.
        Type SubGraphNodeType => typeof(SubGraphNode);

        /// Whether the port carries the flow (only flow connections can cross into a sub-graph). By default the ports typed <see cref="GraphConnection"/>.
        /// A graph type whose flow leaves through other ports (the answers of a dialog line are dynamic ports typed as text) says so here.
        bool IsFlowPort(NodePort port) => port.ValueType == typeof(GraphConnection);

        /// Points the created sub-graph node at the new graph. Override together with <see cref="SubGraphNodeType"/>.
        void AssignSubGraph(Node subGraphNode, NodeGraph created)
        {
            if (subGraphNode is SubGraphNode sub) sub.subGraph = created as GraphBase;
        }
    }

    public static class SubGraphExtractionPolicies
    {
        private readonly static List<ISubGraphExtractionPolicy> policies = new() { new DefaultPolicy() };

        public static void Register(ISubGraphExtractionPolicy policy)
        {
            if (policy != null && !policies.Contains(policy)) policies.Add(policy);
        }

        public static ISubGraphExtractionPolicy GetFor(NodeGraph source)
        {
            ISubGraphExtractionPolicy best = null;
            foreach (var policy in policies)
            {
                if (!policy.AppliesTo(source)) continue;
                if (best == null || policy.Priority > best.Priority) best = policy;
            }

            return best;
        }

        /// New graph next to the source graph, same type, nothing else.
        private class DefaultPolicy : ISubGraphExtractionPolicy
        {
            public int Priority => int.MinValue;
            public bool AppliesTo(NodeGraph source) => true;

            public string SuggestFolder(NodeGraph source) =>
                Path.GetDirectoryName(AssetDatabase.GetAssetPath(source))?.Replace('\\', '/') ?? "Assets";

            public void Configure(NodeGraph source, NodeGraph created)
            {
            }

            public void AfterExtract(NodeGraph source, NodeGraph created)
            {
            }

            public IEnumerable<string> GetAffectedAssetPaths(NodeGraph source) => Array.Empty<string>();
        }
    }
}
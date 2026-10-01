using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using XNode;

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
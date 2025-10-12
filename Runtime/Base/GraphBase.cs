using System.Collections.Generic;
using UnityEngine;
using XNode;
using Xprees.Core;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Base
{
    [RequireNode(typeof(StartNode), typeof(EndNode))]
    public class GraphBase : NodeGraph, IResettable
    {
        private readonly HashSet<GraphParserBase> _activeParsers = new();
        public bool IsActive => _activeParsers.Count > 0;

        /// Call this method before your start working with the graph.
        /// Activates and initializes the graph, if it already wasn't initialized.
        public void Activate(GraphParserBase parserCtx)
        {
            if (_activeParsers.Count <= 0)
            {
                Initialize();
            }

            _activeParsers.Add(parserCtx);
        }

        /// Call this method after you stop working with the graph.
        /// Deactivates and cleans-up the graph, if no-one else is using the graph.
        public void Deactivate(GraphParserBase parserCtx)
        {
            _activeParsers.Remove(parserCtx);

            if (_activeParsers.Count <= 0)
            {
                CleanUp();
            }
        }

        /// Initializes the graph.
        protected virtual void Initialize()
        {
            // Override this method to run graph initialization logic.
        }

        /// Cleans-up the graph state.
        protected virtual void CleanUp()
        {
            // Override this method to run graph clean-up logic.
        }

        protected virtual void OnEnable() => BackupStartState();

        public virtual void BackupStartState()
        {
            foreach (var node in nodes)
            {
                if (node is BaseNode baseNode)
                {
                    baseNode.BackupStartState();
                }
            }
            // Override this method to back up graph state.
        }

        /// Resets the state of the graph aka. all nodes in the graph.
        public virtual void ResetState()
        {
            foreach (var node in nodes)
            {
                if (node is IResettable resettableNode)
                {
                    resettableNode.ResetState();
                }
            }
            // Override this method to reset graph state.
        }

#if UNITY_EDITOR
        [Space]
        [Tooltip("Editor only description of graph. Describe what is it for, etc.")]
        [TextArea]
        public string description;
#endif
    }
}
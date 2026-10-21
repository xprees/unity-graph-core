using System;
using XNode;

namespace Xprees.Graph.Core.Editor.Validation
{
    public enum GraphIssueSeverity
    {
        Error,
        Warning,

        /// Informational marker (TODO/FIXME/HACK left by an author). Not a problem.
        Todo,
    }

    /// Single problem found in a graph by the <see cref="GraphValidator"/>.
    public class GraphIssue
    {
        public GraphIssueSeverity Severity { get; }
        public NodeGraph Graph { get; }

        /// Node the issue belongs to. Can be null for graph-level issues (e.g. missing node entries).
        public Node Node { get; }

        public string PortName { get; }
        public string Message { get; }

        /// Automatic fix. Null if the issue has to be fixed manually.
        public Action Fix { get; }

        public string FixLabel { get; }

        /// Safe fixes don't change runtime flow and are applied by "Fix All Safe".
        /// Unsafe fixes (e.g. restoring a lost edge) must be reviewed and applied one by one.
        public bool IsSafeFix { get; }

        /// Error or warning. Todo issues are informational and never count as problems.
        public bool IsProblem => Severity is GraphIssueSeverity.Error or GraphIssueSeverity.Warning;

        public bool IsFixable => Fix != null;
        public bool IsSafelyFixable => IsFixable && IsSafeFix;

        public GraphIssue(
            GraphIssueSeverity severity,
            NodeGraph graph,
            Node node,
            string portName,
            string message,
            Action fix = null,
            string fixLabel = null,
            bool isSafeFix = true
        )
        {
            Severity = severity;
            Graph = graph;
            Node = node;
            PortName = portName;
            Message = message;
            Fix = fix;
            FixLabel = fixLabel ?? "Fix";
            IsSafeFix = isSafeFix;
        }
    }
}
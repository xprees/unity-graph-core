using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using XNode;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Nodes.Debug;

namespace Xprees.Graph.Core.Editor.Validation
{
    /// Finds broken or suspicious connections in xNode graphs. Validation is read-only,
    /// safe fixes are provided as <see cref="GraphIssue.Fix"/> actions (with Undo support).
    public static class GraphValidator
    {
        public static List<GraphIssue> Validate(NodeGraph graph)
        {
            var issues = new List<GraphIssue>();
            if (!graph) return issues;

            var graphNodes = new HashSet<Node>(graph.nodes.Where(n => n));

            CheckNodeEntries(graph, issues);

            foreach (var node in graphNodes)
            {
                if (node.graph != graph)
                {
                    issues.Add(new GraphIssue(GraphIssueSeverity.Error, graph, node, null,
                        $"Node belongs to a different graph ('{(node.graph ? node.graph.name : "none")}').",
                        () => SetNodeGraph(node, graph), "Set graph"));
                }

                foreach (var port in node.Ports)
                {
                    CheckConnections(graph, graphNodes, port, issues);
                }
            }

            var unreachable = CheckUnreachable(graph, graphNodes, issues);
            CheckDeadEnds(graph, graphNodes, unreachable, issues);

            return issues;
        }

        /// Applies all safe fixes of the graph (fixes that don't change the runtime flow). Returns the number of applied fixes.
        public static int FixAllSafe(NodeGraph graph)
        {
            var fixable = Validate(graph).Where(issue => issue.IsSafelyFixable).ToList();
            foreach (var issue in fixable) issue.Fix();
            return fixable.Count;
        }

        #region Checks

        private static void CheckNodeEntries(NodeGraph graph, List<GraphIssue> issues)
        {
            var nullCount = graph.nodes.Count(n => !n);
            if (nullCount <= 0) return;

            issues.Add(new GraphIssue(GraphIssueSeverity.Error, graph, null, null,
                $"Graph contains {nullCount} missing node(s) (deleted node or missing script).",
                () => RemoveNullNodes(graph), "Remove missing"));
        }

        private static void CheckConnections(NodeGraph graph, HashSet<Node> graphNodes, NodePort port, List<GraphIssue> issues)
        {
            var node = port.node;
            var validCount = 0;

            foreach (var connection in PortConnectionAccessor.GetConnections(port))
            {
                if (!IsValid(connection, graphNodes, out var reason))
                {
                    // Runtime follows outputs only - a broken output entry can stop the flow, a broken input entry is just garbage
                    var severity = port.IsOutput ? GraphIssueSeverity.Error : GraphIssueSeverity.Warning;
                    issues.Add(new GraphIssue(severity, graph, node, port.fieldName,
                        $"Broken connection: {reason}.",
                        () => RemoveInvalidConnections(port, graphNodes), "Remove broken"));
                    continue;
                }

                validCount++;
                var target = connection.TargetPort;
                if (PortConnectionAccessor.ListsConnectionTo(target, port)) continue;

                // One-sided link. Runtime follows outputs only, so a missing output entry breaks the flow.
                if (port.IsOutput)
                {
                    issues.Add(new GraphIssue(GraphIssueSeverity.Warning, graph, node, port.fieldName,
                        $"One-sided link to '{target.node.name}.{target.fieldName}' (input doesn't list it back).",
                        () => AddBackLink(target, port), "Add back-link"));
                }
                else if (target.connectionType == Node.ConnectionType.Override && HasValidConnection(target, graphNodes))
                {
                    // The output was reconnected elsewhere, this input only keeps a stale entry
                    issues.Add(new GraphIssue(GraphIssueSeverity.Warning, graph, node, port.fieldName,
                        $"Stale link from '{target.node.name}.{target.fieldName}' (the output is connected elsewhere).",
                        () => RemoveLink(port, target), "Remove stale"));
                }
                else
                {
                    issues.Add(new GraphIssue(GraphIssueSeverity.Error, graph, node, port.fieldName,
                        $"Lost edge from '{target.node.name}.{target.fieldName}' - invisible and not followed at runtime. Restore it only if the flow should continue here.",
                        () => AddBackLink(target, port), "Restore edge", isSafeFix: false));
                }
            }

            if (port.connectionType == Node.ConnectionType.Override && validCount > 1)
            {
                issues.Add(new GraphIssue(GraphIssueSeverity.Error, graph, node, port.fieldName,
                    $"Override port has {validCount} connections - only the first one is used. Remove the extra ones."));
            }
        }

        /// Returns nodes not reachable from StartNode or any AsyncStartBaseNode.
        private static HashSet<Node> CheckUnreachable(NodeGraph graph, HashSet<Node> graphNodes, List<GraphIssue> issues)
        {
            var unreachable = new HashSet<Node>();
            var roots = graphNodes.Where(n => n is StartNode or AsyncStartBaseNode).ToList();
            if (roots.Count <= 0) return unreachable;

            var visited = new HashSet<Node>(roots);
            var queue = new Queue<Node>(roots);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var output in current.Outputs)
                {
                    foreach (var connection in PortConnectionAccessor.GetConnections(output))
                    {
                        if (!IsValid(connection, graphNodes, out _)) continue;
                        if (visited.Add(connection.node)) queue.Enqueue(connection.node);
                    }
                }
            }

            foreach (var node in graphNodes)
            {
                if (visited.Contains(node) || IsIgnored(node) || !node.Ports.Any()) continue;

                unreachable.Add(node);
                issues.Add(new GraphIssue(GraphIssueSeverity.Warning, graph, node, null,
                    "Node is unreachable from the Start node or any Async Start node."));
            }

            return unreachable;
        }

        private static void CheckDeadEnds(NodeGraph graph, HashSet<Node> graphNodes, HashSet<Node> unreachable, List<GraphIssue> issues)
        {
            foreach (var node in graphNodes)
            {
                if (node is not BaseNode || IsIgnored(node)) continue;

                foreach (var port in node.Ports)
                {
                    if (IsDynamicListBackingPort(port) || HasValidConnection(port, graphNodes)) continue;

                    if (port.IsOutput && node is not EndNode)
                    {
                        issues.Add(new GraphIssue(GraphIssueSeverity.Warning, graph, node, port.fieldName,
                            "Output is not connected - the flow ends here."));
                    }
                    else if (port.IsInput && !unreachable.Contains(node))
                    {
                        issues.Add(new GraphIssue(GraphIssueSeverity.Warning, graph, node, port.fieldName,
                            "Input is not connected."));
                    }
                }
            }
        }

        #endregion

        #region Helpers

        private static bool IsIgnored(Node node) => node is NoteNode or NodeGroup;

        private static bool IsValid(PortConnectionAccessor.RawConnection connection, HashSet<Node> graphNodes, out string reason)
        {
            if (!connection.node)
            {
                reason = "target node is missing";
                return false;
            }

            if (string.IsNullOrEmpty(connection.fieldName))
            {
                reason = $"target port name is empty on '{connection.node.name}'";
                return false;
            }

            if (!graphNodes.Contains(connection.node))
            {
                reason = $"target node '{connection.node.name}' is not part of this graph";
                return false;
            }

            if (connection.TargetPort == null)
            {
                reason = $"port '{connection.fieldName}' doesn't exist on '{connection.node.name}'";
                return false;
            }

            reason = null;
            return true;
        }

        private static bool HasValidConnection(NodePort port, HashSet<Node> graphNodes) =>
            PortConnectionAccessor.GetConnections(port).Any(c => IsValid(c, graphNodes, out _));

        /// xNode creates a never connected static "backing" port for fields with dynamicPortList (e.g. 'answers').
        private static bool IsDynamicListBackingPort(NodePort port)
        {
            if (port.IsDynamic) return false;

            var field = port.node.GetType().GetField(port.fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return false;

            return field.GetCustomAttributes(true).Any(attribute =>
                attribute is Node.InputAttribute { dynamicPortList: true } or Node.OutputAttribute { dynamicPortList: true });
        }

        #endregion

        #region Fixes

        private static void RemoveNullNodes(NodeGraph graph)
        {
            Undo.RecordObject(graph, "Remove missing nodes");
            graph.nodes.RemoveAll(n => !n);
            EditorUtility.SetDirty(graph);
        }

        private static void SetNodeGraph(Node node, NodeGraph graph)
        {
            Undo.RecordObject(node, "Set node graph");
            node.graph = graph;
            EditorUtility.SetDirty(node);
        }

        private static void RemoveInvalidConnections(NodePort port, HashSet<Node> graphNodes)
        {
            Undo.RecordObject(port.node, "Remove broken connections");
            PortConnectionAccessor.RemoveConnections(port, c => !IsValid(c, graphNodes, out _));
            EditorUtility.SetDirty(port.node);
        }

        private static void RemoveLink(NodePort port, NodePort target)
        {
            Undo.RecordObject(port.node, "Remove stale link");
            PortConnectionAccessor.RemoveConnections(port, c => c.node == target.node && c.fieldName == target.fieldName);
            EditorUtility.SetDirty(port.node);
        }

        private static void AddBackLink(NodePort port, NodePort target)
        {
            if (PortConnectionAccessor.ListsConnectionTo(port, target)) return;

            Undo.RecordObject(port.node, "Add back-link");
            PortConnectionAccessor.AddConnection(port, target);
            EditorUtility.SetDirty(port.node);
        }

        #endregion

    }
}
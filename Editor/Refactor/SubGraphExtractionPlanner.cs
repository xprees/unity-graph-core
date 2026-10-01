using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using XNode;
using XNodeEditor;
using Xprees.Graph.Core.Attributes;
using Xprees.Graph.Core.Base;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Editor.Validation;

namespace Xprees.Graph.Core.Editor.Refactor
{
    /// Decides whether a selection can be extracted into a sub-graph. Reads the graph only, never changes it.
    ///
    /// A SubGraphNode has one flow input and one flow output, so the selection must have exactly one entry
    /// (one input port receiving the flow from outside) and all its exits must lead to the same outside input port.
    public static class SubGraphExtractionPlanner
    {
        private const BindingFlags fieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string defaultName = "SubGraph";

        public static SubGraphExtractionResult Plan(NodeGraph graph, IEnumerable<Node> selection, bool requireAsset = true)
        {
            var result = new SubGraphExtractionResult();
            if (!graph)
            {
                result.errors.Add("No graph is open.");
                return result;
            }

            if (graph is not GraphBase)
            {
                result.errors.Add("Only graphs derived from GraphBase can use a SubGraphNode.");
                return result;
            }

            if (requireAsset && !AssetDatabase.Contains(graph))
            {
                result.errors.Add("The graph is not saved as an asset.");
                return result;
            }

            var selected = (selection ?? Enumerable.Empty<Node>()).Where(n => n && n.graph == graph).Distinct().ToList();
            var plan = new SubGraphExtractionPlan { graph = graph };
            result.plan = plan;

            CollectMembers(graph, selected, plan, result);
            if (result.errors.Count > 0) return result;

            CheckGraphSpecificNodes(plan, result);
            CheckBoundary(plan, result);
            WarnAboutNodeReferences(plan, result);

            if (GraphValidator.Validate(graph).Any(issue => issue.Severity == GraphIssueSeverity.Error))
            {
                result.errors.Add("The graph has validation errors. Fix them first, extracting saves the graph.");
            }

            plan.suggestedName = SuggestName(graph, selected);
            return result;
        }

        /// Start, End and async start nodes stay in the source graph. An async start node is only a trigger - the path behind it moves,
        /// and the edge leaving the trigger becomes the entry of the extracted flow.
        private static bool IsStructural(Node node) => node is StartNode or EndNode or AsyncStartBaseNode;

        private static bool IsContent(Node node) => node is not NodeGroup && !IsStructural(node) && node.Ports.Any();

        private static void CollectMembers(NodeGraph graph, List<Node> selected, SubGraphExtractionPlan plan, SubGraphExtractionResult result)
        {
            var members = new HashSet<Node>();
            foreach (var node in selected.Where(n => !IsStructural(n))) members.Add(node);

            // A selected group brings the nodes inside of it
            foreach (var group in selected.OfType<NodeGroup>())
            {
                foreach (var inside in group.GetNodes().Where(n => !IsStructural(n))) members.Add(inside);
            }

            if (selected.Any(IsStructural)) result.warnings.Add("Start, End and async start nodes stay in the source graph.");

            if (!members.Any(IsContent))
            {
                result.errors.Add("Select the nodes (or a group with nodes) you want to move to a sub-graph.");
                return;
            }

            // Keep the order of the graph for stable output
            plan.members = graph.nodes.Where(n => n && members.Contains(n)).ToList();
        }

        /// Nodes tied to one kind of graph ([AllowOnlyInGraphs]) need the runtime of that graph (dialog / chat UI parsers). A SubGraphNode traverses its graph with a plain parser, so they would stop working.
        private static void CheckGraphSpecificNodes(SubGraphExtractionPlan plan, SubGraphExtractionResult result)
        {
            foreach (var node in plan.members)
            {
                if (Attribute.GetCustomAttribute(node.GetType(), typeof(AllowOnlyInGraphsAttribute), true) == null) continue;

                result.errors.Add($"'{node.name}' ({node.GetType().Name}) is graph specific and needs the runtime of its graph. " +
                                  "A SubGraphNode runs its graph with a generic parser, so it can't be extracted.");
            }
        }

        private static void CheckBoundary(SubGraphExtractionPlan plan, SubGraphExtractionResult result)
        {
            var members = new HashSet<Node>(plan.members);
            var seen = new HashSet<(NodePort, NodePort)>();

            void AddCrossing(NodePort output, NodePort input)
            {
                if (seen.Add((output, input))) plan.crossingEdges.Add((output, input));
            }

            foreach (var node in plan.graph.nodes.Where(n => n))
            {
                var inside = members.Contains(node);
                foreach (var port in node.Ports)
                {
                    foreach (var connection in PortConnectionAccessor.GetConnections(port))
                    {
                        var other = connection.TargetPort;
                        if (other == null || inside == members.Contains(other.node)) continue;

                        // Edges are stored on both ports - normalize to (output, input), handles one-sided links too
                        if (port.IsOutput) AddCrossing(port, other);
                        else AddCrossing(other, port);
                    }
                }
            }

            foreach (var (output, input) in plan.crossingEdges.Where(e => !IsFlow(e.output) || !IsFlow(e.input)))
            {
                result.errors.Add($"'{output.node.name}.{output.fieldName}' -> '{input.node.name}.{input.fieldName}' is not a flow connection. "
                                  +
                                  "Only flow connections can cross into a sub-graph.");
            }

            var entries = plan.crossingEdges.Where(e => members.Contains(e.input.node)).ToList();
            var exits = plan.crossingEdges.Where(e => members.Contains(e.output.node)).ToList();

            var entryPorts = entries.Select(e => e.input).Distinct().ToList();
            if (entryPorts.Count == 0)
            {
                result.errors.Add(
                    "Nothing leads into the selection. It needs exactly one entry, a connection coming from a node outside of it.");
            }
            else if (entryPorts.Count > 1)
            {
                result.errors.Add("The selection has " + entryPorts.Count + " entry points (" + Describe(entryPorts) + "). " +
                                  "A SubGraphNode has a single input - adjust the selection so only one node receives the flow from outside.");
            }
            else
            {
                plan.entryPort = entryPorts[0];
                plan.entrySources = entries.Select(e => e.output).Distinct().ToList();
            }

            var exitTargets = exits.Select(e => e.input).Distinct().ToList();
            if (exitTargets.Count > 1)
            {
                result.errors.Add("The selection leads to " + exitTargets.Count + " different nodes (" + Describe(exitTargets) + "). " +
                                  "A SubGraphNode has a single output - adjust the selection so all exits continue in the same node.");
            }
            else if (exitTargets.Count == 1)
            {
                plan.exitTarget = exitTargets[0];
                plan.exitSources = exits.Select(e => e.output).Distinct().ToList();
            }
        }

        private static bool IsFlow(NodePort port) => port.ValueType == typeof(GraphConnection);

        private static string Describe(IEnumerable<NodePort> ports) => string.Join(", ", ports.Select(p => $"{p.node.name}.{p.fieldName}"));

        /// Fields pointing at other nodes (not through ports) keep pointing at the old object after the move,
        /// when only one of both nodes moves.
        private static void WarnAboutNodeReferences(SubGraphExtractionPlan plan, SubGraphExtractionResult result)
        {
            var members = new HashSet<Node>(plan.members);
            foreach (var node in plan.graph.nodes.Where(n => n))
            {
                for (var type = node.GetType(); type != null && type != typeof(Node); type = type.BaseType)
                {
                    foreach (var field in type.GetFields(fieldFlags | BindingFlags.DeclaredOnly))
                    {
                        if (!typeof(Node).IsAssignableFrom(field.FieldType)) continue;
                        if (!field.IsPublic && !field.IsDefined(typeof(SerializeField))) continue;
                        if (field.GetValue(node) is not Node referenced || !referenced) continue;

                        if (members.Contains(node) == members.Contains(referenced)) continue;

                        result.warnings.Add($"'{node.name}' references '{referenced.name}' through the field '{field.Name}'. " +
                                            "The reference would be broken by moving only one of them.");
                    }
                }
            }
        }

        /// Names the sub-graph after the selected group (unless it still has the default title), otherwise after the async start node
        /// that triggers the extracted flow (selected or inside a selected group), otherwise "SubGraph".
        private static string SuggestName(NodeGraph graph, List<Node> selected)
        {
            var label = defaultName;

            var groups = selected.OfType<NodeGroup>().ToList();
            var groupName = groups.Count == 1 ? groups[0].name : null;
            var asyncStart = selected.Concat(groups.SelectMany(g => g.GetNodes())).OfType<AsyncStartBaseNode>()
                .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n.name));

            if (!string.IsNullOrWhiteSpace(groupName) && groupName != NodeEditorUtilities.NodeDefaultName(typeof(NodeGroup))) label = groupName;
            else if (asyncStart) label = asyncStart.name;

            var invalid = Path.GetInvalidFileNameChars();
            var safe = new string($"{graph.name}_{label}".Where(c => !invalid.Contains(c)).ToArray()).Trim().Replace(' ', '_');
            return safe;
        }
    }
}
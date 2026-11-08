using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XNode;
using XNodeEditor;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Editor.Graph;
using Xprees.Graph.Core.Editor.Validation;

namespace Xprees.Graph.Core.Editor.Refactor
{
    /// Moves the nodes of a <see cref="SubGraphExtractionPlan"/> into a new graph asset and replaces them with a SubGraphNode.
    ///
    /// The node objects are moved (not copied), so edges between them, reroute points and fields pointing from one moved node
    /// to another stay intact. The edges crossing the selection boundary are cut first and rebuilt through the Start / End nodes
    /// of the new graph and the SubGraphNode. Asset creation and sub-asset moves are undone at the file level, see <see cref="SubGraphExtractionUndo"/>.
    public static class SubGraphExtractor
    {
        private const float startOffset = 300f;
        private const float endOffset = 500f;

        /// Returns the new graph, or null if the extraction was not possible.
        public static NodeGraph Extract(SubGraphExtractionPlan plan, string folder, string assetName)
        {
            var source = plan.graph;
            if (!source || plan.entryPort == null) return null;

            // xNode's Connect records the wired nodes on its own. Those records must share the group of the file-level undo record, a separate
            // earlier step would later apply mid-extraction snapshots onto the restored graph. The file restore runs last and wins.
            using var undo = GraphUndo.Group(SubGraphExtractionUndo.UndoName);
            var policy = SubGraphExtractionPolicies.GetFor(source);
            var snapshot = SubGraphExtractionUndo.Begin(policy.GetAffectedAssetPaths(source).Append(AssetDatabase.GetAssetPath(source)));

            var created = (NodeGraph) ScriptableObject.CreateInstance(source.GetType());
            policy.Configure(source, created);

            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{assetName}.asset");
            created.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(created, path);

            // xNode's importer already added the required Start / End nodes while creating the asset - reuse them
            var startNode = GetOrCreateNode<StartNode>(created, new Vector2(plan.TopLeft.x - startOffset, plan.entryPort.node.position.y));
            var endNode = GetOrCreateNode<EndNode>(created,
                new Vector2(plan.BottomRight.x + endOffset, plan.exitTarget?.node.position.y ?? plan.Center.y));

            CutCrossingEdges(plan);
            RemoveOrphanedEnds(plan);
            MoveNodes(plan, created);
            WireInsideGraph(plan, startNode, endNode);

            var subGraphNode = CreateSubGraphNode(plan, created, policy);

            EditorUtility.SetDirty(source);
            EditorUtility.SetDirty(created);
            AssetDatabase.SaveAssets();

            policy.AfterExtract(source, created);
            SubGraphExtractionUndo.Commit(snapshot, policy.GetAffectedAssetPaths(source).Append(path));

            // Moved nodes are no longer part of the shown graph - never leave them selected
            Selection.activeObject = subGraphNode;
            EditorGUIUtility.PingObject(created);
            NodeEditorWindow.RepaintAll();
            return created;
        }

        private static T GetOrCreateNode<T>(NodeGraph graph, Vector2 position) where T : Node
        {
            var node = graph.nodes.OfType<T>().FirstOrDefault();
            if (!node)
            {
                node = graph.AddNode<T>();
                node.name = NodeEditorUtilities.NodeDefaultName(typeof(T));
                AssetDatabase.AddObjectToAsset(node, graph);
            }

            node.position = position;
            return node;
        }

        /// Removes the boundary edges from both ports, so no connection into another asset is ever serialized.
        private static void CutCrossingEdges(SubGraphExtractionPlan plan)
        {
            foreach (var (output, input) in plan.crossingEdges)
            {
                PortConnectionAccessor.RemoveConnections(output, c => c.node == input.node && c.fieldName == input.fieldName);
                PortConnectionAccessor.RemoveConnections(input, c => c.node == output.node && c.fieldName == output.fieldName);
            }
        }

        /// End nodes the flow left through besides the kept exit target: once the cut took their edges they would only sit there unreachable.
        /// An End that still has other connections (another branch ends there) stays.
        private static void RemoveOrphanedEnds(SubGraphExtractionPlan plan)
        {
            var source = plan.graph;
            foreach (var end in plan.mergedEnds.Where(n => n && !n.Ports.Any(p => p.IsConnected)).ToList())
            {
                source.RemoveNode(end);
                AssetDatabase.RemoveObjectFromAsset(end);
                Object.DestroyImmediate(end, true);
            }
        }

        private static void MoveNodes(SubGraphExtractionPlan plan, NodeGraph target)
        {
            var source = plan.graph;
            foreach (var node in plan.members)
            {
                source.nodes.Remove(node);
                AssetDatabase.RemoveObjectFromAsset(node);

                node.graph = target;
                target.nodes.Add(node);
                AssetDatabase.AddObjectToAsset(node, target);
            }
        }

        private static void WireInsideGraph(SubGraphExtractionPlan plan, StartNode startNode, EndNode endNode)
        {
            if (startNode) startNode.GetOutputPort(nameof(StartNode.start)).Connect(plan.entryPort);

            if (!endNode) return;

            var endInput = endNode.GetInputPort(nameof(EndNode.end));
            foreach (var exit in plan.exitSources) exit.Connect(endInput);
        }

        private static Node CreateSubGraphNode(SubGraphExtractionPlan plan, NodeGraph created, ISubGraphExtractionPolicy policy)
        {
            var source = plan.graph;
            // The policy picks the node that plays the new graph (SubGraphNode, or the node of a graph type with its own runtime, e.g. a dialog)
            var node = source.AddNode(policy.SubGraphNodeType);
            node.position = plan.Center;
            node.name = created.name;
            policy.AssignSubGraph(node, created);
            AssetDatabase.AddObjectToAsset(node, source);

            var input = node.GetInputPort(nameof(LinearFlowBaseNode.input));
            foreach (var entrySource in plan.entrySources) entrySource.Connect(input);

            if (plan.exitTarget != null) node.GetOutputPort(nameof(LinearFlowBaseNode.output)).Connect(plan.exitTarget);
            return node;
        }
    }
}
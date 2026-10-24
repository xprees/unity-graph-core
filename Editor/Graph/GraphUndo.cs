using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using XNode;
using Xprees.Graph.Core.Editor.Validation;
using Object = UnityEngine.Object;

namespace Xprees.Graph.Core.Editor.Graph
{
    /// Undo helpers for graph edits made from code (editor tools, authoring services, MCP tools).
    /// xNode stores every edge on both nodes and its own NodePort.Connect records only the two connected nodes, so an edit that clears
    /// or replaces connections must also record the nodes on the other end. One user action = one <see cref="Group"/>.
    public static class GraphUndo
    {
        private static int depth;

        /// Opens a named undo group: everything recorded until Dispose becomes one Ctrl+Z step. Nested groups join the outermost one.
        public static GroupScope Group(string name) => new(name);

        public readonly struct GroupScope : IDisposable
        {
            private readonly int _group;
            private readonly string _name;
            private readonly bool _outermost;

            internal GroupScope(string name)
            {
                _name = name;
                _outermost = depth++ == 0;
                _group = -1;
                if (!_outermost) return;

                Undo.IncrementCurrentGroup();
                _group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(name);
            }

            public void Dispose()
            {
                depth = Math.Max(0, depth - 1);
                if (!_outermost) return;

                Undo.SetCurrentGroupName(_name);
                Undo.CollapseUndoOperations(_group);
            }
        }

        /// Records the node and every node connected to it. Call before connections of the node change (removal, clear, dynamic port removal).
        public static void RecordNodeWithNeighbours(Node node, string name)
        {
            if (!node) return;

            var objects = new List<Object> { node };
            foreach (var port in node.Ports) AddConnectedNodes(port, objects);
            Record(objects, name);
        }

        /// Records the nodes of the ports and every node connected to them. Call before connecting, disconnecting or clearing the ports.
        public static void RecordPorts(string name, params NodePort[] ports)
        {
            var objects = new List<Object>();
            foreach (var port in ports)
            {
                if (port == null || !port.node) continue;

                objects.Add(port.node);
                AddConnectedNodes(port, objects);
            }

            Record(objects, name);
        }

        /// NodePort.Connect that also records the nodes an Override port disconnects.
        public static void Connect(NodePort output, NodePort input, string name = "Connect Nodes")
        {
            RecordPorts(name, output, input);
            output.Connect(input);
        }

        public static void Disconnect(NodePort output, NodePort input, string name = "Disconnect Nodes")
        {
            RecordPorts(name, output, input);
            output.Disconnect(input);
        }

        public static void ClearConnections(NodePort port, string name = "Clear Connections")
        {
            RecordPorts(name, port);
            port.ClearConnections();
        }

        /// Removes the node with its connections: records the graph and the connected nodes, then destroys the node (sub-asset) through Undo.
        public static void RemoveNode(NodeGraph graph, Node node, string name)
        {
            if (!graph || !node) return;

            Undo.RecordObject(graph, name);
            RecordNodeWithNeighbours(node, name);
            graph.RemoveNode(node);
            Undo.DestroyObjectImmediate(node);
        }

        /// Raw connections, NodePort.GetConnections would drop broken entries before they are recorded.
        private static void AddConnectedNodes(NodePort port, List<Object> objects)
        {
            foreach (var connection in PortConnectionAccessor.GetConnections(port))
            {
                if (connection.node) objects.Add(connection.node);
            }
        }

        private static void Record(List<Object> objects, string name)
        {
            if (objects.Count > 0) Undo.RecordObjects(objects.Distinct().ToArray(), name);
        }
    }
}
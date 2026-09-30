using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using XNode;

namespace Xprees.Graph.Core.Editor.Validation
{
    /// Raw, side effect free access to xNode port connections.
    /// NodePort.GetConnection(i) / GetConnections() silently remove broken entries, so the validator can't use them.
    internal static class PortConnectionAccessor
    {
        private const BindingFlags instanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly static FieldInfo connectionsField = typeof(NodePort).GetField("connections", instanceFlags);
        private readonly static Type portConnectionType = typeof(NodePort).GetNestedType("PortConnection", BindingFlags.NonPublic);
        private readonly static FieldInfo connectionFieldNameField = portConnectionType?.GetField("fieldName", instanceFlags);
        private readonly static FieldInfo connectionNodeField = portConnectionType?.GetField("node", instanceFlags);

        public readonly struct RawConnection
        {
            public readonly int index;
            public readonly Node node;
            public readonly string fieldName;

            public RawConnection(int index, Node node, string fieldName)
            {
                this.index = index;
                this.node = node;
                this.fieldName = fieldName;
            }

            /// Target port, resolved without touching the source port. Null if the connection is broken.
            public NodePort TargetPort => node && !string.IsNullOrEmpty(fieldName) ? node.GetPort(fieldName) : null;
        }

        private static IList GetList(NodePort port) => connectionsField?.GetValue(port) as IList;

        public static List<RawConnection> GetConnections(NodePort port)
        {
            var result = new List<RawConnection>();
            var list = GetList(port);
            if (list == null) return result;

            for (var i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                if (entry == null)
                {
                    result.Add(new RawConnection(i, null, null));
                    continue;
                }

                var node = connectionNodeField.GetValue(entry) as Node;
                var fieldName = connectionFieldNameField.GetValue(entry) as string;
                result.Add(new RawConnection(i, node, fieldName));
            }

            return result;
        }

        /// True if the port lists a connection to the target port.
        public static bool ListsConnectionTo(NodePort port, NodePort target)
        {
            foreach (var connection in GetConnections(port))
            {
                if (connection.node == target.node && connection.fieldName == target.fieldName) return true;
            }

            return false;
        }

        /// Removes every connection entry matching the predicate. Returns the number of removed entries.
        public static int RemoveConnections(NodePort port, Predicate<RawConnection> shouldRemove)
        {
            var list = GetList(port);
            if (list == null) return 0;

            var removed = 0;
            var connections = GetConnections(port);
            for (var i = connections.Count - 1; i >= 0; i--)
            {
                if (!shouldRemove(connections[i])) continue;

                list.RemoveAt(connections[i].index);
                removed++;
            }

            return removed;
        }

        /// Adds a connection entry to the target port on the port, without the side effects of NodePort.Connect
        /// (which clears existing connections on Override ports).
        public static void AddConnection(NodePort port, NodePort target)
        {
            var list = GetList(port);
            if (list == null || portConnectionType == null) return;

            var entry = Activator.CreateInstance(portConnectionType, instanceFlags, null, new object[] { target }, null);
            list.Add(entry);
        }
    }
}
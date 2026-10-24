using System.Collections.Generic;
using UnityEngine;

namespace Xprees.Graph.Core.Editor.Layout
{
    public enum LayoutNodeKind
    {
        Node,

        /// Start / AsyncStart / sub-graph entry: always begins its own lane.
        Entry,
        Group,
    }

    /// One node of a graph as the layout sees it: its full drawn rect and what it is.
    public sealed class LayoutNode
    {
        public int Id;
        public Rect Rect;
        public LayoutNodeKind Kind;

        /// Lane order of entries: 0 = the main Start (first lane), 1 = other entries (ordered by position).
        public int EntryPriority = 1;

        /// Drawn group height minus its body height, see <see cref="LayoutGeometry.GroupChrome"/>.
        public float GroupChrome = LayoutGeometry.GroupChrome;

        public object Source;

        public bool IsGroup => Kind == LayoutNodeKind.Group;
    }

    /// A connection from an output port of <see cref="From"/> to <see cref="To"/>. <see cref="Port"/> is the output's order on the node.
    public readonly struct LayoutEdge
    {
        public readonly int From;
        public readonly int To;
        public readonly int Port;

        public LayoutEdge(int from, int to, int port = 0)
        {
            From = from;
            To = to;
            Port = port;
        }
    }

    /// Plain description of a graph (nodes with rects, connections, groups) the layout algorithms work on. No Unity editor state.
    public sealed class GraphLayoutModel
    {
        public readonly List<LayoutNode> Nodes = new();
        public readonly List<LayoutEdge> Edges = new();

        public LayoutNode Add(Rect rect, LayoutNodeKind kind = LayoutNodeKind.Node, object source = null)
        {
            var node = new LayoutNode { Id = Nodes.Count, Rect = rect, Kind = kind, Source = source };
            Nodes.Add(node);
            return node;
        }

        public void Connect(int from, int to, int port = 0) => Edges.Add(new LayoutEdge(from, to, port));

        /// Same rule as NodeGroup.GetNodes: the top-left of the node lies within the group's body (+ 30 px slack below).
        public static bool Contains(LayoutNode group, Vector2 position)
        {
            var rect = group.Rect;
            var bodyHeight = rect.height - group.GroupChrome;
            return position.x >= rect.xMin && position.x <= rect.xMax
                                           && position.y >= rect.yMin && position.y <= rect.yMin + bodyHeight + LayoutGeometry.GroupMemberSlack;
        }

        /// Innermost group containing each node (-1 = none). A group can only lie inside a larger group (ties by lower id), so there are no cycles.
        public int[] ComputeParents()
        {
            var parents = new int[Nodes.Count];
            for (var i = 0; i < Nodes.Count; i++)
            {
                var node = Nodes[i];
                var best = -1;
                var bestArea = float.MaxValue;
                foreach (var group in Nodes)
                {
                    if (!group.IsGroup || group.Id == node.Id || !Contains(group, node.Rect.position)) continue;
                    if (node.IsGroup && !IsLarger(group, node)) continue;

                    var area = Area(group.Rect);
                    if (area < bestArea || (Mathf.Approximately(area, bestArea) && group.Id > best))
                    {
                        best = group.Id;
                        bestArea = area;
                    }
                }

                parents[i] = best;
            }

            return parents;
        }

        private static bool IsLarger(LayoutNode group, LayoutNode child)
        {
            var groupArea = Area(group.Rect);
            var childArea = Area(child.Rect);
            return groupArea > childArea || (Mathf.Approximately(groupArea, childArea) && group.Id < child.Id);
        }

        private static float Area(Rect rect) => rect.width * rect.height;
    }
}
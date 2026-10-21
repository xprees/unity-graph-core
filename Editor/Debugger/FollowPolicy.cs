using System.Collections.Generic;

namespace Xprees.Graph.Core.Editor.Debugger
{
    /// Pure decision of the Follow mode: the view only moves when a followed record triggers a different node than the last time
    /// it was looked at. Records that stay on their node never move the view, so the user navigates graphs freely in between.
    public static class FollowPolicy
    {
        /// Updates the remembered active nodes and returns the id of the record whose active node changed (the last one when several did),
        /// or null when nothing moved. With a filter only that record counts, the others are only remembered.
        public static int? FindMoved<TNode>(IDictionary<int, TNode> lastActive, IEnumerable<KeyValuePair<int, TNode>> current, int? filter)
            where TNode : class
        {
            int? moved = null;
            foreach (var (id, node) in current)
            {
                if (node == null) continue;
                if (lastActive.TryGetValue(id, out var last) && ReferenceEquals(last, node)) continue;

                lastActive[id] = node;
                if (filter.HasValue && id != filter.Value) continue;

                moved = id;
            }

            return moved;
        }
    }
}
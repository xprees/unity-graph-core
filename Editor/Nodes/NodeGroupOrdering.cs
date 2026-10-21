using System;
using System.Collections.Generic;

namespace Xprees.Graph.Core.Editor.Nodes
{
    /// Pure ordering rule of node groups: xNode draws nodes in list order, so groups must come before regular nodes (drawn below them).
    /// The relative order of the groups (and of the regular nodes) is never changed, an already correct list is never touched.
    public static class NodeGroupOrdering
    {
        /// True when no group stands after a regular node.
        public static bool IsGroupsFirst<T>(IReadOnlyList<T> items, Func<T, bool> isGroup)
        {
            var seenRegular = false;
            for (var i = 0; i < items.Count; i++)
            {
                if (isGroup(items[i]))
                {
                    if (seenRegular) return false;
                }
                else
                {
                    seenRegular = true;
                }
            }

            return true;
        }

        /// Stable partition (groups first). Returns false and leaves the list untouched when it is already correct.
        public static bool MoveGroupsFirst<T>(IList<T> items, Func<T, bool> isGroup)
        {
            var snapshot = new List<T>(items);
            if (IsGroupsFirst(snapshot, isGroup)) return false;

            var index = 0;
            foreach (var item in snapshot)
            {
                if (isGroup(item)) items[index++] = item;
            }

            foreach (var item in snapshot)
            {
                if (!isGroup(item)) items[index++] = item;
            }

            return true;
        }
    }
}
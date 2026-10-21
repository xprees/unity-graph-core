using System;
using System.Collections.Generic;
using UnityEditor;
using XNode;

namespace Xprees.Graph.Core.Editor.Debugger
{
    /// Lets game code add entries to the Debug context menu of every node (for example run from node) without graph-core knowing about it.
    public static class GraphNodeDebugMenu
    {
        private readonly static List<Action<GenericMenu, Node>> contributors = new();

        public static void Register(Action<GenericMenu, Node> contributor)
        {
            if (contributor != null && !contributors.Contains(contributor)) contributors.Add(contributor);
        }

        internal static void AddItems(GenericMenu menu, Node node)
        {
            foreach (var contributor in contributors) contributor(menu, node);
        }
    }
}
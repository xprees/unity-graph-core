using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XNode;

namespace Xprees.Graph.Core.Editor.Bookmarks
{
    /// Pure slot table: (graph guid, slot 1..9) -> node id. Serializable so the store can persist it.
    [Serializable]
    public sealed class BookmarkTable
    {
        public const int MinSlot = 1;
        public const int MaxSlot = 9;

        [SerializeField] private List<Entry> entries = new();

        public IReadOnlyList<Entry> Entries => entries;

        public static bool IsValidSlot(int slot) => slot >= MinSlot && slot <= MaxSlot;

        /// Replaces the bookmark of the slot. Returns false for an invalid slot or empty ids.
        public bool Set(string graphGuid, int slot, string nodeId)
        {
            if (!IsValidSlot(slot) || string.IsNullOrEmpty(graphGuid) || string.IsNullOrEmpty(nodeId)) return false;

            var index = IndexOf(graphGuid, slot);
            var entry = new Entry { graphGuid = graphGuid, slot = slot, nodeId = nodeId };
            if (index >= 0) entries[index] = entry;
            else entries.Add(entry);
            return true;
        }

        public bool TryGet(string graphGuid, int slot, out string nodeId)
        {
            var index = IndexOf(graphGuid, slot);
            nodeId = index >= 0 ? entries[index].nodeId : null;
            return index >= 0;
        }

        public bool Remove(string graphGuid, int slot)
        {
            var index = IndexOf(graphGuid, slot);
            if (index < 0) return false;

            entries.RemoveAt(index);
            return true;
        }

        private int IndexOf(string graphGuid, int slot) => entries.FindIndex(e => e.slot == slot && e.graphGuid == graphGuid);

        [Serializable]
        public struct Entry
        {
            public string graphGuid;
            public int slot;
            public string nodeId;
        }
    }

    /// Per-user node bookmarks (Ctrl+Shift+1..9 sets, Ctrl+1..9 jumps). Stored in UserSettings, never in the graph asset.
    public static class GraphBookmarks
    {
        public static bool Set(NodeGraph graph, int slot, Node node)
        {
            if (!graph || !node || node.graph != graph) return false;

            var guid = GuidOf(graph);
            var id = GlobalObjectId.GetGlobalObjectIdSlow(node);
            if (guid == null || id.identifierType == 0) return false; // Graph or node not saved as an asset yet

            if (!GraphBookmarkStore.instance.Set(guid, slot, id.ToString())) return false;

            return true;
        }

        /// The bookmarked node, null when the slot is empty or the node no longer exists.
        public static Node Get(NodeGraph graph, int slot)
        {
            var guid = GuidOf(graph);
            if (guid == null || !GraphBookmarkStore.instance.TryGet(guid, slot, out var id)) return null;
            if (!GlobalObjectId.TryParse(id, out var globalId)) return null;

            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) as Node;
        }

        public static bool IsSet(NodeGraph graph, int slot)
        {
            var guid = GuidOf(graph);
            return guid != null && GraphBookmarkStore.instance.TryGet(guid, slot, out _);
        }

        private static string GuidOf(NodeGraph graph)
        {
            if (!graph) return null;

            var path = AssetDatabase.GetAssetPath(graph);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
        }
    }

    [FilePath("UserSettings/GraphBookmarks.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class GraphBookmarkStore : ScriptableSingleton<GraphBookmarkStore>
    {
        [SerializeField] private BookmarkTable table = new();

        public bool Set(string graphGuid, int slot, string nodeId)
        {
            if (!table.Set(graphGuid, slot, nodeId)) return false;

            Save(true);
            return true;
        }

        public bool TryGet(string graphGuid, int slot, out string nodeId) => table.TryGet(graphGuid, slot, out nodeId);
    }
}
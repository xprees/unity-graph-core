using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using XNode;
using XNodeEditor;

namespace Xprees.Graph.Core.Editor.Nodes
{
    /// Pure rule: puts an item that moved inside a list back to the index it had. Other items keep their relative order.
    public static class NodeOrderGuard
    {
        /// Returns true when the item had to be moved back.
        public static bool RestoreIndex<T>(IList<T> items, T item, int originalIndex) where T : class
        {
            if (originalIndex < 0 || originalIndex >= items.Count) return false;

            var current = items.IndexOf(item);
            if (current < 0 || current == originalIndex) return false;

            items.RemoveAt(current);
            items.Insert(originalIndex, item);
            return true;
        }
    }

    /// Wraps another node editor and keeps it from reordering graph.nodes while it draws. The xNodeGroups package editor moves its group to index 0
    /// on every Repaint, so with N groups the list rotated forever (the Nodes list in the inspector reshuffled, also during Play Mode).
    /// Installed by <see cref="GuardedNodeEditors"/> in place of the editor of the guarded node type, never used through an attribute.
    public sealed class GuardedNodeEditor : NodeEditor
    {
        private NodeEditor _inner;

        public override void OnCreate()
        {
            if (!GuardedNodeEditors.TryGetInnerType(target.GetType(), out var innerType)) return;

            _inner = (NodeEditor) Activator.CreateInstance(innerType);
            Sync();
            _inner.OnCreate();
        }

        private void Sync()
        {
            _inner.target = target;
            _inner.window = window;
            _inner.serializedObject = serializedObject;
        }

        public override void OnHeaderGUI()
        {
            if (_inner == null)
            {
                base.OnHeaderGUI();
                return;
            }

            Sync();
            _inner.OnHeaderGUI();
        }

        public override void OnBodyGUI()
        {
            if (_inner == null)
            {
                base.OnBodyGUI();
                return;
            }

            Sync();
            var nodes = target.graph.nodes;
            var index = nodes.IndexOf(target);
            _inner.OnBodyGUI();

            if (Event.current.type != EventType.Repaint) return;

            NodeOrderGuard.RestoreIndex(nodes, target, index);
            if (Application.isPlaying) return;

            // Groups are drawn below regular nodes. Only reorders when a group really stands above a regular node (stable), never while playing.
            var groupType = target.GetType();
            if (NodeGroupOrdering.MoveGroupsFirst(nodes, n => n && n.GetType() == groupType)) EditorUtility.SetDirty(target.graph);
        }

        public override int GetWidth() => _inner != null ? Synced().GetWidth() : base.GetWidth();

        public override Color GetTint() => _inner != null ? Synced().GetTint() : base.GetTint();

        public override GUIStyle GetBodyStyle() => _inner != null ? Synced().GetBodyStyle() : base.GetBodyStyle();

        public override GUIStyle GetBodyHighlightStyle() => _inner != null ? Synced().GetBodyHighlightStyle() : base.GetBodyHighlightStyle();

        public override void AddContextMenuItems(GenericMenu menu)
        {
            if (_inner == null) base.AddContextMenuItems(menu);
            else Synced().AddContextMenuItems(menu);
        }

        public override void OnRename()
        {
            if (_inner == null) base.OnRename();
            else Synced().OnRename();
        }

        private NodeEditor Synced()
        {
            Sync();
            return _inner;
        }
    }

    /// Replaces the cached editor type of node types whose editor misbehaves with a <see cref="GuardedNodeEditor"/> around it.
    /// xNode throws on a second editor for the same node type, so the replacement is done in its (private) editor type cache.
    [InitializeOnLoad]
    public static class GuardedNodeEditors
    {
        private const string XNodeGroupType = "XNode.NodeGroups.NodeGroup";

        private static readonly Dictionary<Type, Type> InnerTypes = new();

        private static bool _installed;

        static GuardedNodeEditors()
        {
            EditorApplication.delayCall += EnsureInstalled;
        }

        internal static bool TryGetInnerType(Type nodeType, out Type innerType) => InnerTypes.TryGetValue(nodeType, out innerType);

        /// Idempotent. Also called when a graph editor is created, so the swap is in place before the first node is drawn.
        public static void EnsureInstalled()
        {
            if (_installed) return;

            try
            {
                Install();
                _installed = true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"Node order guard could not be installed: {e.Message}");
            }
        }

        private static void Install()
        {
            var baseType = typeof(NodeEditor).BaseType;
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            var cacheField = baseType?.GetField("editorTypes", flags);
            var lookup = baseType?.GetMethod("GetEditorType", flags);
            if (cacheField == null || lookup == null) return;

            lookup.Invoke(null, new object[] { typeof(Node) }); // fills the cache when it is still empty
            if (cacheField.GetValue(null) is not Dictionary<Type, Type> cache) return;

            var replaced = false;
            foreach (var nodeType in TypeCache.GetTypesDerivedFrom<Node>())
            {
                if (nodeType.FullName != XNodeGroupType) continue;
                if (!cache.TryGetValue(nodeType, out var current) || current == typeof(GuardedNodeEditor)) continue;

                InnerTypes[nodeType] = current;
                cache[nodeType] = typeof(GuardedNodeEditor);
                replaced = true;
            }

            // Editors created before the swap are rebuilt on demand
            if (replaced && baseType.GetField("editors", flags | BindingFlags.Public)?.GetValue(null) is IDictionary editors) editors.Clear();
        }
    }
}
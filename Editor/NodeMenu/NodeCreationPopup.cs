using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using XNode;
using XNodeEditor;
using Xprees.Graph.Core.Attributes;

namespace Xprees.Graph.Core.Editor.NodeMenu
{
    /// Searchable replacement of xNode's "create node" context menu. Lists only node types the graph accepts
    /// (<see cref="NodeTypeFilter"/>) and respects [DisallowMultipleNodes].
    public class NodeCreationPopup : PopupWindowContent
    {
        private const float width = 320f;
        private const float height = 420f;
        private const float searchHeight = 28f;
        private const int searchFontSize = 15;
        private const string searchControlName = "NodeCreationSearch";

        /// Keeps folder expansion between popups.
        private readonly static TreeViewState<int> sharedTreeState = new();

        private static GUIStyle searchStyle;
        private static GUIStyle cancelStyle;
        private static GUIStyle cancelEmptyStyle;

        private readonly NodeTreeView _tree;
        private readonly SearchField _searchField = new();
        private readonly Action<Type> _onCreate;
        private string _search = "";
        private bool _focusRequested = true;

        private NodeCreationPopup(List<NodeMenuEntry> entries, Action<Type> onCreate)
        {
            _onCreate = onCreate;
            sharedTreeState.searchString = ""; // The shared state would otherwise restore the previous search results without the text
            _tree = new NodeTreeView(sharedTreeState, entries);
            _tree.OnEntryChosen += entry => Choose(entry.type);
        }

        /// Opens the popup at the activator rect (call from within the OnGUI of the node editor window).
        /// The node is created at <paramref name="gridPosition"/> and auto-connected to a dragged port, if any.
        public static void Show(
            NodeGraphEditor graphEditor,
            NodeGraph graph,
            NodeEditorWindow window,
            Vector2 gridPosition,
            Rect activatorRect,
            bool autoConnect = true
        )
        {
            var entries = BuildEntries(graphEditor, graph);
            var popup = new NodeCreationPopup(entries, type =>
            {
                var node = graphEditor.CreateNode(type, gridPosition);
                if (autoConnect) window.AutoConnect(node);
            });
            PopupWindow.Show(activatorRect, popup);
        }

        /// Opens the popup below the activator (e.g. the toolbar + button). The node is created in the center of the visible graph.
        public static void ShowAtViewCenter(NodeEditorWindow window, Rect activatorRect, float topInset)
        {
            if (!window || !window.graph) return;

            var graphEditor = NodeGraphEditor.GetEditor(window.graph, window);
            var viewCenter = new Vector2(window.position.width * 0.5f, (window.position.height + topInset) * 0.5f);
            Show(graphEditor, window.graph, window, window.WindowToGridPosition(viewCenter), activatorRect, false);
        }

        public override Vector2 GetWindowSize() => new(width, height);

        public override void OnOpen()
        {
            if (sharedTreeState.expandedIDs.Count == 0) _tree.ExpandAll();
            _tree.SelectFirstLeaf(); // Enter creates the first node right away
        }

        public override void OnGUI(Rect rect)
        {
            HandleKeys();

            var searchRect = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, searchHeight);
            GUI.SetNextControlName(searchControlName);
            var newSearch = _searchField.OnGUI(searchRect, _search, GetSearchStyle(), cancelStyle, cancelEmptyStyle);
            if (_focusRequested && Event.current.type == EventType.Repaint)
            {
                _searchField.SetFocus();
                _focusRequested = false;
            }

            if (newSearch != _search)
            {
                _search = newSearch;
                _tree.searchString = _search;
                _tree.SelectFirstLeaf();
            }

            var treeRect = new Rect(rect.x, searchRect.yMax + 4f, rect.width, rect.height - searchRect.height - 8f);
            _tree.OnGUI(treeRect);
        }

        private static GUIStyle GetSearchStyle()
        {
            searchStyle ??= new GUIStyle("ToolbarSearchTextField")
                { fontSize = searchFontSize, fixedHeight = 0f, alignment = TextAnchor.MiddleLeft };
            cancelStyle ??= new GUIStyle("ToolbarSearchCancelButton") { fixedHeight = 0f };
            cancelEmptyStyle ??= new GUIStyle("ToolbarSearchCancelButtonEmpty") { fixedHeight = 0f };
            return searchStyle;
        }

        private void HandleKeys()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;

            // Keys are handled here whatever has the focus, so typing keeps going into the search field while the arrows drive the tree
            switch (e.keyCode)
            {
                case KeyCode.Escape:
                    editorWindow.Close();
                    e.Use();
                    break;
                case KeyCode.DownArrow:
                    _tree.MoveSelection(1);
                    e.Use();
                    break;
                case KeyCode.UpArrow:
                    _tree.MoveSelection(-1);
                    e.Use();
                    break;
                case KeyCode.RightArrow or KeyCode.LeftArrow when string.IsNullOrEmpty(_search): // Otherwise they move the text caret
                    _tree.ExpandOrCollapseSelected(e.keyCode == KeyCode.RightArrow);
                    e.Use();
                    break;
                case KeyCode.Return or KeyCode.KeypadEnter:
                    _tree.ActivateSelected();
                    e.Use();
                    break;
            }
        }

        private void Choose(Type type)
        {
            editorWindow.Close();
            _onCreate(type);
        }

        private static List<NodeMenuEntry> BuildEntries(NodeGraphEditor graphEditor, NodeGraph graph)
        {
            var entries = new List<NodeMenuEntry>();
            foreach (var type in NodeEditorReflection.nodeTypes)
            {
                var path = graphEditor.GetNodeMenuName(type);
                if (string.IsNullOrEmpty(path)) continue;
                if (IsTestType(type) || !NodeTypeFilter.IsAllowed(type, graph)) continue;

                // [DisallowMultipleNodes] - types that reached their limit stay listed, disabled
                string disabledReason = null;
                if (NodeEditorUtilities.GetAttrib(type, out Node.DisallowMultipleNodesAttribute disallow)
                    && graph.nodes.Count(n => n && n.GetType() == type) >= disallow.max)
                {
                    disabledReason = disallow.max == 1
                        ? "Already in the graph (only one allowed)"
                        : $"Limit reached ({disallow.max} per graph)";
                }

                var description = type.GetCustomAttributes(typeof(NodeDescriptionAttribute), true)
                    .OfType<NodeDescriptionAttribute>().FirstOrDefault()?.description;

                entries.Add(new NodeMenuEntry
                {
                    type = type,
                    path = path,
                    name = path[(path.LastIndexOf('/') + 1)..],
                    description = description,
                    order = graphEditor.GetNodeMenuOrder(type),
                    pinnedOrder = GetPinnedOrder(type),
                    disabledReason = disabledReason,
                    icon = GetIcon(type),
                });
            }

            return entries;
        }

        private readonly static Dictionary<Assembly, bool> testAssemblyCache = new();

        /// Node types defined by test assemblies (anything referencing NUnit) are not meant for real graphs.
        private static bool IsTestType(Type type)
        {
            var assembly = type.Assembly;
            if (!testAssemblyCache.TryGetValue(assembly, out var isTest))
            {
                isTest = assembly.GetReferencedAssemblies().Any(a => a.Name == "nunit.framework");
                testAssemblyCache[assembly] = isTest;
            }

            return isTest;
        }

        private static int? GetPinnedOrder(Type nodeType) =>
            Attribute.GetCustomAttribute(nodeType, typeof(PinnedNodeAttribute), false) is PinnedNodeAttribute pinned ? pinned.order : null;

        /// Custom script icon assigned to the node script (same rule as the node header icon), otherwise none.
        private static Texture GetIcon(Type nodeType)
        {
            var thumbnail = AssetPreview.GetMiniTypeThumbnail(nodeType);
            if (thumbnail == null ||
                thumbnail.name.Contains("TextAsset Icon") ||
                thumbnail.name.Contains("cs Script Icon") ||
                thumbnail.name.Contains("ScriptableObject Icon"))
            {
                return null;
            }

            return thumbnail;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Xprees.Graph.Core.Editor.NodeMenu
{
    /// One creatable node type in the node tree.
    public class NodeMenuEntry
    {
        public Type type;
        public string path;
        public string name;
        public string description;
        public int order;
        public Texture icon;

        /// Position in the Pinned section, null when the node is not pinned.
        public int? pinnedOrder;

        /// Node is restricted to the current graph type - listed in the featured section on top instead of its own folder.
        public bool graphSpecific;

        /// Why the node can't be created right now (e.g. [DisallowMultipleNodes] limit reached). Null when it can be.
        public string disabledReason;

        public bool IsEnabled => disabledReason == null;
    }

    /// Searchable tree of creatable nodes grouped by their menu path ("Dialog/Show Line" -> folder Dialog, item Show Line).
    public class NodeTreeView : TreeView<int>
    {
        private const float rowIconSize = 18f;
        private const float pathLabelMinWidth = 60f;
        private const float leafRowHeight = 36f;
        private const float folderRowHeight = 32f;
        private const float folderIconSize = 18f;
        private const float leafTitleHeight = 17f;
        private const float leafDescriptionHeight = 14f;
        private const string pinnedFolderKey = "pinned:/";
        private const string featuredFolderKey = "featured:/";

        private readonly static Color separatorColor = new(0.5f, 0.5f, 0.5f, 0.25f);
        private readonly static Color folderBackgroundColor = new(0.5f, 0.5f, 0.5f, 0.08f);
        private static GUIStyle folderStyle;
        private static GUIStyle titleStyle;
        private static GUIStyle descriptionStyle;

        private readonly List<NodeMenuEntry> _entries;
        private readonly string _featuredLabel;
        private readonly Dictionary<int, NodeMenuEntry> _entryById = new();
        private readonly HashSet<int> _pinnedIds = new(); // Second listing of pinned nodes - not part of search results

        public event Action<NodeMenuEntry> OnEntryChosen;

        public NodeTreeView(TreeViewState<int> state, List<NodeMenuEntry> entries, string featuredLabel = "Featured") : base(state)
        {
            _entries = entries;
            _featuredLabel = featuredLabel;
            rowHeight = leafRowHeight;
            showAlternatingRowBackgrounds = false;
            showBorder = false;
            Reload();
        }

        public NodeMenuEntry GetSelectedEntry()
        {
            var selection = GetSelection();
            return selection.Count > 0 && _entryById.TryGetValue(selection[0], out var entry) ? entry : null;
        }

        /// Moves the selection by the given number of visible rows (clamped).
        public void MoveSelection(int delta)
        {
            var rows = GetRows();
            if (rows.Count == 0) return;

            var selection = GetSelection();
            var index = selection.Count > 0 ? IndexOfRow(rows, selection[0]) : -1;
            var next = index < 0 ? (delta > 0 ? 0 : rows.Count - 1) : Mathf.Clamp(index + delta, 0, rows.Count - 1);
            SetSelection(new List<int> { rows[next].id }, TreeViewSelectionOptions.RevealAndFrame);
        }

        /// Right expands the selected folder, Left collapses it (or jumps to the parent folder from a node).
        public void ExpandOrCollapseSelected(bool expand)
        {
            var selection = GetSelection();
            if (selection.Count == 0 || hasSearch) return;

            var id = selection[0];
            var item = FindItem(id, rootItem);
            if (item == null) return;

            if (item.hasChildren)
            {
                if (expand != IsExpanded(id)) SetExpanded(id, expand);
                else if (expand) MoveSelection(1); // Already open - step into it
                else if (item.parent != null && item.parent.id != rootItem.id) SelectItem(item.parent.id);
            }
            else if (!expand && item.parent != null && item.parent.id != rootItem.id)
            {
                SelectItem(item.parent.id);
            }
        }

        /// Enter: creates the selected node or toggles the selected folder.
        public void ActivateSelected()
        {
            var entry = GetSelectedEntry();
            if (entry != null)
            {
                if (entry.IsEnabled) OnEntryChosen?.Invoke(entry);
                return;
            }

            var selection = GetSelection();
            if (selection.Count > 0 && !hasSearch) SetExpanded(selection[0], !IsExpanded(selection[0]));
        }

        private void SelectItem(int id) => SetSelection(new List<int> { id }, TreeViewSelectionOptions.RevealAndFrame);

        private static int IndexOfRow(IList<TreeViewItem<int>> rows, int id)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].id == id) return i;
            }

            return -1;
        }

        /// Selects the first leaf, so Enter creates the top search result, and scrolls to the top.
        /// Not framing the selection keeps the first folder header visible; the shared state would otherwise keep the previous scroll.
        public void SelectFirstLeaf()
        {
            var first = GetRows().FirstOrDefault(row => _entryById.TryGetValue(row.id, out var entry) && entry.IsEnabled);
            if (first != null) SetSelection(new List<int> { first.id });
            state.scrollPos = Vector2.zero;
        }

        #region Tree building

        protected override TreeViewItem<int> BuildRoot()
        {
            _entryById.Clear();
            _pinnedIds.Clear();
            var usedIds = new HashSet<int> { 0 };
            var root = new TreeViewItem<int>(0, -1);
            var folders = new Dictionary<string, TreeViewItem<int>>();

            var pinned = _entries.Where(e => e.pinnedOrder.HasValue).OrderBy(e => e.pinnedOrder).ThenBy(e => e.name).ToList();
            TreeViewItem<int> pinnedFolder = null;
            if (pinned.Count > 0)
            {
                pinnedFolder = new TreeViewItem<int>(UniqueId(pinnedFolderKey, usedIds), 0, "Pinned")
                {
                    icon = EditorGUIUtility.IconContent("Pinned").image as Texture2D,
                };
                root.AddChild(pinnedFolder);
                foreach (var entry in pinned)
                {
                    var leaf = new TreeViewItem<int>(UniqueId(pinnedFolderKey + entry.path, usedIds), 1, entry.name);
                    _entryById[leaf.id] = entry;
                    _pinnedIds.Add(leaf.id);
                    pinnedFolder.AddChild(leaf);
                }
            }

            // Graph specific nodes are moved here (pinned ones stay in Pinned and in their folder). Unlike Pinned, they are searchable and,
            // being first in the tree, rank first in the flat search results.
            var featured = _entries.Where(e => e.graphSpecific && !e.pinnedOrder.HasValue)
                .OrderBy(e => e.order).ThenBy(e => e.name, StringComparer.OrdinalIgnoreCase).ToList();
            TreeViewItem<int> featuredFolder = null;
            if (featured.Count > 0)
            {
                featuredFolder = new TreeViewItem<int>(UniqueId(featuredFolderKey, usedIds), 0, _featuredLabel)
                {
                    icon = EditorGUIUtility.IconContent("d_Favorite").image as Texture2D,
                };
                root.AddChild(featuredFolder);
                foreach (var entry in featured)
                {
                    var leaf = new TreeViewItem<int>(UniqueId(featuredFolderKey + entry.path, usedIds), 1, entry.name);
                    _entryById[leaf.id] = entry;
                    featuredFolder.AddChild(leaf);
                }
            }

            foreach (var entry in _entries.Where(e => !featured.Contains(e)).OrderBy(e => e.order)
                         .ThenBy(e => e.path, StringComparer.OrdinalIgnoreCase))
            {
                var segments = entry.path.Split('/');
                var parent = root;
                var folderPath = "";

                for (var i = 0; i < segments.Length - 1; i++)
                {
                    folderPath = folderPath.Length == 0 ? segments[i] : $"{folderPath}/{segments[i]}";
                    if (!folders.TryGetValue(folderPath, out var folder))
                    {
                        folder = new TreeViewItem<int>(UniqueId(folderPath, usedIds), parent.depth + 1, segments[i]);
                        folders[folderPath] = folder;
                        parent.AddChild(folder);
                    }

                    parent = folder;
                }

                var leaf = new TreeViewItem<int>(UniqueId(entry.path, usedIds), parent.depth + 1, segments[^1]);
                _entryById[leaf.id] = entry;
                parent.AddChild(leaf);
            }

            SortFoldersFirst(root);
            if (pinnedFolder != null)
            {
                root.children.Remove(pinnedFolder);
                root.children.Insert(0, pinnedFolder);
            }

            if (featuredFolder != null)
            {
                root.children.Remove(featuredFolder);
                root.children.Insert(0, featuredFolder);
            }

            SetupDepthsFromParentsAndChildren(root);
            return root;
        }

        private static int UniqueId(string key, HashSet<int> used)
        {
            var id = key.GetHashCode();
            while (!used.Add(id)) id++;
            return id;
        }

        private static void SortFoldersFirst(TreeViewItem<int> item)
        {
            if (!item.hasChildren) return;

            // Stable sort - leaves keep the [CreateNodeMenu] order
            var sorted = item.children
                .Cast<TreeViewItem<int>>()
                .Select((child, index) => (child, index))
                .OrderBy(x => x.child.hasChildren ? 0 : 1)
                .ThenBy(x => x.child.hasChildren ? x.child.displayName : "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.index)
                .Select(x => x.child)
                .ToList();

            item.children.Clear();
            foreach (var child in sorted)
            {
                item.children.Add(child);
                SortFoldersFirst(child);
            }
        }

        #endregion

        #region Search

        protected override bool DoesItemMatchSearch(TreeViewItem<int> item, string search)
        {
            if (_pinnedIds.Contains(item.id)) return false;
            if (!_entryById.TryGetValue(item.id, out var entry)) return false; // Folders are not results - matching leaves are listed flat

            // Every word has to match somewhere in the name, path or description
            foreach (var word in search.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Contains(entry.path, word) && !Contains(entry.description, word)) return false;
            }

            return true;
        }

        private static bool Contains(string text, string word) =>
            !string.IsNullOrEmpty(text) && text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;

        #endregion

        #region Drawing & input

        protected override float GetCustomRowHeight(int row, TreeViewItem<int> item) =>
            _entryById.ContainsKey(item.id) ? leafRowHeight : folderRowHeight;

        protected override void RowGUI(RowGUIArgs args)
        {
            if (_entryById.TryGetValue(args.item.id, out var entry))
            {
                using (new EditorGUI.DisabledScope(!entry.IsEnabled)) DrawLeaf(args, entry);
            }
            else DrawFolder(args);

            // Separator between the rows
            if (Event.current.type == EventType.Repaint)
            {
                var rect = args.rowRect;
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), separatorColor);
            }
        }

        private void DrawFolder(RowGUIArgs args)
        {
            var rect = args.rowRect;
            folderStyle ??= new GUIStyle(EditorStyles.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };

            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(rect, folderBackgroundColor);

            // The built-in foldout is disabled (CanChangeExpandedState) - it sticks to the top of tall rows. Clicking the row toggles the folder.
            if (!hasSearch && Event.current.type == EventType.Repaint)
            {
                var foldoutRect = new Rect(rect.x + GetFoldoutIndent(args.item), rect.y + (rect.height - 16f) * 0.5f, 16f, 16f);
                EditorStyles.foldout.Draw(foldoutRect, GUIContent.none, false, false, IsExpanded(args.item.id), false);
            }

            var x = rect.x + GetContentIndent(args.item);
            if (args.item.icon)
            {
                GUI.DrawTexture(new Rect(x, rect.y + (rect.height - folderIconSize) * 0.5f, folderIconSize, folderIconSize), args.item.icon,
                    ScaleMode.ScaleToFit);
                x += folderIconSize + 6f;
            }

            GUI.Label(new Rect(x, rect.y, rect.xMax - x, rect.height), args.item.displayName, folderStyle);
        }

        private void DrawLeaf(RowGUIArgs args, NodeMenuEntry entry)
        {
            var rect = args.rowRect;
            titleStyle ??= new GUIStyle(EditorStyles.label) { fontSize = 13, fontStyle = FontStyle.Bold, clipping = TextClipping.Clip };
            descriptionStyle ??= new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip };

            var indent = hasSearch ? 0f : GetContentIndent(args.item);
            var x = rect.x + indent + 4f;

            if (entry.icon)
            {
                GUI.DrawTexture(new Rect(x, rect.y + (rect.height - rowIconSize) * 0.5f, rowIconSize, rowIconSize), entry.icon,
                    ScaleMode.ScaleToFit);
            }

            x += rowIconSize + 6f;
            var width = rect.xMax - x - 4f;

            var summary = entry.IsEnabled ? GetFirstLine(entry.description) : entry.disabledReason;
            var hasSummary = summary.Length > 0;

            // Title above, one line of description below. Without a description the title is centered.
            var textHeight = hasSummary ? leafTitleHeight + leafDescriptionHeight : leafTitleHeight;
            var top = rect.y + (rect.height - textHeight) * 0.5f;
            var titleRect = new Rect(x, top, width, leafTitleHeight);
            GUI.Label(titleRect, entry.name, titleStyle);

            if (hasSummary)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    GUI.Label(new Rect(x, top + leafTitleHeight, width, leafDescriptionHeight), Truncate(summary, descriptionStyle, width),
                        descriptionStyle);
                }
            }

            // Flat search results show where the node lives
            if (hasSearch && entry.path.Contains('/'))
            {
                var folderPath = entry.path[..entry.path.LastIndexOf('/')];
                var nameWidth = titleStyle.CalcSize(new GUIContent(entry.name)).x;
                var pathRect = new Rect(x + nameWidth + 8f, top, rect.xMax - (x + nameWidth + 8f), leafTitleHeight);
                if (pathRect.width > pathLabelMinWidth)
                {
                    using (new EditorGUI.DisabledScope(true)) GUI.Label(pathRect, folderPath, descriptionStyle);
                }
            }

            if (hasSummary) GUI.Label(rect, new GUIContent("", entry.IsEnabled ? entry.description : entry.disabledReason), GUIStyle.none);
        }

        private static string GetFirstLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var newLine = text.IndexOf('\n');
            return (newLine < 0 ? text : text[..newLine]).Trim();
        }

        /// IMGUI can't ellipsize - cut the text to fit the width and append "...".
        private static string Truncate(string text, GUIStyle style, float width)
        {
            if (style.CalcSize(new GUIContent(text)).x <= width) return text;

            var length = Mathf.Min(text.Length, Mathf.Max(1, (int) (text.Length * width / style.CalcSize(new GUIContent(text)).x)));
            while (length > 1 && style.CalcSize(new GUIContent(text[..length] + "...")).x > width) length--;
            return text[..length].TrimEnd() + "...";
        }

        protected override void SingleClickedItem(int id)
        {
            if (_entryById.TryGetValue(id, out var entry))
            {
                if (entry.IsEnabled) OnEntryChosen?.Invoke(entry);
            }
            else SetExpanded(id, !IsExpanded(id));
        }

        protected override void KeyEvent()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown || e.keyCode is not (KeyCode.Return or KeyCode.KeypadEnter)) return;

            var entry = GetSelectedEntry();
            if (entry == null) return;

            OnEntryChosen?.Invoke(entry);
            e.Use();
        }

        protected override bool CanChangeExpandedState(TreeViewItem<int> item) => false;

        protected override bool CanMultiSelect(TreeViewItem<int> item) => false;

        #endregion

    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XNode;
using XNodeEditor;
using Xprees.Graph.Core.Editor.Graph;
using Xprees.Graph.Core.Editor.Minimap;
using Xprees.Graph.Core.Editor.Navigation;
using Xprees.Graph.Core.Editor.NodeMenu;
using Xprees.Graph.Core.Editor.Validation;
using PopupWindow = UnityEditor.PopupWindow;

namespace Xprees.Graph.Core.Editor.Toolbar
{
    /// UI Toolkit toolbar injected on top of the xNode editor window (xNode is IMGUI only and has no toolbar).
    /// UI Toolkit elements receive clicks before xNode's IMGUI, so toolbar clicks don't leak into the graph.
    public static partial class GraphEditorToolbar
    {
        public const float Height = 21f;

        // xNode pads its GUI by a hardcoded 19 (docked) / 22 (floating) for the tab strip - an old Unity value
        private const float xNodeDockedTopPadding = 19f;
        private const float xNodeFloatingTopPadding = 22f;
        private const float fallbackTabHeight = 24f;
        private const float iconSize = 14f;
        private const float sidePadding = 8f;

        // Separator arrow + item padding of a ToolbarBreadcrumbs item, plus a safety margin for text measured with IMGUI
        private const float breadcrumbChrome = 26f;
        private const float breadcrumbsMinWidth = 60f;
        private const string ellipsis = "…";

        private const string toolbarName = "xprees-graph-toolbar";
        private const string breadcrumbsName = "xprees-graph-breadcrumbs";
        private const string badgeName = "xprees-graph-validation-badge";
        private const string badgeIconName = "xprees-graph-validation-icon";
        private const string todoButtonName = "xprees-graph-todo-button";
        private const string todoLabelName = "xprees-graph-todo-label";
        private const long badgeRefreshIntervalMs = 1000;

        private readonly static float tabHeight = GetTabHeight();

        /// Adds the toolbar to the window if missing (window content is recreated after domain reload).
        public static void EnsureAttached(NodeEditorWindow window, NodeGraph graph)
        {
            if (!window) return;

            var root = window.rootVisualElement;
            var toolbar = root.Q(toolbarName);
            if (toolbar == null)
            {
                // The root covers the whole window - let clicks outside the toolbar through to xNode
                root.pickingMode = PickingMode.Ignore;
                root.style.overflow = Overflow.Visible;
                toolbar = Create(window);
                root.Add(toolbar);
                Refresh(window, graph);
            }

            ApplyTopOffset(window, toolbar);
        }

        /// xNode paints its graph from a hardcoded top padding, which is smaller than the tab strip of current Unity versions.
        /// The difference shows as a strip of graph between the tab and the toolbar - cover it by stretching the toolbar upwards.
        private static void ApplyTopOffset(NodeEditorWindow window, VisualElement toolbar)
        {
            var xNodePadding = window.docked ? xNodeDockedTopPadding : xNodeFloatingTopPadding;
            var rootOffset = window.rootVisualElement.worldBound.y; // Window content starts below the tab strip
            var bleed = Mathf.Max(0f, Mathf.Min(tabHeight, rootOffset) - xNodePadding);

            if (Mathf.Approximately(toolbar.style.top.value.value, -bleed)) return;

            toolbar.style.top = -bleed;
            toolbar.style.height = Height + bleed;
            toolbar.style.paddingTop = bleed;
        }

        private static float GetTabHeight()
        {
            var dockArea = typeof(EditorWindow).Assembly.GetType("UnityEditor.DockArea");
            var field = dockArea?.GetField("kTabHeight", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            return field?.GetValue(null) is float height ? height : fallbackTabHeight;
        }

        /// Rebuilds breadcrumbs and the validation badge for the shown graph.
        public static void Refresh(NodeEditorWindow window, NodeGraph graph)
        {
            if (!window) return;

            var toolbar = window.rootVisualElement.Q(toolbarName);
            if (toolbar == null) return;

            RefreshBreadcrumbs(toolbar);
            RefreshBadge(toolbar, graph);
        }

        private static VisualElement Create(NodeEditorWindow window)
        {
            var toolbar = new UnityEditor.UIElements.Toolbar { name = toolbarName };
            toolbar.style.position = Position.Absolute;
            toolbar.style.left = 0;
            toolbar.style.right = 0;
            toolbar.style.top = 0;
            toolbar.style.height = Height;
            toolbar.style.paddingLeft = sidePadding;
            toolbar.style.paddingRight = sidePadding;

            var addButton = CreateButton("d_Toolbar Plus", null, "Create a node in the center of the view.", null);
            addButton.clicked += () => NodeCreationPopup.ShowAtViewCenter(window, addButton.worldBound, Height);
            addButton.style.marginRight = 6;
            toolbar.Add(addButton);

            // Takes exactly the space the buttons leave (basis 0), so its width never depends on its content and the
            // crumbs can be collapsed to fit without a layout feedback loop
            var breadcrumbs = new ToolbarBreadcrumbs { name = breadcrumbsName };
            breadcrumbs.style.flexGrow = 1;
            breadcrumbs.style.flexShrink = 1;
            breadcrumbs.style.flexBasis = 0;
            breadcrumbs.style.minWidth = breadcrumbsMinWidth;
            breadcrumbs.style.overflow = Overflow.Hidden;
            breadcrumbs.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (Mathf.Abs(evt.newRect.width - evt.oldRect.width) > 1f) RefreshBreadcrumbs(toolbar);
            });
            toolbar.Add(breadcrumbs);

            toolbar.Add(CreateDebugGroup(window));
            toolbar.Add(CreateSeparator());

            var mapToggle = GraphMinimap.CreateToolbarToggle(window);
            if (DebugIcons.Decorate(mapToggle, "Grid.Default", "UnityEditor.SceneHierarchyWindow")) MarkCollapsibleText(mapToggle);
            toolbar.Add(mapToggle);
            toolbar.Add(GraphLod.CreateToolbarToggle());
            toolbar.Add(CreateLayoutGroup(window));

            var frameAll = CreateButton("d_ViewToolZoom", "Frame All", "Center the view on all nodes and fit them into the window.",
                () => GraphBaseEditor.FrameAll(window));
            MarkCollapsibleText(frameAll);
            toolbar.Add(frameAll);

            var validateButton = CreateButton(null, "Validate",
                "Validate this graph: checks broken connections, unreachable nodes and dead ends. The badge shows the error/warning count; TODO notes are listed separately. Click to open the Graph Validator on this graph.",
                () => GraphValidatorWindow.OpenFor(window.graph));
            validateButton.Insert(0, CreateIcon(null, badgeIconName));
            MarkCollapsibleText(validateButton);
            validateButton.Add(new Label { name = badgeName, style = { marginLeft = 4, unityFontStyleAndWeight = FontStyle.Bold } });
            toolbar.Add(validateButton);

            var todoIndex = -1;
            var todoButton = CreateButton("console.infoicon.sml", null, "Focus the next TODO note of this graph.", () =>
            {
                var todos = GraphValidator.Validate(window.graph).Where(i => i.Severity == GraphIssueSeverity.Todo && i.Node).ToList();
                if (todos.Count <= 0) return;

                todoIndex = (todoIndex + 1) % todos.Count;
                GraphBaseEditor.FocusNode(todos[todoIndex].Node);
            });
            todoButton.name = todoButtonName;
            todoButton.Add(new Label { name = todoLabelName });
            todoButton.style.display = DisplayStyle.None;
            toolbar.Add(todoButton);

            var validatorButton = CreateButton("d_UnityEditor.ConsoleWindow", "Validator", "Open the Graph Validator window.",
                GraphValidatorWindow.Open);
            MarkCollapsibleText(validatorButton);
            toolbar.Add(validatorButton);

            // Icon-only mode for narrow windows so nothing overflows or wraps
            var narrow = false;
            toolbar.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var next = ToolbarLogic.IsNarrow(evt.newRect.width, narrow);
                if (next == narrow) return;

                narrow = next;
                ApplyNarrowMode(toolbar, narrow);
            });

            // Keep the badge up to date while the graph is being edited
            toolbar.schedule.Execute(() => RefreshBadge(toolbar, window ? window.graph : null)).Every(badgeRefreshIntervalMs);

            ScheduleDebugRefresh(toolbar, window);

            // Breadcrumbs change on navigation from any window
            void OnChainChanged() => RefreshBreadcrumbs(toolbar);
            toolbar.RegisterCallback<AttachToPanelEvent>(_ => GraphNavigation.OnChainChanged += OnChainChanged);
            toolbar.RegisterCallback<DetachFromPanelEvent>(_ => GraphNavigation.OnChainChanged -= OnChainChanged);

            return toolbar;
        }

        private static ToolbarButton CreateButton(string iconName, string text, string tooltip, Action onClick)
        {
            var button = onClick != null ? new ToolbarButton(onClick) { tooltip = tooltip } : new ToolbarButton { tooltip = tooltip };
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;

            if (iconName != null) button.Add(CreateIcon(iconName));
            if (!string.IsNullOrEmpty(text)) button.Add(new Label(text));
            return button;
        }

        private static Image CreateIcon(string iconName, string elementName = null)
        {
            var icon = new Image
            {
                name = elementName,
                image = iconName != null ? EditorGUIUtility.IconContent(iconName).image : null,
                pickingMode = PickingMode.Ignore,
            };
            icon.style.width = iconSize;
            icon.style.height = iconSize;
            icon.style.marginRight = 4;
            return icon;
        }

        private static void RefreshBreadcrumbs(VisualElement toolbar)
        {
            var breadcrumbs = toolbar.Q<ToolbarBreadcrumbs>(breadcrumbsName);
            if (breadcrumbs == null) return;

            var available = breadcrumbs.resolvedStyle.width;
            if (float.IsNaN(available) || available <= 0f) return; // Not laid out yet, the geometry callback calls this again

            breadcrumbs.Clear();
            var chain = GraphNavigation.Chain;
            if (chain.Count == 0) return;

            var names = chain.Select(c => c.graph ? c.graph.name : "(missing)").ToList();
            var style = EditorStyles.toolbarButton;
            var widths = names.Select(n => style.CalcSize(new GUIContent(n)).x + breadcrumbChrome).ToList();
            var ellipsisWidth = style.CalcSize(new GUIContent(ellipsis)).x + breadcrumbChrome;
            var layout = ToolbarLogic.ComputeBreadcrumbs(widths, ellipsisWidth, available);
            var fullPath = string.Join(" > ", names);

            var tooltips = new List<string>();

            void Push(int index, string text)
            {
                var isCurrent = index == chain.Count - 1;
                var graph = chain[index].graph;
                breadcrumbs.PushItem(text, isCurrent ? () => EditorGUIUtility.PingObject(graph) : () => GraphNavigation.NavigateTo(index));
                tooltips.Add(text == names[index] ? fullPath : $"{names[index]}\n{fullPath}");
            }

            if (layout.ShowFirst) Push(0, names[0]);

            if (layout.HasEllipsis)
            {
                breadcrumbs.PushItem(ellipsis, () => ShowChainPopup(breadcrumbs));
                tooltips.Add(fullPath);
            }

            for (var i = Mathf.Max(layout.TailStart, layout.ShowFirst ? 1 : 0); i < chain.Count; i++)
            {
                var text = names[i];
                if (layout.TruncateLast && i == chain.Count - 1)
                {
                    var used = (layout.ShowFirst ? widths[0] : 0f) + ellipsisWidth;
                    text = FitMiddle(names[i], available - used - breadcrumbChrome, style);
                }

                Push(i, text);
            }

            // Items are the children of the breadcrumbs in push order
            var items = breadcrumbs.Children().ToList();
            for (var i = 0; i < items.Count && i < tooltips.Count; i++) items[i].tooltip = tooltips[i];
        }

        /// Longest middle-ellipsis form of the name that fits the width.
        private static string FitMiddle(string name, float width, GUIStyle style)
        {
            for (var keep = name.Length; keep > 0; keep--)
            {
                var candidate = ToolbarLogic.MiddleEllipsis(name, keep);
                if (style.CalcSize(new GUIContent(candidate)).x <= width) return candidate;
            }

            return ellipsis;
        }

        private static void ShowChainPopup(VisualElement breadcrumbs)
        {
            var chain = GraphNavigation.Chain;
            var graphs = chain.Select(c => c.graph).ToList();
            var popup = new BreadcrumbChainPopup(graphs, index =>
            {
                if (index == graphs.Count - 1) EditorGUIUtility.PingObject(graphs[index]);
                else GraphNavigation.NavigateTo(index);
            });
            PopupWindow.Show(breadcrumbs.worldBound, popup);
        }

        private static void RefreshBadge(VisualElement toolbar, NodeGraph graph)
        {
            var badge = toolbar.Q<Label>(badgeName);
            var badgeIcon = toolbar.Q<Image>(badgeIconName);
            var todoButton = toolbar.Q(todoButtonName);
            if (badge == null) return;

            if (!graph || EditorApplication.isCompiling)
            {
                badge.text = string.Empty;
                if (badgeIcon != null) badgeIcon.image = null;
                if (todoButton != null) todoButton.style.display = DisplayStyle.None;
                return;
            }

            var issues = GraphValidator.Validate(graph);
            var errors = issues.Count(i => i.Severity == GraphIssueSeverity.Error);
            var warnings = issues.Count(i => i.Severity == GraphIssueSeverity.Warning);
            var todos = issues.Count(i => i.Severity == GraphIssueSeverity.Todo);

            if (todoButton != null)
            {
                todoButton.style.display = todos > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                todoButton.Q<Label>(todoLabelName).text = $"{todos} TODO";
            }

            badge.text = errors > 0 ? $"{errors} errors" : warnings > 0 ? $"{warnings} warnings" : "OK";
            badge.style.color = errors > 0 ? new Color(1f, 0.4f, 0.4f) : warnings > 0 ? new Color(1f, 0.8f, 0.3f) : new Color(0.5f, 0.9f, 0.5f);
            badge.tooltip = $"{errors} error(s), {warnings} warning(s)";

            if (badgeIcon != null)
            {
                var iconName = errors > 0 ? "console.erroricon.sml" : warnings > 0 ? "console.warnicon.sml" : "TestPassed";
                badgeIcon.image = EditorGUIUtility.IconContent(iconName).image;
            }
        }
    }
}
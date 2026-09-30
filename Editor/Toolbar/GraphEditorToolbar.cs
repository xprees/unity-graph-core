using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XNode;
using XNodeEditor;
using Xprees.Graph.Core.Editor.Graph;
using Xprees.Graph.Core.Editor.Navigation;
using Xprees.Graph.Core.Editor.Validation;

namespace Xprees.Graph.Core.Editor.Toolbar
{
    /// UI Toolkit toolbar injected on top of the xNode editor window (xNode is IMGUI only and has no toolbar).
    /// UI Toolkit elements receive clicks before xNode's IMGUI, so toolbar clicks don't leak into the graph.
    public static class GraphEditorToolbar
    {
        public const float Height = 21f;

        // xNode pads its GUI by a hardcoded 19 (docked) / 22 (floating) for the tab strip - an old Unity value
        private const float xNodeDockedTopPadding = 19f;
        private const float xNodeFloatingTopPadding = 22f;
        private const float fallbackTabHeight = 24f;
        private const float iconSize = 14f;
        private const float sidePadding = 8f;

        private const string toolbarName = "xprees-graph-toolbar";
        private const string breadcrumbsName = "xprees-graph-breadcrumbs";
        private const string badgeName = "xprees-graph-validation-badge";
        private const string badgeIconName = "xprees-graph-validation-icon";
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

            var breadcrumbs = new ToolbarBreadcrumbs { name = breadcrumbsName };
            breadcrumbs.style.flexShrink = 1;
            breadcrumbs.style.overflow = Overflow.Hidden;
            toolbar.Add(breadcrumbs);

            toolbar.Add(new ToolbarSpacer { style = { flexGrow = 1 } });

            toolbar.Add(CreateButton("d_ViewToolZoom", "Frame All", "Center the view on all nodes and fit them into the window.",
                () => GraphBaseEditor.FrameAll(window)));

            var validateButton = CreateButton(null, "Validate", "Validate this graph - opens the Graph Validator scoped to it.",
                () => GraphValidatorWindow.OpenFor(window.graph));
            validateButton.Insert(0, CreateIcon(null, badgeIconName));
            validateButton.Add(new Label { name = badgeName, style = { marginLeft = 4, unityFontStyleAndWeight = FontStyle.Bold } });
            toolbar.Add(validateButton);

            toolbar.Add(CreateButton("d_UnityEditor.ConsoleWindow", "Validator", "Open the Graph Validator window.",
                GraphValidatorWindow.Open));

            // Keep the badge up to date while the graph is being edited
            toolbar.schedule.Execute(() => RefreshBadge(toolbar, window ? window.graph : null)).Every(badgeRefreshIntervalMs);

            // Breadcrumbs change on navigation from any window
            void OnChainChanged() => RefreshBreadcrumbs(toolbar);
            toolbar.RegisterCallback<AttachToPanelEvent>(_ => GraphNavigation.OnChainChanged += OnChainChanged);
            toolbar.RegisterCallback<DetachFromPanelEvent>(_ => GraphNavigation.OnChainChanged -= OnChainChanged);

            return toolbar;
        }

        private static ToolbarButton CreateButton(string iconName, string text, string tooltip, Action onClick)
        {
            var button = new ToolbarButton(onClick) { tooltip = tooltip };
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;

            if (iconName != null) button.Add(CreateIcon(iconName));
            button.Add(new Label(text));
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

            breadcrumbs.Clear();
            var chain = GraphNavigation.Chain;
            for (var i = 0; i < chain.Count; i++)
            {
                var index = i;
                var isCurrent = i == chain.Count - 1;
                breadcrumbs.PushItem(chain[i].graph.name, isCurrent ? null : () => GraphNavigation.NavigateTo(index));
            }
        }

        private static void RefreshBadge(VisualElement toolbar, NodeGraph graph)
        {
            var badge = toolbar.Q<Label>(badgeName);
            var badgeIcon = toolbar.Q<Image>(badgeIconName);
            if (badge == null) return;

            if (!graph || EditorApplication.isCompiling)
            {
                badge.text = string.Empty;
                if (badgeIcon != null) badgeIcon.image = null;
                return;
            }

            var issues = GraphValidator.Validate(graph);
            var errors = issues.Count(i => i.Severity == GraphIssueSeverity.Error);
            var warnings = issues.Count - errors;

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
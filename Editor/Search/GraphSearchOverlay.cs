using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using XNodeEditor;
using Xprees.Graph.Core.Editor.Graph;
using Xprees.Graph.Core.Editor.Navigation;
using Xprees.Graph.Core.Editor.Toolbar;

namespace Xprees.Graph.Core.Editor.Search
{
    /// Ctrl+F search box over the graph window (UI Toolkit, like the toolbar): TextField + result list.
    /// Enter / F3 select the next result, Shift+Enter / Shift+F3 the previous one, a click focuses the node, Escape closes.
    public static class GraphSearchOverlay
    {
        private const string overlayName = "xprees-graph-search";
        private const float width = 360f;
        private const float listMaxHeight = 220f;
        private const float rowHeight = 20f;

        private sealed class State
        {
            public NodeEditorWindow Window;
            public TextField Field;
            public ListView List;
            public Label Count;
            public readonly List<GraphSearchResult> Results = new();
            public bool Updating;
        }

        /// Opens the overlay (or refocuses it) and re-runs the search on the shown graph.
        public static void Show(NodeEditorWindow window)
        {
            if (!window) return;

            var overlay = window.rootVisualElement.Q(overlayName);
            if (overlay == null)
            {
                overlay = Create(window);
                window.rootVisualElement.Add(overlay);
            }

            var state = (State) overlay.userData;
            overlay.style.display = DisplayStyle.Flex;
            Run(state);
            state.Field.schedule.Execute(() =>
            {
                state.Field.Focus();
                state.Field.SelectAll();
            });
        }

        public static bool IsOpen(NodeEditorWindow window)
        {
            var overlay = window ? window.rootVisualElement.Q(overlayName) : null;
            return overlay != null && overlay.style.display != DisplayStyle.None;
        }

        /// Selects the next (or previous) result of the open overlay.
        public static void Step(NodeEditorWindow window, int direction)
        {
            var overlay = window ? window.rootVisualElement.Q(overlayName) : null;
            if (overlay?.userData is State state && IsOpen(window)) Step(state, direction);
        }

        /// True while the overlay owns the keyboard, graph shortcuts must then not act.
        public static bool HasKeyboardFocus(NodeEditorWindow window)
        {
            if (!IsOpen(window)) return false;

            var overlay = window.rootVisualElement.Q(overlayName);
            var focused = overlay.panel?.focusController?.focusedElement as VisualElement;
            for (var element = focused; element != null; element = element.parent)
            {
                if (element == overlay) return true;
            }

            return false;
        }

        public static void Close(NodeEditorWindow window)
        {
            var overlay = window ? window.rootVisualElement.Q(overlayName) : null;
            if (overlay == null) return;

            overlay.style.display = DisplayStyle.None;
            window.Focus();
            window.Repaint();
        }

        private static VisualElement Create(NodeEditorWindow window)
        {
            var state = new State { Window = window };

            var overlay = new VisualElement { name = overlayName, userData = state };
            overlay.style.position = Position.Absolute;
            overlay.style.top = GraphEditorToolbar.Height + 6;
            overlay.style.left = Length.Percent(50);
            overlay.style.translate = new Translate(-width * 0.5f, 0);
            overlay.style.width = width;
            overlay.style.backgroundColor = new Color(0.17f, 0.17f, 0.17f, 0.98f);
            overlay.style.borderTopWidth = overlay.style.borderBottomWidth = overlay.style.borderLeftWidth = overlay.style.borderRightWidth = 1;
            overlay.style.borderTopColor = overlay.style.borderBottomColor =
                overlay.style.borderLeftColor = overlay.style.borderRightColor = new Color(0.1f, 0.1f, 0.1f);
            overlay.style.paddingTop = overlay.style.paddingBottom = overlay.style.paddingLeft = overlay.style.paddingRight = 4;

            // Keys typed here must never reach xNode (Delete, F, Ctrl+V, A ...); the focused field still gets them in the target phase
            overlay.RegisterCallback<KeyDownEvent>(evt => evt.StopPropagation());
            overlay.RegisterCallback<KeyUpEvent>(evt => evt.StopPropagation());
            overlay.RegisterCallback<ValidateCommandEvent>(evt => evt.StopPropagation());
            overlay.RegisterCallback<ExecuteCommandEvent>(evt => evt.StopPropagation());

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            state.Field = new TextField
            {
                style = { flexGrow = 1, marginLeft = 0, marginRight = 4 },
                tooltip = "Node name, type, note text, text fields, referenced asset names. Enter = next, Shift+Enter = previous, Esc = close."
            };
            state.Field.RegisterValueChangedCallback(_ => Run(state));
            state.Field.RegisterCallback<KeyDownEvent>(evt => OnFieldKey(state, evt), TrickleDown.TrickleDown);
            row.Add(state.Field);

            state.Count = new Label { style = { minWidth = 56, unityTextAlign = TextAnchor.MiddleRight } };
            row.Add(state.Count);
            overlay.Add(row);

            state.List = new ListView
            {
                fixedItemHeight = rowHeight,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                itemsSource = state.Results,
                makeItem = () => new Label { style = { unityTextAlign = TextAnchor.MiddleLeft, overflow = Overflow.Hidden } },
                bindItem = (element, index) => ((Label) element).text = Describe(state.Results[index]),
                style = { maxHeight = listMaxHeight, marginTop = 4, display = DisplayStyle.None },
            };
            state.List.selectionChanged += _ => OnSelected(state);
            overlay.Add(state.List);

            return overlay;
        }

        private static void OnFieldKey(State state, KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.F3:
                    Step(state, evt.shiftKey ? -1 : 1);
                    break;
                case KeyCode.Escape:
                    Close(state.Window);
                    break;
                default:
                    return;
            }

            evt.StopPropagation();
            state.Field.focusController?.IgnoreEvent(evt);
        }

        private static void Run(State state)
        {
            state.Results.Clear();
            var graph = state.Window ? state.Window.graph : null;
            state.Results.AddRange(GraphSearch.Find(graph, state.Field.value));

            state.Updating = true;
            state.List.ClearSelection();
            state.List.Rebuild();
            state.Updating = false;

            var hasQuery = !string.IsNullOrWhiteSpace(state.Field.value);
            state.List.style.display = state.Results.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            state.List.style.height = Mathf.Min(listMaxHeight, state.Results.Count * rowHeight);
            state.Count.text = hasQuery ? $"{state.Results.Count} found" : string.Empty;
        }

        private static void Step(State state, int direction)
        {
            var count = state.Results.Count;
            if (count == 0) return;

            var current = state.List.selectedIndex;
            var next = current < 0 ? (direction > 0 ? 0 : count - 1) : (current + direction + count) % count;
            state.List.selectedIndex = next;
            state.List.ScrollToItem(next);
        }

        private static void OnSelected(State state)
        {
            if (state.Updating) return;

            var index = state.List.selectedIndex;
            if (index < 0 || index >= state.Results.Count) return;

            var node = state.Results[index].Node;
            if (!node) return;

            // A node of a sub-graph: enter it from the node that references it so the breadcrumbs stay right
            var window = state.Window;
            if (window && window.graph && node.graph != window.graph)
            {
                var via = GraphReferences.FindReferencingNode(window.graph, node.graph);
                if (via) GraphNavigation.EnterGraph(via, node.graph);
            }

            GraphBaseEditor.FocusNode(node);
            state.Field.schedule.Execute(() => state.Field.Focus()); // Keep typing and stepping possible
        }

        private static string Describe(GraphSearchResult result)
        {
            if (!result.Node) return string.Empty;

            var graphName = result.Graph ? result.Graph.name : string.Empty;
            var matched = result.Kind == GraphSearchMatchKind.Name ? string.Empty : $"  [{result.Kind}: {result.Matched}]";
            return $"{result.Node.name}{matched}  - {graphName}";
        }
    }
}
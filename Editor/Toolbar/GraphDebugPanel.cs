using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XNode;
using XNodeEditor;
using Xprees.Graph.Core.Editor.Debugger;
using Xprees.Graph.Core.Editor.Graph;
using Xprees.Variables.Base;
using Object = UnityEngine.Object;

namespace Xprees.Graph.Core.Editor.Toolbar
{
    /// Right side panel of the graph window (UI Toolkit, like the toolbar) with the Variables and History tabs.
    /// Only created while the debugger is on, and its refresh pauses when the debugger is turned off.
    public static class GraphDebugPanel
    {
        private const string panelName = "xprees-graph-debug-panel";
        private const string widthKey = "Xprees.GraphDebugger.PanelWidth";
        private const string collapsedKey = "Xprees.GraphDebugger.PanelCollapsed";
        private const string tabKey = "Xprees.GraphDebugger.PanelTab";
        private const float defaultWidth = 320f;
        private const float minWidth = 220f;
        private const float collapsedWidth = 28f;
        private const long refreshIntervalMs = 150;
        private const double collectIntervalSeconds = 1.0;
        private const int maxHistoryRows = 500;
        private const int maxRecordRows = 50;
        private const float rowHeight = 18f;
        private const float recordsMaxHeight = 110f;

        private sealed class VariableRow
        {
            public VariableBaseSO Variable;
            public List<Node> Nodes;
            public VisualElement Root;
            public VisualElement Editor;
            public Action<object> SetValue;
            public string LastText;
            public double ChangedAt;
            public VariableAccess.ValueKind Kind;
            public Label NameLabel;
            public string FullName;
            public int NameWidthChars;
            public Foldout Foldout;
            public VisualElement FoldoutBody;
            public Object Inspected;
            public bool BodyBuilt;
        }

        private sealed class PanelState
        {
            public NodeEditorWindow Window;
            public IVisualElementScheduledItem Refresh;
            public VisualElement Content;
            public VisualElement Resizer;
            public VisualElement Tabs;
            public ToolbarButton CollapseButton;
            public ToolbarToggle VariablesTab;
            public ToolbarToggle HistoryTab;
            public VisualElement VariablesPage;
            public VisualElement HistoryPage;
            public ScrollView VariablesScroll;
            public Label VariablesInfo;
            public VisualElement RecordsBox;
            public ListView HistoryList;
            public Toggle FollowTail;
            public readonly List<VariableRow> Rows = new();
            public readonly List<HistoryEntry> HistoryItems = new();
            public NodeGraph CollectedGraph;
            public double CollectedAt;
            public int HistoryVersion = -1;
            public NodeGraph HistoryGraph;
            public int? HistoryFilter;
            public string RecordsSignature;
            public bool Dragging;
            public float Width;
            public bool Collapsed;
        }

        /// Adds the panel while the debugger is on, hides it (and stops its refresh) when it is off.
        public static void EnsureAttached(NodeEditorWindow window)
        {
            if (!window) return;

            var root = window.rootVisualElement;
            var panel = root.Q(panelName);
            if (!GraphDebuggerSettings.Enabled)
            {
                if (panel == null) return;

                panel.style.display = DisplayStyle.None;
                (panel.userData as PanelState)?.Refresh?.Pause();
                return;
            }

            if (panel == null)
            {
                panel = Create(window);
                root.Add(panel);
            }

            if (panel.style.display == DisplayStyle.None)
            {
                panel.style.display = DisplayStyle.Flex;
                (panel.userData as PanelState)?.Refresh?.Resume();
            }
        }

        /// True while a text field of the panel has keyboard focus, graph shortcuts must then not act.
        public static bool HasKeyboardFocus(NodeEditorWindow window)
        {
            if (!window) return false;

            var panel = window.rootVisualElement.Q(panelName);
            if (panel == null || panel.style.display == DisplayStyle.None) return false;

            var focused = panel.panel?.focusController?.focusedElement as VisualElement;
            for (var element = focused; element != null; element = element.parent)
            {
                if (element == panel) return true;
            }

            return false;
        }

        #region Build

        private static VisualElement Create(NodeEditorWindow window)
        {
            var state = new PanelState
            {
                Window = window,
                Width = Mathf.Max(minWidth, EditorPrefs.GetFloat(widthKey, defaultWidth)),
                Collapsed = EditorPrefs.GetBool(collapsedKey, false),
            };

            var panel = new VisualElement { name = panelName, userData = state };
            panel.style.position = Position.Absolute;
            panel.style.top = GraphEditorToolbar.Height;
            panel.style.bottom = 0;
            panel.style.right = 0;
            panel.style.flexDirection = FlexDirection.Row;
            panel.style.backgroundColor = new Color(0.17f, 0.17f, 0.17f, 0.98f);
            panel.style.borderLeftWidth = 1;
            panel.style.borderLeftColor = new Color(0.1f, 0.1f, 0.1f);

            // Keys typed into the panel must never reach the xNode shortcuts (Delete, Ctrl+V, F, A ...). Bubble phase, so the fields still get them.
            panel.RegisterCallback<KeyDownEvent>(evt => evt.StopPropagation());
            panel.RegisterCallback<KeyUpEvent>(evt => evt.StopPropagation());
            panel.RegisterCallback<ValidateCommandEvent>(evt => evt.StopPropagation());
            panel.RegisterCallback<ExecuteCommandEvent>(evt => evt.StopPropagation());

            state.Resizer = CreateResizer(state, panel);
            panel.Add(state.Resizer);

            var column = new VisualElement { style = { flexGrow = 1, flexShrink = 1, minWidth = 0 } };
            column.Add(CreateHeader(state, panel));

            state.Content = new VisualElement { style = { flexGrow = 1, minHeight = 0 } };
            state.VariablesPage = CreateVariablesPage(state);
            state.HistoryPage = CreateHistoryPage(state);
            state.Content.Add(state.VariablesPage);
            state.Content.Add(state.HistoryPage);
            column.Add(state.Content);
            panel.Add(column);

            ShowTab(state, EditorPrefs.GetInt(tabKey, 0));
            ApplyLayout(state, panel);

            state.Refresh = panel.schedule.Execute(() => Tick(state)).Every(refreshIntervalMs);
            return panel;
        }

        private static VisualElement CreateResizer(PanelState state, VisualElement panel)
        {
            var resizer = new VisualElement { style = { width = 4, backgroundColor = new Color(0.12f, 0.12f, 0.12f) } };
            resizer.RegisterCallback<PointerDownEvent>(evt =>
            {
                state.Dragging = true;
                resizer.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            resizer.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!state.Dragging) return;

                var max = Mathf.Max(minWidth, state.Window.position.width - 120f);
                state.Width = Mathf.Clamp(state.Width - evt.deltaPosition.x, minWidth, max);
                ApplyLayout(state, panel);
                evt.StopPropagation();
            });
            resizer.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!state.Dragging) return;

                state.Dragging = false;
                resizer.ReleasePointer(evt.pointerId);
                EditorPrefs.SetFloat(widthKey, state.Width);
                evt.StopPropagation();
            });
            return resizer;
        }

        private static VisualElement CreateHeader(PanelState state, VisualElement panel)
        {
            var header = new UnityEditor.UIElements.Toolbar();
            header.style.height = GraphEditorToolbar.Height;
            header.style.flexShrink = 0;

            state.CollapseButton = new ToolbarButton { tooltip = "Collapse or expand the debug panel." };
            state.CollapseButton.clicked += () =>
            {
                state.Collapsed = !state.Collapsed;
                EditorPrefs.SetBool(collapsedKey, state.Collapsed);
                ApplyLayout(state, panel);
            };
            header.Add(state.CollapseButton);

            state.Tabs = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
            state.VariablesTab = new ToolbarToggle { text = "Variables" };
            state.VariablesTab.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue) ShowTab(state, 0);
                else state.VariablesTab.SetValueWithoutNotify(true);
            });
            state.HistoryTab = new ToolbarToggle { text = "History" };
            state.HistoryTab.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue) ShowTab(state, 1);
                else state.HistoryTab.SetValueWithoutNotify(true);
            });
            state.Tabs.Add(state.VariablesTab);
            state.Tabs.Add(state.HistoryTab);
            header.Add(state.Tabs);
            return header;
        }

        private static VisualElement CreateVariablesPage(PanelState state)
        {
            var page = new VisualElement { style = { flexGrow = 1, minHeight = 0 } };
            state.VariablesInfo = new Label
                { style = { paddingLeft = 6, paddingTop = 4, paddingBottom = 4, whiteSpace = WhiteSpace.Normal, display = DisplayStyle.None } };
            page.Add(state.VariablesInfo);
            state.VariablesScroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            page.Add(state.VariablesScroll);
            return page;
        }

        private static VisualElement CreateHistoryPage(PanelState state)
        {
            var page = new VisualElement { style = { flexGrow = 1, minHeight = 0 } };

            // Column: instances (own size-limited scroll area), the Follow row, then the history list taking the rest. Nothing is absolute.
            state.RecordsBox = new ScrollView(ScrollViewMode.Vertical)
            {
                style =
                {
                    maxHeight = recordsMaxHeight, flexGrow = 0, flexShrink = 0, borderBottomWidth = 1,
                    borderBottomColor = new Color(0.1f, 0.1f, 0.1f)
                },
            };
            page.Add(state.RecordsBox);

            var followRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row, alignItems = Align.Center, height = 22, minHeight = 22, flexShrink = 0, paddingLeft = 6
                },
            };
            state.FollowTail = new Toggle { text = "Follow newest", value = true, tooltip = "Keep the newest history entry in view." };
            followRow.Add(state.FollowTail);
            page.Add(followRow);

            state.HistoryList = new ListView
            {
                fixedItemHeight = rowHeight,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.All,
                style = { flexGrow = 1, flexShrink = 1, flexBasis = 0, minHeight = 0 },
                itemsSource = state.HistoryItems,
                makeItem = () => CreateRowLabel(string.Empty),
                bindItem = (element, index) => BindHistoryRow(state, (Label) element, index),
            };
            state.HistoryList.selectionChanged += selected =>
            {
                foreach (var item in selected)
                {
                    if (item is not HistoryEntry entry || !entry.Node) continue;

                    GraphDebugRenderer.ShowNode(entry.Record, entry.Node);
                    break;
                }
            };
            page.Add(state.HistoryList);
            return page;
        }

        private static void BindHistoryRow(PanelState state, Label label, int index)
        {
            if (index < 0 || index >= state.HistoryItems.Count) return;

            var entry = state.HistoryItems[index];
            var text = DebuggerText.HistoryRow(entry, entry.Node ? entry.Node.name : "(deleted)", GraphRuntimeTracer.Instance.ReferenceTime);
            label.text = text;
            label.tooltip = text;
        }

        private static void ShowTab(PanelState state, int tab)
        {
            EditorPrefs.SetInt(tabKey, tab);
            state.VariablesTab.SetValueWithoutNotify(tab == 0);
            state.HistoryTab.SetValueWithoutNotify(tab == 1);
            state.VariablesPage.style.display = tab == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            state.HistoryPage.style.display = tab == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            state.HistoryVersion = -1;
        }

        private static void ApplyLayout(PanelState state, VisualElement panel)
        {
            // A chevron icon, the text only when the icon is missing
            var chevron = DebugIcons.Find(state.Collapsed ? "tab_prev" : "tab_next");
            state.CollapseButton.iconImage = chevron ? Background.FromTexture2D(chevron) : default;
            state.CollapseButton.text = chevron ? string.Empty : state.Collapsed ? "<" : ">";
            state.CollapseButton.tooltip = state.Collapsed ? "Expand the debug panel." : "Collapse the debug panel.";
            state.Tabs.style.display = state.Collapsed ? DisplayStyle.None : DisplayStyle.Flex;
            state.Content.style.display = state.Collapsed ? DisplayStyle.None : DisplayStyle.Flex;
            state.Resizer.style.display = state.Collapsed ? DisplayStyle.None : DisplayStyle.Flex;
            panel.style.width = state.Collapsed ? collapsedWidth : state.Width;
        }

        #endregion

        #region Refresh

        private static void Tick(PanelState state)
        {
            if (!state.Window || !GraphDebuggerSettings.Enabled || state.Collapsed) return;

            var graph = state.Window.graph;
            if (state.VariablesPage.style.display != DisplayStyle.None) RefreshVariables(state, graph);

            if (state.HistoryPage.style.display != DisplayStyle.None) RefreshHistory(state, graph);
        }

        private static void RefreshVariables(PanelState state, NodeGraph graph)
        {
            var tracer = GraphRuntimeTracer.Instance;
            if (tracer.IsLoadedTrace)
            {
                ShowInfo(state, "Variables are not available for a loaded trace.");
                ClearRows(state);
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            if (graph != state.CollectedGraph || now - state.CollectedAt > collectIntervalSeconds)
            {
                state.CollectedGraph = graph;
                state.CollectedAt = now;
                RebuildRowsIfChanged(state, graph);
            }

            var playing = Application.isPlaying;
            if (state.Rows.Count == 0) ShowInfo(state, "No variables are referenced by the nodes of this graph.");
            else ShowInfo(state, playing ? null : "Not playing: values are read-only.");

            foreach (var row in state.Rows)
            {
                UpdateRow(row, playing, now);
            }
        }

        private static void ShowInfo(PanelState state, string text)
        {
            var display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            if (state.VariablesInfo.style.display != display) state.VariablesInfo.style.display = display;
            if (state.VariablesInfo.text != text) state.VariablesInfo.text = text;
        }

        private static void ClearRows(PanelState state)
        {
            if (state.Rows.Count == 0) return;

            state.Rows.Clear();
            state.VariablesScroll.Clear();
        }

        private static void RebuildRowsIfChanged(PanelState state, NodeGraph graph)
        {
            var usage = CollectUsage(graph);
            var same = usage.Count == state.Rows.Count;
            if (same)
            {
                for (var i = 0; i < usage.Count; i++)
                {
                    if (usage[i].Key == state.Rows[i].Variable && usage[i].Value.Count == state.Rows[i].Nodes.Count) continue;

                    same = false;
                    break;
                }
            }

            if (same) return;

            state.Rows.Clear();
            state.VariablesScroll.Clear();
            foreach (var pair in usage)
            {
                var row = CreateRow(pair.Key, pair.Value);
                state.Rows.Add(row);
                state.VariablesScroll.Add(row.Root);
            }
        }

        /// Every variable referenced by nodes of the graph with the nodes that use it, sorted by name.
        internal static List<KeyValuePair<VariableBaseSO, List<Node>>> CollectUsage(NodeGraph graph)
        {
            var map = new Dictionary<VariableBaseSO, List<Node>>();
            var buffer = new List<VariableBaseSO>();
            if (graph)
            {
                foreach (var node in graph.nodes)
                {
                    if (!node) continue;

                    buffer.Clear();
                    VariableAccess.CollectVariables(node, buffer);
                    foreach (var variable in buffer)
                    {
                        if (!map.TryGetValue(variable, out var nodes))
                        {
                            nodes = new List<Node>();
                            map[variable] = nodes;
                        }

                        nodes.Add(node);
                    }
                }
            }

            var result = new List<KeyValuePair<VariableBaseSO, List<Node>>>(map);
            result.Sort((a, b) => string.Compare(a.Key.name, b.Key.name, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        private static VariableRow CreateRow(VariableBaseSO variable, List<Node> nodes)
        {
            var row = new VariableRow { Variable = variable, Nodes = nodes };
            var valueType = VariableAccess.ValueTypeOf(variable);
            row.Kind = VariableAccess.Classify(valueType);
            row.Root = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column, flexShrink = 0, paddingLeft = 6, paddingRight = 6, paddingTop = 3, paddingBottom = 3,
                    borderBottomWidth = 1, borderBottomColor = new Color(0.1f, 0.1f, 0.1f, 0.6f),
                },
            };

            // Line 1: full name (middle ellipsis), usage count, type badge
            var nameLine = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, minHeight = 18 } };
            var path = AssetDatabase.GetAssetPath(variable);
            row.FullName = variable.name;
            row.NameLabel = new Label(variable.name)
            {
                tooltip =
                    $"{variable.name}\n{variable.GetType().Name}{(string.IsNullOrEmpty(path) ? string.Empty : $"\n{path}")}\nClick to ping the variable and select the {nodes.Count} node(s) using it.",
                style =
                {
                    flexGrow = 1, flexShrink = 1, minWidth = 0, overflow = Overflow.Hidden, whiteSpace = WhiteSpace.NoWrap,
                    unityTextAlign = TextAnchor.MiddleLeft, unityFontStyleAndWeight = FontStyle.Bold
                },
            };
            row.NameLabel.RegisterCallback<PointerDownEvent>(_ => SelectUsage(variable, nodes));
            row.NameLabel.RegisterCallback<GeometryChangedEvent>(_ => ApplyName(row));
            nameLine.Add(row.NameLabel);
            nameLine.Add(new Label($"×{nodes.Count}")
                { tooltip = $"Used by {nodes.Count} node(s) of this graph.", style = { flexShrink = 0, marginLeft = 4, opacity = 0.6f } });
            if (row.Kind is VariableAccess.ValueKind.UnityObject or VariableAccess.ValueKind.Complex && valueType != null)
            {
                nameLine.Add(CreateBadge(valueType.Name, valueType.FullName));
            }

            row.Root.Add(nameLine);

            // Line 2: the value, stretching the available width
            if (row.Kind == VariableAccess.ValueKind.UnityObject) row.Editor = CreateObjectField(row, valueType);
            else if (VariableAccess.IsEditable(variable)) row.Editor = CreateEditor(row, valueType);

            if (row.Editor == null)
            {
                var label = new Label
                {
                    style =
                    {
                        overflow = Overflow.Hidden, whiteSpace = WhiteSpace.NoWrap, textOverflow = TextOverflow.Ellipsis,
                        unityTextAlign = TextAnchor.MiddleRight
                    },
                };
                row.Editor = label;
                row.SetValue = value =>
                {
                    var text = VariableAccess.Format(value);
                    label.text = text;
                    label.tooltip = text;
                };
            }

            row.Editor.style.flexGrow = 1;
            row.Editor.style.flexShrink = 1;
            row.Editor.style.minWidth = 0;
            row.Editor.style.minHeight = 18;
            row.Root.Add(row.Editor);

            if (row.Kind is VariableAccess.ValueKind.UnityObject or VariableAccess.ValueKind.Complex) AddFoldout(row);

            return row;
        }

        private static Label CreateBadge(string text, string tooltip) => new(text)
        {
            tooltip = tooltip,
            style =
            {
                flexShrink = 0, marginLeft = 4, paddingLeft = 4, paddingRight = 4, fontSize = 10, borderTopLeftRadius = 6,
                borderTopRightRadius = 6,
                borderBottomLeftRadius = 6, borderBottomRightRadius = 6, backgroundColor = new Color(0.3f, 0.45f, 0.65f, 0.5f), maxWidth = 120,
                overflow = Overflow.Hidden, whiteSpace = WhiteSpace.NoWrap, textOverflow = TextOverflow.Ellipsis,
            },
        };

        /// Fits the name into the label width with a middle ellipsis (UI Toolkit only knows end ellipsis).
        private static void ApplyName(VariableRow row)
        {
            var width = row.NameLabel.resolvedStyle.width;
            if (float.IsNaN(width) || width <= 0f) return;

            var maxChars = Mathf.Max(6, (int) (width / 7f));
            if (maxChars == row.NameWidthChars) return;

            row.NameWidthChars = maxChars;
            row.NameLabel.text = DebuggerText.MiddleEllipsis(row.FullName, maxChars);
        }

        private static VisualElement CreateObjectField(VariableRow row, Type type)
        {
            var variable = row.Variable;
            var field = new ObjectField { objectType = type, allowSceneObjects = true };
            field.RegisterValueChangedCallback(evt =>
            {
                // Edits only change the runtime value while playing, otherwise the field snaps back
                if (Application.isPlaying && VariableAccess.TryWriteObject(variable, evt.newValue)) return;

                if (VariableAccess.TryRead(variable, out var current)) field.SetValueWithoutNotify(current as Object);
            });
            row.SetValue = value => field.SetValueWithoutNotify(value as Object);
            return field;
        }

        private static string FoldoutKey(VariableBaseSO variable) =>
            $"Xprees.GraphDebugger.VarExpanded.{AssetDatabase.GetAssetPath(variable)}.{variable.name}";

        private static void AddFoldout(VariableRow row)
        {
            row.Foldout = new Foldout
                { text = row.Kind == VariableAccess.ValueKind.UnityObject ? "Inspect" : "Contents (read-only)", style = { marginTop = 2 } };
            row.Foldout.SetValueWithoutNotify(SessionState.GetBool(FoldoutKey(row.Variable), false));
            row.FoldoutBody = new VisualElement { style = { flexShrink = 0 } };
            row.Foldout.Add(row.FoldoutBody);
            row.Foldout.RegisterValueChangedCallback(evt =>
            {
                SessionState.SetBool(FoldoutKey(row.Variable), evt.newValue);
                if (evt.newValue) EnsureBody(row);
            });
            row.Root.Add(row.Foldout);
        }

        /// Builds the inline inspector lazily (only while expanded) and rebuilds it when the referenced object changed. One level deep, nothing inside is expanded again.
        private static void EnsureBody(VariableRow row)
        {
            if (row.Foldout == null || !row.Foldout.value || !row.Variable) return;

            if (row.Kind == VariableAccess.ValueKind.UnityObject)
            {
                VariableAccess.TryRead(row.Variable, out var value);
                var target = value as Object;
                if (row.BodyBuilt && row.Inspected == target) return;

                row.BodyBuilt = true;
                row.Inspected = target;
                row.FoldoutBody.Clear();
                if (!target) row.FoldoutBody.Add(new Label("null (nothing to inspect)") { style = { opacity = 0.6f } });
                else if (target == row.Variable) row.FoldoutBody.Add(new Label("The variable itself.") { style = { opacity = 0.6f } });
                else row.FoldoutBody.Add(new InspectorElement(target));

                return;
            }

            if (row.BodyBuilt) return;

            row.BodyBuilt = true;
            row.FoldoutBody.Clear();
            var serialized = new SerializedObject(row.Variable);
            var property = serialized.FindProperty("currentValue");
            if (property == null)
            {
                row.FoldoutBody.Add(new Label("The value is not serializable, only its text is shown.")
                    { style = { opacity = 0.6f, whiteSpace = WhiteSpace.Normal } });
                return;
            }

            var propertyField = new PropertyField(property);
            propertyField.Bind(serialized);
            row.FoldoutBody.Add(propertyField);
            row.FoldoutBody.SetEnabled(false);
        }

        private static void SelectUsage(VariableBaseSO variable, List<Node> nodes)
        {
            EditorGUIUtility.PingObject(variable);
            var alive = nodes.FindAll(n => n);
            if (alive.Count == 0) return;

            Selection.objects = alive.ToArray();
            GraphBaseEditor.FocusNode(alive[0]);
            Selection.objects = alive.ToArray();
        }

        /// A field for the variable's value type, null when the type has no editor (the row falls back to a label).
        private static VisualElement CreateEditor(VariableRow row, Type type)
        {
            var variable = row.Variable;

            VisualElement Make<T>(BaseField<T> field)
            {
                field.RegisterValueChangedCallback(evt => VariableAccess.TryWrite(variable, evt.newValue));
                row.SetValue = value => field.SetValueWithoutNotify((T) value);
                return field;
            }

            if (type == typeof(bool)) return Make(new Toggle());
            if (type == typeof(int)) return Make(new IntegerField());
            if (type == typeof(long)) return Make(new LongField());
            if (type == typeof(float)) return Make(new FloatField());
            if (type == typeof(double)) return Make(new DoubleField());
            if (type == typeof(string)) return Make(new TextField());

            if (type != null && type.IsEnum)
            {
                var field = new EnumField((Enum) Enum.ToObject(type, 0));
                field.RegisterValueChangedCallback(evt => VariableAccess.TryWrite(variable, evt.newValue));
                row.SetValue = value => field.SetValueWithoutNotify((Enum) value);
                return field;
            }

            return null;
        }

        private static void UpdateRow(VariableRow row, bool playing, double now)
        {
            if (!row.Variable)
            {
                row.Root.SetEnabled(false);
                return;
            }

            // Labels and object fields stay enabled so the asset can still be pinged or opened outside Play Mode
            row.Editor.SetEnabled(playing || row.Editor is Label or ObjectField);
            if (row.Kind == VariableAccess.ValueKind.UnityObject && row.FoldoutBody != null) row.FoldoutBody.SetEnabled(playing);
            if (!VariableAccess.TryRead(row.Variable, out var value)) return;

            var text = ValueKey(row.Kind, value);
            if (text == row.LastText)
            {
                ApplyFlash(row, now);
                return;
            }

            EnsureBody(row);

            if (row.LastText != null) row.ChangedAt = now;
            row.LastText = text;

            // A field being typed into keeps the text of the user
            var focused = row.Editor.panel?.focusController?.focusedElement as VisualElement;
            var editing = focused != null && (focused == row.Editor || row.Editor.Contains(focused));
            if (!editing || row.Editor is Label) SafeSet(row, value);
            ApplyFlash(row, now);
        }

        /// Change key of a value: object references compare by identity (two assets can share a name), the rest by their text.
        private static string ValueKey(VariableAccess.ValueKind kind, object value)
        {
            if (kind != VariableAccess.ValueKind.UnityObject) return VariableAccess.Format(value);

            return value is Object obj && obj ? $"#{obj.GetInstanceID()}" : "null";
        }

        private static void SafeSet(VariableRow row, object value)
        {
            try
            {
                row.SetValue?.Invoke(value);
            }
            catch (InvalidCastException)
            {
                // A value of an unexpected type is shown by its text only
            }
        }

        private static void ApplyFlash(VariableRow row, double now)
        {
            var flash = LiveValues.FlashAmount(now, row.ChangedAt);
            if (flash > 0f) row.Root.style.backgroundColor = new Color(1f, 0.85f, 0.2f, flash * 0.3f);
            else row.Root.style.backgroundColor = StyleKeyword.Null;
        }

        private static void RefreshHistory(PanelState state, NodeGraph graph)
        {
            var tracer = GraphRuntimeTracer.Instance;
            var filter = GraphDebugRenderer.FilterFor(graph);
            RefreshRecords(state, tracer, graph);

            if (state.HistoryVersion == tracer.Version && state.HistoryGraph == graph && state.HistoryFilter == filter) return;

            state.HistoryVersion = tracer.Version;
            state.HistoryGraph = graph;
            state.HistoryFilter = filter;

            state.HistoryItems.Clear();
            state.HistoryItems.AddRange(DebuggerText.FilterHistory(tracer.History, graph, filter, maxHistoryRows));
            state.HistoryList.RefreshItems();
            if (state.FollowTail.value && state.HistoryItems.Count > 0) state.HistoryList.ScrollToItem(-1);
        }

        /// One fixed-height, single-line row: long text is cut with an ellipsis and the tooltip has all of it.
        private static Label CreateRowLabel(string text) => new(text)
        {
            tooltip = text,
            style =
            {
                height = rowHeight, minHeight = rowHeight, flexShrink = 0, paddingLeft = 6, paddingRight = 4, overflow = Overflow.Hidden,
                whiteSpace = WhiteSpace.NoWrap, textOverflow = TextOverflow.Ellipsis, unityTextAlign = TextAnchor.MiddleLeft,
            },
        };

        private static void RefreshRecords(PanelState state, GraphRuntimeTracer tracer, NodeGraph graph)
        {
            var signature = $"{tracer.Version}|{graph?.GetInstanceID()}";
            if (signature == state.RecordsSignature) return;

            state.RecordsSignature = signature;
            state.RecordsBox.Clear();
            var shown = 0;
            var hidden = 0;
            foreach (var record in tracer.Instances)
            {
                if (record.Graph != graph) continue;

                if (shown >= maxRecordRows)
                {
                    hidden++;
                    continue;
                }

                shown++;
                var captured = record;
                var active = captured.ActiveNode ? captured.ActiveNode.name : "-";
                var status = captured.IsAlive ? captured.IsPausedAtGate ? "paused" : "running" : $"ended ({captured.EndReason})";
                var text = $"{captured}  {status}  at {active}";
                var label = CreateRowLabel(text);
                label.RegisterCallback<PointerDownEvent>(_ => GraphDebugRenderer.ShowNode(captured, captured.ActiveNode));
                state.RecordsBox.Add(label);
            }

            if (hidden > 0) state.RecordsBox.Add(CreateRowLabel($"+{hidden} more instances (use the Instance filter)"));
            if (shown == 0) state.RecordsBox.Add(CreateRowLabel("No instances recorded for this graph."));
        }

        #endregion

    }
}
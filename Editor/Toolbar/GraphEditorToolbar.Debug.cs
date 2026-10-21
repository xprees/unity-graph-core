using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XNode;
using XNodeEditor;
using Xprees.Graph.Core.Editor.Debugger;
using Xprees.Graph.Core.Editor.Navigation;

namespace Xprees.Graph.Core.Editor.Toolbar
{
    /// Debugger controls of the toolbar: master toggle, instance filter, Follow, Continue/Step, Clear, Show trace and the trace menu.
    public static partial class GraphEditorToolbar
    {
        private const string debugToggleName = "xprees-graph-debug-toggle";
        private const string debugDetailsName = "xprees-graph-debug-details";
        private const string instanceButtonName = "xprees-graph-debug-instance";
        private const string followToggleName = "xprees-graph-debug-follow";
        private const string continueButtonName = "xprees-graph-debug-continue";
        private const string stepButtonName = "xprees-graph-debug-step";
        private const string showTraceToggleName = "xprees-graph-debug-showtrace";
        private const string statusLabelName = "xprees-graph-debug-status";
        private const string statusIconName = "xprees-graph-debug-status-icon";
        private const string instanceLabelName = "xprees-graph-debug-instance-label";
        private const string traceGroupName = "xprees-graph-debug-trace-group";
        private const string traceMenuButtonName = "xprees-graph-debug-trace-menu";
        private const string collapsibleTextClass = "xprees-toolbar-collapsible-text";
        private const string narrowClass = "xprees-toolbar-narrow";
        private const float statusMaxWidth = 240f;
        private const float instanceMaxWidth = 150f;
        private const float transportButtonSize = 24f;
        private const long debugRefreshIntervalMs = 100;

        private readonly static Color separatorColor = new(0.5f, 0.5f, 0.5f, 0.45f);

        private static VisualElement CreateDebugGroup(NodeEditorWindow window)
        {
            var group = new VisualElement
                { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, flexShrink = 1, minWidth = 0 } };

            var debugToggle = new ToolbarToggle
            {
                name = debugToggleName, text = "Debug",
                tooltip =
                    "Graph debugger on/off.\nOn: while the game runs, every graph records which nodes and edges were taken, and shows the active node, "
                    +
                    "live values and breakpoints.\nOff: nothing is recorded and there is no overhead."
            };
            debugToggle.RegisterValueChangedCallback(evt =>
            {
                GraphDebuggerSettings.Enabled = evt.newValue;
                UpdateDebugUi(group, window);
            });
            DebugIcons.Decorate(debugToggle, "DebuggerEnabled");
            group.Add(debugToggle);

            var details = new VisualElement
                { name = debugDetailsName, style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            var statusBox = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row, alignItems = Align.Center, marginLeft = 6, marginRight = 6, flexShrink = 1,
                    overflow = Overflow.Hidden, maxWidth = statusMaxWidth
                }
            };
            var pauseTexture = DebugIcons.Find("PauseButton");
            if (pauseTexture)
            {
                var pauseIcon = DebugIcons.CreateImage(pauseTexture);
                pauseIcon.name = statusIconName;
                pauseIcon.style.display = DisplayStyle.None;
                statusBox.Add(pauseIcon);
            }

            var status = new Label
            {
                name = statusLabelName,
                style =
                {
                    unityTextAlign = TextAnchor.MiddleLeft, flexShrink = 1, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis,
                    whiteSpace = WhiteSpace.NoWrap
                }
            };
            statusBox.Add(status);
            details.Add(statusBox);

            details.Add(CreateSeparator());

            var instanceButton = new ToolbarButton
            {
                name = instanceButtonName, tooltip =
                    "Instance filter.\nThe same graph can run several times at once (main flow, async starts, sub-graphs, dialogs, chats). " +
                    "Pick one to show only its run, or All.",
                style =
                {
                    flexDirection = FlexDirection.Row, alignItems = Align.Center, flexShrink = 1, maxWidth = instanceMaxWidth, minWidth = 0
                }
            };
            instanceButton.Add(new Label("Instance: All")
            {
                name = instanceLabelName, pickingMode = PickingMode.Ignore,
                style = { flexShrink = 1, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis, whiteSpace = WhiteSpace.NoWrap }
            });
            AddChevron(instanceButton, 2f);
            instanceButton.clicked += () => ShowInstanceMenu(window, instanceButton);
            details.Add(instanceButton);

            var follow = new ToolbarToggle
            {
                name = followToggleName, text = "Follow", tooltip =
                    "Follow the running flow.\nWhen a node is triggered (or a breakpoint hits), the view jumps to it and opens its graph. " +
                    "You can still move around freely in between."
            };
            follow.RegisterValueChangedCallback(evt => GraphDebuggerSettings.Follow = evt.newValue);
            if (DebugIcons.Decorate(follow, "ViewToolOrbit", "animationvisibilitytoggleon")) MarkCollapsibleText(follow);
            details.Add(follow);

            details.Add(CreateSeparator());

            // Transport group: icon-only square buttons side by side
            details.Add(CreateTransportButton(continueButtonName, "Continue", "PlayButton",
                "Continue: resume the flow that is stopped at a breakpoint and run until the next breakpoint.\nEnabled only while a flow is held.",
                () => ReleasePaused(window, false)));
            details.Add(CreateTransportButton(stepButtonName, "Step", "StepButton",
                "Step: run the node the flow is stopped at and pause again before the next node of the same flow.\nEnabled only while a flow is held.",
                () => ReleasePaused(window, true)));

            details.Add(CreateSeparator());
            details.Add(CreateTraceGroup());

            group.Add(details);
            return group;
        }

        /// Compound control: eye toggle (Show trace) plus a chevron that opens the trace menu.
        private static VisualElement CreateTraceGroup()
        {
            var compound = new VisualElement
            {
                name = traceGroupName,
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, flexShrink = 0 }
            };

            var showTrace = new ToolbarToggle
            {
                name = showTraceToggleName,
                tooltip =
                    "Show trace in Edit Mode (pressed = on).\nKeeps drawing the last recorded or loaded run (grayed-out unvisited nodes, taken edges) after Play Mode ends."
            };
            showTrace.RegisterValueChangedCallback(evt => GraphDebuggerSettings.ShowTraceInEditMode = evt.newValue);
            if (!DebugIcons.Decorate(showTrace, "animationvisibilitytoggleon", "scenevis_visible_hover")) showTrace.text = "Show trace";
            showTrace.style.marginRight = 0;
            compound.Add(showTrace);

            var menuButton = new ToolbarButton
            {
                name = traceMenuButtonName,
                tooltip =
                    "Trace menu: clear the trace, heatmap, coverage options, save / load a trace, load the last run.\nDialogs open in Temp/graph-debugger (emptied when Unity closes - save elsewhere to keep it).",
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginLeft = 0, paddingLeft = 2, paddingRight = 2 }
            };
            AddChevron(menuButton, 0f);
            menuButton.clicked += () => ShowTraceMenu(menuButton);
            compound.Add(menuButton);
            return compound;
        }

        private static ToolbarButton CreateTransportButton(
            string elementName,
            string fallbackText,
            string iconName,
            string tooltip,
            Action onClick
        )
        {
            var button = new ToolbarButton(onClick)
            {
                name = elementName, tooltip = tooltip,
                style =
                {
                    flexDirection = FlexDirection.Row, alignItems = Align.Center, justifyContent = Justify.Center,
                    width = transportButtonSize, minWidth = transportButtonSize, paddingLeft = 0, paddingRight = 0, marginLeft = 0,
                    marginRight = 0
                }
            };
            var texture = DebugIcons.Find(iconName);
            if (texture) button.Add(DebugIcons.CreateImage(texture, 0f));
            else button.Add(new Label(fallbackText) { pickingMode = PickingMode.Ignore });
            return button;
        }

        /// Dropdown arrow at the end of a button (a plain character when the icon is missing).
        private static void AddChevron(VisualElement button, float marginLeft)
        {
            var texture = DebugIcons.Find("icon dropdown", "Toolbar Minus");
            VisualElement chevron;
            if (texture)
            {
                chevron = DebugIcons.CreateImage(texture, 0f);
            }
            else
            {
                chevron = new Label("▾");
            }

            chevron.pickingMode = PickingMode.Ignore;
            chevron.style.marginLeft = marginLeft;
            button.Add(chevron);
        }

        private static VisualElement CreateSeparator() =>
            new()
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    width = 1, height = Height - 9f, marginLeft = 4, marginRight = 4, flexShrink = 0, backgroundColor = separatorColor
                }
            };

        /// Marks the text label of a decorated toggle so it hides when the window is narrow (the icon and tooltip stay).
        private static void MarkCollapsibleText(Toggle toggle) =>
            toggle.Q<Label>(className: Toggle.labelUssClassName)?.AddToClassList(collapsibleTextClass);

        private static void MarkCollapsibleText(Button button) => button.Q<Label>()?.AddToClassList(collapsibleTextClass);

        /// Hides or shows every collapsible text label under the toolbar for the given window width.
        private static void ApplyNarrowMode(VisualElement toolbar, bool narrow)
        {
            toolbar.EnableInClassList(narrowClass, narrow);
            var display = narrow ? DisplayStyle.None : DisplayStyle.Flex;
            toolbar.Query<Label>(className: collapsibleTextClass).ForEach(label => label.style.display = display);
        }

        private static void ScheduleDebugRefresh(VisualElement toolbar, NodeEditorWindow window)
        {
            var seenVersion = -1;
            var seenPlaying = false;
            toolbar.schedule.Execute(() =>
            {
                if (!window) return;

                UpdateDebugUi(toolbar, window);

                // xNode never repaints by itself: only while a run is shown and the recording changed
                var playing = Application.isPlaying;
                var version = GraphRuntimeTracer.Instance.Version;
                var changed = version != seenVersion || playing != seenPlaying;
                seenVersion = version;
                seenPlaying = playing;
                var live = GraphDebuggerSettings.Enabled && LiveValues.NeedsRepaint;
                if ((changed || live) && GraphDebuggerSettings.Enabled && (playing || GraphDebuggerSettings.ShowTraceInEditMode))
                    window.Repaint();
            }).Every(debugRefreshIntervalMs);
        }

        private static void UpdateDebugUi(VisualElement root, NodeEditorWindow window)
        {
            var toggle = root.Q<ToolbarToggle>(debugToggleName);
            var details = root.Q(debugDetailsName);
            if (toggle == null || details == null) return;

            var enabled = GraphDebuggerSettings.Enabled;
            toggle.SetValueWithoutNotify(enabled);
            details.style.display = enabled ? DisplayStyle.Flex : DisplayStyle.None;
            if (!enabled) return;

            root.Q<ToolbarToggle>(followToggleName).SetValueWithoutNotify(GraphDebuggerSettings.Follow);
            root.Q<ToolbarToggle>(showTraceToggleName).SetValueWithoutNotify(GraphDebuggerSettings.ShowTraceInEditMode);

            var tracer = GraphRuntimeTracer.Instance;
            var filter = GraphDebugRenderer.InstanceFilter;
            var instanceText = "Instance: All";
            if (filter.HasValue)
            {
                instanceText = tracer.TryGetRecord(filter.Value, out var filtered)
                    ? $"Instance: {filtered}"
                    : $"Instance: #{filter.Value} (gone)";
            }

            var instanceLabel = root.Q<Label>(instanceLabelName);
            if (instanceLabel.text != instanceText) instanceLabel.text = instanceText;

            var paused = FindPausedRecord(window, tracer);
            root.Q<ToolbarButton>(continueButtonName).SetEnabled(paused != null);
            root.Q<ToolbarButton>(stepButtonName).SetEnabled(paused != null);

            var status = root.Q<Label>(statusLabelName);
            var statusText = WithCoverage(BuildStatus(tracer, paused), window, tracer);
            if (status.text != statusText) status.text = statusText;
            if (status.tooltip != statusText) status.tooltip = statusText;
            var statusIcon = root.Q(statusIconName);
            if (statusIcon != null) statusIcon.style.display = paused != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static string BuildStatus(GraphRuntimeTracer tracer, ParserRecord paused)
        {
            if (paused != null) return $"Paused at {(paused.ActiveNode ? paused.ActiveNode.name : "?")} ({paused})";
            if (tracer.IsLoadedTrace) return $"Trace: {tracer.TraceSourceName} (read-only)";
            if (!Application.isPlaying) return tracer.HasTrace ? "Trace: last run" : string.Empty;

            var running = 0;
            foreach (var record in tracer.Instances)
            {
                if (record.IsAlive) running++;
            }

            return running > 0 ? $"{running} running" : string.Empty;
        }

        /// The record Continue/Step act on: the filtered one, else one of this graph, else any held record.
        private static ParserRecord FindPausedRecord(NodeEditorWindow window, GraphRuntimeTracer tracer)
        {
            var filter = GraphDebugRenderer.InstanceFilter;
            ParserRecord any = null;
            foreach (var record in tracer.Instances)
            {
                if (!record.IsPausedAtGate) continue;
                if (filter.HasValue && record.Id == filter.Value) return record;
                if (window && record.Graph == window.graph) return record;
                any ??= record;
            }

            return filter.HasValue ? null : any;
        }

        private static void ReleasePaused(NodeEditorWindow window, bool step)
        {
            var record = FindPausedRecord(window, GraphRuntimeTracer.Instance);
            if (record == null) return;

            if (step) GraphDebugGate.Step(record.Id);
            else GraphDebugGate.Continue(record.Id);
        }

        private static void ShowInstanceMenu(NodeEditorWindow window, VisualElement anchor)
        {
            var tracer = GraphRuntimeTracer.Instance;
            var current = GraphDebugRenderer.InstanceFilter;
            var graphs = CollectGraphs(window ? window.graph : null);

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("All instances"), !current.HasValue, () => GraphDebugRenderer.InstanceFilter = null);
            foreach (var record in tracer.Instances.Where(r => graphs.Contains(r.Graph)).OrderBy(r => r.Id))
            {
                var id = record.Id;
                var label = record.IsAlive ? record.ToString() : $"{record} (ended)";
                menu.AddItem(new GUIContent(label), current == id, () => GraphDebugRenderer.InstanceFilter = id);
            }

            menu.DropDown(anchor.worldBound);
        }

        /// The graph and every graph it references (sub-graphs, dialogs), cycle safe.
        private static HashSet<NodeGraph> CollectGraphs(NodeGraph root)
        {
            var result = new HashSet<NodeGraph>();
            if (!root) return result;

            var queue = new Queue<NodeGraph>();
            queue.Enqueue(root);
            result.Add(root);
            while (queue.Count > 0)
            {
                var graph = queue.Dequeue();
                foreach (var node in graph.nodes)
                {
                    if (!node) continue;

                    foreach (var child in GraphReferences.GetReferencedGraphs(node))
                    {
                        if (child && result.Add(child)) queue.Enqueue(child);
                    }
                }
            }

            return result;
        }

        private static void ShowTraceMenu(VisualElement anchor)
        {
            var tracer = GraphRuntimeTracer.Instance;
            var state = new TraceMenuState(tracer.HasTrace, Application.isPlaying, File.Exists(GraphTraceFile.LastRunPath),
                GraphDebuggerSettings.Heatmap, GraphDebuggerSettings.HeatmapLogScale, GraphDebuggerSettings.ShowCumulativeCoverage);

            var menu = new GenericMenu();
            foreach (var entry in ToolbarLogic.BuildTraceMenu(state))
            {
                var content = new GUIContent(entry.Path);
                var action = entry.Action;
                if (entry.IsSeparator) menu.AddSeparator(entry.Path);
                else if (entry.Enabled) menu.AddItem(content, entry.Checked, () => RunTraceMenuAction(action, tracer));
                else menu.AddDisabledItem(content, entry.Checked);
            }

            menu.DropDown(anchor.worldBound);
        }

        private static void RunTraceMenuAction(TraceMenuAction action, GraphRuntimeTracer tracer)
        {
            switch (action)
            {
                case TraceMenuAction.ClearTrace:
                    tracer.Clear();
                    GraphDebugRenderer.RepaintAll();
                    break;
                case TraceMenuAction.ToggleHeatmap:
                    GraphDebuggerSettings.Heatmap = !GraphDebuggerSettings.Heatmap;
                    break;
                case TraceMenuAction.ToggleHeatmapLogScale:
                    GraphDebuggerSettings.HeatmapLogScale = !GraphDebuggerSettings.HeatmapLogScale;
                    break;
                case TraceMenuAction.ToggleCumulativeCoverage:
                    GraphDebuggerSettings.ShowCumulativeCoverage = !GraphDebuggerSettings.ShowCumulativeCoverage;
                    break;
                case TraceMenuAction.ResetCoverage:
                    GraphCoverageService.Reset();
                    GraphDebugRenderer.RepaintAll();
                    break;
                case TraceMenuAction.SaveTraceAs:
                    SaveTraceAs(tracer);
                    break;
                case TraceMenuAction.LoadTrace:
                    LoadTraceFromPicker(tracer);
                    break;
                case TraceMenuAction.LoadLastRun:
                    LoadTrace(tracer, GraphTraceFile.LastRunPath);
                    break;
            }
        }

        private static void SaveTraceAs(GraphRuntimeTracer tracer)
        {
            var directory = GraphTraceFile.PickerDirectory;
            var defaultName = string.IsNullOrEmpty(tracer.RunName) ? "trace" : tracer.RunName;
            var path = EditorUtility.SaveFilePanel("Save graph trace", directory, defaultName, GraphTraceFile.Extension);
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                GraphTraceFile.Save(tracer, path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Graph debugger: could not save the trace: {exception.Message}");
            }
        }

        private static void LoadTraceFromPicker(GraphRuntimeTracer tracer)
        {
            var directory = GraphTraceFile.PickerDirectory;
            var path = EditorUtility.OpenFilePanel("Load graph trace", directory, GraphTraceFile.Extension);
            if (!string.IsNullOrEmpty(path)) LoadTrace(tracer, path);
        }

        private static void LoadTrace(GraphRuntimeTracer tracer, string path)
        {
            if (!GraphTraceFile.Load(tracer, path)) return;

            // The loaded run is drawn in Edit Mode, that is what it was loaded for
            GraphDebuggerSettings.ShowTraceInEditMode = true;
            GraphDebugRenderer.RepaintAll();
        }
    }
}
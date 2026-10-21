using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XNodeEditor;
using Xprees.Graph.Core.Base;
using Xprees.Graph.Core.Debugging;
using Xprees.Graph.Core.Editor.Debugger;
using Xprees.Graph.Core.Editor.Graph;

namespace Xprees.Graph.Core.Editor.Nodes
{
    /// Live variable values under the node fields and the runtime-state line under the node title.
    public partial class BaseNodeEditor
    {
        private readonly static string[] bodyExcludes = { "m_Script", "graph", "position", "ports" };
        private readonly static Color liveValueColor = new(0.55f, 0.85f, 1f);
        private readonly static Color liveFlashColor = new(1f, 0.85f, 0.2f);

        private static GUIStyle liveValueStyle;
        private static GUIStyle stateLineStyle;

        private string _stateLine;
        private bool _lodCollapsed;

        /// Decided on the Layout event only, so Layout and Repaint of one pass always agree (a different control count breaks IMGUI layout).
        private bool IsLodCollapsed()
        {
            if (Event.current.type == EventType.Layout)
            {
                _lodCollapsed = SupportsDebugTint && window &&
                                GraphLod.ShouldCollapse(GraphLod.Enabled, window.zoom, GraphLod.Threshold, Selection.Contains(target));
            }

            return _lodCollapsed;
        }

        /// Card body of the zoomed-out level of detail: only the ports, so edges still attach at the right places.
        private void DrawCollapsedBody()
        {
            foreach (var port in target.Ports)
            {
                if (NodeEditorGUILayout.IsDynamicPortListPort(port)) continue;

                NodeEditorGUILayout.PortField(port);
            }
        }

        /// Copy of xNode's body loop (NodeEditor.OnBodyGUI) that can draw a live value row after a field. ReferenceDrawerBase stays untouched.
        private void DrawBodyFields()
        {
#if ODIN_INSPECTOR
            base.OnBodyGUI();
#else
            serializedObject.Update();

            var live = LiveValues.IsActive;
            var iterator = serializedObject.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (bodyExcludes.Contains(iterator.name)) continue;

                NodeEditorGUILayout.PropertyField(iterator, true);
                if (live) DrawLiveValue(iterator);
            }

            // Dynamic ports in the order in which they are serialized
            foreach (var dynamicPort in target.DynamicPorts)
            {
                if (NodeEditorGUILayout.IsDynamicPortListPort(dynamicPort)) continue;

                NodeEditorGUILayout.PortField(dynamicPort);
            }

            serializedObject.ApplyModifiedProperties();
#endif
        }

        /// A compact value row under a variable field or under a reference that points to a variable (not under inlined references).
        private void DrawLiveValue(SerializedProperty property)
        {
            if (property.depth != 0) return;

            using var marker = GraphDebugMarkers.LiveValue.Auto();
            var field = VariableAccess.FindVariableField(target.GetType(), property.name);
            if (field == null || VariableAccess.IsCollectionField(field)) return;

            var variable = VariableAccess.ResolveVariable(field.GetValue(target));
            if (!variable) return;

            var text = LiveValues.Sample(variable, out var flash);
            liveValueStyle ??= new GUIStyle(EditorStyles.miniLabel)
            {
                clipping = TextClipping.Clip,
                padding = new RectOffset(6, 4, 0, 0),
                normal = { textColor = liveValueColor },
            };

            var rect = GUILayoutUtility.GetRect(GUIContent.none, liveValueStyle, GUILayout.Height(14f));
            if (Event.current.type != EventType.Repaint) return;

            if (flash > 0f)
            {
                var color = liveFlashColor;
                color.a = flash * 0.4f;
                EditorGUI.DrawRect(rect, color);
                NodeEditorWindow.current?.Repaint();
            }

            GUI.Label(rect, $"▶ {text ?? "?"}", liveValueStyle);
        }

        /// Phase, time in node, visits, last port and the node's own state text. The text is rebuilt on Layout only, so the layout never changes between events.
        private void DrawRuntimeStateLine()
        {
            if (!SupportsDebugTint) return;

            if (Event.current.type == EventType.Layout) _stateLine = BuildRuntimeStateLine();
            if (string.IsNullOrEmpty(_stateLine)) return;

            stateLineStyle ??= new GUIStyle(EditorStyles.miniLabel)
            {
                clipping = TextClipping.Clip,
                padding = new RectOffset(6, 4, 0, 2),
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) },
            };
            GUILayout.Label(_stateLine, stateLineStyle);
        }

        private string BuildRuntimeStateLine()
        {
            if (!GraphDebugRenderer.IsActive) return null;

            using var marker = GraphDebugMarkers.StateLine.Auto();
            var tracer = GraphRuntimeTracer.Instance;
            var trace = tracer.GetNode(target);
            if (trace.VisitCount <= 0 && trace.ActiveIn.Count == 0) return null;

            var filter = GraphDebugRenderer.FilterFor(target.graph);
            ParserRecord active = null;
            foreach (var record in trace.ActiveIn)
            {
                if (!record.IsAlive || filter.HasValue && record.Id != filter.Value) continue;

                active = record;
                break;
            }

            string provider = null;
            if (active != null && target is IGraphNodeDebugState debugState && LiveValues.IsActive)
            {
                GraphParserBase parser = null;
                active.Parser?.TryGetTarget(out parser);
                try
                {
                    provider = debugState.GetDebugState(new NodeDebugContext(parser, trace.LastEnteredTime, trace.VisitCount, trace.LastPort));
                }
                catch (Exception)
                {
                    provider = null;
                }
            }

            if (active != null) LiveValues.Touch();
            return DebuggerText.NodeState(active?.Phase, tracer.ReferenceTime, trace.LastEnteredTime, trace.VisitCount,
                trace.LastPort?.fieldName, provider);
        }
    }
}
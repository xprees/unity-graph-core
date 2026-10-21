using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XNode;
using XNodeEditor;
using Xprees.Graph.Core.Editor.Debugger;

namespace Xprees.Graph.Core.Editor.Nodes
{
    /// Debugger visuals of a node: dimming of nodes the run did not reach, desaturation of disabled ones and the breakpoint dot.
    public partial class BaseNodeEditor
    {
        private const float headerHitHeight = 30f;
        private const float dimmedAlpha = 0.45f;
        private const string breakpointMenuRoot = "Debug/";

        private readonly static Color breakpointColor = new(0.9f, 0.15f, 0.15f);
        private readonly static Color breakpointDisabledColor = new(0.55f, 0.55f, 0.55f);

        private bool _debugTinted;
        private Color _debugRestoreColor;

        /// Notes and other custom bodies that skip the base body GUI return false, so GUI.color is never left dimmed.
        protected virtual bool SupportsDebugTint => true;

        private NodeVisualState DebugState => SupportsDebugTint ? GraphDebugRenderer.GetState(target) : NodeVisualState.Idle;

        public override Color GetTint()
        {
            var tint = base.GetTint();
            switch (DebugState)
            {
                case NodeVisualState.NotVisited:
                    tint.a *= dimmedAlpha;
                    break;
                case NodeVisualState.Disabled:
                    var gray = tint.grayscale;
                    tint = Color.Lerp(tint, new Color(gray, gray, gray, tint.a), 0.8f);
                    break;
            }

            var heat = SupportsDebugTint ? GraphDebugRenderer.GetHeat(target) : -1f;
            if (heat >= 0f) tint = Color.Lerp(tint, GraphHeatmap.Evaluate(heat), 0.75f);

            return tint;
        }

        /// Dims the node content. Restored by <see cref="EndDebugVisuals"/> at the end of the body.
        private void BeginDebugVisuals()
        {
            _debugTinted = false;
            if (Event.current.type != EventType.Repaint) return;

            var state = DebugState;
            if (state != NodeVisualState.NotVisited && state != NodeVisualState.Disabled) return;

            _debugRestoreColor = GUI.color;
            var color = _debugRestoreColor;
            color.a *= state == NodeVisualState.NotVisited ? dimmedAlpha : 0.7f;
            GUI.color = color;
            _debugTinted = true;
        }

        private void EndDebugVisuals()
        {
            if (!_debugTinted) return;

            GUI.color = _debugRestoreColor;
            _debugTinted = false;
        }

        /// Draws the dot only. The click is handled by GraphBaseEditor (it sees the raw mouse events after xNode's controls and does not
        /// depend on this node's layout, the header label or its description tooltip).
        private void DrawBreakpointDot()
        {
            var e = Event.current;
            if (e.type != EventType.Repaint) return;

            var width = GetWidth();
            var zoom = window ? window.zoom : 1f;
            var hit = BreakpointDotLayout.LocalHit(width, zoom);
            var overDot = hit.Contains(e.mousePosition);
            var overHeader = new Rect(0f, 0f, width, headerHitHeight).Contains(e.mousePosition);
            var has = GraphBreakpoints.Has(target);
            if (!has && !overHeader && !GraphDebuggerSettings.Enabled) return;

            var dot = BreakpointDotLayout.LocalDot(width, zoom);
            var enabled = GraphBreakpoints.IsEnabled(target);
            var previous = GUI.color;

            if (overDot)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.2f);
                GUI.DrawTexture(hit, NodeEditorResources.dot);
            }

            if (has && enabled)
            {
                GUI.color = breakpointColor;
                GUI.DrawTexture(dot, NodeEditorResources.dot);
                GUI.color = overDot ? Color.white : new Color(1f, 1f, 1f, 0.7f);
                GUI.DrawTexture(dot, NodeEditorResources.dotOuter);
            }
            else if (has)
            {
                // Disabled: hollow grey ring with a dash
                GUI.color = breakpointDisabledColor;
                GUI.DrawTexture(dot, NodeEditorResources.dotOuter);
                var dash = new Rect(dot.center.x - dot.width * 0.3f, dot.center.y - 1f, dot.width * 0.6f, 2f);
                GUI.DrawTexture(dash, Texture2D.whiteTexture);
            }
            else
            {
                // Empty ring: faint while the debugger is on, stronger and red-tinted under the mouse
                GUI.color = overDot
                    ? new Color(breakpointColor.r, breakpointColor.g, breakpointColor.b, 0.95f)
                    : new Color(0.85f, 0.85f, 0.85f, GraphDebuggerSettings.Enabled ? 0.55f : 0.3f);
                GUI.DrawTexture(dot, NodeEditorResources.dotOuter);
            }

            GUI.color = previous;
            if (overDot) DrawBreakpointHint(hit);
        }

        /// Tooltip-like box under the dot. Says what a click does and, when something blocks the breakpoint, why.
        private void DrawBreakpointHint(Rect hit)
        {
            var text = "Click to toggle breakpoint (Alt, Ctrl or Shift+click: enable/disable, right-click: menu)";
            var why = BreakpointDiagnosis.HoverHint(target);
            if (why != null) text += Environment.NewLine + why;

            var content = new GUIContent(text);
            var style = EditorStyles.helpBox;
            var size = style.CalcSize(content);
            var rect = new Rect(hit.xMax - size.x, hit.yMax + 2f, size.x, size.y);
            GUI.Box(rect, content, style);
        }

        /// Top-level breakpoint entries at the top of the node menu. They act on every selected node, like F9.
        private void AddTopLevelBreakpointItems(GenericMenu menu)
        {
            var nodes = Selection.GetFiltered<Node>(SelectionMode.Unfiltered);
            if (nodes.Length == 0 || Array.IndexOf(nodes, target) < 0) nodes = new Node[] { target };

            var anySet = false;
            var anyEnabled = false;
            foreach (var node in nodes)
            {
                if (!node || !GraphBreakpoints.Has(node)) continue;

                anySet = true;
                anyEnabled |= GraphBreakpoints.IsEnabled(node);
            }

            menu.AddItem(new GUIContent("Toggle Breakpoint (F9)"), anySet, () => GraphDebugShortcuts.ToggleOn(nodes));
            if (!anySet) return;

            var enable = !anyEnabled;
            menu.AddItem(new GUIContent(enable ? "Enable Breakpoint" : "Disable Breakpoint"), false, () =>
            {
                foreach (var node in nodes)
                {
                    if (node && GraphBreakpoints.Has(node)) GraphBreakpoints.SetEnabled(node, enable);
                }
            });
            menu.AddItem(new GUIContent("Remove Breakpoint"), false, () =>
            {
                foreach (var node in nodes) GraphBreakpoints.Remove(node);
            });
            menu.AddItem(new GUIContent("Remove All Breakpoints in Graph"), false, () =>
            {
                foreach (var node in Enumerable.ToList(GraphBreakpoints.In(target.graph))) GraphBreakpoints.Remove(node);
            });
            menu.AddSeparator(string.Empty);
        }

        private void AddDebugContextMenuItems(GenericMenu menu)
        {
            var node = target;
            GraphNodeDebugMenu.AddItems(menu, node);
            menu.AddItem(new GUIContent(breakpointMenuRoot + "Toggle Breakpoint"), GraphBreakpoints.Has(node),
                () => GraphBreakpoints.Toggle(node));
            menu.AddItem(new GUIContent(breakpointMenuRoot + "Test breakpoint (dry run)"), false,
                () => UnityEngine.Debug.Log(BreakpointDiagnosis.Report(node), node));
            if (!GraphBreakpoints.Has(node)) return;

            var enabled = GraphBreakpoints.IsEnabled(node);
            menu.AddItem(new GUIContent(breakpointMenuRoot + (enabled ? "Disable Breakpoint" : "Enable Breakpoint")), false,
                () => GraphBreakpoints.SetEnabled(node, !enabled));
        }
    }
}
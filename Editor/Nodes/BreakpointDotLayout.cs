using UnityEngine;

namespace Xprees.Graph.Core.Editor.Nodes
{
    /// Pure geometry of the breakpoint dot in a node header. The hit area is generous and lives inside xNode's 30 px title band.
    /// Local space = node space (origin at the node's top left, unzoomed), window space = the raw window coordinates xNode's controls use.
    /// Zoom follows xNode: a node is drawn local / zoom pixels big, so zoom above 1 is zoomed out. The dot keeps a minimum on-screen size
    /// when zoomed out, limited by the title band.
    public static class BreakpointDotLayout
    {
        public const float HeaderBand = 30f;
        public const float HitSize = 28f;
        public const float DotSize = 18f;
        public const float HitMargin = 4f;
        public const float HitTop = 1f;
        public const float DragTolerance = 5f;
        public const float MinScreenHit = 14f;
        public const float MinScreenDot = 8f;

        /// Space at the right end of the header the title text must not use.
        public const float ReservedHeaderWidth = HitSize + HitMargin + 2f;

        public static Rect LocalHit(float nodeWidth, float zoom = 1f)
        {
            var size = Mathf.Min(Mathf.Max(HitSize, MinScreenHit * Mathf.Max(zoom, 0.01f)), HeaderBand - HitTop);
            return new Rect(nodeWidth - size - HitMargin, HitTop, size, size);
        }

        public static Rect LocalDot(float nodeWidth, float zoom = 1f)
        {
            var hit = LocalHit(nodeWidth, zoom);
            var size = Mathf.Min(Mathf.Max(DotSize, MinScreenDot * Mathf.Max(zoom, 0.01f)), hit.height - 2f);
            return new Rect(hit.center.x - size * 0.5f, hit.center.y - size * 0.5f, size, size);
        }

        /// Hit area of a node in window space. nodeWindowPosition is the window position of the node's top left corner.
        public static Rect WindowHit(Vector2 nodeWindowPosition, float nodeWidth, float zoom)
        {
            var local = LocalHit(nodeWidth, zoom);
            return new Rect(nodeWindowPosition + local.position / zoom, local.size / zoom);
        }

        /// A click counts only when the mouse barely moved between press and release (a drag moves the node).
        public static bool IsClick(Vector2 pressed, Vector2 released) => (released - pressed).sqrMagnitude <= DragTolerance * DragTolerance;

        /// Short status text shown after a click. Mentions the debugger when it is off, the breakpoint cannot fire then.
        public static string FeedbackMessage(DotClickAction action, bool enabledAfter, string nodeName, bool debuggerOn)
        {
            var text = action switch
            {
                DotClickAction.Add => $"Breakpoint set on {nodeName}",
                DotClickAction.Remove => $"Breakpoint removed from {nodeName}",
                _ => enabledAfter ? $"Breakpoint enabled on {nodeName}" : $"Breakpoint disabled on {nodeName}",
            };
            if (action != DotClickAction.Remove && enabledAfter && !debuggerOn) text += " (debugger is off - turn on Debug in the toolbar)";
            return text;
        }

        /// What a left click on the dot does. Alt or Ctrl flips enabled/disabled without removing the breakpoint.
        public static DotClickAction ResolveClick(bool hasBreakpoint, bool altOrControl)
        {
            if (!hasBreakpoint) return DotClickAction.Add;
            return altOrControl ? DotClickAction.ToggleEnabled : DotClickAction.Remove;
        }
    }

    public enum DotClickAction
    {
        Add,
        Remove,
        ToggleEnabled,
    }
}
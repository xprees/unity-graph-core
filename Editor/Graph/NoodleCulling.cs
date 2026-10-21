using UnityEngine;

namespace Xprees.Graph.Core.Editor.Graph
{
    /// xNode draws every connection of the graph, also the ones far outside the view, and a curvy noodle is one draw call per few pixels of length
    /// (Handles.DrawAAPolyLine per segment). A connection that cannot reach the view is drawn as a straight line instead, which is invisible anyway.
    public static class NoodleCulling
    {
        private const float margin = 24f;

        /// Pure: true when a curvy noodle between two grid points (no reroutes) cannot touch the view rect (grid space).
        /// xNode's tangents are horizontal with half the distance, so the curve stays inside the endpoints' box grown by that horizontally.
        public static bool CanSkipCurve(Rect viewGrid, Vector2 from, Vector2 to)
        {
            var reach = Vector2.Distance(from, to) * 0.5f;
            var xMin = Mathf.Min(from.x, to.x) - reach - margin;
            var xMax = Mathf.Max(from.x, to.x) + reach + margin;
            var yMin = Mathf.Min(from.y, to.y) - margin;
            var yMax = Mathf.Max(from.y, to.y) + margin;
            return xMax < viewGrid.xMin || xMin > viewGrid.xMax || yMax < viewGrid.yMin || yMin > viewGrid.yMax;
        }
    }
}
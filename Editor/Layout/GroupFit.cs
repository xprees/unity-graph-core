using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Xprees.Graph.Core.Editor.Layout
{
    /// Group rects around their contents. Rects are the full drawn group (title + body + bottom bar).
    public static class GroupFit
    {
        /// The drawn rect of a group that holds <paramref name="children"/> with padding around them and the title above.
        /// Keeps the group editor's minimal size. Returns null when there are no children.
        public static Rect? Fit(IEnumerable<Rect> children, float chrome = LayoutGeometry.GroupChrome)
        {
            var list = children?.ToList();
            if (list == null || list.Count == 0) return null;

            var bounds = LayoutGeometry.Bounds(list);
            var xMin = bounds.xMin - LayoutGeometry.GroupPadding;
            var yMin = bounds.yMin - LayoutGeometry.GroupPadding - LayoutGeometry.GroupTitle;
            var width = Mathf.Max(LayoutGeometry.GroupMinWidth, Mathf.Ceil(bounds.xMax + LayoutGeometry.GroupPadding - xMin));
            var height = Mathf.Max(LayoutGeometry.GroupMinBodyHeight + chrome, Mathf.Ceil(bounds.yMax + LayoutGeometry.GroupPadding - yMin));
            return new Rect(xMin, yMin, width, height);
        }

        /// The current rect grown (never shrunk) so it also holds the fitted rect.
        public static Rect Grow(Rect current, Rect? fitted) => fitted.HasValue ? LayoutGeometry.Union(current, fitted.Value) : current;
    }
}
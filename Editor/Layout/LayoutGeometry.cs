using System.Collections.Generic;
using UnityEngine;

namespace Xprees.Graph.Core.Editor.Layout
{
    /// Constants and rect math shared by the graph layout commands. Positions are in xNode grid space (y grows down).
    public static class LayoutGeometry
    {
        /// Horizontal gap between flow columns.
        public const float GapH = 80f;

        /// Vertical gap between nodes of a column and between lanes.
        public const float GapV = 40f;

        /// xNode snaps dragged nodes to (k * 16 - 8), arranged nodes use the same lattice.
        public const float Grid = 16f;

        private const float gridOffset = 8f;

        /// Space kept between a shifted block and the nodes it had to avoid.
        public const float Clearance = 16f;

        /// Space between a group's border and its contents.
        public const float GroupPadding = 32f;

        /// Height of the group title band above the body.
        public const float GroupTitle = 32f;

        /// Full drawn group height minus its body height (title + bottom bar). Measured: body 686 -> drawn 736.
        public const float GroupChrome = 50f;

        /// <see cref="Xprees.Graph.Core.Base.Nodes.NodeGroup.GetNodes"/> accepts nodes whose top lies up to 30 px below the body.
        public const float GroupMemberSlack = 30f;

        /// The minimal group body size the group editor allows when resizing by hand.
        public const float GroupMinWidth = 200f;

        public const float GroupMinBodyHeight = 100f;

        /// Snaps a coordinate to xNode's drag lattice (k * 16 - 8).
        public static float Snap(float value) => Mathf.Round((value + gridOffset) / Grid) * Grid - gridOffset;

        public static Vector2 Snap(Vector2 value) => new(Snap(value.x), Snap(value.y));

        /// The first lattice coordinate at or after the value.
        public static float SnapUp(float value) => Mathf.Ceil((value + gridOffset) / Grid - 0.0001f) * Grid - gridOffset;

        /// Top-left where the contents of a group start (inside padding, below the title), on the lattice.
        public static Vector2 GroupContentOrigin(Rect group) =>
            new(SnapUp(group.xMin + GroupPadding), SnapUp(group.yMin + GroupTitle + GroupPadding));

        /// Rounds a relative distance up to a whole number of grid steps, so snapped positions stay on the lattice.
        public static float CeilToGrid(float value) => Mathf.Ceil(value / Grid - 0.0001f) * Grid;

        /// Rounds a relative distance to the nearest whole number of grid steps.
        public static float RoundToGrid(float value) => Mathf.Round(value / Grid) * Grid;

        /// Rounds an offset away from zero to whole grid steps (keeps the clearance it was computed with).
        public static float OutwardToGrid(float value) => value >= 0 ? CeilToGrid(value) : -CeilToGrid(-value);

        /// True when the rects share area (touching edges do not overlap).
        public static bool Overlaps(Rect a, Rect b) => a.xMin < b.xMax && b.xMin < a.xMax && a.yMin < b.yMax && b.yMin < a.yMax;

        /// Bounds of all rects, or an empty rect at zero when there are none.
        public static Rect Bounds(IEnumerable<Rect> rects)
        {
            var any = false;
            var bounds = new Rect();
            foreach (var rect in rects)
            {
                bounds = any ? Union(bounds, rect) : rect;
                any = true;
            }

            return bounds;
        }

        public static Rect Union(Rect a, Rect b) =>
            Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
    }
}
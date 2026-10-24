using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Xprees.Graph.Core.Editor.Layout
{
    /// Finds the smallest shift of a block of rects so that none of them overlaps a fixed rect. Moving right / down is preferred,
    /// left / up costs more. Offsets are whole grid steps, so snapped positions stay snapped.
    public static class FreeSpotFinder
    {
        /// Extra cost factor of moving left or up.
        private const float backwardPenalty = 1.5f;

        public static Vector2 FindOffset(IReadOnlyList<Rect> block, IReadOnlyList<Rect> obstacles, float clearance = LayoutGeometry.Clearance)
        {
            if (block == null || block.Count == 0 || obstacles == null || obstacles.Count == 0) return Vector2.zero;
            if (IsFree(block, obstacles, Vector2.zero)) return Vector2.zero;

            var bounds = LayoutGeometry.Bounds(block);
            var xs = new HashSet<float> { 0f };
            var ys = new HashSet<float> { 0f };
            foreach (var obstacle in obstacles)
            {
                xs.Add(LayoutGeometry.OutwardToGrid(obstacle.xMax + clearance - bounds.xMin));
                xs.Add(LayoutGeometry.OutwardToGrid(obstacle.xMin - clearance - bounds.xMax));
                ys.Add(LayoutGeometry.OutwardToGrid(obstacle.yMax + clearance - bounds.yMin));
                ys.Add(LayoutGeometry.OutwardToGrid(obstacle.yMin - clearance - bounds.yMax));
            }

            var candidates = new List<(Vector2 offset, float cost)>(xs.Count * ys.Count);
            foreach (var x in xs)
            {
                foreach (var y in ys)
                {
                    if (x == 0f && y == 0f) continue;

                    var wx = x < 0 ? -x * backwardPenalty : x;
                    var wy = y < 0 ? -y * backwardPenalty : y;
                    candidates.Add((new Vector2(x, y), wx * wx + wy * wy));
                }
            }

            // Ties: prefer right over down, then smaller vertical moves
            candidates.Sort((a, b) =>
            {
                var byCost = a.cost.CompareTo(b.cost);
                if (byCost != 0) return byCost;

                var byY = Mathf.Abs(a.offset.y).CompareTo(Mathf.Abs(b.offset.y));
                return byY != 0 ? byY : b.offset.x.CompareTo(a.offset.x);
            });

            foreach (var candidate in candidates)
            {
                if (IsFree(block, obstacles, candidate.offset)) return candidate.offset;
            }

            // Unreachable: right of every obstacle is always free
            return new Vector2(LayoutGeometry.OutwardToGrid(obstacles.Max(o => o.xMax) + clearance - bounds.xMin), 0f);
        }

        public static bool IsFree(IReadOnlyList<Rect> block, IReadOnlyList<Rect> obstacles, Vector2 offset)
        {
            var bounds = LayoutGeometry.Bounds(block);
            bounds.position += offset;
            foreach (var obstacle in obstacles)
            {
                if (!LayoutGeometry.Overlaps(bounds, obstacle)) continue;

                foreach (var rect in block)
                {
                    var moved = rect;
                    moved.position += offset;
                    if (LayoutGeometry.Overlaps(moved, obstacle)) return false;
                }
            }

            return true;
        }
    }
}
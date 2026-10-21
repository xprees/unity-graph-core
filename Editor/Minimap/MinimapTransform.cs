using System.Collections.Generic;
using UnityEngine;

namespace Xprees.Graph.Core.Editor.Minimap
{
    /// Pure grid <-> minimap math. The world bounds are fitted into the minimap (uniform scale, centered, with padding).
    public readonly struct MinimapTransform
    {
        public readonly Rect World;
        public readonly Vector2 MapSize;
        public readonly float Scale;
        private readonly Vector2 _offset;

        public MinimapTransform(Rect world, Vector2 mapSize, float padding)
        {
            World = world;
            MapSize = mapSize;

            var inner = new Vector2(Mathf.Max(1f, mapSize.x - padding * 2f), Mathf.Max(1f, mapSize.y - padding * 2f));
            var w = Mathf.Max(1f, world.width);
            var h = Mathf.Max(1f, world.height);
            Scale = Mathf.Min(inner.x / w, inner.y / h);
            _offset = new Vector2((mapSize.x - w * Scale) * 0.5f, (mapSize.y - h * Scale) * 0.5f);
        }

        public Vector2 ToMap(Vector2 grid) => new((grid.x - World.xMin) * Scale + _offset.x, (grid.y - World.yMin) * Scale + _offset.y);

        public Vector2 ToGrid(Vector2 map) => new((map.x - _offset.x) / Scale + World.xMin, (map.y - _offset.y) / Scale + World.yMin);

        public Rect ToMap(Rect grid) => new(ToMap(grid.position), grid.size * Scale);

        /// Grid rectangle visible in an xNode window: it is centered on -panOffset and spans the window size times the zoom.
        public static Rect ViewportInGrid(Vector2 panOffset, float zoom, Vector2 windowSize)
        {
            var size = windowSize * zoom;
            return new Rect(-panOffset - size * 0.5f, size);
        }

        /// The panOffset that centers the view on a grid point.
        public static Vector2 PanOffsetFor(Vector2 gridPoint) => -gridPoint;

        /// Bounding rect of the rects, or a unit rect at the origin when there are none.
        public static Rect Bounds(IEnumerable<Rect> rects)
        {
            var any = false;
            var bounds = new Rect();
            foreach (var rect in rects)
            {
                bounds = any
                    ? Rect.MinMaxRect(
                        Mathf.Min(bounds.xMin, rect.xMin), Mathf.Min(bounds.yMin, rect.yMin),
                        Mathf.Max(bounds.xMax, rect.xMax), Mathf.Max(bounds.yMax, rect.yMax))
                    : rect;
                any = true;
            }

            return any ? bounds : new Rect(0, 0, 1, 1);
        }
    }
}
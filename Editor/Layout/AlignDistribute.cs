using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Xprees.Graph.Core.Editor.Layout
{
    public enum LayoutCommand
    {
        Arrange,
        AlignLeft,
        AlignRight,
        AlignTop,
        AlignBottom,

        /// Same horizontal center (nodes in one column).
        AlignCenterH,

        /// Same vertical center (nodes in one row).
        AlignCenterV,

        /// Left to right with the horizontal gap, keeps y.
        DistributeH,

        /// Top to bottom with the vertical gap, keeps x.
        DistributeV,
    }

    /// Names of the layout commands used by menus and the MCP tool.
    public static class LayoutCommandNames
    {
        private readonly static (LayoutCommand command, string id, string label)[] all =
        {
            (LayoutCommand.Arrange, "arrange", "Arrange Selection"),
            (LayoutCommand.AlignLeft, "align-left", "Align/Left"),
            (LayoutCommand.AlignRight, "align-right", "Align/Right"),
            (LayoutCommand.AlignTop, "align-top", "Align/Top"),
            (LayoutCommand.AlignBottom, "align-bottom", "Align/Bottom"),
            (LayoutCommand.AlignCenterH, "align-center-h", "Align/Center Horizontally (one column)"),
            (LayoutCommand.AlignCenterV, "align-center-v", "Align/Center Vertically (one row)"),
            (LayoutCommand.DistributeH, "distribute-h", "Distribute/Horizontally"),
            (LayoutCommand.DistributeV, "distribute-v", "Distribute/Vertically"),
        };

        public static IEnumerable<LayoutCommand> All => all.Select(x => x.command);

        public static string Id(LayoutCommand command) => all.First(x => x.command == command).id;

        /// Menu path, '/' separates the submenu.
        public static string MenuPath(LayoutCommand command) => all.First(x => x.command == command).label;

        /// Short name for undo and notifications, e.g. "Align Left".
        public static string UndoName(LayoutCommand command) => command == LayoutCommand.Arrange
            ? "Arrange Nodes"
            : all.First(x => x.command == command).label.Replace('/', ' ').Replace(" (one column)", string.Empty)
                .Replace(" (one row)", string.Empty);

        public static bool TryParse(string value, out LayoutCommand command)
        {
            var key = value?.Trim().ToLowerInvariant().Replace('_', '-').Replace(' ', '-');
            foreach (var entry in all)
            {
                if (entry.id != key && !string.Equals(entry.command.ToString(), value?.Trim(), StringComparison.OrdinalIgnoreCase)) continue;

                command = entry.command;
                return true;
            }

            command = LayoutCommand.Arrange;
            return false;
        }

        public static string Ids => string.Join(", ", all.Select(x => x.id));
    }

    /// Align and distribute math on top-level rects (positions only, sizes never change).
    public static class AlignDistribute
    {
        /// New top-left positions for the rects. Align moves on one axis only; distribute keeps the order along its axis and puts the gap
        /// between neighbours (rounded up to grid steps), starting at the first rect.
        public static Dictionary<int, Vector2> Apply(
            IReadOnlyDictionary<int, Rect> rects,
            LayoutCommand command,
            float gapH = LayoutGeometry.GapH,
            float gapV = LayoutGeometry.GapV
        )
        {
            var result = rects.ToDictionary(p => p.Key, p => p.Value.position);
            if (rects.Count < 2) return result;

            var bounds = LayoutGeometry.Bounds(rects.Values);
            switch (command)
            {
                case LayoutCommand.AlignLeft:
                    foreach (var p in rects) result[p.Key] = new Vector2(bounds.xMin, p.Value.y);
                    break;
                case LayoutCommand.AlignRight:
                    foreach (var p in rects) result[p.Key] = new Vector2(bounds.xMax - p.Value.width, p.Value.y);
                    break;
                case LayoutCommand.AlignTop:
                    foreach (var p in rects) result[p.Key] = new Vector2(p.Value.x, bounds.yMin);
                    break;
                case LayoutCommand.AlignBottom:
                    foreach (var p in rects) result[p.Key] = new Vector2(p.Value.x, bounds.yMax - p.Value.height);
                    break;
                case LayoutCommand.AlignCenterH:
                    foreach (var p in rects) result[p.Key] = new Vector2(bounds.center.x - p.Value.width * 0.5f, p.Value.y);
                    break;
                case LayoutCommand.AlignCenterV:
                    foreach (var p in rects) result[p.Key] = new Vector2(p.Value.x, bounds.center.y - p.Value.height * 0.5f);
                    break;
                case LayoutCommand.DistributeH:
                {
                    var ordered = rects.OrderBy(p => p.Value.xMin).ThenBy(p => p.Value.yMin).ThenBy(p => p.Key).ToList();
                    var x = ordered[0].Value.xMin;
                    foreach (var p in ordered)
                    {
                        result[p.Key] = new Vector2(x, p.Value.y);
                        x += LayoutGeometry.CeilToGrid(p.Value.width + gapH);
                    }

                    break;
                }
                case LayoutCommand.DistributeV:
                {
                    var ordered = rects.OrderBy(p => p.Value.yMin).ThenBy(p => p.Value.xMin).ThenBy(p => p.Key).ToList();
                    var y = ordered[0].Value.yMin;
                    foreach (var p in ordered)
                    {
                        result[p.Key] = new Vector2(p.Value.x, y);
                        y += LayoutGeometry.CeilToGrid(p.Value.height + gapV);
                    }

                    break;
                }
            }

            return result;
        }
    }
}
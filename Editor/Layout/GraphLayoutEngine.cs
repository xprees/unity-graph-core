using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Xprees.Graph.Core.Editor.Layout
{
    /// Runs a layout command on a <see cref="GraphLayoutModel"/> and returns the new rects of the nodes and groups that changed.
    /// Rules: unselected nodes never move. A selected group is a block: its contents are arranged inside it and it is resized to fit.
    /// Selected nodes inside an unselected group are arranged in place inside that group, which grows to fit. Nested groups go inside-out.
    /// After arranging, a block that overlaps an unselected node (or a group the block is not inside) shifts to the nearest free spot.
    public sealed class GraphLayoutEngine
    {
        private readonly GraphLayoutModel _model;
        private readonly Rect[] _rects;
        private readonly int[] _parent;
        private readonly List<int>[] _children;
        private readonly HashSet<int> _moving = new();
        private readonly HashSet<int> _wholeGroups = new();
        private readonly HashSet<int> _placed = new();
        private readonly float _gapH;
        private readonly float _gapV;

        private GraphLayoutEngine(GraphLayoutModel model, float gapH, float gapV)
        {
            _model = model;
            _gapH = gapH;
            _gapV = gapV;
            _rects = model.Nodes.Select(n => n.Rect).ToArray();
            _parent = model.ComputeParents();
            _children = new List<int>[_rects.Length];
            for (var i = 0; i < _rects.Length; i++) _children[i] = new List<int>();
            for (var i = 0; i < _rects.Length; i++)
            {
                if (_parent[i] >= 0) _children[_parent[i]].Add(i);
            }
        }

        public static Dictionary<int, Rect> Run(
            GraphLayoutModel model,
            IEnumerable<int> selection,
            LayoutCommand command,
            float gapH = LayoutGeometry.GapH,
            float gapV = LayoutGeometry.GapV
        )
        {
            var engine = new GraphLayoutEngine(model, gapH, gapV);
            if (!engine.Select(selection)) return new Dictionary<int, Rect>();

            if (command == LayoutCommand.Arrange) engine.ArrangeScopes();
            else engine.AlignOrDistribute(command);

            var changed = new Dictionary<int, Rect>();
            for (var i = 0; i < engine._rects.Length; i++)
            {
                if (engine._rects[i] != model.Nodes[i].Rect) changed[i] = engine._rects[i];
            }

            return changed;
        }

        /// Innermost group of each node, as the engine sees it (for tests and callers).
        public static int[] Parents(GraphLayoutModel model) => model.ComputeParents();

        private bool Select(IEnumerable<int> selection)
        {
            foreach (var id in selection ?? Enumerable.Empty<int>())
            {
                if (id >= 0 && id < _rects.Length) AddDeep(id);
            }

            foreach (var id in _moving)
            {
                if (_model.Nodes[id].IsGroup) _wholeGroups.Add(id);
            }

            return _moving.Count > 0;
        }

        private void AddDeep(int id)
        {
            if (!_moving.Add(id)) return;

            foreach (var child in _children[id]) AddDeep(child);
        }

        #region Arrange

        private void ArrangeScopes()
        {
            var scopes = _moving.Select(id => _parent[id]).Distinct().OrderByDescending(Depth).ThenBy(c => c).ToList();
            foreach (var scope in scopes)
            {
                var items = _moving.Where(id => _parent[id] == scope).OrderBy(id => id).ToList();
                ArrangeScope(scope, items);
            }
        }

        private void ArrangeScope(int scope, List<int> items)
        {
            var itemSet = new HashSet<int>(items);
            var edges = new List<LayoutEdge>();
            foreach (var edge in _model.Edges)
            {
                var from = Lift(edge.From, scope, itemSet);
                var to = Lift(edge.To, scope, itemSet);
                if (from >= 0 && to >= 0 && from != to) edges.Add(new LayoutEdge(from, to, edge.Port));
            }

            var layoutItems = items.Select(id => new LayeredLayout.Item
            {
                Id = id,
                Size = _rects[id].size,
                Position = _rects[id].position,
                IsEntry = _model.Nodes[id].Kind == LayoutNodeKind.Entry,
                EntryPriority = _model.Nodes[id].EntryPriority,
            }).ToList();
            var layout = LayeredLayout.Arrange(layoutItems, edges, _gapH, _gapV);

            var insideWholeGroup = scope >= 0 && _wholeGroups.Contains(scope);
            var anchor = LayoutGeometry.Snap(new Vector2(items.Min(id => _rects[id].xMin), items.Min(id => _rects[id].yMin)));
            if (scope >= 0)
            {
                // Keep the block inside the group's padding, so the group grows right / down only
                var origin = LayoutGeometry.GroupContentOrigin(_rects[scope]);
                anchor = insideWholeGroup ? origin : Vector2.Max(anchor, origin);
            }

            foreach (var id in items) TranslateDeep(id, anchor + layout.Positions[id] - _rects[id].position);

            // Inside a selected group there is nothing unselected to avoid: the group itself is the block one level up
            if (!insideWholeGroup) ShiftToFreeSpot(items, scope >= 0 ? new[] { scope } : new int[0]);

            foreach (var id in items) MarkPlaced(id);

            if (scope < 0) return;

            var fitted = GroupFit.Fit(_children[scope].Select(c => _rects[c]), _model.Nodes[scope].GroupChrome);
            _rects[scope] = insideWholeGroup && fitted.HasValue ? fitted.Value : GroupFit.Grow(_rects[scope], fitted);
            GrowAncestors(_parent[scope]);
        }

        /// The item of this scope that contains <paramref name="id"/> (the node itself or one of its groups), or -1.
        private int Lift(int id, int scope, HashSet<int> items)
        {
            while (id >= 0 && _parent[id] != scope) id = _parent[id];
            return id >= 0 && items.Contains(id) ? id : -1;
        }

        #endregion

        #region Align / distribute

        private void AlignOrDistribute(LayoutCommand command)
        {
            // Units are the top-level selected items: a node whose group is selected moves with that group
            var top = _moving.Where(id => _parent[id] < 0 || !_moving.Contains(_parent[id])).OrderBy(id => id).ToList();
            var targets = AlignDistribute.Apply(top.ToDictionary(id => id, id => _rects[id]), command, _gapH, _gapV);

            var moved = false;
            foreach (var id in top)
            {
                var delta = targets[id] - _rects[id].position;
                if (delta == Vector2.zero) continue;

                TranslateDeep(id, delta);
                moved = true;
            }

            if (!moved) return;

            var containers = top.SelectMany(Ancestors).Distinct().ToList();
            ShiftToFreeSpot(top, containers);
            foreach (var group in containers.OrderByDescending(Depth))
            {
                _rects[group] = GroupFit.Grow(_rects[group],
                    GroupFit.Fit(_children[group].Select(c => _rects[c]), _model.Nodes[group].GroupChrome));
            }
        }

        #endregion

        #region Helpers

        /// Shifts the items (with their contents) so they overlap nothing that stays in place. Groups containing the block and nodes that
        /// are still waiting for their own scope are not obstacles.
        private void ShiftToFreeSpot(List<int> items, IEnumerable<int> containers)
        {
            var excluded = new HashSet<int>();
            foreach (var id in items) CollectDeep(id, excluded);
            foreach (var container in containers)
            {
                for (var g = container; g >= 0; g = _parent[g]) excluded.Add(g);
            }

            foreach (var id in _moving)
            {
                if (!_placed.Contains(id)) excluded.Add(id);
            }

            var obstacles = Enumerable.Range(0, _rects.Length).Where(i => !excluded.Contains(i)).Select(i => _rects[i]).ToList();
            var offset = FreeSpotFinder.FindOffset(items.Select(id => _rects[id]).ToList(), obstacles);
            if (offset == Vector2.zero) return;

            foreach (var id in items) TranslateDeep(id, offset);
        }

        /// Unselected groups that contain moved nodes grow to fit them, inside-out.
        private void GrowAncestors(int group)
        {
            for (var g = group; g >= 0 && !_wholeGroups.Contains(g); g = _parent[g])
            {
                _rects[g] = GroupFit.Grow(_rects[g], GroupFit.Fit(_children[g].Select(c => _rects[c]), _model.Nodes[g].GroupChrome));
            }
        }

        private IEnumerable<int> Ancestors(int id)
        {
            for (var g = _parent[id]; g >= 0; g = _parent[g]) yield return g;
        }

        private int Depth(int id)
        {
            var depth = 0;
            for (var g = id; g >= 0; g = _parent[g]) depth++;
            return depth;
        }

        private void TranslateDeep(int id, Vector2 delta)
        {
            if (delta == Vector2.zero) return;

            _rects[id].position += delta;
            foreach (var child in _children[id]) TranslateDeep(child, delta);
        }

        private void CollectDeep(int id, HashSet<int> into)
        {
            into.Add(id);
            foreach (var child in _children[id]) CollectDeep(child, into);
        }

        private void MarkPlaced(int id)
        {
            _placed.Add(id);
            foreach (var child in _children[id]) MarkPlaced(child);
        }

        #endregion

    }
}
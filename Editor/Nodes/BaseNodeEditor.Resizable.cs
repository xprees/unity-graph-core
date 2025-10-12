using System;
using UnityEditor;
using UnityEngine;
using XNode;
using XNodeEditor;
using Xprees.Graph.Core.Attributes;
using Xprees.Graph.Core.Base.Nodes;

namespace Xprees.Graph.Core.Editor.Nodes
{
    public partial class BaseNodeEditor
    {
        private NodeResizableWidthAttribute resizableWidthAttribute;

        private bool _isResizable;
        protected bool IsResizable => _isResizable;

        private bool _isDragging;
        protected bool IsDragging => _isDragging;

        public override int GetWidth()
        {
            if (!_isResizable || resizableWidthAttribute == null) return base.GetWidth();

            var baseNode = target as BaseNode;
            var width = baseNode!.width;
            if (width <= resizableWidthAttribute.minWidth) baseNode.width = resizableWidthAttribute.minWidth;

            return width;
        }

        protected virtual void HandleResizingIfAttributePresent()
        {
            resizableWidthAttribute = GetTargetAssignedAttribute<NodeResizableWidthAttribute>(target);
            _isResizable = resizableWidthAttribute is { isResizable: true };
            if (!_isResizable) return;

            // Handle click event to enter edit mode
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                // Ignore clicks on resize handle
                var isResizeArea = false;
                if (NodeEditorWindow.current.nodeSizes.TryGetValue(target, out var size))
                {
                    var lowerRight = new Rect(size.x - 34, size.y - 34, 30, 30);
                    isResizeArea = lowerRight.Contains(e.mousePosition);
                }

                if (!isResizeArea)
                {
                    e.Use(); // Consume the event
                }
            }

            var node = target as BaseNode;
            Resizable(target, ref _isDragging, ref node!.width, resizableWidthAttribute.minWidth, resizableWidthAttribute.maxWidth);
        }

        private static void Resizable(Node node, ref bool isDragging, ref int width, int minWidth, int maxWidth)
        {
            var e = Event.current;
            Vector2 size;

            switch (e.type)
            {
                case EventType.MouseDrag:
                    if (isDragging)
                    {
                        width = Mathf.Clamp((int) e.mousePosition.x + 16, Math.Max(60, minWidth), maxWidth);
                        NodeEditorWindow.current.Repaint();
                    }

                    break;
                case EventType.MouseDown:
                    // Ignore everything except left clicks
                    if (e.button != 0) return;
                    if (NodeEditorWindow.current.nodeSizes.TryGetValue(node, out size))
                    {
                        // Mouse position checking is in node local space
                        var lowerRight = new Rect(size.x - 34, size.y - 34, 30, 30);
                        if (lowerRight.Contains(e.mousePosition)) isDragging = true;
                    }

                    break;
                case EventType.MouseUp:
                    isDragging = false;
                    break;
                case EventType.Repaint:
                    // Add scale cursors
                    if (NodeEditorWindow.current.nodeSizes.TryGetValue(node, out size))
                    {
                        var lowerRight = new Rect(node.position, new Vector2(30, 30));
                        lowerRight.y += size.y - 34;
                        lowerRight.x += size.x - 34;
                        lowerRight = NodeEditorWindow.current.GridToWindowRect(lowerRight);
                        NodeEditorWindow.current.onLateGUI -= AddRectOnLateGUI;
                        NodeEditorWindow.current.onLateGUI += AddRectOnLateGUI;
                        void AddRectOnLateGUI() => AddMouseRect(lowerRight);
                    }

                    break;
            }
        }

        private static void AddMouseRect(Rect rect) => EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeUpLeft);

    }
}
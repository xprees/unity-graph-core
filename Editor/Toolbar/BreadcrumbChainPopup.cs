using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XNode;

namespace Xprees.Graph.Core.Editor.Toolbar
{
    /// Popup of the collapsed "…" breadcrumb: the whole chain from the root (top) to the current graph, all rows aligned.
    public class BreadcrumbChainPopup : PopupWindowContent
    {
        private const float width = 280f;
        private const float rowHeight = 22f;
        private const float iconSize = 16f;

        private readonly List<NodeGraph> _graphs;
        private readonly Action<int> _onChoose;

        public BreadcrumbChainPopup(List<NodeGraph> graphs, Action<int> onChoose)
        {
            _graphs = graphs;
            _onChoose = onChoose;
        }

        public override Vector2 GetWindowSize() => new(width, _graphs.Count * rowHeight + 4f);

        public override void OnGUI(Rect rect)
        {
            for (var i = 0; i < _graphs.Count; i++)
            {
                var graph = _graphs[i];
                var row = new Rect(2f, 2f + i * rowHeight, rect.width - 4f, rowHeight);
                var isCurrent = i == _graphs.Count - 1;

                if (isCurrent) EditorGUI.DrawRect(row, new Color(0.24f, 0.37f, 0.59f, 0.6f));
                else if (row.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(row, new Color(1f, 1f, 1f, 0.1f));

                var x = row.x + 4f;
                var icon = graph ? EditorGUIUtility.ObjectContent(graph, graph.GetType()).image : null;
                if (icon) GUI.DrawTexture(new Rect(x, row.y + (rowHeight - iconSize) * 0.5f, iconSize, iconSize), icon, ScaleMode.ScaleToFit);

                var labelRect = new Rect(x + iconSize + 4f, row.y, row.xMax - x - iconSize - 8f, rowHeight);
                var text = graph ? graph.name : "(missing)";
                GUI.Label(labelRect, new GUIContent(text, text), isCurrent ? EditorStyles.boldLabel : EditorStyles.label);

                if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                {
                    editorWindow.Close();
                    _onChoose(i);
                    GUIUtility.ExitGUI();
                }
            }

            if (Event.current.type == EventType.MouseMove) editorWindow.Repaint();
        }
    }
}
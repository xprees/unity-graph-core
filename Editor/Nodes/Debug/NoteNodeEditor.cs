using UnityEditor;
using UnityEngine;
using XNodeEditor;
using Xprees.Graph.Core.Nodes.Debug;

namespace Xprees.Graph.Core.Editor.Nodes.Debug
{
    [CustomNodeEditor(typeof(NoteNode))]
    public class NoteNodeEditor : BaseNodeEditor
    {
        private readonly static GUIStyle textStyle = new()
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            wordWrap = true,
            richText = true,
            padding = new RectOffset(10, 10, 5, 5),
            normal = new GUIStyleState { textColor = new Color32(29, 28, 34, 255) },
        };

        private bool _editing;

        private NoteNode _noteNode;


        public override void OnBodyGUI()
        {
            _noteNode = target as NoteNode;
            if (_noteNode == null) return;

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
                    _editing = true;
                    e.Use(); // Consume the event
                }
            }

            // Display text field when editing, otherwise show label
            EditorGUILayout.BeginVertical();
            try
            {
                if (_editing)
                {
                    GUI.SetNextControlName("DebugNoteTextField");
                    var newText = EditorGUILayout.TextArea(_noteNode.text, textStyle);

                    // Set focus to the text field
                    EditorGUI.FocusTextInControl("DebugNoteTextField");

                    // Check for Enter key or clicking outside to exit edit mode
                    if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return && e.control ||
                        e.type == EventType.MouseDown && !GUILayoutUtility.GetLastRect().Contains(e.mousePosition))
                    {
                        _editing = false;
                        _noteNode.text = newText;
                        GUIUtility.keyboardControl = 0; // Clear text selection
                        GUI.FocusControl(null);
                        e.Use();
                    }
                    else
                    {
                        _noteNode.text = newText;
                    }
                }
                else
                {
                    GUILayout.Label(_noteNode.text, textStyle);
                }
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }

            HandleResizingIfAttributePresent();
        }
    }
}
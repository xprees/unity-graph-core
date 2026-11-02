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
        private bool _focusPending;

        /// Unique per note, several notes can be visible at once
        private string ControlName => "DebugNoteTextField_" + target.GetEntityId();

        private void StopEditing()
        {
            _editing = false;
            _focusPending = false;
            if (GUI.GetNameOfFocusedControl() == ControlName) GUI.FocusControl(null);
        }

        private NoteNode _noteNode;

        /// Notes are not part of the flow and draw their own body, so they are never dimmed by the debugger.
        protected override bool SupportsDebugTint => false;

        public override void OnBodyGUI()
        {
            _noteNode = target as NoteNode;
            if (_noteNode == null) return;

            // Every visible node runs this for every event, so only react to clicks inside THIS note (node-local coords inside its BeginArea)
            // rawType: a click on another node's field may already be used by the time this note is drawn, it must still end editing here
            var e = Event.current;
            if (e.rawType == EventType.MouseDown && e.button == 0)
            {
                var inside = false;
                var inResizeHandle = false;
                if (NodeEditorWindow.current.nodeSizes.TryGetValue(target, out var size))
                {
                    inside = new Rect(Vector2.zero, size).Contains(e.mousePosition);
                    inResizeHandle = new Rect(size.x - 34, size.y - 34, 30, 30).Contains(e.mousePosition);
                }

                if (inside && !inResizeHandle && !_editing && e.type == EventType.MouseDown)
                {
                    _editing = true;
                    _focusPending = true;
                    e.Use();
                    NodeEditorWindow.current.Repaint();
                }
                else if (!inside && _editing)
                {
                    StopEditing(); // the click is not used, so it still reaches the node / field it was meant for
                }
            }

            // Display text field when editing, otherwise show label
            EditorGUILayout.BeginVertical();
            try
            {
                if (_editing)
                {
                    var controlName = ControlName;
                    GUI.SetNextControlName(controlName);
                    EditorGUI.BeginChangeCheck();
                    var newText = EditorGUILayout.TextArea(_noteNode.text, textStyle);
                    if (EditorGUI.EndChangeCheck()) SetText(newText);

                    // Focus only right after entering edit mode, until the first repaint (focusing every event steals the keyboard from every
                    // other field). Not just in the click event itself: that one is already used and the text area did not exist in its layout.
                    if (_focusPending)
                    {
                        EditorGUI.FocusTextInControl(controlName);
                        if (e.type == EventType.Repaint) _focusPending = false;
                    }

                    // Ctrl+Enter exits edit mode
                    if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return && e.control)
                    {
                        StopEditing();
                        e.Use();
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

        /// Through the serialized object, so the edit is recorded for Undo and the TODO rename of NoteNode.OnValidate joins the same step.
        private void SetText(string text)
        {
            serializedObject.Update();
            serializedObject.FindProperty(nameof(NoteNode.text)).stringValue = text;
            serializedObject.ApplyModifiedProperties();
        }
    }
}
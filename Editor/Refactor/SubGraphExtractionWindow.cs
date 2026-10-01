using System.IO;
using UnityEditor;
using UnityEngine;
using XNode;

namespace Xprees.Graph.Core.Editor.Refactor
{
    /// Confirmation window of the "Extract to SubGraph" refactor: shows what is going to happen and lets you change the name and location.
    /// A normal utility window (not a popup), because the folder dialog takes the focus and would close a popup.
    public class SubGraphExtractionWindow : EditorWindow
    {
        private NodeGraph _graph;
        private Node[] _selection;
        private SubGraphExtractionResult _result;
        private ISubGraphExtractionPolicy _policy;

        private string _folder;
        private string _assetName;
        private Vector2 _scroll;

        public static void Open(NodeGraph graph, Node[] selection)
        {
            var window = CreateInstance<SubGraphExtractionWindow>();
            window.titleContent = new GUIContent("Extract to SubGraph");
            window.minSize = new Vector2(440f, 320f);
            window.Initialize(graph, selection);
            window.ShowUtility();
        }

        private void Initialize(NodeGraph graph, Node[] selection)
        {
            _graph = graph;
            _selection = selection;
            _policy = SubGraphExtractionPolicies.GetFor(graph);
            _result = SubGraphExtractionPlanner.Plan(graph, selection);
            _folder = _policy.SuggestFolder(graph);
            _assetName = _result.plan?.suggestedName ?? "SubGraph";
        }

        private void OnGUI()
        {
            if (!_graph)
            {
                Close();
                return;
            }

            EditorGUILayout.Space(6);
            DrawPlan();
            EditorGUILayout.Space(6);
            DrawTarget();
            GUILayout.FlexibleSpace();
            DrawButtons();
        }

        private void DrawPlan()
        {
            var plan = _result.plan;
            if (plan != null && _result.errors.Count == 0)
            {
                EditorGUILayout.LabelField($"{plan.members.Count} node(s) move from '{_graph.name}' to a new graph.", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Entry: {plan.entryPort.node.name}.{plan.entryPort.fieldName}   Exit: " +
                                           (plan.exitTarget != null ? $"{plan.exitTarget.node.name}.{plan.exitTarget.fieldName}" : "none (the flow ends)"),
                    EditorStyles.miniLabel);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(150f));
            foreach (var error in _result.errors) EditorGUILayout.HelpBox(error, MessageType.Error);
            foreach (var warning in _result.warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
            EditorGUILayout.EndScrollView();
        }

        private void DrawTarget()
        {
            using (new EditorGUI.DisabledScope(!_result.CanExtract))
            {
                _assetName = EditorGUILayout.TextField("Name", _assetName);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("Folder");
                    EditorGUILayout.SelectableLabel(_folder, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    if (GUILayout.Button("Select...", GUILayout.Width(80f))) SelectLocation();
                }

                EditorGUILayout.LabelField("Result", AssetDatabase.GenerateUniqueAssetPath(GetPath()), EditorStyles.miniLabel);
            }
        }

        private void DrawButtons()
        {
            EditorGUILayout.HelpBox("Both graphs are saved. Undo (Ctrl+Z) restores the original graphs and removes the new one.", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(90f))) Close();

                using (new EditorGUI.DisabledScope(!_result.CanExtract || string.IsNullOrWhiteSpace(_assetName) || !_folder.StartsWith("Assets") && !_folder.StartsWith("Packages")))
                {
                    if (GUILayout.Button("Create SubGraph", GUILayout.Width(130f))) Create();
                }
            }
        }

        private string GetPath() => $"{_folder}/{_assetName}.asset";

        private void SelectLocation()
        {
            var path = EditorUtility.SaveFilePanelInProject("Save SubGraph", _assetName, "asset", "Choose where to save the new graph.", _folder);
            if (string.IsNullOrEmpty(path)) return;

            _folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            _assetName = Path.GetFileNameWithoutExtension(path);
        }

        private void Create()
        {
            // The graph could have been edited while the window was open - plan again right before touching anything
            var result = SubGraphExtractionPlanner.Plan(_graph, _selection);
            if (!result.CanExtract)
            {
                _result = result;
                Repaint();
                return;
            }

            EnsureFolder(_folder);
            var created = SubGraphExtractor.Extract(result.plan, _folder, _assetName);
            Close();
            if (created) Debug.Log($"Extracted nodes to '{AssetDatabase.GetAssetPath(created)}'.", created);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XNode;
using XNodeEditor;
using Xprees.Graph.Core.Base;
using Xprees.Graph.Core.Editor.Graph;

namespace Xprees.Graph.Core.Editor.Validation
{
    /// Scans graphs for broken, one-sided or dangling connections and unreachable nodes.
    public class GraphValidatorWindow : EditorWindow
    {
        private const float buttonWidth = 50f;
        private const float fixButtonWidth = 110f;

        private readonly List<NodeGraph> _scannedGraphs = new();
        private readonly Dictionary<NodeGraph, List<GraphIssue>> _issues = new();
        private readonly Dictionary<NodeGraph, bool> _foldouts = new();

        private Vector2 _scrollPos;
        private bool _showErrors = true;
        private bool _showWarnings = true;
        private bool _hideValidGraphs = true;
        private string _searchFilter = "";

        private GUIContent _errorIcon;
        private GUIContent _warningIcon;

        [MenuItem("Window/Graph/Validator", priority = 100)]
        public static void Open()
        {
            var window = GetWindow<GraphValidatorWindow>("Graph Validator");
            window.Show();
        }

        /// Opens the validator scoped to a single graph with its issues expanded.
        public static void OpenFor(NodeGraph graph)
        {
            if (!graph) return;

            var window = GetWindow<GraphValidatorWindow>("Graph Validator");
            window.Scan(new[] { graph });
            window._foldouts[graph] = true;
            window.Show();
        }

        private void OnEnable()
        {
            _errorIcon = EditorGUIUtility.IconContent("console.erroricon.sml");
            _warningIcon = EditorGUIUtility.IconContent("console.warnicon.sml");
        }

        // Graphs could be edited in the node editor in the meantime
        private void OnFocus() => Revalidate();

        private void OnGUI()
        {
            DrawToolbar();

            if (_scannedGraphs.Count <= 0)
            {
                EditorGUILayout.HelpBox("Scan the project or the selected graphs to find broken connections.", MessageType.Info);
                return;
            }

            DrawSummary();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            foreach (var graph in _scannedGraphs.ToList())
            {
                if (!graph) continue;
                DrawGraph(graph);
            }

            EditorGUILayout.EndScrollView();
        }

        #region Scanning

        private void ScanProject()
        {
            var graphs = AssetDatabase.FindAssets($"t:{nameof(GraphBase)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<NodeGraph>)
                .Where(g => g);
            Scan(graphs);
        }

        private void ScanSelection()
        {
            var graphs = Selection.objects
                .Select(o => o switch
                {
                    NodeGraph graph => graph,
                    Node node => node.graph,
                    _ => null,
                })
                .Where(g => g)
                .Distinct();
            Scan(graphs);
        }

        private void Scan(IEnumerable<NodeGraph> graphs)
        {
            _scannedGraphs.Clear();
            _scannedGraphs.AddRange(graphs.OrderBy(g => AssetDatabase.GetAssetPath(g)));
            Revalidate();
        }

        private void Revalidate()
        {
            _scannedGraphs.RemoveAll(g => !g);
            _issues.Clear();
            foreach (var graph in _scannedGraphs)
            {
                _issues[graph] = GraphValidator.Validate(graph);
            }

            Repaint();
        }

        private void Revalidate(NodeGraph graph)
        {
            _issues[graph] = GraphValidator.Validate(graph);
            Repaint();
        }

        #endregion

        #region Drawing

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Scan Project", EditorStyles.toolbarButton, GUILayout.Width(90))) ScanProject();
            if (GUILayout.Button("Scan Selection", EditorStyles.toolbarButton, GUILayout.Width(100))) ScanSelection();

            GUILayout.Space(8);
            _showErrors = GUILayout.Toggle(_showErrors, "Errors", EditorStyles.toolbarButton, GUILayout.Width(55));
            _showWarnings = GUILayout.Toggle(_showWarnings, "Warnings", EditorStyles.toolbarButton, GUILayout.Width(70));
            _hideValidGraphs = GUILayout.Toggle(_hideValidGraphs, "Hide valid", EditorStyles.toolbarButton, GUILayout.Width(75));

            GUILayout.FlexibleSpace();
            _searchFilter = GUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));

            var fixableCount = _issues.Values.Sum(list => list.Count(i => i.IsSafelyFixable));
            using (new EditorGUI.DisabledScope(fixableCount <= 0))
            {
                if (GUILayout.Button($"Fix All Safe ({fixableCount})", EditorStyles.toolbarButton, GUILayout.Width(110)))
                {
                    FixAll();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSummary()
        {
            var errors = _issues.Values.Sum(list => list.Count(i => i.Severity == GraphIssueSeverity.Error));
            var warnings = _issues.Values.Sum(list => list.Count(i => i.Severity == GraphIssueSeverity.Warning));
            var invalidGraphs = _issues.Count(kvp => kvp.Value.Count > 0);

            EditorGUILayout.LabelField(
                $"Scanned {_scannedGraphs.Count} graph(s) - {invalidGraphs} with issues, {errors} error(s), {warnings} warning(s)",
                EditorStyles.boldLabel);
        }

        private void DrawGraph(NodeGraph graph)
        {
            var allIssues = _issues.TryGetValue(graph, out var list) ? list : new List<GraphIssue>();
            if (_hideValidGraphs && allIssues.Count <= 0) return;

            var visible = allIssues.Where(IsVisible).ToList();
            if (_hideValidGraphs && visible.Count <= 0 && !string.IsNullOrEmpty(_searchFilter)) return;

            var errors = allIssues.Count(i => i.Severity == GraphIssueSeverity.Error);
            var warnings = allIssues.Count - errors;

            EditorGUILayout.BeginHorizontal();
            _foldouts.TryGetValue(graph, out var expanded);
            var label = allIssues.Count <= 0 ? $"{graph.name} - OK" : $"{graph.name} - {errors} error(s), {warnings} warning(s)";
            _foldouts[graph] = EditorGUILayout.Foldout(expanded, label, true);

            if (GUILayout.Button("Ping", GUILayout.Width(buttonWidth))) EditorGUIUtility.PingObject(graph);
            if (GUILayout.Button("Open", GUILayout.Width(buttonWidth))) OpenInEditor(graph, null);

            using (new EditorGUI.DisabledScope(!allIssues.Any(i => i.IsSafelyFixable)))
            {
                if (GUILayout.Button("Fix All Safe", GUILayout.Width(fixButtonWidth)))
                {
                    GraphValidator.FixAllSafe(graph);
                    Revalidate(graph);
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.EndHorizontal();

            if (!_foldouts[graph]) return;

            EditorGUI.indentLevel++;
            foreach (var issue in visible)
            {
                DrawIssue(issue);
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        private void DrawIssue(GraphIssue issue)
        {
            EditorGUILayout.BeginHorizontal();

            var icon = issue.Severity == GraphIssueSeverity.Error ? _errorIcon : _warningIcon;
            GUILayout.Space(EditorGUI.indentLevel * 15f);
            GUILayout.Label(icon, GUILayout.Width(18), GUILayout.Height(18));

            var location = issue.Node ? issue.Node.name : "<graph>";
            if (!string.IsNullOrEmpty(issue.PortName)) location += $" [{issue.PortName}]";
            GUILayout.Label(new GUIContent($"{location}: {issue.Message}", issue.Message), EditorStyles.wordWrappedLabel);

            using (new EditorGUI.DisabledScope(!issue.Node))
            {
                if (GUILayout.Button("Ping", GUILayout.Width(buttonWidth))) EditorGUIUtility.PingObject(issue.Node);
            }

            if (GUILayout.Button("Open", GUILayout.Width(buttonWidth))) OpenInEditor(issue.Graph, issue.Node);

            using (new EditorGUI.DisabledScope(!issue.IsFixable))
            {
                var fixContent = new GUIContent(issue.IsFixable ? issue.FixLabel : "Manual",
                    issue.IsFixable && !issue.IsSafeFix ? "Changes the runtime flow - review before applying." : null);
                if (GUILayout.Button(fixContent, GUILayout.Width(fixButtonWidth)))
                {
                    issue.Fix();
                    Revalidate(issue.Graph);
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private bool IsVisible(GraphIssue issue)
        {
            if (issue.Severity == GraphIssueSeverity.Error && !_showErrors) return false;
            if (issue.Severity == GraphIssueSeverity.Warning && !_showWarnings) return false;
            if (string.IsNullOrEmpty(_searchFilter)) return true;

            return issue.Message.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0
                   || (issue.Node && issue.Node.name.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                   || issue.Graph.name.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        #endregion

        private void FixAll()
        {
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            foreach (var graph in _scannedGraphs.Where(g => g)) GraphValidator.FixAllSafe(graph);
            Undo.SetCurrentGroupName("Fix all safe graph issues");
            Undo.CollapseUndoOperations(undoGroup);
            Revalidate();
            GUIUtility.ExitGUI();
        }

        private static void OpenInEditor(NodeGraph graph, Node node)
        {
            if (node)
            {
                GraphBaseEditor.FocusNode(node);
                return;
            }

            NodeEditorWindow.Open(graph);
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XNodeEditor;

namespace Xprees.Graph.Core.Editor.Refactor
{
    /// Undo / redo for the sub-graph extraction.
    ///
    /// Unity's Undo can't revert asset creation and the moving of sub-assets, so the extraction is undone at the file level:
    /// the assets it touches (source graph, new graph and whatever the policy changes, with their .meta files) are stored
    /// before and after the operation in the Temp folder. A hidden record object goes through the normal Undo stack,
    /// and when undo / redo flips it, the files of the matching state are written back.
    /// Edits made after the extraction are undone first by the stack order, so the files always match the expected state.
    public static class SubGraphExtractionUndo
    {
        private const string undoName = "Extract to SubGraph";

        private readonly static Dictionary<int, int> appliedSteps = new();

        [Serializable]
        private class Manifest
        {
            public List<Entry> entries = new();
        }

        [Serializable]
        private class Entry
        {
            public string path;
            public bool hasBefore;
            public bool hasAfter;
        }

        internal class FileState
        {
            public byte[] asset;
            public byte[] meta;
        }

        /// State of the touched assets before the operation.
        public class Snapshot
        {
            internal readonly Dictionary<string, FileState> before = new();
        }

        [InitializeOnLoadMethod]
        private static void Initialize() => Undo.undoRedoPerformed += OnUndoRedo;

        /// Saves pending changes and captures the files of the given assets.
        public static Snapshot Begin(IEnumerable<string> assetPaths)
        {
            AssetDatabase.SaveAssets();

            var snapshot = new Snapshot();
            foreach (var path in assetPaths.Where(p => !string.IsNullOrEmpty(p)).Distinct()) snapshot.before[path] = Read(path);
            return snapshot;
        }

        /// Captures the state after the operation and registers the undo step.
        /// <paramref name="assetPaths"/> are the assets touched by the operation, the ones from <see cref="Begin"/> are included automatically.
        public static void Commit(Snapshot snapshot, IEnumerable<string> assetPaths)
        {
            AssetDatabase.SaveAssets();

            var paths = snapshot.before.Keys.Concat(assetPaths.Where(p => !string.IsNullOrEmpty(p))).Distinct().ToList();
            var operationId = Guid.NewGuid().ToString("N");
            var directory = GetDirectory(operationId);
            Directory.CreateDirectory(directory);

            var manifest = new Manifest();
            for (var i = 0; i < paths.Count; i++)
            {
                var before = snapshot.before.TryGetValue(paths[i], out var state) ? state : new FileState();
                var after = Read(paths[i]);

                Write(directory, i, "before", before);
                Write(directory, i, "after", after);
                manifest.entries.Add(new Entry { path = paths[i], hasBefore = before.asset != null, hasAfter = after.asset != null });
            }

            File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonUtility.ToJson(manifest));

            var record = ScriptableObject.CreateInstance<ExtractionUndoRecord>();
            record.hideFlags = HideFlags.HideAndDontSave;
            record.operationId = operationId;

            Undo.IncrementCurrentGroup();
            Undo.RecordObject(record, undoName);
            record.step = 1;
            Undo.SetCurrentGroupName(undoName);
            Undo.FlushUndoRecordObjects();
            appliedSteps[record.GetInstanceID()] = 1;
        }

        private static void OnUndoRedo()
        {
            var changed = false;
            foreach (var record in Resources.FindObjectsOfTypeAll<ExtractionUndoRecord>())
            {
                var id = record.GetInstanceID();

                // After a domain reload the applied state is unknown - the step was just flipped by this undo / redo
                var applied = appliedSteps.TryGetValue(id, out var step) ? step : 1 - record.step;
                if (applied == record.step) continue;

                Apply(record.operationId, record.step);
                appliedSteps[id] = record.step;
                changed = true;
            }

            if (changed) NodeEditorWindow.RepaintAll();
        }

        private static void Apply(string operationId, int step)
        {
            var directory = GetDirectory(operationId);
            var manifestPath = Path.Combine(directory, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning("The undo data of the sub-graph extraction is gone (the Temp folder was cleared).");
                return;
            }

            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            try
            {
                RestoreFiles(directory, manifest, step);
            }
            catch (IOException exception)
            {
                Debug.LogWarning($"Undo / redo of the sub-graph extraction could not restore all files: {exception.Message}");
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void RestoreFiles(string directory, Manifest manifest, int step)
        {
            var suffix = step == 1 ? "after" : "before";
            for (var i = 0; i < manifest.entries.Count; i++)
            {
                var entry = manifest.entries[i];
                var exists = step == 1 ? entry.hasAfter : entry.hasBefore;
                var assetFile = GetFullPath(entry.path);

                if (exists)
                {
                    // The folder may have been deleted since - recreate it, Refresh adds its .meta
                    Directory.CreateDirectory(Path.GetDirectoryName(assetFile)!);
                    File.WriteAllBytes(assetFile, File.ReadAllBytes(Path.Combine(directory, $"{i}.{suffix}")));
                    var metaBackup = Path.Combine(directory, $"{i}.{suffix}.meta");
                    if (File.Exists(metaBackup)) File.WriteAllBytes(assetFile + ".meta", File.ReadAllBytes(metaBackup));
                }
                else
                {
                    DeleteIfExists(assetFile);
                    DeleteIfExists(assetFile + ".meta");
                }
            }
        }

        private static FileState Read(string assetPath)
        {
            var file = GetFullPath(assetPath);
            return new FileState
            {
                asset = File.Exists(file) ? File.ReadAllBytes(file) : null,
                meta = File.Exists(file + ".meta") ? File.ReadAllBytes(file + ".meta") : null,
            };
        }

        private static void Write(string directory, int index, string suffix, FileState state)
        {
            if (state.asset != null) File.WriteAllBytes(Path.Combine(directory, $"{index}.{suffix}"), state.asset);
            if (state.meta != null) File.WriteAllBytes(Path.Combine(directory, $"{index}.{suffix}.meta"), state.meta);
        }

        private static void DeleteIfExists(string file)
        {
            if (File.Exists(file)) File.Delete(file);
        }

        private static string GetProjectRoot() => Directory.GetParent(Application.dataPath)!.FullName;

        private static string GetFullPath(string assetPath) => Path.Combine(GetProjectRoot(), assetPath);

        private static string GetDirectory(string operationId) => Path.Combine(GetProjectRoot(), "Temp", "XpreesGraphUndo", operationId);
    }
}
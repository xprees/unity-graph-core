using UnityEngine;

namespace Xprees.Graph.Core.Editor.Refactor
{
    /// Tiny hidden object the Unity Undo system tracks for one extraction. Undo / redo flips <see cref="step"/>,
    /// <see cref="SubGraphExtractionUndo"/> reacts by restoring the asset files from before / after the extraction.
    internal class ExtractionUndoRecord : ScriptableObject
    {
        public string operationId;

        /// 0 = state before the extraction, 1 = state after it.
        public int step;
    }
}
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Xprees.Graph.Core.Editor.Debugger
{
    /// Holds parsers at a node until the user continues. One gate per parser record.
    public static class GraphDebugGate
    {
        private static readonly Dictionary<int, GateEntry> Gates = new();

        public static bool IsHeld(int recordId) => Gates.ContainsKey(recordId);

        /// Tests use their own tracer instances, so a gate is released only by its record.
        public static void Continue(ParserRecord record) => Release(record.Id, false);

        /// Resumes the record. The flow resumes on the next Update when playing, inline otherwise.
        public static void Continue(int recordId) => Release(recordId, false);

        /// Resumes the record and breaks again at the next node it enters.
        public static void Step(int recordId) => Release(recordId, true);

        public static void ContinueAll() => ReleaseAll();

        /// Resumes every held record (debugger switched off, breakpoint removed).
        public static void ReleaseAll()
        {
            var ids = new List<int>(Gates.Keys);
            foreach (var id in ids) Release(id, false);
        }

        /// Cancels every held record, the flows end with OperationCanceledException (Play Mode exit).
        public static void CancelAll()
        {
            var gates = new List<GateEntry>(Gates.Values);
            Gates.Clear();
            foreach (var gate in gates) gate.Source.TrySetCanceled();
        }

        internal static void Cancel(int recordId)
        {
            if (!Gates.Remove(recordId, out var gate)) return;
            gate.Source.TrySetCanceled();
        }

        internal static UniTask Wait(ParserRecord record, CancellationToken cancellationToken)
        {
            Release(record.Id, false); // a record never holds two gates

            var gate = new UniTaskCompletionSource();
            Gates[record.Id] = new GateEntry(record, gate);
            return Hold(record.Id, gate, cancellationToken);
        }

        private static async UniTask Hold(int recordId, UniTaskCompletionSource gate, CancellationToken cancellationToken)
        {
            CancellationTokenRegistration registration = default;
            if (cancellationToken.CanBeCanceled) registration = cancellationToken.Register(() => gate.TrySetCanceled(cancellationToken));

            try
            {
                await gate.Task;
            }
            finally
            {
                registration.Dispose();
                if (Gates.TryGetValue(recordId, out var current) && current.Source == gate) Gates.Remove(recordId);
            }

            // Never resume the flow inside OnGUI (Continue is clicked there). EditMode tests resume inline.
            if (Application.isPlaying) await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        private static void Release(int recordId, bool step)
        {
            if (!Gates.Remove(recordId, out var gate)) return;

            gate.Record.Owner.OnReleased(gate.Record, step);
            if (Application.isPlaying && EditorApplication.isPaused && GraphDebuggerSettings.FreezeWorldOnBreak)
                EditorApplication.isPaused = false;
            gate.Source.TrySetResult();
        }

        private readonly struct GateEntry
        {
            public readonly ParserRecord Record;
            public readonly UniTaskCompletionSource Source;

            public GateEntry(ParserRecord record, UniTaskCompletionSource source)
            {
                Record = record;
                Source = source;
            }
        }
    }
}
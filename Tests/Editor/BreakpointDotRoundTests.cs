using System;
using NUnit.Framework;
using UnityEngine;
using Xprees.Graph.Core.Editor.Debugger;
using Xprees.Graph.Core.Editor.Nodes;

namespace Xprees.Graph.Core.Tests
{
    public class BreakpointDotRoundTests
    {
        private static Vector2 NodeOrigin => new(120f, 80f);

        [TestCase(0.3f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        [TestCase(2f)]
        [TestCase(3f)]
        public void Hit_InsideTitleBand_AndHitTestingMatchesAtEveryZoom(float zoom)
        {
            var local = BreakpointDotLayout.LocalHit(300f, zoom);
            Assert.LessOrEqual(local.yMax, BreakpointDotLayout.HeaderBand, "inside the title band");
            Assert.LessOrEqual(local.xMax, 300f);
            Assert.IsTrue(local.Contains(BreakpointDotLayout.LocalDot(300f, zoom).center));

            var window = BreakpointDotLayout.WindowHit(NodeOrigin, 300f, zoom);
            Assert.IsTrue(window.Contains(NodeOrigin + local.center / zoom), "centre hits");
            Assert.IsTrue(window.Contains(NodeOrigin + (local.position + new Vector2(1f, 1f)) / zoom), "near corner hits");
            Assert.IsFalse(window.Contains(NodeOrigin + (local.position - new Vector2(3f, 3f)) / zoom), "outside misses");
            Assert.IsFalse(window.Contains(NodeOrigin + new Vector2(5f, 15f) / zoom), "title text area misses");
        }

        [Test]
        public void DefaultSize_IsLargeAtZoomOne()
        {
            Assert.GreaterOrEqual(BreakpointDotLayout.LocalDot(300f).width, 16f);
            Assert.GreaterOrEqual(BreakpointDotLayout.LocalHit(300f).width, 28f);
        }

        [Test]
        public void ZoomedOut_KeepsMinimumScreenSize_WithinTheBand()
        {
            var hit = BreakpointDotLayout.LocalHit(300f, 3f);
            var dot = BreakpointDotLayout.LocalDot(300f, 3f);
            Assert.GreaterOrEqual(hit.width / 3f, BreakpointDotLayout.MinScreenHit * 0.6f);
            Assert.GreaterOrEqual(dot.width / 3f, BreakpointDotLayout.MinScreenDot * 0.6f);
            Assert.LessOrEqual(hit.yMax, BreakpointDotLayout.HeaderBand);
        }

        [Test]
        public void ReservedHeaderWidth_CoversTheHitArea()
        {
            var hit = BreakpointDotLayout.LocalHit(300f);
            Assert.GreaterOrEqual(BreakpointDotLayout.ReservedHeaderWidth, 300f - hit.x);
        }

        [Test]
        public void ResolveClick_Matrix()
        {
            Assert.AreEqual(DotClickAction.Add, BreakpointDotLayout.ResolveClick(false, false));
            Assert.AreEqual(DotClickAction.Add, BreakpointDotLayout.ResolveClick(false, true));
            Assert.AreEqual(DotClickAction.Remove, BreakpointDotLayout.ResolveClick(true, false));
            Assert.AreEqual(DotClickAction.ToggleEnabled, BreakpointDotLayout.ResolveClick(true, true));
        }

        [Test]
        public void FeedbackMessage_NamesNodeAndWarnsWhenDebuggerOff()
        {
            Assert.AreEqual("Breakpoint removed from A", BreakpointDotLayout.FeedbackMessage(DotClickAction.Remove, false, "A", false));
            StringAssert.Contains("set on A", BreakpointDotLayout.FeedbackMessage(DotClickAction.Add, true, "A", true));
            StringAssert.DoesNotContain("debugger is off", BreakpointDotLayout.FeedbackMessage(DotClickAction.Add, true, "A", true));
            StringAssert.Contains("debugger is off", BreakpointDotLayout.FeedbackMessage(DotClickAction.Add, true, "A", false));
            StringAssert.Contains("disabled on A", BreakpointDotLayout.FeedbackMessage(DotClickAction.ToggleEnabled, false, "A", false));
        }

        private static BreakpointDiagnosis.Inputs In(
            bool on = true,
            bool has = true,
            bool enabled = true,
            bool suppressed = false,
            bool persistent = true,
            bool running = true
        ) => new(on, has, enabled, suppressed, persistent, running);

        [Test]
        public void Diagnosis_Verdicts_InTracerOrder()
        {
            Assert.AreEqual(BreakpointDiagnosis.Verdict.WouldFire, BreakpointDiagnosis.Evaluate(In()));
            Assert.AreEqual(BreakpointDiagnosis.Verdict.DebuggerOff, BreakpointDiagnosis.Evaluate(In(on: false, has: false)));
            Assert.AreEqual(BreakpointDiagnosis.Verdict.NoBreakpoint, BreakpointDiagnosis.Evaluate(In(has: false, enabled: false)));
            Assert.AreEqual(BreakpointDiagnosis.Verdict.BreakpointDisabled, BreakpointDiagnosis.Evaluate(In(enabled: false, suppressed: true)));
            Assert.AreEqual(BreakpointDiagnosis.Verdict.BreaksSuppressed, BreakpointDiagnosis.Evaluate(In(suppressed: true)));
        }

        [Test]
        public void Diagnosis_NotSavedAndNotRunning_AreNotesNotBlockers()
        {
            var inputs = In(persistent: false, running: false);
            Assert.AreEqual(BreakpointDiagnosis.Verdict.WouldFire, BreakpointDiagnosis.Evaluate(inputs));
            Assert.AreEqual(2, BreakpointDiagnosis.Notes(inputs).Count);
            Assert.AreEqual(0, BreakpointDiagnosis.Notes(In()).Count);
            Assert.AreEqual(0, BreakpointDiagnosis.Notes(In(has: false, persistent: false)).Count);
        }

        [Test]
        public void Diagnosis_Explanations_AreNonEmptyAndMentionTheFix()
        {
            StringAssert.Contains("toolbar", BreakpointDiagnosis.Explain(BreakpointDiagnosis.Verdict.DebuggerOff));
            foreach (BreakpointDiagnosis.Verdict v in Enum.GetValues(typeof(BreakpointDiagnosis.Verdict)))
            {
                Assert.IsNotEmpty(BreakpointDiagnosis.Explain(v));
            }
        }
    }
}
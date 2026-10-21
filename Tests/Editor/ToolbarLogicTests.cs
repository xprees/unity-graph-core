using System.Linq;
using NUnit.Framework;
using Xprees.Graph.Core.Editor.Toolbar;

namespace Xprees.Graph.Core.Tests
{
    public class ToolbarLogicTests
    {
        private static TraceMenuState State(
            bool hasTrace = true,
            bool isPlaying = false,
            bool lastRun = true,
            bool heatmap = false,
            bool log = false,
            bool cumulative = false
        ) => new(hasTrace, isPlaying, lastRun, heatmap, log, cumulative);

        private static TraceMenuEntry Entry(TraceMenuState state, TraceMenuAction action) =>
            ToolbarLogic.BuildTraceMenu(state).Single(e => e.Action == action);

        [Test]
        public void Menu_ContainsEveryActionOnce()
        {
            var entries = ToolbarLogic.BuildTraceMenu(State());
            foreach (var action in new[]
                     {
                         TraceMenuAction.ClearTrace, TraceMenuAction.ToggleHeatmap, TraceMenuAction.ToggleHeatmapLogScale,
                         TraceMenuAction.ToggleCumulativeCoverage, TraceMenuAction.ResetCoverage, TraceMenuAction.SaveTraceAs,
                         TraceMenuAction.LoadTrace, TraceMenuAction.LoadLastRun,
                     })
            {
                Assert.AreEqual(1, entries.Count(e => e.Action == action), action.ToString());
            }
        }

        [Test]
        public void Menu_CoverageItemsLiveInSubmenu()
        {
            var state = State();
            Assert.IsTrue(Entry(state, TraceMenuAction.ToggleHeatmapLogScale).Path.StartsWith("Coverage/"));
            Assert.IsTrue(Entry(state, TraceMenuAction.ToggleCumulativeCoverage).Path.StartsWith("Coverage/"));
            Assert.IsTrue(Entry(state, TraceMenuAction.ResetCoverage).Path.StartsWith("Coverage/"));
            Assert.AreEqual("Heatmap", Entry(state, TraceMenuAction.ToggleHeatmap).Path);
        }

        [Test]
        public void Menu_CheckedStateFollowsSettings()
        {
            var state = State(heatmap: true, log: true, cumulative: true);
            Assert.IsTrue(Entry(state, TraceMenuAction.ToggleHeatmap).Checked);
            Assert.IsTrue(Entry(state, TraceMenuAction.ToggleHeatmapLogScale).Checked);
            Assert.IsTrue(Entry(state, TraceMenuAction.ToggleCumulativeCoverage).Checked);

            var off = State();
            Assert.IsFalse(Entry(off, TraceMenuAction.ToggleHeatmap).Checked);
            Assert.IsFalse(Entry(off, TraceMenuAction.ToggleHeatmapLogScale).Checked);
        }

        [Test]
        public void Menu_SaveNeedsTrace()
        {
            Assert.IsTrue(Entry(State(hasTrace: true), TraceMenuAction.SaveTraceAs).Enabled);
            Assert.IsFalse(Entry(State(hasTrace: false), TraceMenuAction.SaveTraceAs).Enabled);
        }

        [Test]
        public void Menu_LoadingWaitsForPlayModeToEnd()
        {
            var playing = State(isPlaying: true);
            Assert.IsFalse(Entry(playing, TraceMenuAction.LoadTrace).Enabled);
            Assert.IsFalse(Entry(playing, TraceMenuAction.LoadLastRun).Enabled);
            Assert.IsTrue(Entry(playing, TraceMenuAction.ClearTrace).Enabled);
        }

        [Test]
        public void Menu_LoadLastRunNeedsFile()
        {
            Assert.IsTrue(Entry(State(lastRun: true), TraceMenuAction.LoadLastRun).Enabled);
            Assert.IsFalse(Entry(State(lastRun: false), TraceMenuAction.LoadLastRun).Enabled);
            Assert.IsTrue(Entry(State(lastRun: false), TraceMenuAction.LoadTrace).Enabled);
        }

        [Test]
        public void Menu_SeparatorsHaveNoAction()
        {
            Assert.IsTrue(ToolbarLogic.BuildTraceMenu(State()).Any(e => e.IsSeparator));
        }

        [Test]
        public void Narrow_BelowThresholdIsNarrow()
        {
            Assert.IsTrue(ToolbarLogic.IsNarrow(ToolbarLogic.NarrowWidth - 1f, false));
            Assert.IsFalse(ToolbarLogic.IsNarrow(ToolbarLogic.NarrowWidth, false));
        }

        [Test]
        public void Narrow_HysteresisKeepsNarrowUntilClearlyWider()
        {
            var inBand = ToolbarLogic.NarrowWidth + ToolbarLogic.NarrowHysteresis / 2f;
            Assert.IsTrue(ToolbarLogic.IsNarrow(inBand, true));
            Assert.IsFalse(ToolbarLogic.IsNarrow(inBand, false));
            Assert.IsFalse(ToolbarLogic.IsNarrow(ToolbarLogic.NarrowWidth + ToolbarLogic.NarrowHysteresis, true));
        }
    }
}
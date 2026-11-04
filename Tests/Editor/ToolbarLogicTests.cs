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

        // Crumb widths 100 each, ellipsis 30
        private static ToolbarLogic.BreadcrumbLayout Crumbs(int count, float available) =>
            ToolbarLogic.ComputeBreadcrumbs(Enumerable.Repeat(100f, count).ToList(), 30f, available);

        [Test]
        public void Crumbs_FullChainWhenItFits()
        {
            var layout = Crumbs(4, 400f);
            Assert.IsTrue(layout.ShowFirst);
            Assert.IsFalse(layout.HasEllipsis);
            Assert.IsFalse(layout.TruncateLast);
            Assert.AreEqual(1, layout.TailStart);
        }

        [Test]
        public void Crumbs_HidesMiddleKeepingFirstAndLast()
        {
            var layout = Crumbs(5, 330f); // first 100 + ellipsis 30 + last 100 fits, one more crumb (100) does not
            Assert.IsTrue(layout.ShowFirst);
            Assert.IsTrue(layout.HasEllipsis);
            Assert.AreEqual(4, layout.TailStart);
            Assert.AreEqual(1, layout.HiddenFrom);
        }

        [Test]
        public void Crumbs_GreedyTailKeepsMoreCrumbsWhenTheyFit()
        {
            var layout = Crumbs(5, 330f + 100f);
            Assert.IsTrue(layout.ShowFirst);
            Assert.AreEqual(3, layout.TailStart);
        }

        [Test]
        public void Crumbs_DropsFirstWhenItDoesNotFit()
        {
            var layout = Crumbs(5, 200f); // first + ellipsis + last = 230
            Assert.IsFalse(layout.ShowFirst);
            Assert.IsTrue(layout.HasEllipsis);
            Assert.AreEqual(4, layout.TailStart);
            Assert.IsFalse(layout.TruncateLast);
        }

        [Test]
        public void Crumbs_TruncatesLastWhenNothingFits()
        {
            var layout = Crumbs(5, 100f); // ellipsis + last = 130
            Assert.IsFalse(layout.ShowFirst);
            Assert.IsTrue(layout.HasEllipsis);
            Assert.AreEqual(4, layout.TailStart);
            Assert.IsTrue(layout.TruncateLast);
        }

        [Test]
        public void Crumbs_SingleCrumbOnlyTruncates()
        {
            Assert.IsFalse(Crumbs(1, 200f).TruncateLast);
            var layout = Crumbs(1, 50f);
            Assert.IsTrue(layout.ShowFirst);
            Assert.IsFalse(layout.HasEllipsis);
            Assert.IsTrue(layout.TruncateLast);
        }

        [Test]
        public void Crumbs_TwoCrumbsCollapseToEllipsisAndLast()
        {
            var layout = Crumbs(2, 150f);
            Assert.IsFalse(layout.ShowFirst);
            Assert.IsTrue(layout.HasEllipsis);
            Assert.AreEqual(1, layout.TailStart);
        }

        [Test]
        public void Crumbs_NoWidthTruncatesLast()
        {
            Assert.IsTrue(Crumbs(3, 0f).TruncateLast);
            Assert.IsFalse(Crumbs(0, 0f).HasEllipsis);
        }

        [Test]
        public void MiddleEllipsis_KeepsBothEnds()
        {
            Assert.AreEqual("abc…xyz", ToolbarLogic.MiddleEllipsis("abcdefghxyz", 6));
            Assert.AreEqual("abcd…xyz", ToolbarLogic.MiddleEllipsis("abcdefghxyz", 7));
            Assert.AreEqual("short", ToolbarLogic.MiddleEllipsis("short", 5));
            Assert.AreEqual("…", ToolbarLogic.MiddleEllipsis("short", 0));
        }
    }
}
using System.Linq;
using Morsoul.Engine;
using NUnit.Framework;

namespace Morsoul.Tests
{
    /// <summary>
    /// GraphAudit — User Story 2 design-time enumeration over authored-link
    /// subsets: unlock counts, minimal antichains, S* shortcut detection,
    /// dead conclusions, the exactly-one warning and the size cap.
    /// </summary>
    public class GraphAuditTests
    {
        EvidenceGraph _graph;

        [SetUp]
        public void SetUp() => _graph = TestGraphs.BuildValid();

        static GraphAudit.ConclusionAudit Audit(GraphAudit.Report r, string id) =>
            r.Conclusions.First(c => c.ConclusionId == id);

        [Test]
        public void FixtureGraph_AllSubsetsEvaluated()
        {
            var report = GraphAudit.Analyze(_graph);
            Assert.IsFalse(report.Skipped);
            Assert.AreEqual(3, report.AuthoredLinkCount);
            Assert.AreEqual(8, report.TotalSubsets); // 2^3
            Assert.AreEqual(8, report.EvaluatedSubsets);
        }

        [Test]
        public void FixtureGraph_SingleEdgeBoardIsAShortcut()
        {
            var report = GraphAudit.Analyze(_graph);
            var complete = Audit(report, "h_complete");

            // {b->c} alone reaches phi 0.857 >= 0.7 — it bypasses the a->b leg
            // of S*, so the fixture graph itself demonstrates a real shortcut.
            Assert.AreEqual(4, complete.UnlockingSubsetCount); // all boards containing b->c
            Assert.AreEqual(1, complete.MinimalUnlockingSets.Count);
            CollectionAssert.AreEquivalent(
                new[] { new DirectedEdge("b", "c") }, complete.MinimalUnlockingSets[0]);
            Assert.IsTrue(complete.HasSolutionShortcut);
            Assert.IsTrue(report.HasErrors);
        }

        [Test]
        public void GatedGraph_ExactlyOneBoardUnlocks_NoFindings()
        {
            var def = TestGraphs.FiveClueDef();
            // Make every authored edge load-bearing: S* = E_KK, h* draws
            // support from both legs, and alpha + theta_high are tuned so
            // dropping any single link falls below the unlock threshold
            // (0.85). Missing a->b yields phi ~0.83 — still fails.
            def.Parameters.alpha = 0.85f;
            def.Parameters.highThreshold = 0.85f;
            def.SolutionEdges = new[]
            {
                new DirectedEdge("a", "b"), new DirectedEdge("b", "c"),
                new DirectedEdge("d", "e"),
            };
            def.Conclusions[2].supportingEdges = new[]
            {
                TestGraphs.Ref("c"), TestGraphs.Ref("e"),
            };
            var g = TestGraphs.BuildValid(def);

            var report = GraphAudit.Analyze(g);
            var complete = Audit(report, "h_complete");
            Assert.AreEqual(1, complete.UnlockingSubsetCount);
            Assert.AreEqual(3, complete.MinimalUnlockingSets[0].Count);
            Assert.IsFalse(complete.HasSolutionShortcut);
            Assert.That(report.Findings, Has.None.Contains("h_complete"));
            Assert.IsFalse(report.HasErrors);
        }

        [Test]
        public void FixtureGraph_PartialUnlocksOnAnyBoardWithAB()
        {
            var report = GraphAudit.Analyze(_graph);
            var partial = Audit(report, "h_partial");

            Assert.AreEqual(4, partial.UnlockingSubsetCount);
            Assert.AreEqual(1, partial.MinimalUnlockingSets.Count);
            CollectionAssert.AreEquivalent(
                new[] { new DirectedEdge("a", "b") }, partial.MinimalUnlockingSets[0]);
        }

        [Test]
        public void StartingConclusion_AlwaysUnlocked_NoFindings()
        {
            var report = GraphAudit.Analyze(_graph);
            var h0 = Audit(report, "h_wrong");
            Assert.AreEqual(8, h0.UnlockingSubsetCount);
            Assert.AreEqual(1, h0.MinimalUnlockingSets.Count);
            Assert.IsEmpty(h0.MinimalUnlockingSets[0]);
            Assert.That(report.Findings, Has.None.Contains("h_wrong"));
        }

        [Test]
        public void ShortcutBoard_IsFlaggedAsError()
        {
            var def = TestGraphs.FiveClueDef();
            // a->c alone pushes c's propagated support past the high
            // threshold without the authored S* path.
            def.AuthoredClueEdges = def.AuthoredClueEdges
                .Concat(new[] { TestGraphs.Edge("a", "c", 1f) }).ToArray();
            var g = TestGraphs.BuildValid(def);

            var report = GraphAudit.Analyze(g);
            var complete = Audit(report, "h_complete");

            Assert.IsTrue(complete.HasSolutionShortcut);
            Assert.IsTrue(report.HasErrors);
            Assert.That(report.Findings, Has.Some.StartsWith("ERROR").And.Contains("h_complete"));
            // {a->c, b->c} bypasses the authored a->b leg of S*.
            Assert.IsTrue(complete.MinimalUnlockingSets.Any(set =>
                set.Any(e => e.sourceId == "a" && e.targetId == "c") &&
                !set.Any(e => e.sourceId == "a" && e.targetId == "b")));
        }

        [Test]
        public void DeadConclusion_WarnsNeverUnlocks()
        {
            var def = TestGraphs.FiveClueDef();
            // Support weight so small the conclusion can never reach its tier.
            def.Conclusions = def.Conclusions.Concat(new[]
            {
                TestGraphs.Conclusion("h_dead", ConclusionTier.Partial,
                    new[] { TestGraphs.Ref("d", 0.01f) }),
            }).ToArray();
            var g = TestGraphs.BuildValid(def);

            var report = GraphAudit.Analyze(g);
            var dead = Audit(report, "h_dead");
            Assert.AreEqual(0, dead.UnlockingSubsetCount);
            Assert.That(report.Findings, Has.Some.Contains("h_dead").And.Contains("never"));
        }

        [Test]
        public void OverCapGraph_IsSkippedWithError()
        {
            var report = GraphAudit.Analyze(_graph, maxAuthoredLinks: 2);
            Assert.IsTrue(report.Skipped);
            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(0, report.EvaluatedSubsets);
        }

        [Test]
        public void MonotonicFlag_ReflectsSpecCondition()
        {
            Assert.IsTrue(GraphAudit.Analyze(_graph).Monotonic);

            var def = TestGraphs.FiveClueDef();
            // Give the partial conclusion a contradicting edge -> not monotone.
            def.Conclusions[1].contradictingEdges = new[] { TestGraphs.Ref("e", 0.5f) };
            var g = TestGraphs.BuildValid(def);
            Assert.IsFalse(GraphAudit.Analyze(g).Monotonic);
        }
    }
}

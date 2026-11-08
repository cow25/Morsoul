using System.Collections.Generic;
using System.Linq;
using Morsoul.Data;
using Morsoul.Engine;
using NUnit.Framework;
using UnityEngine;

namespace Morsoul.Tests
{
    /// <summary>
    /// Weighted deduction engine tests on synthetic graphs.
    /// Fixture graph: a-&gt;b-&gt;c chain (S* = both links) plus a d-&gt;e
    /// off-solution authored link; alpha 0.5, reliability 1 everywhere, so
    /// x = [0.5, 0.75, 0.875] along the chain and rho_ref = 0.875.
    /// </summary>
    public class DeductionEngineTests
    {
        [TearDown]
        public void TearDown() => TestCases.DestroyAll();

        static WeightedClueEdge E(string s, string t, float w = 1f) => TestGraphs.Edge(s, t, w);
        static WeightedClueRef R(string clue, float w = 1f) => TestGraphs.Ref(clue, w);
        static ConclusionDef Conclusion(string id, ConclusionTier tier,
            WeightedClueRef[] support = null, WeightedClueRef[] contradict = null) =>
            TestGraphs.Conclusion(id, tier, support, contradict);
        static EvidenceGraphDef ValidDef() => TestGraphs.FiveClueDef();
        static EvidenceGraph ValidGraph() => TestGraphs.BuildValid();
        static DeductionInput Input(EvidenceGraph g, DirectedEdge[] links, string conclusion,
            string[] visible = null, string[] collected = null) =>
            TestGraphs.Input(g, links, conclusion, visible, collected);

        static DirectedEdge L(string s, string t) => new DirectedEdge(s, t);

        // ---------- Build-time validation ----------

        [Test]
        public void ValidDef_Builds_ComputesReferenceSupportAndSolutionOrder()
        {
            var g = ValidGraph();
            Assert.That(g.ReferenceSupport, Is.EqualTo(0.875).Within(1e-6));
            Assert.That(g.SolutionOrder, Is.EqualTo(new[] { "a", "b", "c" }));
            Assert.That(g.FinalSolutionClue, Is.EqualTo("c"));
        }

        [Test]
        public void Build_RejectsNullDef_AndEmptyClues()
        {
            Assert.That(EvidenceGraph.Build(null, out _), Is.Not.Empty);
            var def = ValidDef();
            def.ClueIds = new string[0];
            Assert.That(EvidenceGraph.Build(def, out _), Is.Not.Empty);
        }

        [Test]
        public void Build_RejectsDuplicateAndMissingReliability()
        {
            var dup = ValidDef();
            dup.ClueIds = new[] { "a", "a", "b", "c", "d", "e" };
            Assert.That(EvidenceGraph.Build(dup, out _).Any(e => e.Contains("Duplicate clue id")), Is.True);

            var missing = ValidDef();
            missing.Reliabilities = missing.Reliabilities.Where(r => r.clueId != "a").ToArray();
            Assert.That(EvidenceGraph.Build(missing, out _).Any(e => e.Contains("'a' has no reliability")), Is.True);

            var bad = ValidDef();
            bad.Reliabilities[0].value = 1.5f;
            Assert.That(EvidenceGraph.Build(bad, out _).Any(e => e.Contains("(0, 1]")), Is.True);
        }

        [Test]
        public void Build_RejectsMalformedAuthoredEdges()
        {
            var unknown = ValidDef();
            unknown.AuthoredClueEdges = unknown.AuthoredClueEdges.Concat(new[] { E("a", "zzz") }).ToArray();
            Assert.That(EvidenceGraph.Build(unknown, out _).Any(e => e.Contains("unknown clue")), Is.True);

            var self = ValidDef();
            self.AuthoredClueEdges = self.AuthoredClueEdges.Concat(new[] { E("a", "a") }).ToArray();
            Assert.That(EvidenceGraph.Build(self, out _).Any(e => e.Contains("self-link")), Is.True);

            var weight = ValidDef();
            weight.AuthoredClueEdges[0].weight = 0f;
            Assert.That(EvidenceGraph.Build(weight, out _).Any(e => e.Contains("weight")), Is.True);

            var dupEdge = ValidDef();
            dupEdge.AuthoredClueEdges = dupEdge.AuthoredClueEdges.Concat(new[] { E("a", "b") }).ToArray();
            Assert.That(EvidenceGraph.Build(dupEdge, out _).Any(e => e.Contains("Duplicate authored edge")), Is.True);
        }

        [Test]
        public void Build_RejectsCycles_InAuthoredEdgesAndDependencies()
        {
            var c6 = ValidDef();
            c6.AuthoredClueEdges = c6.AuthoredClueEdges.Concat(new[] { E("c", "a") }).ToArray();
            Assert.That(EvidenceGraph.Build(c6, out _).Any(e => e.StartsWith("C6")), Is.True);

            var c1 = ValidDef();
            c1.UnlockDependencies = new[] { L("a", "d"), L("d", "a") };
            Assert.That(EvidenceGraph.Build(c1, out _).Any(e => e.StartsWith("C1")), Is.True);
        }

        [Test]
        public void Build_EnforcesC2_C3_C4()
        {
            var c2 = ValidDef();
            c2.ClueIds = c2.ClueIds.Concat(new[] { "f" }).ToArray();
            c2.Reliabilities = c2.Reliabilities.Concat(
                new[] { new ClueReliability { clueId = "f", value = 1f } }).ToArray();
            Assert.That(EvidenceGraph.Build(c2, out _).Any(e => e.StartsWith("C2")), Is.True);

            var c3 = ValidDef();
            c3.Conclusions[2].supportingEdges = new WeightedClueRef[0];
            Assert.That(EvidenceGraph.Build(c3, out _).Any(e => e.StartsWith("C3")), Is.True);

            var c4 = ValidDef();
            c4.Conclusions[1].contradictingEdges = new[] { R("b", 0.5f) }; // b already supports h_partial
            Assert.That(EvidenceGraph.Build(c4, out _).Any(e => e.StartsWith("C4")), Is.True);
        }

        [Test]
        public void Build_EnforcesC7_SolutionMustUnlockComplete()
        {
            var def = ValidDef();
            def.SolutionEdges = new[] { L("a", "b") }; // c never activates: phi(complete) = 0
            var errors = EvidenceGraph.Build(def, out _);
            Assert.That(errors.Any(e => e.StartsWith("C7")), Is.True,
                "Incomplete S* must be rejected: " + string.Join(" | ", errors));
        }

        [Test]
        public void Build_RejectsSolutionEdgeNotInAuthoredGraph()
        {
            var def = ValidDef();
            def.SolutionEdges = def.SolutionEdges.Concat(new[] { L("c", "d") }).ToArray();
            Assert.That(EvidenceGraph.Build(def, out _).Any(e => e.Contains("not an authored clue edge")), Is.True);
        }

        [Test]
        public void Build_EnforcesSizeLimits_AndParameterRanges()
        {
            var big = ValidDef();
            big.ClueIds = Enumerable.Range(0, 11).Select(i => "k" + i).ToArray();
            big.Reliabilities = big.ClueIds.Select(c => new ClueReliability { clueId = c, value = 1f }).ToArray();
            Assert.That(EvidenceGraph.Build(big, out _).Any(e => e.Contains("limit is")), Is.True);

            var many = ValidDef();
            many.Conclusions = many.Conclusions.Concat(new[]
            {
                Conclusion("h4", ConclusionTier.Wrong), Conclusion("h5", ConclusionTier.Wrong),
                Conclusion("h6", ConclusionTier.Wrong),
            }).ToArray();
            Assert.That(EvidenceGraph.Build(many, out _).Any(e => e.Contains("limit is")), Is.True);

            var alpha = ValidDef();
            alpha.Parameters.alpha = 1.5f;
            Assert.That(EvidenceGraph.Build(alpha, out _).Any(e => e.Contains("alpha")), Is.True);

            var thresholds = ValidDef();
            thresholds.Parameters.lowThreshold = 0.9f;
            Assert.That(EvidenceGraph.Build(thresholds, out _).Any(e => e.Contains("Thresholds")), Is.True);
        }

        [Test]
        public void Build_RejectsStaleCachedValues()
        {
            var stale = ValidDef();
            stale.CachedReferenceSupport = 0.123f;
            Assert.That(EvidenceGraph.Build(stale, out _).Any(e => e.Contains("cachedReferenceSupport")), Is.True);

            var order = ValidDef();
            order.SolutionTopologicalOrder = new[] { "c", "b", "a" };
            Assert.That(EvidenceGraph.Build(order, out _).Any(e => e.Contains("solutionTopologicalOrder")), Is.True);

            var fresh = ValidDef();
            fresh.CachedReferenceSupport = 0.875f;
            fresh.SolutionTopologicalOrder = new[] { "a", "b", "c" };
            Assert.That(EvidenceGraph.Build(fresh, out var g), Is.Empty);
            Assert.That(g, Is.Not.Null);
        }

        [Test]
        public void ReferenceSupport_IncludesCompleteContradictionEdges()
        {
            var def = ValidDef();
            def.Parameters.contradictionWeight = 0.5f;
            def.Conclusions[2].contradictingEdges = new[] { R("a") };

            Assert.That(EvidenceGraph.Build(def, out var g), Is.Empty);
            // rho_ref = 0.875 - 0.5 * 0.5 = 0.625.
            Assert.That(g.ReferenceSupport, Is.EqualTo(0.625).Within(1e-6));

            var cert = DeductionEngine.Solve(g,
                Input(g, new[] { L("a", "b"), L("b", "c") }, "h_complete"));
            Assert.That(cert.SelectedSupport, Is.EqualTo(1.0).Within(1e-6));
        }

        // ---------- Solver ----------

        [Test]
        public void FullSolution_PropagatesAndScores_HotComplete()
        {
            var g = ValidGraph();
            var cert = DeductionEngine.Solve(g,
                Input(g, new[] { L("a", "b"), L("b", "c") }, "h_complete"));

            Assert.That(cert.Status, Is.EqualTo(DeductionStatus.Ok));
            Assert.That(cert.Verdict, Is.EqualTo(ConclusionTier.Complete));
            Assert.That(cert.IsSolved, Is.True);
            Assert.That(cert.SelectedSupport, Is.EqualTo(1.0).Within(1e-6));
            Assert.That(cert.Band, Is.EqualTo(SupportBand.Hot));
            Assert.That(cert.InventedLinkCount, Is.EqualTo(0));
            Assert.That(cert.PropagatedClueValues["a"], Is.EqualTo(0.5).Within(1e-6));
            Assert.That(cert.PropagatedClueValues["b"], Is.EqualTo(0.75).Within(1e-6));
            Assert.That(cert.PropagatedClueValues["c"], Is.EqualTo(0.875).Within(1e-6));
            Assert.That(cert.RuntimeMs, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void FullAuthoredBoard_OffSolutionLinksAreValidNotSpurious()
        {
            var g = ValidGraph();
            var cert = DeductionEngine.Solve(g,
                Input(g, new[] { L("a", "b"), L("b", "c"), L("d", "e") }, "h_complete"));
            Assert.That(cert.Status, Is.EqualTo(DeductionStatus.Ok));
            Assert.That(cert.InventedLinkCount, Is.EqualTo(0)); // authored but off S* is still a valid link
            Assert.That(cert.SelectedSupport, Is.EqualTo(1.0).Within(1e-6));
            Assert.That(cert.SupportByConclusion["h_partial"], Is.EqualTo(0.375 / 0.875).Within(1e-6));
        }

        [Test]
        public void HighSupportWithoutAllSolutionLinks_LocksComplete()
        {
            var g = ValidGraph();
            var links = new[] { L("b", "c") };
            var cert = DeductionEngine.Solve(g, Input(g, links, "h_complete"));

            Assert.That(cert.SelectedSupport, Is.GreaterThanOrEqualTo(g.Parameters.highThreshold));
            Assert.That(cert.Status, Is.EqualTo(DeductionStatus.ConclusionLocked));
            Assert.That(cert.IsSolved, Is.False);
            Assert.That(DeductionEngine.IsConclusionUnlocked(g,
                g.GetConclusion("h_complete"), cert.SelectedSupport, links), Is.False);
        }

        [Test]
        public void PartialBoard_LocksComplete_ButUnlocksPartial()
        {
            var g = ValidGraph();
            var links = new[] { L("a", "b") };

            var locked = DeductionEngine.Solve(g, Input(g, links, "h_complete"));
            Assert.That(locked.Status, Is.EqualTo(DeductionStatus.ConclusionLocked));
            Assert.That(locked.SelectedSupport, Is.EqualTo(0.0).Within(1e-6));
            Assert.That(locked.Band, Is.EqualTo(SupportBand.Cold));

            var partial = DeductionEngine.Solve(g, Input(g, links, "h_partial"));
            Assert.That(partial.Status, Is.EqualTo(DeductionStatus.Ok));
            Assert.That(partial.Verdict, Is.EqualTo(ConclusionTier.Partial));
            Assert.That(partial.SelectedSupport, Is.EqualTo(0.375 / 0.875).Within(1e-6));
            Assert.That(partial.Band, Is.EqualTo(SupportBand.Warm));
        }

        [Test]
        public void EmptyBoard_StartingConclusionAlwaysUnlocked()
        {
            var g = ValidGraph();
            var cert = DeductionEngine.Solve(g, Input(g, new DirectedEdge[0], "h_wrong"));
            Assert.That(cert.Status, Is.EqualTo(DeductionStatus.Ok));
            Assert.That(cert.Verdict, Is.EqualTo(ConclusionTier.Wrong));
            Assert.That(cert.SelectedSupport, Is.EqualTo(0.0));
            Assert.That(cert.Band, Is.EqualTo(SupportBand.Cold));
            Assert.That(cert.PropagatedClueValues, Is.Empty);
        }

        [Test]
        public void InventedAndReversedLinks_CountAndPenalise()
        {
            var g = ValidGraph();
            var links = new[] { L("a", "b"), L("b", "c"), L("c", "a"), L("b", "a") };
            var cert = DeductionEngine.Solve(g, Input(g, links, "h_complete"));
            Assert.That(cert.InventedLinkCount, Is.EqualTo(2)); // c->a invented, b->a reversed
            Assert.That(cert.SelectedSupport, Is.EqualTo(1.0 - 0.2).Within(1e-6));
            Assert.That(cert.Band, Is.EqualTo(SupportBand.Hot)); // 0.8 >= high 0.7
        }

        [Test]
        public void DuplicateLinks_CollapseBeforeScoring()
        {
            var g = ValidGraph();
            var single = DeductionEngine.Solve(g, Input(g, new[] { L("a", "b"), L("b", "c") }, "h_complete"));
            var duped = DeductionEngine.Solve(g,
                Input(g, new[] { L("a", "b"), L("a", "b"), L("b", "c"), L("b", "c") }, "h_complete"));
            Assert.That(duped.SelectedSupport, Is.EqualTo(single.SelectedSupport).Within(1e-9));
            Assert.That(duped.InventedLinkCount, Is.EqualTo(single.InventedLinkCount));
        }

        [Test]
        public void Solve_RejectsMalformedInputs()
        {
            var g = ValidGraph();
            var all = g.ClueIds.ToArray();

            Assert.That(DeductionEngine.Solve(g,
                Input(g, new[] { L("a", "a") }, "h_complete")).Status,
                Is.EqualTo(DeductionStatus.InvalidInput)); // self-link

            Assert.That(DeductionEngine.Solve(g,
                Input(g, new[] { L("a", "d") }, "h_complete",
                    visible: all, collected: new[] { "a", "b" })).Status,
                Is.EqualTo(DeductionStatus.InvalidInput)); // endpoint not collected

            Assert.That(DeductionEngine.Solve(g,
                Input(g, null, "h_complete",
                    visible: new[] { "a", "b" }, collected: new[] { "a", "b", "c" })).Status,
                Is.EqualTo(DeductionStatus.InvalidInput)); // C not subset of U

            Assert.That(DeductionEngine.Solve(g,
                Input(g, null, "h_complete", visible: new[] { "a", "ghost" },
                    collected: new[] { "a" })).Status,
                Is.EqualTo(DeductionStatus.InvalidInput)); // unknown visible id

            Assert.That(DeductionEngine.Solve(g,
                Input(g, null, "h_nope")).Status,
                Is.EqualTo(DeductionStatus.InvalidInput)); // unknown conclusion

            var cert = DeductionEngine.Solve(g,
                Input(g, new[] { L("a", "a") }, "h_complete"));
            Assert.That(cert.Errors, Is.Not.Empty);
        }

        [Test]
        public void Solve_ReportsUncollectedAndUnlinkedClues()
        {
            var g = ValidGraph();
            var cert = DeductionEngine.Solve(g, Input(g, new[] { L("a", "b") }, "h_wrong",
                visible: new[] { "a", "b", "c", "d" }, collected: new[] { "a", "b", "d" }));
            Assert.That(cert.UncollectedClueIds, Is.EqualTo(new[] { "c" }));   // U \ C
            Assert.That(cert.UnlinkedClueIds, Is.EqualTo(new[] { "d" }));      // collected, not in K_act
        }

        [Test]
        public void ContradictionWeight_ReducesSupport()
        {
            var def = ValidDef();
            def.Parameters.contradictionWeight = 0.5f;
            def.Conclusions = def.Conclusions.Concat(new[]
            {
                Conclusion("h_con", ConclusionTier.Partial,
                    support: new[] { R("c") }, contradict: new[] { R("a") }),
            }).ToArray();
            var errors = EvidenceGraph.Build(def, out var g);
            Assert.That(errors, Is.Empty, string.Join(" | ", errors));

            var cert = DeductionEngine.Solve(g,
                Input(g, new[] { L("a", "b"), L("b", "c") }, "h_complete"));
            // rho(h_con) = sup 0.875 - 0.5 * (1.0 * x_a 0.5) = 0.625; phi = 0.625/0.875
            Assert.That(cert.SupportByConclusion["h_con"], Is.EqualTo(0.625 / 0.875).Within(1e-6));
        }

        [Test]
        public void Solve_IsDeterministic()
        {
            var g = ValidGraph();
            var links = new[] { L("a", "b"), L("b", "c"), L("c", "a") };
            var first = DeductionEngine.Solve(g, Input(g, links, "h_complete"));
            var second = DeductionEngine.Solve(g, Input(g, links, "h_complete"));
            Assert.That(second.SelectedSupport, Is.EqualTo(first.SelectedSupport));
            Assert.That(second.SupportByConclusion.Values, Is.EqualTo(first.SupportByConclusion.Values));
            Assert.That(second.PropagatedClueValues, Is.EqualTo(first.PropagatedClueValues));
        }

        // ---------- Hint helpers (spec step 5) ----------

        [Test]
        public void HintHelpers_TrackUnfinishedSolutionClues()
        {
            var g = ValidGraph();
            Assert.That(g.NextUnfinishedSolutionClue(new DirectedEdge[0]), Is.EqualTo("a"));
            Assert.That(g.NextUnfinishedSolutionClue(new[] { L("a", "b") }), Is.EqualTo("b"));
            Assert.That(g.NextUnfinishedSolutionClue(new[] { L("a", "b"), L("b", "c") }), Is.Null);
            Assert.That(g.IncidentSolutionLinks("b"), Has.Count.EqualTo(2));
            Assert.That(g.FinalSolutionClue, Is.EqualTo("c"));
        }

        // ---------- EvidenceGraphData / CaseData integration ----------

        [Test]
        public void EvidenceGraphData_ValidateAndBuildGraph()
        {
            var data = ScriptableObject.CreateInstance<EvidenceGraphData>();
            var def = ValidDef();
            data.caseId = def.CaseId;
            data.clueIds = def.ClueIds;
            data.reliabilities = def.Reliabilities;
            data.authoredClueEdges = def.AuthoredClueEdges;
            data.solutionEdges = def.SolutionEdges;
            data.conclusions = def.Conclusions;
            data.startingWrongConclusionId = def.StartingWrongConclusionId;
            data.completeConclusionId = def.CompleteConclusionId;
            data.parameters = def.Parameters;
            try
            {
                Assert.That(data.Validate(), Is.Empty);
                var graph = data.BuildGraph();
                Assert.That(graph, Is.Not.Null);
                Assert.That(graph.ReferenceSupport, Is.EqualTo(0.875).Within(1e-6));
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void EvidenceGraphData_FeedbackLookup_UsesAuthoredAndFallbackData()
        {
            var data = ScriptableObject.CreateInstance<EvidenceGraphData>();
            try
            {
                data.conclusions = new[]
                {
                    new ConclusionDef
                    {
                        id = "wrong", tier = ConclusionTier.Wrong,
                        text = "Wrong", submissionFeedback = "authored rejection",
                    },
                    new ConclusionDef
                    {
                        id = "partial", tier = ConclusionTier.Partial,
                        text = "Partial",
                    },
                };
                data.tutorialRedHerringClueIds = new[] { "rh" };
                data.tutorialRedHerringFeedback = "red herring feedback";
                data.tutorialReversedLinkFeedback = "reversed feedback";
                data.tutorialGenericLinkFeedback = "generic tutorial feedback";
                data.authoredClueEdges = new[] { E("a", "b") };
                data.solutionEdges = new[] { L("a", "b") };

                Assert.That(data.GetConclusionSubmissionFeedback("wrong"), Is.EqualTo("authored rejection"));
                Assert.That(data.GetConclusionSubmissionFeedback("partial"), Is.Null);
                Assert.That(data.GetConclusionSubmissionFeedback("missing"), Is.Null);

                Assert.That(data.GetTutorialLinkFeedback(L("rh", "a")), Is.EqualTo("red herring feedback"));
                Assert.That(data.GetTutorialLinkFeedback(L("b", "a")), Is.EqualTo("reversed feedback"));
                Assert.That(data.GetTutorialLinkFeedback(L("c", "d")), Is.EqualTo("generic tutorial feedback"));
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void CaseData_Validate_FlagsEvidenceGraphProblems()
        {
            var f = TestCases.Build(1, true);
            Assert.That(f.caseData.Validate(), Is.Empty);

            var data = ScriptableObject.CreateInstance<EvidenceGraphData>();
            f.assets.Add(data);
            data.caseId = 5; // mismatched case
            data.clueIds = new[] { "a" };
            f.caseData.evidenceGraph = data;

            var errors = f.caseData.Validate();
            Assert.That(errors.Any(e => e.Contains("does not match case")), Is.True);
        }
    }
}

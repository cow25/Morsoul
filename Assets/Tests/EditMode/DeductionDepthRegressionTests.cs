using System;
using System.Collections.Generic;
using System.Linq;
using Morsoul.Core;
using Morsoul.Data;
using Morsoul.Engine;
using Morsoul.Hub;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Morsoul.Tests
{
    public class DeductionDepthRegressionTests
    {
        static readonly int[] AllCaseIds = { 0, 1, 2, 3, 4, 5 };

        static CaseData LoadCanonicalCase(int caseId) =>
            AssetDatabase.LoadAssetAtPath<CaseData>($"Assets/Data/Cases/Case{caseId:00}/Case_{caseId:00}.asset");

        static int LongestDirectedPathNodes(IEnumerable<DirectedEdge> edges)
        {
            var adj = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var nodes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in edges)
            {
                nodes.Add(e.sourceId);
                nodes.Add(e.targetId);
                if (!adj.TryGetValue(e.sourceId, out var list))
                    adj[e.sourceId] = list = new List<string>();
                list.Add(e.targetId);
            }

            var memo = new Dictionary<string, int>(StringComparer.Ordinal);
            int Dfs(string u)
            {
                if (memo.TryGetValue(u, out int cached)) return cached;
                int best = 1;
                if (adj.TryGetValue(u, out var nexts))
                    foreach (var v in nexts)
                        best = Math.Max(best, 1 + Dfs(v));
                memo[u] = best;
                return best;
            }

            int maxNodes = 0;
            foreach (var n in nodes) maxNodes = Math.Max(maxNodes, Dfs(n));
            return maxNodes;
        }

        [TestCaseSource(nameof(AllCaseIds))]
        public void CanonicalCase_MeetsMinimumDepthAndPassesGraphAudit(int caseId)
        {
            var caseData = LoadCanonicalCase(caseId);
            Assert.NotNull(caseData, $"Missing canonical Case_{caseId:00}.asset");
            Assert.That(caseData.Validate(), Is.Empty, $"Case_{caseId:00} failed validation.");

            var evg = caseData.evidenceGraph;
            Assert.NotNull(evg, $"Case_{caseId:00} has no EvidenceGraphData.");
            Assert.That(evg.Validate(), Is.Empty, $"EVG_{caseId:00} failed validation.");

            // Minimum Design Requirements: >= 4 meaningful S* links & >= 3-clue directed chain.
            Assert.That(evg.solutionEdges.Length, Is.GreaterThanOrEqualTo(4),
                $"Case {caseId} must have at least 4 solution links.");
            int longestChain = LongestDirectedPathNodes(evg.solutionEdges);
            Assert.That(longestChain, Is.GreaterThanOrEqualTo(3),
                $"Case {caseId} must have a directed chain of at least 3 distinct clues (found {longestChain}).");

            var graph = evg.BuildGraph();
            Assert.NotNull(graph);

            // Every S* link must carry an authored rationale.
            foreach (var edge in graph.SolutionEdges)
                Assert.That(graph.TryGetEdgeRationale(edge, out _), Is.True,
                    $"Case {caseId} solution edge {edge} is missing an authored rationale.");

            // GraphAudit must pass with 0 errors and 0 findings, and unique Complete unlock.
            var report = GraphAudit.Analyze(graph);
            Assert.That(report.HasErrors, Is.False, string.Join("\n", report.Findings));
            Assert.That(report.Findings, Is.Empty, string.Join("\n", report.Findings));
            var complete = report.Conclusions.Single(c => c.Tier == ConclusionTier.Complete);
            Assert.That(complete.UnlockingSubsetCount, Is.EqualTo(1));
            Assert.That(complete.MinimalUnlockingSets.Count, Is.EqualTo(1));
            Assert.That(complete.HasSolutionShortcut, Is.False);

            // Complete solves on S*, and every Partial stays locked on empty board and unlocks on S*.
            var allClues = graph.ClueIds.ToArray();
            var allSolution = graph.SolutionEdges.ToArray();
            var fullCert = DeductionEngine.Solve(graph, new DeductionInput
            {
                VisibleClueIds = allClues,
                CollectedClueIds = allClues,
                Links = allSolution,
                SelectedConclusionId = graph.CompleteConclusionId,
            });
            Assert.That(fullCert.IsSolved, Is.True);

            foreach (var c in graph.Conclusions.Where(x => x.tier == ConclusionTier.Partial))
            {
                var emptyPartial = DeductionEngine.Solve(graph, new DeductionInput
                {
                    VisibleClueIds = allClues,
                    CollectedClueIds = allClues,
                    Links = Array.Empty<DirectedEdge>(),
                    SelectedConclusionId = c.id,
                });
                var fullPartial = DeductionEngine.Solve(graph, new DeductionInput
                {
                    VisibleClueIds = allClues,
                    CollectedClueIds = allClues,
                    Links = allSolution,
                    SelectedConclusionId = c.id,
                });
                Assert.That(emptyPartial.Status, Is.EqualTo(DeductionStatus.ConclusionLocked),
                    $"Case {caseId} partial conclusion {c.id} unlocked on empty board.");
                Assert.That(fullPartial.Status, Is.EqualTo(DeductionStatus.Ok),
                    $"Case {caseId} partial conclusion {c.id} never unlocked on S*.");
            }
        }

        [Test]
        public void NextUnfinishedSolutionClue_PrioritizesNextClueOnExistingConnection()
        {
            var evg0 = LoadCanonicalCase(0).evidenceGraph;
            var g0 = evg0.BuildGraph();

            // Empty board -> root T1_00
            Assert.That(g0.NextUnfinishedSolutionClue(Array.Empty<DirectedEdge>()), Is.EqualTo("T1_00"));
            // T1_00 -> C1_00 drawn -> next clue continuing from C1_00 is C2_00
            Assert.That(g0.NextUnfinishedSolutionClue(new[] { new DirectedEdge("T1_00", "C1_00") }), Is.EqualTo("C2_00"));
            // T1_00 -> C1_00 -> C2_00 drawn -> next clue continuing from C2_00 is C3_00
            Assert.That(g0.NextUnfinishedSolutionClue(new[]
            {
                new DirectedEdge("T1_00", "C1_00"),
                new DirectedEdge("C1_00", "C2_00"),
            }), Is.EqualTo("C3_00"));
            // All S* drawn -> null
            Assert.That(g0.NextUnfinishedSolutionClue(g0.SolutionEdges), Is.Null);
        }

        [Test]
        public void HubHudController_PostPrologueNeverSticksOnPrologue()
        {
            var core = CoreRuntime.Ensure();
            var hudGo = new GameObject("HubHudRegression");
            var hud = hudGo.AddComponent<HubHudController>();
            hud.caseText = "Case 2: The Muted Belfry";
            hud.objectiveCaseId = 2;
            try
            {
                core.Session.Reset();
                core.Session.PrologueCompleted = true;
                core.Session.CurrentCase = 0; // stale pre-advance state
                hud.RefreshObjective();
                Assert.That(hud.AppliedCaseText, Is.Not.EqualTo("Prologue"));
                Assert.That(hud.AppliedCaseText, Does.StartWith("Case 2"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hudGo);
                UnityEngine.Object.DestroyImmediate(core.gameObject);
            }
        }
    }
}

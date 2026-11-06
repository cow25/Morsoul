using System.Linq;
using Morsoul.Engine;
using NUnit.Framework;

namespace Morsoul.Tests
{
    /// <summary>
    /// Shared synthetic evidence-graph fixtures.
    /// FiveClueDef: a-&gt;b-&gt;c solution chain plus an off-solution d-&gt;e
    /// authored link; alpha 0.5, reliability 1, so x = [0.5, 0.75, 0.875]
    /// along the chain and rho_ref = 0.875.
    /// </summary>
    internal static class TestGraphs
    {
        public static WeightedClueEdge Edge(string s, string t, float w = 1f) =>
            new WeightedClueEdge { sourceId = s, targetId = t, weight = w };

        public static WeightedClueRef Ref(string clue, float w = 1f) =>
            new WeightedClueRef { clueId = clue, weight = w };

        public static ConclusionDef Conclusion(string id, ConclusionTier tier,
            WeightedClueRef[] support = null, WeightedClueRef[] contradict = null) =>
            new ConclusionDef
            {
                id = id,
                text = id + " text.",
                tier = tier,
                supportingEdges = support ?? new WeightedClueRef[0],
                contradictingEdges = contradict ?? new WeightedClueRef[0],
            };

        public static EvidenceGraphDef FiveClueDef()
        {
            string[] clues = { "a", "b", "c", "d", "e" };
            return new EvidenceGraphDef
            {
                CaseId = 9,
                ClueIds = clues,
                Reliabilities = clues.Select(c => new ClueReliability { clueId = c, value = 1f }).ToArray(),
                AuthoredClueEdges = new[] { Edge("a", "b"), Edge("b", "c"), Edge("d", "e", 0.8f) },
                SolutionEdges = new[] { new DirectedEdge("a", "b"), new DirectedEdge("b", "c") },
                UnlockDependencies = new DirectedEdge[0],
                Conclusions = new[]
                {
                    Conclusion("h_wrong", ConclusionTier.Wrong),
                    Conclusion("h_partial", ConclusionTier.Partial, new[] { Ref("b", 0.5f) }),
                    Conclusion("h_complete", ConclusionTier.Complete, new[] { Ref("c") }),
                },
                StartingWrongConclusionId = "h_wrong",
                CompleteConclusionId = "h_complete",
                Parameters = new SolverParameters
                {
                    alpha = 0.5f,
                    contradictionWeight = 0f,
                    inventedLinkPenalty = 0.1f,
                    lowThreshold = 0.3f,
                    highThreshold = 0.7f,
                },
            };
        }

        /// <summary>Builds a graph, asserting the definition is valid.</summary>
        public static EvidenceGraph BuildValid(EvidenceGraphDef def = null)
        {
            var errors = EvidenceGraph.Build(def ?? FiveClueDef(), out var graph);
            Assert.That(errors, Is.Empty, "Fixture graph must build cleanly: " +
                string.Join(" | ", errors));
            return graph;
        }

        public static DeductionInput Input(EvidenceGraph g, DirectedEdge[] links, string conclusion,
            string[] visible = null, string[] collected = null) => new DeductionInput
        {
            VisibleClueIds = visible ?? g.ClueIds.ToArray(),
            CollectedClueIds = collected ?? g.ClueIds.ToArray(),
            Links = links ?? new DirectedEdge[0],
            SelectedConclusionId = conclusion,
        };
    }
}

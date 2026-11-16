using System;
using System.Collections.Generic;
using System.Linq;

namespace Morsoul.Engine
{
    /// <summary>
    /// Design-time audit (Algorithm.md User Story 2): runs every subset of a
    /// case's authored links E_KK through the solver so a designer can confirm
    /// exactly which boards unlock each conclusion. Authored subsets contain
    /// no invented links, so the gamma penalty is always zero here.
    ///
    /// Exhaustive enumeration is 2^|E_KK|; practical because authored-link
    /// counts are small (spec sample: 5 links -&gt; 32 subsets). The audit
    /// refuses graphs above <see cref="DefaultMaxAuthoredLinks"/> edges rather
    /// than silently running for minutes.
    ///
    /// Reported per conclusion: how many subsets unlock it and the minimal
    /// unlocking sets (the antichain — boards where removing any one link
    /// drops the unlock). For the complete conclusion, S* must be the only
    /// minimal set: any minimal set missing part of S* is a shortcut design
    /// error, and extra authored links are permitted supersets but remain
    /// progression errors when drawn.
    /// </summary>
    public static class GraphAudit
    {
        /// <summary>2^18 = 262k solver runs; larger graphs need a smarter audit.</summary>
        public const int DefaultMaxAuthoredLinks = 18;

        public sealed class ConclusionAudit
        {
            public string ConclusionId;
            public ConclusionTier Tier;
            public bool IsStarting;
            public int UnlockingSubsetCount;
            public List<List<DirectedEdge>> MinimalUnlockingSets = new List<List<DirectedEdge>>();
            /// <summary>Complete conclusion only: a minimal set missing at least one S* link.</summary>
            public bool HasSolutionShortcut;
        }

        public sealed class Report
        {
            public int AuthoredLinkCount;
            public long TotalSubsets;
            public long EvaluatedSubsets;
            public bool Skipped;
            /// <summary>True when every source's authored out-weights sum to &lt;= 1 and no
            /// conclusion has contradicting edges — the spec's monotonicity condition, meaning
            /// unlocking sets are pure up-sets (informational; the audit is exact either way).</summary>
            public bool Monotonic;
            public List<ConclusionAudit> Conclusions = new List<ConclusionAudit>();
            public List<string> Findings = new List<string>();
            public bool HasErrors => Findings.Any(f => f.StartsWith("ERROR", StringComparison.Ordinal));
        }

        public static Report Analyze(EvidenceGraph graph, int maxAuthoredLinks = DefaultMaxAuthoredLinks)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            var report = new Report();

            // Deterministic edge order so reports are stable across runs.
            var edges = graph.AuthoredEdges.Keys
                .OrderBy(e => e.sourceId, StringComparer.Ordinal)
                .ThenBy(e => e.targetId, StringComparer.Ordinal)
                .ToArray();
            report.AuthoredLinkCount = edges.Length;
            report.Monotonic = IsMonotone(graph);

            if (edges.Length > maxAuthoredLinks)
            {
                report.Skipped = true;
                report.Findings.Add(
                    $"ERROR authored link count {edges.Length} exceeds audit cap " +
                    $"{maxAuthoredLinks} (2^|E_KK| enumeration is exponential).");
                return report;
            }

            long total = 1L << edges.Length;
            report.TotalSubsets = total;
            report.EvaluatedSubsets = total;

            var unlockingMasks = new Dictionary<string, List<ulong>>(StringComparer.Ordinal);
            foreach (var c in graph.Conclusions)
                unlockingMasks[c.id] = new List<ulong>();

            string[] allClues = graph.ClueIds.ToArray();
            for (ulong mask = 0; mask < (ulong)total; mask++)
            {
                var links = new List<DirectedEdge>();
                for (int i = 0; i < edges.Length; i++)
                    if ((mask & (1UL << i)) != 0)
                        links.Add(edges[i]);

                var cert = DeductionEngine.Solve(graph, new DeductionInput
                {
                    VisibleClueIds = allClues,
                    CollectedClueIds = allClues,
                    Links = links.ToArray(),
                    SelectedConclusionId = graph.StartingConclusionId,
                });

                foreach (var c in graph.Conclusions)
                    if (DeductionEngine.IsConclusionUnlocked(graph, c,
                            cert.SupportByConclusion[c.id], links,
                            cert.PropagatedClueValues, cert.SolutionPropagatedClueValues, cert.InventedLinkCount))
                        unlockingMasks[c.id].Add(mask);
            }

            var solutionMask = MaskOf(edges, graph.IsSolutionLink);
            foreach (var c in graph.Conclusions)
            {
                var masks = unlockingMasks[c.id];
                var audit = new ConclusionAudit
                {
                    ConclusionId = c.id,
                    Tier = c.tier,
                    IsStarting = c.id == graph.StartingConclusionId,
                    UnlockingSubsetCount = masks.Count,
                };

                // Minimal sets: iterate by popcount; a mask is minimal iff no
                // already-accepted minimal mask is a proper subset of it.
                var mins = new List<ulong>();
                foreach (ulong m in masks.OrderBy(PopCount))
                {
                    bool coversMinimal = false;
                    foreach (ulong min in mins)
                        if ((min & ~m) == 0UL) { coversMinimal = true; break; }
                    if (coversMinimal) continue;
                    mins.Add(m);
                    audit.MinimalUnlockingSets.Add(
                        Enumerable.Range(0, edges.Length)
                            .Where(i => (m & (1UL << i)) != 0UL)
                            .Select(i => edges[i]).ToList());
                }

                if (c.id == graph.CompleteConclusionId)
                    audit.HasSolutionShortcut = mins.Any(m => (m & solutionMask) != solutionMask);

                report.Conclusions.Add(audit);
            }

            AddFindings(report, solutionMask, graph.IsSolutionLink);
            return report;
        }

        static void AddFindings(Report report, ulong solutionMask,
            Func<DirectedEdge, bool> isSolutionLink)
        {
            foreach (var audit in report.Conclusions)
            {
                if (audit.IsStarting) continue; // h0 is always unlocked by design.

                if (audit.UnlockingSubsetCount == 0)
                    report.Findings.Add(
                        $"WARN conclusion '{audit.ConclusionId}' is never unlocked by any " +
                        "authored-link subset — dead content or unreachable threshold.");
            }

            var complete = report.Conclusions
                .FirstOrDefault(c => c.Tier == ConclusionTier.Complete);
            if (complete == null) return;

            if (complete.HasSolutionShortcut)
            {
                int solutionCount = PopCount(solutionMask);
                var shortcuts = complete.MinimalUnlockingSets
                    .Where(set => set.Count(isSolutionLink) < solutionCount)
                    .Select(FormatSet).ToList();
                report.Findings.Add(
                    $"ERROR complete conclusion '{complete.ConclusionId}' has unintended " +
                    "unlock path(s) missing part of S*: " + string.Join("; ", shortcuts));
            }
            if (complete.MinimalUnlockingSets.Count != 1)
                report.Findings.Add(
                    $"WARN complete conclusion '{complete.ConclusionId}' has " +
                    $"{complete.MinimalUnlockingSets.Count} minimal unlocking sets; " +
                    "User Story 2 expects S* to be the only minimal unlock.");
        }

        static string FormatSet(List<DirectedEdge> set) =>
            set.Count == 0 ? "{}" : "{" + string.Join(", ", set.Select(e => $"{e.sourceId}->{e.targetId}")) + "}";

        static int PopCount(ulong m)
        {
            int n = 0;
            while (m != 0) { n += (int)(m & 1UL); m >>= 1; }
            return n;
        }

        static ulong MaskOf(DirectedEdge[] edges, Func<DirectedEdge, bool> predicate)
        {
            ulong mask = 0;
            for (int i = 0; i < edges.Length; i++)
                if (predicate(edges[i])) mask |= 1UL << i;
            return mask;
        }

        static bool IsMonotone(EvidenceGraph graph)
        {
            var outSums = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var pair in graph.AuthoredEdges)
            {
                outSums.TryGetValue(pair.Key.sourceId, out double s);
                outSums[pair.Key.sourceId] = s + pair.Value;
            }
            if (outSums.Values.Any(s => s > 1.0 + 1e-9)) return false;
            return graph.Conclusions.All(c =>
                c.contradictingEdges == null || c.contradictingEdges.Length == 0);
        }
    }
}

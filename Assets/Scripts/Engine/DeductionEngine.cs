using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Morsoul.Engine
{
    /// <summary>
    /// SOLVE-DEDUCTION (Algorithm.md Algorithm 1): seeded damped propagation
    /// on the evidence DAG in O(n + |L_valid|), plus per-conclusion scoring,
    /// reference normalisation, invented-link penalty, band and unlock gate.
    /// Pure and deterministic: no I/O, no engine calls, no scene state.
    /// </summary>
    public static class DeductionEngine
    {
        public static DeductionCertificate Solve(EvidenceGraph graph, DeductionInput input)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            var watch = Stopwatch.StartNew();

            var errors = ValidateInput(graph, input);
            if (errors.Count > 0)
            {
                watch.Stop();
                return DeductionCertificate.Invalid(errors, watch.Elapsed.TotalMilliseconds);
            }

            var p = graph.Parameters;
            var visible = new HashSet<string>(input.VisibleClueIds, StringComparer.Ordinal);
            var collected = new HashSet<string>(input.CollectedClueIds, StringComparer.Ordinal);

            // Duplicate links collapse to one before scoring.
            var drawn = new HashSet<DirectedEdge>(input.Links);

            // L_valid = L intersect E_KK; L_spur = L \ L_valid.
            var validEdges = new Dictionary<DirectedEdge, double>();
            int spurCount = 0;
            foreach (var edge in drawn)
            {
                if (graph.AuthoredEdges.TryGetValue(edge, out double w))
                    validEdges[edge] = w;
                else
                    spurCount++;
            }

            var x = DampedPropagation.Run(validEdges, graph.Reliability, p.alpha,
                out var kAct);
            if (x.Count < kAct.Count)
            {
                // Cycle guard: cannot trigger under C6 but must never hang or
                // silently mis-score if it somehow does.
                watch.Stop();
                return DeductionCertificate.Invalid(
                    new List<string> { "Drawn valid links contain a cycle." },
                    watch.Elapsed.TotalMilliseconds);
            }

            // rho(h') = max(0, Sup - lambda * Con); phi = clamp(rho/rho_ref) - gamma*|L_spur|.
            var phi = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var conclusion in graph.Conclusions)
            {
                double sup = Score(conclusion.supportingEdges, kAct, x);
                double con = Score(conclusion.contradictingEdges, kAct, x);
                double rho = Math.Max(0.0, sup - p.contradictionWeight * con);
                double normalised = graph.ReferenceSupport > 0
                    ? Math.Min(1.0, rho / graph.ReferenceSupport)
                    : 0.0;
                phi[conclusion.id] = Math.Max(0.0, normalised - p.inventedLinkPenalty * spurCount);
            }

            var selected = graph.GetConclusion(input.SelectedConclusionId);
            double selectedSupport = phi[input.SelectedConclusionId];
            var status = IsUnlocked(graph, selected, selectedSupport, validEdges.Keys)
                ? DeductionStatus.Ok
                : DeductionStatus.ConclusionLocked;

            watch.Stop();
            return new DeductionCertificate
            {
                Status = status,
                SelectedConclusionId = input.SelectedConclusionId,
                Verdict = selected.tier,
                SelectedSupport = selectedSupport,
                Band = BandOf(p, selectedSupport),
                SupportByConclusion = phi,
                PropagatedClueValues = x,
                InventedLinkCount = spurCount,
                UncollectedClueIds = DifferenceInOrder(input.VisibleClueIds, collected),
                UnlinkedClueIds = DifferenceInOrder(input.CollectedClueIds, kAct),
                RuntimeMs = watch.Elapsed.TotalMilliseconds,
            };
        }

        static double Score(WeightedClueRef[] edges, HashSet<string> kAct,
            IReadOnlyDictionary<string, double> x)
        {
            double total = 0;
            foreach (var e in edges ?? new WeightedClueRef[0])
                if (e != null && kAct.Contains(e.clueId) &&
                    x.TryGetValue(e.clueId, out double v))
                    total += e.weight * v;
            return total;
        }

        /// <summary>
        /// The unlock gate from Step 4: h0 is always unlocked; wrong/partial
        /// conclusions need phi at the low threshold; complete needs both high
        /// support and the structural S* ⊆ L_valid gate. Exposed for the board
        /// UI's conclusion selector and the design-time audit.
        /// </summary>
        public static bool IsConclusionUnlocked(EvidenceGraph graph, ConclusionDef conclusion,
            double phi, IEnumerable<DirectedEdge> drawnLinks)
            => IsUnlocked(graph, conclusion, phi, drawnLinks);

        static bool IsUnlocked(EvidenceGraph graph, ConclusionDef conclusion, double phi,
            IEnumerable<DirectedEdge> drawnLinks)
        {
            if (conclusion.id == graph.StartingConclusionId)
                return true; // h0 is always unlocked.
            double threshold = conclusion.tier == ConclusionTier.Complete
                ? graph.Parameters.highThreshold
                : graph.Parameters.lowThreshold;
            if (phi < threshold - 1e-9) return false;
            return conclusion.tier != ConclusionTier.Complete ||
                   graph.HasAllSolutionLinks(drawnLinks);
        }

        static SupportBand BandOf(SolverParameters p, double phi)
        {
            if (phi < p.lowThreshold) return SupportBand.Cold;
            if (phi < p.highThreshold) return SupportBand.Warm;
            return SupportBand.Hot;
        }

        /// <summary>Elements of orderedIds not present in set, preserving input order.</summary>
        static List<string> DifferenceInOrder(IEnumerable<string> orderedIds, HashSet<string> set)
        {
            var diff = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in orderedIds)
                if (!set.Contains(id) && seen.Add(id))
                    diff.Add(id);
            return diff;
        }

        /// <summary>
        /// valid(inp): ids exist in V; C subset of U subset of K; no
        /// self-links; link endpoints are collected clues; the selected
        /// conclusion exists; parameters in range.
        /// </summary>
        static List<string> ValidateInput(EvidenceGraph graph, DeductionInput input)
        {
            var errors = new List<string>();
            if (input == null)
            {
                errors.Add("DeductionInput is null.");
                return errors;
            }

            var visible = new HashSet<string>(StringComparer.Ordinal);
            var collected = new HashSet<string>(StringComparer.Ordinal);

            foreach (string id in input.VisibleClueIds ?? new string[0])
            {
                if (string.IsNullOrEmpty(id) || !graph.ContainsClue(id))
                    errors.Add($"Visible clue '{id}' is not in the case graph.");
                else
                    visible.Add(id);
            }
            foreach (string id in input.CollectedClueIds ?? new string[0])
            {
                if (string.IsNullOrEmpty(id) || !graph.ContainsClue(id))
                    errors.Add($"Collected clue '{id}' is not in the case graph.");
                else if (!visible.Contains(id))
                    errors.Add($"Collected clue '{id}' is not visible (C is not a subset of U).");
                else
                    collected.Add(id);
            }
            foreach (var edge in input.Links ?? new DirectedEdge[0])
            {
                if (edge.sourceId == edge.targetId)
                    errors.Add($"Link '{edge}' is a self-link.");
                else if (!collected.Contains(edge.sourceId) || !collected.Contains(edge.targetId))
                    errors.Add($"Link '{edge}' touches a clue that is not collected.");
            }

            if (string.IsNullOrEmpty(input.SelectedConclusionId) ||
                !graph.ContainsConclusion(input.SelectedConclusionId))
                errors.Add($"Selected conclusion '{input.SelectedConclusionId}' is not in the case graph.");

            var p = graph.Parameters;
            if (!(p.alpha > 0f && p.alpha < 1f))
                errors.Add($"alpha {p.alpha} is out of range (0, 1).");
            if (p.contradictionWeight < 0f || p.inventedLinkPenalty < 0f)
                errors.Add("Negative engine parameters are not allowed.");
            if (!(p.lowThreshold > 0f && p.lowThreshold < p.highThreshold && p.highThreshold <= 1f))
                errors.Add("Thresholds are out of range (0 < low < high <= 1 required).");

            return errors;
        }
    }
}

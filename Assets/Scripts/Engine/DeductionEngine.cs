using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Morsoul.Engine
{
    /// <summary>
    /// Pure, deterministic deduction engine. Evaluates the evidence DAG through
    /// damped propagation with evidence-based scoring, structural unlock gates,
    /// off-path leak prevention, and inward link checks.
    /// </summary>
    public static class DeductionEngine
    {
        // --- Public Entry Point -------------------------------------------------

        /// <summary>
        /// Solves the deduction board given the case graph and player board inputs.
        /// Pure and deterministic: no I/O, no engine calls, no scene dependencies.
        /// </summary>
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
            var collected = new HashSet<string>(input.CollectedClueIds, StringComparer.Ordinal);
            var drawn = new HashSet<DirectedEdge>(input.Links);

            // Seed dictionary: pins every clue to uniform 0.5 in PrunedMode, or uses authored reliabilities.
            IReadOnlyDictionary<string, double> seeds;
            if (p.PrunedMode)
            {
                var uniform = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (var cid in graph.ClueIds)
                    uniform[cid] = 0.5;
                seeds = uniform;
            }
            else
            {
                seeds = graph.Reliability;
            }

            // Separate valid authored edges from invented (spur) edges.
            var validEdges = new Dictionary<DirectedEdge, double>();
            int spurCount = 0;
            foreach (var edge in drawn)
            {
                if (graph.AuthoredEdges.TryGetValue(edge, out double w))
                    validEdges[edge] = w;
                else
                    spurCount++;
            }

            // Damped propagation on valid edges.
            var x = DampedPropagation.Run(validEdges, seeds, p.alpha, out var kAct);
            if (x.Count < kAct.Count)
            {
                watch.Stop();
                return DeductionCertificate.Invalid(
                    new List<string> { "Drawn valid links contain a cycle." },
                    watch.Elapsed.TotalMilliseconds);
            }

            // Propagate over solution edges only to feed the inward link check.
            var solutionEdges = new Dictionary<DirectedEdge, double>();
            foreach (var kvp in graph.AuthoredEdges)
            {
                if (graph.SolutionEdges.Contains(kvp.Key))
                    solutionEdges[kvp.Key] = kvp.Value;
            }

            IReadOnlyDictionary<string, double> xSolution;
            try
            {
                xSolution = DampedPropagation.Run(solutionEdges, seeds, p.alpha, out _);
            }
            catch (Exception)
            {
                xSolution = x; // Degenerate graph fallback: gate fails closed.
            }

            // Evidence-based scoring for all conclusions.
            var phi = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var conclusion in graph.Conclusions)
            {
                double sup = Score(conclusion.supportingEdges, x, seeds, p.alpha);
                double con = Score(conclusion.contradictingEdges, x, seeds, p.alpha);
                double rho = Math.Max(0.0, sup - p.contradictionWeight * con);
                double normalised = graph.ReferenceSupport > 0
                    ? Math.Min(1.0, rho / graph.ReferenceSupport)
                    : 0.0;
                phi[conclusion.id] = Math.Max(0.0, normalised - p.inventedLinkPenalty * spurCount);
            }

            // Evaluate the unlock gate for every conclusion to determine the highest unlocked tier.
            ConclusionTier resolvedTier = ConclusionTier.Incorrect;
            foreach (var c in graph.Conclusions)
            {
                if (IsUnlocked(graph, c, phi[c.id], validEdges.Keys, x, xSolution, seeds, spurCount)
                    && TierRank(c.tier) > TierRank(resolvedTier))
                {
                    resolvedTier = c.tier;
                }
            }

            var selected = graph.GetConclusion(input.SelectedConclusionId);
            double selectedSupport = phi[input.SelectedConclusionId];
            var status = IsUnlocked(graph, selected, selectedSupport, validEdges.Keys, x, xSolution, seeds, spurCount)
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

                ResolvedTier = resolvedTier,
                SolutionPropagatedClueValues = xSolution,
            };
        }

        // --- Scoring ------------------------------------------------------------

        static double Score(
            WeightedClueRef[] edges,
            IReadOnlyDictionary<string, double> x,
            IReadOnlyDictionary<string, double> seeds,
            float alpha)
        {
            double total = 0;
            foreach (var e in edges ?? Array.Empty<WeightedClueRef>())
            {
                if (e == null) continue;
                total += e.weight * Evidence(x, e.clueId, seeds, alpha);
            }
            return total;
        }

        static double Evidence(
            IReadOnlyDictionary<string, double> x,
            string clueId,
            IReadOnlyDictionary<string, double> seeds,
            double alpha)
        {
            if (!x.TryGetValue(clueId, out double v)) return 0.0;
            if (!seeds.TryGetValue(clueId, out double r)) r = 0.5;
            return Math.Max(0.0, v - (1.0 - alpha) * r);
        }

        // --- Unlock Gate --------------------------------------------------------

        /// <summary>
        /// Public wrapper for board UI and audit systems using precomputed propagation state.
        /// </summary>
        public static bool IsConclusionUnlocked(
            EvidenceGraph graph,
            ConclusionDef conclusion,
            double phi,
            IEnumerable<DirectedEdge> drawnLinks,
            IReadOnlyDictionary<string, double> x,
            IReadOnlyDictionary<string, double> xSolution,
            int spurCount)
            => IsUnlocked(graph, conclusion, phi, drawnLinks, x, xSolution,
                          graph.Parameters.PrunedMode ? UniformSeeds(graph) : graph.Reliability,
                          spurCount);

        /// <summary>
        /// Backward-compatible overload for callers without precomputed propagation state.
        /// </summary>
        public static bool IsConclusionUnlocked(
            EvidenceGraph graph,
            ConclusionDef conclusion,
            double phi,
            IEnumerable<DirectedEdge> drawnLinks)
        {
            if (graph == null || conclusion == null) return false;
            if (conclusion.id == graph.StartingConclusionId) return true;

            var drawnList = (drawnLinks as DirectedEdge[]) ?? drawnLinks?.ToArray() ?? Array.Empty<DirectedEdge>();
            var p = graph.Parameters;
            IReadOnlyDictionary<string, double> seeds = p.PrunedMode ? UniformSeeds(graph) : graph.Reliability;

            var validEdges = new Dictionary<DirectedEdge, double>();
            int spurCount = 0;
            foreach (var edge in drawnList)
            {
                if (graph.AuthoredEdges.TryGetValue(edge, out double w))
                    validEdges[edge] = w;
                else
                    spurCount++;
            }

            var x = DampedPropagation.Run(validEdges, seeds, p.alpha, out _);
            var solutionEdges = new Dictionary<DirectedEdge, double>();
            foreach (var kvp in graph.AuthoredEdges)
            {
                if (graph.SolutionEdges.Contains(kvp.Key))
                    solutionEdges[kvp.Key] = kvp.Value;
            }

            IReadOnlyDictionary<string, double> xSolution;
            try
            {
                xSolution = DampedPropagation.Run(solutionEdges, seeds, p.alpha, out _);
            }
            catch (Exception)
            {
                xSolution = x;
            }

            return IsUnlocked(graph, conclusion, phi, validEdges.Keys, x, xSolution, seeds, spurCount);
        }

        static bool IsUnlocked(
            EvidenceGraph graph,
            ConclusionDef conclusion,
            double phi,
            IEnumerable<DirectedEdge> drawnLinks,
            IReadOnlyDictionary<string, double> x,
            IReadOnlyDictionary<string, double> xSolution,
            IReadOnlyDictionary<string, double> seeds,
            int spurCount)
        {
            if (conclusion.id == graph.StartingConclusionId)
                return true; // h0 is always unlocked.

            var p = graph.Parameters;
            double threshold = conclusion.tier == ConclusionTier.Complete
                ? p.highThreshold
                : p.lowThreshold;

            // Complete Tier Gate
            if (conclusion.tier == ConclusionTier.Complete)
            {
                if (phi < threshold - 1e-9) return false;

                // Structural gate: player board must contain all solution edges.
                if (!graph.HasAllSolutionLinks(drawnLinks)) return false;

                // No spur (invented) links permitted for Complete.
                if (spurCount > 0) return false;

                var targetId = TargetClueOf(conclusion);
                if (string.IsNullOrEmpty(targetId)) return false;

                // Leak check: no off-path clues may receive evidence credit.
                var path = graph.SolutionPathClues();
                double leak = 0.0;
                foreach (var cid in graph.ClueIds)
                {
                    if (path.Contains(cid)) continue;
                    leak += Evidence(x, cid, seeds, p.alpha);
                }
                if (leak > 1e-12) return false;

                // Inward check: no extra credit at target(s) from non-solution links.
                if (!string.IsNullOrEmpty(conclusion.targetClueId))
                {
                    double eT = Evidence(x, conclusion.targetClueId, seeds, p.alpha);
                    double eTs = Evidence(xSolution, conclusion.targetClueId, seeds, p.alpha);
                    if (eT - eTs > 1e-12) return false;
                }
                else if (conclusion.supportingEdges != null)
                {
                    foreach (var sup in conclusion.supportingEdges)
                    {
                        if (sup == null || string.IsNullOrEmpty(sup.clueId)) continue;
                        double eT = Evidence(x, sup.clueId, seeds, p.alpha);
                        double eTs = Evidence(xSolution, sup.clueId, seeds, p.alpha);
                        if (eT - eTs > 1e-12) return false;
                    }
                }

                return true;
            }

            // Partial Tier Gate: rival rule
            if (!string.IsNullOrEmpty(p.RivalClueId))
            {
                var targetId = TargetClueOf(conclusion);
                if (!string.IsNullOrEmpty(targetId))
                {
                    double eRival = Evidence(x, p.RivalClueId, seeds, p.alpha);
                    double eTarget = Evidence(x, targetId, seeds, p.alpha);
                    if (eRival >= eTarget - 1e-9) return false;
                }
            }

            return phi >= threshold - 1e-9;
        }

        static IReadOnlyDictionary<string, double> UniformSeeds(EvidenceGraph graph) =>
            EvidenceGraph.UniformSeeds(graph);

        static string TargetClueOf(ConclusionDef conclusion)
        {
            if (conclusion == null) return null;
            if (!string.IsNullOrEmpty(conclusion.targetClueId))
                return conclusion.targetClueId;
            var edges = conclusion.supportingEdges;
            if (edges == null || edges.Length == 0) return null;
            return edges[0]?.clueId;
        }

        // --- Helpers ------------------------------------------------------------

        static SupportBand BandOf(SolverParameters p, double phi)
        {
            if (phi < p.lowThreshold) return SupportBand.Cold;
            if (phi < p.highThreshold) return SupportBand.Warm;
            return SupportBand.Hot;
        }

        static List<string> DifferenceInOrder(IEnumerable<string> orderedIds, HashSet<string> set)
        {
            var diff = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in orderedIds ?? Array.Empty<string>())
            {
                if (!set.Contains(id) && seen.Add(id))
                    diff.Add(id);
            }
            return diff;
        }

        static int TierRank(ConclusionTier t)
        {
            if (t == ConclusionTier.Complete) return 3;
            if (t == ConclusionTier.Partial) return 2;
            if (t == ConclusionTier.Incorrect || t == ConclusionTier.Wrong) return 1;
            return 0;
        }

        // --- Input Validation ---------------------------------------------------

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

            foreach (string id in input.VisibleClueIds ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(id) || !graph.ContainsClue(id))
                    errors.Add($"Visible clue '{id}' is not in the case graph.");
                else
                    visible.Add(id);
            }
            foreach (string id in input.CollectedClueIds ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(id) || !graph.ContainsClue(id))
                    errors.Add($"Collected clue '{id}' is not in the case graph.");
                else if (!visible.Contains(id))
                    errors.Add($"Collected clue '{id}' is not visible (C is not a subset of U).");
                else
                    collected.Add(id);
            }
            foreach (var edge in input.Links ?? Array.Empty<DirectedEdge>())
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

            if (!string.IsNullOrEmpty(p.RivalClueId) && !graph.ContainsClue(p.RivalClueId))
                errors.Add($"RivalClueId '{p.RivalClueId}' is not in the case graph.");

            return errors;
        }
    }
}

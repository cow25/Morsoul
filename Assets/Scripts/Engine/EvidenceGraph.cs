using System;
using System.Collections.Generic;
using System.Linq;

namespace Morsoul.Engine
{
    /// <summary>
    /// Immutable runtime evidence DAG for one case: clues K, authored
    /// clue-clue links E_KK with weights, solution subset S*, unlock
    /// dependencies R, conclusions H with support/contradiction edges,
    /// engine parameters, the cached reference support rho_ref and the
    /// deterministic topological order pi of the clues touched by S*.
    /// Built and fully validated once per case (Algorithm.md C1-C7);
    /// cyclic or malformed authored data is rejected here, never mid-solve.
    /// </summary>
    public sealed class EvidenceGraph
    {
        public const int MaxClues = 10;
        public const int MaxConclusions = 5;

        readonly HashSet<string> _clueIds;
        readonly Dictionary<string, double> _reliability;
        readonly Dictionary<DirectedEdge, double> _authoredEdges;
        readonly Dictionary<DirectedEdge, string> _edgeRationales;
        readonly HashSet<DirectedEdge> _solutionEdges;
        readonly Dictionary<DirectedEdge, double> _unlockDependencies;
        readonly Dictionary<string, ConclusionDef> _conclusionsById;
        readonly Dictionary<string, List<DirectedEdge>> _incidentSolution;
        readonly List<string> _clueOrder;
        readonly List<ConclusionDef> _conclusionOrder;
        readonly Dictionary<string, double> _referenceEvidence;

        EvidenceGraph()
        {
            _clueIds = new HashSet<string>(StringComparer.Ordinal);
            _reliability = new Dictionary<string, double>(StringComparer.Ordinal);
            _authoredEdges = new Dictionary<DirectedEdge, double>();
            _edgeRationales = new Dictionary<DirectedEdge, string>();
            _solutionEdges = new HashSet<DirectedEdge>();
            _unlockDependencies = new Dictionary<DirectedEdge, double>();
            _conclusionsById = new Dictionary<string, ConclusionDef>(StringComparer.Ordinal);
            _incidentSolution = new Dictionary<string, List<DirectedEdge>>(StringComparer.Ordinal);
            _clueOrder = new List<string>();
            _conclusionOrder = new List<ConclusionDef>();
            _referenceEvidence = new Dictionary<string, double>(StringComparer.Ordinal);
        }

        public int CaseId { get; private set; }
        public SolverParameters Parameters { get; private set; }
        public string StartingConclusionId { get; private set; }
        public string CompleteConclusionId { get; private set; }
        public double ReferenceSupport { get; private set; }
        public IReadOnlyDictionary<string, double> ReferenceEvidence => _referenceEvidence;
        public IReadOnlyList<string> ClueIds => _clueOrder;
        public IReadOnlyList<ConclusionDef> Conclusions => _conclusionOrder;
        public IReadOnlyList<string> SolutionOrder { get; private set; }

        /// <summary>Per-conclusion theta_low (falls back to Parameters.lowThreshold when <= 0).</summary>
        public double GetLowThreshold(ConclusionDef conclusion) =>
            conclusion != null && conclusion.lowThreshold > 0f
                ? conclusion.lowThreshold
                : Parameters.lowThreshold;

        public bool ContainsClue(string id) => id != null && _clueIds.Contains(id);
        public bool ContainsConclusion(string id) => id != null && _conclusionsById.ContainsKey(id);
        public bool IsAuthoredLink(DirectedEdge edge) => _authoredEdges.ContainsKey(edge);
        public bool IsSolutionLink(DirectedEdge edge) => _solutionEdges.Contains(edge);
        public IReadOnlyCollection<DirectedEdge> SolutionEdges => _solutionEdges;

        /// <summary>Returns the authored post-draw rationale for an edge, when present.</summary>
        public bool TryGetEdgeRationale(DirectedEdge edge, out string rationale) =>
            _edgeRationales.TryGetValue(edge, out rationale) && !string.IsNullOrWhiteSpace(rationale);

        /// <summary>True when every authored solution link is present in L.</summary>
        public bool HasAllSolutionLinks(IEnumerable<DirectedEdge> drawnLinks)
        {
            if (drawnLinks == null) return false;
            var drawn = new HashSet<DirectedEdge>(drawnLinks);
            return _solutionEdges.IsSubsetOf(drawn);
        }

        /// <summary>
        /// Set of clues appearing in any solution edge.
        /// Used by the leak check to distinguish on-path from off-path.
        /// </summary>
        public HashSet<string> SolutionPathClues()
        {
            var path = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in SolutionEdges)
            {
                path.Add(e.sourceId);
                path.Add(e.targetId);
            }
            return path;
        }

        public ConclusionDef GetConclusion(string id) =>
            id != null && _conclusionsById.TryGetValue(id, out var def) ? def : null;

        /// <summary>S* links incident to a clue (either direction); empty when none.</summary>
        public IReadOnlyList<DirectedEdge> IncidentSolutionLinks(string clueId) =>
            clueId != null && _incidentSolution.TryGetValue(clueId, out var list)
                ? list
                : (IReadOnlyList<DirectedEdge>)Array.Empty<DirectedEdge>();

        /// <summary>
        /// next(L): highlights the next unlinked clue continuing an existing
        /// drawn S* connection when one exists (forward tip first, then branch
        /// or upstream neighbor), falling back to the first clue in pi with an
        /// undrawn incident S* edge. Returns null when all S* links are drawn.
        /// </summary>
        public string NextUnfinishedSolutionClue(IEnumerable<DirectedEdge> drawnLinks)
        {
            var drawn = drawnLinks == null
                ? new HashSet<DirectedEdge>()
                : new HashSet<DirectedEdge>(drawnLinks);

            var drawnTargets = new HashSet<string>(StringComparer.Ordinal);
            var drawnNodes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var link in drawn)
            {
                if (!_solutionEdges.Contains(link)) continue;
                drawnNodes.Add(link.sourceId);
                drawnNodes.Add(link.targetId);
                drawnTargets.Add(link.targetId);
            }

            if (drawnNodes.Count > 0)
            {
                // 1. Forward continuation from the tip of an existing S* connection.
                foreach (string clue in SolutionOrder)
                {
                    if (!drawnTargets.Contains(clue)) continue;
                    foreach (var edge in IncidentSolutionLinks(clue))
                        if (edge.sourceId == clue && !drawn.Contains(edge))
                            return edge.targetId;
                }

                // 2. Branching outgoing continuation from any connected S* clue.
                foreach (string clue in SolutionOrder)
                {
                    if (!drawnNodes.Contains(clue)) continue;
                    foreach (var edge in IncidentSolutionLinks(clue))
                        if (edge.sourceId == clue && !drawn.Contains(edge))
                            return edge.targetId;
                }

                // 3. Upstream continuation into a mid-chain drawn S* connection.
                foreach (string clue in SolutionOrder)
                {
                    if (!drawnNodes.Contains(clue)) continue;
                    foreach (var edge in IncidentSolutionLinks(clue))
                        if (edge.targetId == clue && !drawn.Contains(edge))
                            return edge.sourceId;
                }
            }

            // Fallback: first clue in pi that still has an undrawn incident S* edge.
            foreach (string clue in SolutionOrder)
            {
                foreach (var edge in IncidentSolutionLinks(clue))
                    if (!drawn.Contains(edge))
                        return clue;
            }
            return null;
        }

        /// <summary>last: the final clue in pi; used by hint tier 3.</summary>
        public string FinalSolutionClue =>
            SolutionOrder != null && SolutionOrder.Count > 0 ? SolutionOrder[SolutionOrder.Count - 1] : null;

        public enum WrongLinkHintKind
        {
            Unrelated,
            Reversed,
            MissingIntermediate,
        }

        /// <summary>
        /// Classifies why an off-S* link failed so the board can give near-miss feedback
        /// without spoiling exact answers.
        /// </summary>
        public WrongLinkHintKind ClassifyWrongLink(DirectedEdge attempted, out string intermediateClueId)
        {
            intermediateClueId = null;
            if (string.IsNullOrEmpty(attempted.sourceId) || string.IsNullOrEmpty(attempted.targetId))
                return WrongLinkHintKind.Unrelated;

            if (_solutionEdges.Contains(new DirectedEdge(attempted.targetId, attempted.sourceId)))
                return WrongLinkHintKind.Reversed;

            foreach (var edge in IncidentSolutionLinks(attempted.sourceId))
            {
                if (edge.sourceId != attempted.sourceId) continue;
                string mid = edge.targetId;
                if (_solutionEdges.Contains(new DirectedEdge(mid, attempted.targetId)))
                {
                    intermediateClueId = mid;
                    return WrongLinkHintKind.MissingIntermediate;
                }
            }

            return WrongLinkHintKind.Unrelated;
        }

        internal bool TryGetReliability(string id, out double value) =>
            _reliability.TryGetValue(id, out value);

        internal IReadOnlyDictionary<string, double> Reliability => _reliability;

        internal IReadOnlyDictionary<DirectedEdge, double> AuthoredEdges => _authoredEdges;
        internal IReadOnlyDictionary<string, ConclusionDef> ConclusionsById => _conclusionsById;

        /// <summary>
        /// Validates a definition against the spec's boundary conditions and
        /// authoring rules, then builds the immutable graph. Returns the
        /// error list; graph is null when it is non-empty.
        /// </summary>
        public static List<string> Build(EvidenceGraphDef def, out EvidenceGraph graph)
        {
            var errors = new List<string>();
            graph = null;
            if (def == null)
            {
                errors.Add("EvidenceGraphDef is null.");
                return errors;
            }

            var g = new EvidenceGraph();
            g.CaseId = def.CaseId;
            g.Parameters = def.Parameters != null ? def.Parameters.Clone() : new SolverParameters();
            g.StartingConclusionId = def.StartingWrongConclusionId;
            g.CompleteConclusionId = def.CompleteConclusionId;

            ValidateClues(def, g, errors);
            ValidateParameters(g.Parameters, errors);
            ValidateConclusions(def, g, errors);
            ValidateAuthoredEdges(def, g, errors);
            ValidateSolutionAndDependencies(def, g, errors);

            if (errors.Count == 0)
            {
                // C6: E_KK acyclic (rejected here, guaranteed for every solve).
                if (!TopologicalSort(_endpointSet(g._authoredEdges.Keys), g._authoredEdges.Keys,
                        out var authoredOrder))
                    errors.Add("C6: authored clue edges E_KK contain a cycle.");

                // C1: R acyclic.
                if (!TopologicalSort(_endpointSet(g._unlockDependencies.Keys), g._unlockDependencies.Keys,
                        out _))
                    errors.Add("C1: unlock dependencies R contain a cycle.");

                if (authoredOrder != null)
                {
                    g.ReferenceSupport = ComputeReferenceSupport(g);
                    g.SolutionOrder = ComputeSolutionOrder(g);
                }
            }

            if (errors.Count == 0)
                ValidateCachedValues(def, g, errors);

            // C7: with C = K and L = S* the complete conclusion must unlock.
            if (errors.Count == 0)
            {
                var cert = DeductionEngine.Solve(g, new DeductionInput
                {
                    VisibleClueIds = g._clueOrder.ToArray(),
                    CollectedClueIds = g._clueOrder.ToArray(),
                    Links = g._solutionEdges.ToArray(),
                    SelectedConclusionId = g.CompleteConclusionId,
                });
                if (cert.Status != DeductionStatus.Ok)
                    errors.Add($"C7: solving the authored solution returned {cert.Status}, expected Ok.");
                else if (cert.SelectedSupport < g.Parameters.highThreshold - 1e-9)
                    errors.Add($"C7: S* yields phi(complete)={cert.SelectedSupport:F4} below " +
                               $"highThreshold {g.Parameters.highThreshold:F4}; the authored solution cannot win.");
            }

            if (errors.Count == 0)
                graph = g;
            return errors;
        }

        static HashSet<string> _endpointSet(IEnumerable<DirectedEdge> edges)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in edges)
            {
                set.Add(e.sourceId);
                set.Add(e.targetId);
            }
            return set;
        }

        static void ValidateClues(EvidenceGraphDef def, EvidenceGraph g, List<string> errors)
        {
            var ids = def.ClueIds ?? new string[0];
            if (ids.Length == 0)
                errors.Add("Evidence graph has no clues.");
            if (ids.Length > MaxClues)
                errors.Add($"Evidence graph has {ids.Length} clues; the limit is {MaxClues}.");
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.IsNullOrEmpty(ids[i]))
                {
                    errors.Add($"Clue id at index {i} is empty.");
                    continue;
                }
                if (!g._clueIds.Add(ids[i]))
                    errors.Add($"Duplicate clue id '{ids[i]}'.");
                else
                    g._clueOrder.Add(ids[i]);
            }

            // r : K -> (0, 1]; every clue exactly once.
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rel in def.Reliabilities ?? new ClueReliability[0])
            {
                if (rel == null)
                {
                    errors.Add("Null reliability entry.");
                    continue;
                }
                if (string.IsNullOrEmpty(rel.clueId) || !g._clueIds.Contains(rel.clueId))
                    errors.Add($"Reliability references unknown clue '{rel.clueId}'.");
                else if (!seen.Add(rel.clueId))
                    errors.Add($"Clue '{rel.clueId}' has more than one reliability entry.");
                else
                    g._reliability[rel.clueId] = rel.value;
                if (rel.value <= 0f || rel.value > 1f)
                    errors.Add($"Reliability for '{rel.clueId}' is {rel.value}; must be in (0, 1].");
            }
            foreach (string id in g._clueOrder)
                if (!seen.Contains(id))
                    errors.Add($"Clue '{id}' has no reliability entry.");
        }

        static void ValidateParameters(SolverParameters p, List<string> errors)
        {
            if (!(p.alpha > 0f && p.alpha < 1f))
                errors.Add($"alpha {p.alpha} must be in (0, 1).");
            if (p.contradictionWeight < 0f)
                errors.Add($"contradictionWeight (lambda) {p.contradictionWeight} must be >= 0.");
            if (p.inventedLinkPenalty < 0f)
                errors.Add($"inventedLinkPenalty (gamma) {p.inventedLinkPenalty} must be >= 0.");
            if (!(p.lowThreshold > 0f && p.lowThreshold < p.highThreshold && p.highThreshold <= 1f))
                errors.Add($"Thresholds must satisfy 0 < low ({p.lowThreshold}) < high ({p.highThreshold}) <= 1.");
        }

        static void ValidateConclusions(EvidenceGraphDef def, EvidenceGraph g, List<string> errors)
        {
            var list = def.Conclusions ?? new ConclusionDef[0];
            if (list.Length == 0)
                errors.Add("Evidence graph has no conclusions.");
            if (list.Length > MaxConclusions)
                errors.Add($"Evidence graph has {list.Length} conclusions; the limit is {MaxConclusions}.");
            for (int i = 0; i < list.Length; i++)
            {
                var c = list[i];
                string name = c == null ? $"index {i}" : (c.id ?? $"index {i}");
                if (c == null)
                {
                    errors.Add($"Conclusion at index {i} is null.");
                    continue;
                }
                if (string.IsNullOrEmpty(c.id))
                    errors.Add($"Conclusion at index {i} has an empty id.");
                else if (!g._conclusionsById.TryAdd(c.id, c))
                    errors.Add($"Duplicate conclusion id '{c.id}'.");
                else
                    g._conclusionOrder.Add(c);
                if (string.IsNullOrEmpty(c.text))
                    errors.Add($"Conclusion '{name}' has empty text.");
                if (!string.IsNullOrEmpty(c.targetClueId) && !g._clueIds.Contains(c.targetClueId))
                    errors.Add($"Conclusion '{name}' targetClueId '{c.targetClueId}' is not in the case graph.");
                if (c.lowThreshold < 0f || (c.lowThreshold > 0f && c.lowThreshold >= g.Parameters.highThreshold))
                    errors.Add($"Conclusion '{name}' lowThreshold {c.lowThreshold} must be in (0, highThreshold).");

                var support = c.supportingEdges ?? new WeightedClueRef[0];
                var contradict = c.contradictingEdges ?? new WeightedClueRef[0];
                var supportIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var e in support)
                    ValidateClueRef(e, name, "supporting", g, errors, supportIds);
                var contradictIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var e in contradict)
                    ValidateClueRef(e, name, "contradicting", g, errors, contradictIds);
                // C4: E+ and E- disjoint per conclusion.
                supportIds.IntersectWith(contradictIds);
                foreach (string id in supportIds)
                    errors.Add($"C4: clue '{id}' both supports and contradicts conclusion '{name}'.");
            }

            if (string.IsNullOrEmpty(def.StartingWrongConclusionId))
                errors.Add("startingWrongConclusionId is empty.");
            else if (!g._conclusionsById.TryGetValue(def.StartingWrongConclusionId, out var start))
                errors.Add($"startingWrongConclusionId '{def.StartingWrongConclusionId}' is not a conclusion.");
            else if (start.tier != ConclusionTier.Wrong)
                errors.Add($"Starting conclusion '{def.StartingWrongConclusionId}' must have tier Wrong.");

            if (string.IsNullOrEmpty(def.CompleteConclusionId))
                errors.Add("completeConclusionId is empty.");
            else if (!g._conclusionsById.TryGetValue(def.CompleteConclusionId, out var complete))
                errors.Add($"completeConclusionId '{def.CompleteConclusionId}' is not a conclusion.");
            else
            {
                if (complete.tier != ConclusionTier.Complete)
                    errors.Add($"Complete conclusion '{def.CompleteConclusionId}' must have tier Complete.");
                // C3: h* has at least one supporting edge.
                if ((complete.supportingEdges == null || complete.supportingEdges.Length == 0))
                    errors.Add($"C3: complete conclusion '{complete.id}' has no supporting edge.");
            }
        }

        static void ValidateClueRef(WeightedClueRef edge, string conclusionId, string kind,
            EvidenceGraph g, List<string> errors, HashSet<string> seen)
        {
            if (edge == null)
            {
                errors.Add($"Conclusion '{conclusionId}' has a null {kind} edge.");
                return;
            }
            if (string.IsNullOrEmpty(edge.clueId) || !g._clueIds.Contains(edge.clueId))
                errors.Add($"Conclusion '{conclusionId}' {kind} edge references unknown clue '{edge.clueId}'.");
            else if (!seen.Add(edge.clueId))
                errors.Add($"Conclusion '{conclusionId}' has duplicate {kind} edges on clue '{edge.clueId}'.");
            if (edge.weight <= 0f || edge.weight > 1f)
                errors.Add($"Conclusion '{conclusionId}' {kind} edge on '{edge.clueId}' " +
                           $"has weight {edge.weight}; must be in (0, 1].");
        }

        static void ValidateAuthoredEdges(EvidenceGraphDef def, EvidenceGraph g, List<string> errors)
        {
            var edges = def.AuthoredClueEdges ?? new WeightedClueEdge[0];
            var touched = new HashSet<string>(StringComparer.Ordinal);
            var outSum = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var e in edges)
            {
                if (e == null)
                {
                    errors.Add("Null authored clue edge.");
                    continue;
                }
                string name = e.sourceId + "->" + e.targetId;
                if (string.IsNullOrEmpty(e.sourceId) || !g._clueIds.Contains(e.sourceId) ||
                    string.IsNullOrEmpty(e.targetId) || !g._clueIds.Contains(e.targetId))
                    errors.Add($"Authored edge '{name}' references an unknown clue.");
                else if (e.sourceId == e.targetId)
                    errors.Add($"Authored edge '{name}' is a self-link.");
                else if (!g._authoredEdges.TryAdd(new DirectedEdge(e.sourceId, e.targetId), e.weight))
                    errors.Add($"Duplicate authored edge '{name}'.");
                else
                {
                    if (!string.IsNullOrWhiteSpace(e.rationale))
                        g._edgeRationales[new DirectedEdge(e.sourceId, e.targetId)] = e.rationale.Trim();
                    touched.Add(e.sourceId);
                    touched.Add(e.targetId);
                    outSum.TryGetValue(e.sourceId, out double sum);
                    outSum[e.sourceId] = sum + e.weight;
                }
                if (e.weight <= 0f || e.weight > 1f)
                    errors.Add($"Authored edge '{name}' has weight {e.weight}; must be in (0, 1].");
            }

            // C2: every clue has at least one clue-clue edge.
            foreach (string id in g._clueOrder)
                if (!touched.Contains(id))
                    errors.Add($"C2: clue '{id}' has no authored clue-clue edge.");

            // C5: out-weight normalisation bound (holds by construction; kept
            // as a documented check).
            foreach (var pair in outSum)
            {
                double normalised = pair.Value / Math.Max(1.0, pair.Value);
                if (normalised > 1.0 + 1e-9)
                    errors.Add($"C5: clue '{pair.Key}' out-weight normalises to {normalised:F4} > 1.");
            }
        }

        static void ValidateSolutionAndDependencies(EvidenceGraphDef def, EvidenceGraph g, List<string> errors)
        {
            var solution = def.SolutionEdges ?? new DirectedEdge[0];
            if (solution.Length == 0)
                errors.Add("Evidence graph has no solution edges (S*).");
            foreach (var e in solution)
            {
                if (!g._authoredEdges.ContainsKey(e))
                    errors.Add($"Solution edge '{e}' is not an authored clue edge.");
                else
                    g._solutionEdges.Add(e);
            }
            foreach (var edge in g._solutionEdges)
                foreach (string id in new[] { edge.sourceId, edge.targetId })
                {
                    if (!g._incidentSolution.TryGetValue(id, out var list))
                        g._incidentSolution[id] = list = new List<DirectedEdge>();
                    list.Add(edge);
                }

            foreach (var e in def.UnlockDependencies ?? new DirectedEdge[0])
            {
                if (string.IsNullOrEmpty(e.sourceId) || !g._clueIds.Contains(e.sourceId) ||
                    string.IsNullOrEmpty(e.targetId) || !g._clueIds.Contains(e.targetId))
                    errors.Add($"Unlock dependency '{e}' references an unknown clue.");
                else if (e.sourceId == e.targetId)
                    errors.Add($"Unlock dependency '{e}' is a self-link.");
                else
                    g._unlockDependencies[e] = 1.0;
            }
        }

        static void ValidateCachedValues(EvidenceGraphDef def, EvidenceGraph g, List<string> errors)
        {
            var supplied = def.SolutionTopologicalOrder ?? new string[0];
            if (supplied.Length > 0 &&
                !supplied.SequenceEqual(g.SolutionOrder, StringComparer.Ordinal))
                errors.Add("solutionTopologicalOrder does not match the computed deterministic order; " +
                           "recache the asset.");

            if (def.CachedReferenceSupport > 0f &&
                Math.Abs(def.CachedReferenceSupport - g.ReferenceSupport) > 1e-4)
            {
                errors.Add($"cachedReferenceSupport ({def.CachedReferenceSupport:F6}) does not match " +
                           $"computed reference support ({g.ReferenceSupport:F6}); recache the asset.");
            }
        }

        internal static IReadOnlyDictionary<string, double> UniformSeeds(EvidenceGraph graph)
        {
            var d = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var cid in graph.ClueIds) d[cid] = 0.5;
            return d;
        }

        /// <summary>
        /// In PrunedMode: computes per-clue reference evidence over S* only (PrunedSdp.ScoreMulti),
        /// and returns refEv at the complete target clue. Otherwise uses legacy E_KK + supporting/contradicting edges.
        /// </summary>
        static double ComputeReferenceSupport(EvidenceGraph g)
        {
            var p = g.Parameters;
            IReadOnlyDictionary<string, double> seeds = p.PrunedMode
                ? UniformSeeds(g)
                : g._reliability;

            // Always populate per-clue reference evidence over S* only (used by PrunedSdp.ScoreMulti).
            var solEdges = new Dictionary<DirectedEdge, double>();
            foreach (var e in g._solutionEdges)
            {
                if (g._authoredEdges.TryGetValue(e, out double w))
                    solEdges[e] = w;
            }
            var xSol = DampedPropagation.Run(solEdges, seeds, p.alpha, out _);
            g._referenceEvidence.Clear();
            foreach (var cid in g._clueOrder)
            {
                double r = seeds.TryGetValue(cid, out double rv) ? rv : 0.5;
                double v = xSol.TryGetValue(cid, out double xv) ? xv : (1.0 - p.alpha) * r;
                g._referenceEvidence[cid] = Math.Max(0.0, v - (1.0 - p.alpha) * r);
            }

            var complete = g._conclusionsById[g.CompleteConclusionId];
            if (p.PrunedMode)
            {
                string targetId = !string.IsNullOrEmpty(complete.targetClueId)
                    ? complete.targetClueId
                    : complete.supportingEdges?.FirstOrDefault(s => s != null)?.clueId;
                return !string.IsNullOrEmpty(targetId) && g._referenceEvidence.TryGetValue(targetId, out double refT)
                    ? refT
                    : 0.0;
            }

            var x = DampedPropagation.Run(g._authoredEdges, seeds, p.alpha, out _);
            double sup = ScoreEvidence(complete.supportingEdges, x, seeds, p.alpha);
            double con = ScoreEvidence(complete.contradictingEdges, x, seeds, p.alpha);
            return Math.Max(0.0, sup - p.contradictionWeight * con);
        }

        static double ScoreEvidence(WeightedClueRef[] edges,
            IReadOnlyDictionary<string, double> x,
            IReadOnlyDictionary<string, double> seeds,
            double alpha)
        {
            double total = 0;
            foreach (var e in edges ?? Array.Empty<WeightedClueRef>())
            {
                if (e == null) continue;
                if (!x.TryGetValue(e.clueId, out double v)) continue;
                if (!seeds.TryGetValue(e.clueId, out double r)) r = 0.5;
                double evidence = Math.Max(0.0, v - (1.0 - alpha) * r);
                total += e.weight * evidence;
            }
            return total;
        }

        static List<string> ComputeSolutionOrder(EvidenceGraph g)
        {
            TopologicalSort(_endpointSet(g._solutionEdges), g._solutionEdges, out var order);
            return order ?? new List<string>();
        }

        /// <summary>
        /// Kahn's algorithm over (nodes, edges) with ordinal id tie-break, so
        /// the produced order is deterministic. Returns false on a cycle.
        /// </summary>
        internal static bool TopologicalSort(IReadOnlyCollection<string> nodes,
            IEnumerable<DirectedEdge> edges, out List<string> order)
        {
            var outEdges = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var indegree = new Dictionary<string, int>(StringComparer.Ordinal);
            var ready = new SortedSet<string>(StringComparer.Ordinal);
            order = new List<string>();

            foreach (string n in nodes ?? Array.Empty<string>())
                indegree[n] = 0;
            foreach (var e in edges ?? Enumerable.Empty<DirectedEdge>())
            {
                if (!indegree.ContainsKey(e.sourceId) || !indegree.ContainsKey(e.targetId))
                    continue;
                if (!outEdges.TryGetValue(e.sourceId, out var list))
                    outEdges[e.sourceId] = list = new List<string>();
                list.Add(e.targetId);
                indegree[e.targetId]++;
            }
            foreach (var pair in indegree)
                if (pair.Value == 0)
                    ready.Add(pair.Key);

            while (ready.Count > 0)
            {
                string u = ready.Min;
                ready.Remove(u);
                order.Add(u);
                if (!outEdges.TryGetValue(u, out var targets)) continue;
                foreach (string v in targets)
                    if (--indegree[v] == 0)
                        ready.Add(v);
            }
            return order.Count == indegree.Count;
        }
    }

    /// <summary>
    /// One-pass seeded damped propagation on a DAG (Algorithm.md Algorithm 1,
    /// lines 4-16). Shared by the solver and the reference-support cache.
    /// </summary>
    internal static class DampedPropagation
    {
        public static Dictionary<string, double> Run(
            IReadOnlyDictionary<DirectedEdge, double> validEdges,
            IReadOnlyDictionary<string, double> reliability,
            double alpha,
            out HashSet<string> activeClues)
        {
            activeClues = new HashSet<string>(StringComparer.Ordinal);
            var outgoing = new Dictionary<string, List<KeyValuePair<string, double>>>(StringComparer.Ordinal);
            var indegree = new Dictionary<string, int>(StringComparer.Ordinal);
            var outWeightSum = new Dictionary<string, double>(StringComparer.Ordinal);
            var accumulated = new Dictionary<string, double>(StringComparer.Ordinal);
            var seed = new Dictionary<string, double>(StringComparer.Ordinal);
            var x = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var pair in validEdges ?? new Dictionary<DirectedEdge, double>())
            {
                var e = pair.Key;
                activeClues.Add(e.sourceId);
                activeClues.Add(e.targetId);
                if (!outgoing.TryGetValue(e.sourceId, out var list))
                    outgoing[e.sourceId] = list = new List<KeyValuePair<string, double>>();
                list.Add(new KeyValuePair<string, double>(e.targetId, pair.Value));
                indegree[e.targetId] = indegree.TryGetValue(e.targetId, out int d) ? d + 1 : 1;
                outWeightSum[e.sourceId] = outWeightSum.TryGetValue(e.sourceId, out double s) ? s + pair.Value : pair.Value;
            }

            var ready = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string u in activeClues)
            {
                indegree.TryGetValue(u, out int d);
                if (d == 0)
                    ready.Add(u);
                accumulated[u] = 0;
                seed[u] = reliability != null && reliability.TryGetValue(u, out double r) ? r : 0;
            }

            while (ready.Count > 0)
            {
                string u = ready.Min;
                ready.Remove(u);
                double du = Math.Max(1.0, outWeightSum.TryGetValue(u, out double s) ? s : 0);
                x[u] = (1.0 - alpha) * seed[u] + alpha * accumulated[u];
                if (!outgoing.TryGetValue(u, out var targets)) continue;
                foreach (var t in targets)
                {
                    accumulated[t.Key] += (t.Value / du) * x[u];
                    if (--indegree[t.Key] == 0)
                        ready.Add(t.Key);
                }
            }
            return x;
        }
    }
}

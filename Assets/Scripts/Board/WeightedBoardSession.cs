using System;
using System.Collections.Generic;
using Morsoul.Engine;

namespace Morsoul.Board
{
    /// <summary>
    /// Headless weighted-board session: the mutable BoardRuntimeState for
    /// one case driven by the pure engine rules. No Unity types — the UI
    /// controller renders this model and forwards pointer input; GameFlow
    /// mirrors Focus/MaxFocus in and out of the persistent FocusModel.
    ///
    /// - DrawLink validates structurally (self-links and links touching
    ///   uncollected clues are invalid input, never charged as errors),
    ///   then runs ProgressionRules.OnLinkDrawn.
    /// - SeverLink is free and non-destructive; re-drawing a severed link
    ///   is a fresh progression event per Data_Schemas.
    /// - Solve evaluates the current board through DeductionEngine.
    /// - Only unlocked conclusions may be selected/submitted; the starting
    ///   wrong conclusion is always available.
    /// </summary>
    public class WeightedBoardSession
    {
        public WeightedBoardSession(EvidenceGraph graph, BoardRuntimeState state)
        {
            Graph = graph ?? throw new ArgumentNullException(nameof(graph));
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public EvidenceGraph Graph { get; }
        public BoardRuntimeState State { get; }

        /// <summary>Most recent Solve() certificate; null until the first solve.</summary>
        public DeductionCertificate LastCertificate { get; private set; }

        // --- Clues ----------------------------------------------------------

        /// <summary>Replaces the visible-clue set U (subset of K enforced).</summary>
        public void SetVisibleClues(IEnumerable<string> clueIds)
        {
            State.VisibleClueIds = FilterToGraph(clueIds);
            State.CollectedClueIds.RemoveAll(id => !State.VisibleClueIds.Contains(id));
        }

        /// <summary>Marks a clue collected (also visible). False on unknown id.</summary>
        public bool CollectClue(string clueId)
        {
            if (!Graph.ContainsClue(clueId)) return false;
            if (!State.VisibleClueIds.Contains(clueId)) State.VisibleClueIds.Add(clueId);
            if (!State.CollectedClueIds.Contains(clueId)) State.CollectedClueIds.Add(clueId);
            return true;
        }

        List<string> FilterToGraph(IEnumerable<string> ids)
        {
            var filtered = new List<string>();
            if (ids == null) return filtered;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ids)
                if (Graph.ContainsClue(id) && seen.Add(id))
                    filtered.Add(id);
            return filtered;
        }

        // --- Links ----------------------------------------------------------

        /// <summary>
        /// Draws a directed link. Structurally invalid draws (self-link or an
        /// endpoint outside C) return a single InvalidInput event and are never
        /// charged. Otherwise forwards to ProgressionRules.OnLinkDrawn — the
        /// returned events drive focus charges, clears/lockout and tier hints.
        /// </summary>
        public List<ProgressionEvent> DrawLink(DirectedEdge link)
        {
            if (link.sourceId == link.targetId ||
                !State.CollectedClueIds.Contains(link.sourceId) ||
                !State.CollectedClueIds.Contains(link.targetId))
            {
                return new List<ProgressionEvent>
                {
                    new ProgressionEvent { Kind = ProgressionEventKind.InvalidInput },
                };
            }
            return ProgressionRules.OnLinkDrawn(State, Graph, link);
        }

        /// <summary>Removes a drawn link; free and non-destructive.</summary>
        public bool SeverLink(DirectedEdge link) => State.Links.Remove(link);

        // --- Node positions -------------------------------------------------

        public void SetNodePosition(string clueId, float x, float y)
        {
            if (Graph.ContainsClue(clueId))
                State.NodePositions[clueId] = new NodePosition { x = x, y = y };
        }

        public bool TryGetNodePosition(string clueId, out NodePosition position)
        {
            position = default;
            return clueId != null && State.NodePositions.TryGetValue(clueId, out position);
        }

        // --- Solving / conclusions ------------------------------------------

        /// <summary>Evaluates the board and caches the certificate.</summary>
        public DeductionCertificate Solve()
        {
            LastCertificate = DeductionEngine.Solve(Graph, new DeductionInput
            {
                VisibleClueIds = State.VisibleClueIds.ToArray(),
                CollectedClueIds = State.CollectedClueIds.ToArray(),
                Links = State.Links.ToArray(),
                SelectedConclusionId = SelectedConclusionId,
            });
            return LastCertificate;
        }

        /// <summary>
        /// Selected conclusion id, defaulting to the always-unlocked starting
        /// wrong conclusion when the saved selection is missing or locked.
        /// </summary>
        public string SelectedConclusionId =>
            Graph.ContainsConclusion(State.SelectedConclusionId)
                ? State.SelectedConclusionId
                : Graph.StartingConclusionId;

        /// <summary>
        /// Selects a conclusion for submission; only unlocked conclusions are
        /// selectable (h0 always, others at their tier's threshold on the last
        /// solve). False when the conclusion is locked or unknown.
        /// </summary>
        public bool SelectConclusion(string conclusionId)
        {
            if (!IsConclusionUnlocked(conclusionId)) return false;
            State.SelectedConclusionId = conclusionId;
            return true;
        }

        /// <summary>True when the conclusion may be selected on the current board.</summary>
        public bool IsConclusionUnlocked(string conclusionId)
        {
            var conclusion = Graph.GetConclusion(conclusionId);
            if (conclusion == null) return false;
            if (conclusionId == Graph.StartingConclusionId) return true;
            var cert = LastCertificate ?? Solve();
            double phi = cert.SupportByConclusion != null &&
                         cert.SupportByConclusion.TryGetValue(conclusionId, out double v)
                ? v
                : 0.0;
            return DeductionEngine.IsConclusionUnlocked(Graph, conclusion, phi, State.Links,
                cert.PropagatedClueValues, cert.SolutionPropagatedClueValues, cert.InventedLinkCount);
        }

        /// <summary>All currently selectable conclusions (locked ones hidden).</summary>
        public List<ConclusionDef> UnlockedConclusions()
        {
            Solve();
            var unlocked = new List<ConclusionDef>();
            foreach (var c in Graph.Conclusions)
                if (IsConclusionUnlocked(c.id)) unlocked.Add(c);
            return unlocked;
        }

        /// <summary>
        /// Submits the selected conclusion: solves, then runs
        /// ProgressionRules.OnSubmission (only a supported complete board
        /// solves; rewards are Case 0 refill, Cases 1-4 +10, Case 5 none).
        /// </summary>
        public List<ProgressionEvent> Submit(out DeductionCertificate certificate)
        {
            certificate = Solve();
            return ProgressionRules.OnSubmission(State, certificate);
        }

        /// <summary>Tier-2/3 hint target for the current board.</summary>
        public string HintTargetClue(int tier)
        {
            if (tier == 2) return Graph.NextUnfinishedSolutionClue(State.Links);
            if (tier == 3) return Graph.FinalSolutionClue;
            return null;
        }

        // --- Focus mirror ---------------------------------------------------

        /// <summary>Mirrors persistent Focus into the runtime state before rules run.</summary>
        public void SyncFocus(float energy, float maximum)
        {
            State.Focus = energy;
            State.MaxFocus = maximum;
        }

        /// <summary>Rest: +1 per completed Focus tick, capped at max.</summary>
        public void ApplyRestTicks(int completedTicks) =>
            ProgressionRules.OnRestTicks(State, completedTicks);

        /// <summary>Closed-game recovery: +floor(minutes/2), capped at max.</summary>
        public void ApplyClosedMinutes(int wholeMinutesClosed) =>
            ProgressionRules.OnReopen(State, wholeMinutesClosed);
    }
}

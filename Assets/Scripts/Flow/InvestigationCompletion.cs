using System.Collections.Generic;
using System.Linq;
using Morsoul.Board;
using Morsoul.Core;
using Morsoul.Data;
using Morsoul.Engine;

namespace Morsoul.Flow
{
    public sealed class InvestigationCompletionResult
    {
        public bool DeductionAccepted { get; internal set; }
        public string Message { get; internal set; }
        public IReadOnlyList<string> CollectedClueIds { get; internal set; } = new string[0];
        public IReadOnlyList<string> SkippedClues { get; internal set; } = new string[0];
    }

    public static class InvestigationCompletion
    {
        public static bool TrySimulate(CaseData data, SessionState session,
            FocusModel focus, out InvestigationCompletionResult result)
        {
            result = new InvestigationCompletionResult();
            if (!UnityEngine.Application.isEditor && !UnityEngine.Debug.isDebugBuild)
                return Fail(result, "Investigation simulation is unavailable in release builds.");
            if (data == null || session == null || focus == null || data.caseId < 0 || data.caseId > 4)
                return Fail(result, "No simulatable investigation context.");
            if (session.CurrentCase < data.caseId)
                return Fail(result, "This case has not been reached.");
            if (data.evidenceGraph == null || data.evidenceGraph.caseId != data.caseId)
                return Fail(result, "Missing or mismatched evidence graph.");
            var graph = data.evidenceGraph.BuildGraph();
            if (graph == null) return Fail(result, "Invalid evidence graph; simulation left progress unchanged.");

            var additions = new List<ClueData>();
            var skipped = new List<string>();
            var seen = new HashSet<string>();
            foreach (var clue in data.clues ?? new ClueData[0])
            {
                if (clue == null || string.IsNullOrEmpty(clue.id) || clue.caseId != data.caseId || !seen.Add(clue.id))
                    return Fail(result, "Invalid or duplicate case clue; simulation left progress unchanged.");
                if (session.HasClue(clue.id)) continue;
                var log = clue.type == ClueType.Latent ? data.latentLog : null;
                if (ClueAcquisition.CanAcquire(clue, session, log, out var reason)) additions.Add(clue);
                else skipped.Add(clue.id + ": " + reason);
            }
            result.SkippedClues = skipped;
            var state = session.CopyBoardState(data.caseId);
            bool alreadySolved = state.BoardSolved;
            if (!alreadySolved)
            {
                if (state.LinkingLockedOut && focus.Percent < ProgressionConstants.ErrorFocusCost)
                    return Fail(result, "The existing board is locked; recover Focus normally before simulation.");
                var board = new WeightedBoardSession(graph, state);
                board.SyncFocus(focus.Percent, focus.Maximum);
                foreach (var clue in data.clues ?? new ClueData[0])
                    if (session.HasClue(clue.id) || additions.Contains(clue)) board.CollectClue(clue.id);
                var links = new HashSet<DirectedEdge>();
                foreach (string id in graph.SolutionOrder)
                    foreach (var link in graph.IncidentSolutionLinks(id))
                        if (links.Add(link) && !state.Links.Contains(link))
                        {
                            var events = board.DrawLink(link);
                            if (events.Any(e => e.Kind != ProgressionEventKind.LinkOk))
                                return Fail(result, "Required solution clues or board access are unavailable.");
                        }
                board.Solve();
                if (!board.SelectConclusion(graph.CompleteConclusionId))
                    return Fail(result, "A complete deduction cannot be accepted without changing existing progress.");
                board.Submit(out var certificate);
                if (certificate.Status != DeductionStatus.Ok || !state.BoardSolved)
                    return Fail(result, "The rules engine did not accept the complete conclusion.");
            }

            if (!alreadySolved)
            {
                if (!session.AcceptInvestigation(state, InvestigationCompletionSource.Simulated))
                    return Fail(result, "Investigation state changed before acceptance.");
                focus.Sync(state.Focus, state.MaxFocus);
                result.DeductionAccepted = true;
            }
            foreach (var clue in additions)
            {
                session.RegisterClue(clue);
                if (clue.type == ClueType.Latent) session.RegisterLatentClue(clue);
            }
            var ids = additions.Select(c => c.id).ToArray();
            if (result.DeductionAccepted || ids.Length > 0)
                session.RecordInvestigationSimulation(data.caseId, ids);
            result.CollectedClueIds = ids;
            result.Message = result.DeductionAccepted
                ? "Development simulation accepted the investigation; return to the suspect in 2D."
                : "Existing deduction preserved; newly eligible clues collected: " + ids.Length;
            return true;
        }

        static bool Fail(InvestigationCompletionResult result, string message)
        {
            result.Message = message;
            return false;
        }
    }
}

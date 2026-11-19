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
                if (state.LinkingLockedOut && focus.Percent < ProgressionConstants.LinkingUnlockFocus)
                    return Fail(result, "The existing board is locked; recover Focus before simulation.");
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
                if (clue.type == ClueType.Latent)
                {
                    session.RegisterLatentClue(clue);
                    if (!string.IsNullOrEmpty(data.uneaseFlagKey))
                        session.SetFlag(data.uneaseFlagKey, false);
                }
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

        public static bool ForceCompleteCase(CoreRuntime core, int caseId)
        {
            if (core == null || caseId < 0 || caseId > 5) return false;
            var data = core.ResolveCase(caseId);
            if (data == null) return false;

            var session = core.Session;
            if (data.toolUnlocked != Tool.None) session.UnlockTool(data.toolUnlocked);

            void TryRegisterClue(ClueData clue)
            {
                if (clue == null || string.IsNullOrEmpty(clue.id)) return;
                if (clue.type == ClueType.Latent)
                {
                    if (ClueAcquisition.CanAcquire(clue, session, data.latentLog, out _) ||
                        session.CurrentCase >= 5 || caseId == 4)
                    {
                        session.RegisterClue(clue);
                        session.RegisterLatentClue(clue);
                        if (!string.IsNullOrEmpty(data.uneaseFlagKey))
                            session.SetFlag(data.uneaseFlagKey, false);
                    }
                    return;
                }
                if (!clue.isBacktrackingOnly || session.CurrentCase >= 5)
                    session.RegisterClue(clue);
            }

            foreach (var clue in data.preloadedClues ?? new ClueData[0]) TryRegisterClue(clue);
            foreach (var clue in data.clues ?? new ClueData[0]) TryRegisterClue(clue);

            if (data.evidenceGraph != null)
            {
                var graph = data.evidenceGraph.BuildGraph();
                if (graph != null)
                {
                    var state = session.CopyBoardState(caseId);
                    if (!state.BoardSolved)
                    {
                        state.LinkingLockedOut = false;
                        var board = new WeightedBoardSession(graph, state);
                        board.SyncFocus(UnityEngine.Mathf.Max(core.Focus.Percent, ProgressionConstants.ErrorFocusCost),
                            core.Focus.Maximum);

                        foreach (var clue in data.preloadedClues ?? new ClueData[0])
                            if (clue != null && !string.IsNullOrEmpty(clue.id)) board.CollectClue(clue.id);
                        foreach (var clue in data.clues ?? new ClueData[0])
                            if (clue != null && !string.IsNullOrEmpty(clue.id)) board.CollectClue(clue.id);
                        foreach (string id in session.ClueIds)
                            board.CollectClue(id);

                        var links = new HashSet<DirectedEdge>();
                        foreach (string id in graph.SolutionOrder)
                            foreach (var link in graph.IncidentSolutionLinks(id))
                                if (links.Add(link) && !state.Links.Contains(link))
                                    board.DrawLink(link);
                        foreach (var link in graph.SolutionEdges)
                            if (links.Add(link) && !state.Links.Contains(link))
                                board.DrawLink(link);

                        board.Solve();
                        board.SelectConclusion(graph.CompleteConclusionId);
                        board.Submit(out _);
                        state.BoardSolved = true;
                        state.SubmittedConclusionId = graph.CompleteConclusionId;

                        if (caseId <= 4)
                            session.AcceptInvestigation(state, InvestigationCompletionSource.Real);
                    }

                    var liveState = session.GetOrCreateBoardState(caseId);
                    if (!object.ReferenceEquals(liveState, state))
                    {
                        liveState.VisibleClueIds = new List<string>(state.VisibleClueIds);
                        liveState.CollectedClueIds = new List<string>(state.CollectedClueIds);
                        liveState.Links = new List<DirectedEdge>(state.Links);
                        liveState.SelectedConclusionId = state.SelectedConclusionId;
                        liveState.SubmittedConclusionId = state.SubmittedConclusionId;
                        liveState.LinkingLockedOut = false;
                        liveState.BoardSolved = true;
                    }
                }
            }

            if (caseId == 0)
            {
                session.PrologueGateUnlocked = true;
                session.PrologueRobeCollected = true;
            }

            core.Bus.Publish(new InvestigationStateChangedEvent { CaseId = caseId });
            return true;
        }

        public static bool CompleteInterrogationAndTriggerCutscene(CoreRuntime core, int caseId)
        {
            if (core == null || caseId < 1 || caseId > 4) return false;
            ForceCompleteCase(core, caseId);

            var session = core.Session;
            session.SetActiveInterrogation(null);
            session.SetInterrogationStrikeCount(caseId, 0);
            session.SetGrade(caseId, Grade.Clean);
            if (caseId == 4)
            {
                session.SetFlag("Case4_BrokkFreed");
                ForceCompleteCase(core, 4);
            }
            session.SetFlag(Morsoul.Hub.PostInterrogationCutsceneController.PendingFlag(caseId));

            var gsm = GameStateManager.Ensure();
            string hubScene = caseId == 2 && UnityEngine.Application.CanStreamedLevelBeLoaded("Hub_CathedralNave")
                ? "Hub_CathedralNave"
                : gsm.routes.Resolve(RouteKeys.Hub(caseId));
            if (UnityEngine.Application.isPlaying &&
                !string.IsNullOrEmpty(hubScene) &&
                UnityEngine.Application.CanStreamedLevelBeLoaded(hubScene))
            {
                gsm.Advance(GameFlowState.Hub);
                core.Bus.Publish(new SceneLoadRequestedEvent { SceneName = hubScene });
            }
            return true;
        }

        static bool Fail(InvestigationCompletionResult result, string message)
        {
            result.Message = message;
            return false;
        }
    }
}

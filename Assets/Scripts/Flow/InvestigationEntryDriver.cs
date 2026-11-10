using System.IO;
using Morsoul.Core;
using Morsoul.Data;
using UnityEngine;

namespace Morsoul.Flow
{
    public static class InvestigationEntryDriver
    {
        static bool _entering;
        static int _pendingCheckpointCase = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            _entering = false;
            _pendingCheckpointCase = -1;
            var core = CoreRuntime.Ensure();
            var catalog = Resources.Load<InvestigationIntegrationCatalog>("InvestigationIntegrationCatalog");
            core.DevelopmentInvestigationFallbackEnabled = catalog != null && catalog.enableDevelopmentFallback;
            core.InvestigationEntryHandler = route => Enter(core, catalog, route);
            core.InterrogationEntryHandler = caseId =>
            {
                var data = core.ResolveCase(caseId);
                return data != null && data.hasInterrogation && data.interrogation != null &&
                    GameStateManager.Ensure().GoToRoute(RouteKeys.CaseLoop, GameFlowState.Interrogation);
            };
        }

        static InvestigationEntryResult Enter(CoreRuntime core,
            InvestigationIntegrationCatalog catalog, string route)
        {
            if (_entering) return Failure("An investigation entry is already in progress.");
            if (catalog == null) return Failure("Investigation integration catalog is missing.");
            var gsm = GameStateManager.Ensure();
            string scene = gsm.routes.Resolve(route);
            bool loadable = !string.IsNullOrEmpty(scene) && Application.CanStreamedLevelBeLoaded(scene);
            var kind = catalog.Decide(route, Application.isEditor || Debug.isDebugBuild,
                loadable, out var area, core.DevelopmentInvestigationFallbackEnabled);
            if (area == null) return Failure("Investigation area is unconfigured or duplicated: " + route);
            if (core.Session.CurrentCase < area.caseId ||
                (!string.IsNullOrEmpty(area.requiredEntryFlag) && !core.Session.HasFlag(area.requiredEntryFlag)))
                return Failure("Complete the area's normal access requirements first.");
            if (kind == InvestigationEntryKind.Blocked)
                return Failure(area.status == InvestigationIntegrationStatus.Integrated
                    ? "Integrated investigation route is not loadable: " + route
                    : "This area is not integrated. Enable development fallback explicitly in F1 > Session to test it.");
            _entering = true;
            try
            {
                if (kind == InvestigationEntryKind.Load)
                    return new InvestigationEntryResult
                    {
                        Success = gsm.EnterInvestigationRoute(route, area.caseId),
                        Message = "Entering integrated investigation: " + route,
                    };
                var data = core.ResolveCase(area.caseId);
                if (area.caseId == 0)
                {
                    string wilderness = gsm.routes.Resolve(RouteKeys.PrologueWilderness);
                    if (string.IsNullOrEmpty(wilderness) || !Application.CanStreamedLevelBeLoaded(wilderness))
                        return Failure("The 2D Prologue approach is not loadable; no investigation was simulated.");
                }
                if (!InvestigationCompletion.TrySimulate(data, core.Session, core.Focus, out var completion))
                    return Failure(completion.Message);
                if (area.caseId == 0)
                {
                    core.Session.PrologueGateUnlocked = true;
                    core.Session.PrologueRobeCollected = true;
                    core.Session.PrologueExitLoaded = true;
                    return new InvestigationEntryResult
                    {
                        Success = gsm.GoToRoute(RouteKeys.PrologueWilderness, GameFlowState.Hub),
                        Message = "Development Prologue investigation simulated; Eliza first contact remains playable.",
                    };
                }
                if (completion.DeductionAccepted || completion.CollectedClueIds.Count > 0)
                    _pendingCheckpointCase = area.caseId;
                if (_pendingCheckpointCase >= 0)
                {
                    gsm.WriteCheckpoint("DevelopmentInvestigation", null, preserveRestoredPose: true);
                    int changedCase = _pendingCheckpointCase;
                    _pendingCheckpointCase = -1;
                    core.Bus.Publish(new InvestigationStateChangedEvent { CaseId = changedCase });
                }
                Debug.Log(completion.Message + " Skipped: " + string.Join("; ", completion.SkippedClues));
                return new InvestigationEntryResult { Success = true, Message = completion.Message };
            }
            catch (IOException e)
            {
                return Failure("Investigation state could not be saved. Retry to checkpoint the retained state: " + e.Message);
            }
            catch (System.UnauthorizedAccessException e)
            {
                return Failure("Investigation checkpoint access failed: " + e.Message);
            }
            finally
            {
                _entering = false;
            }
        }

        static InvestigationEntryResult Failure(string message) =>
            new InvestigationEntryResult { Message = message };
    }
}

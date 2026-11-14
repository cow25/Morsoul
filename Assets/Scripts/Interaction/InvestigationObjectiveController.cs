using Morsoul.Core;
using UnityEngine;

namespace Morsoul.Interaction
{
    /// <summary>
    /// Updates HUDManager's Active Goal text for a 3D investigation scene
    /// based on collected clue progress and Deduction Board completion.
    /// </summary>
    public class InvestigationObjectiveController : MonoBehaviour
    {
        public int caseId = 1;
        public string[] requiredClueIds = { "C1_01", "C2_01", "C3_01" };
        public string gatherObjectiveFormat = "Inspect vault lock, scan solvent residue [Tab], and check cargo desk ({0}/{1}).";
        public string boardObjective = "Evidence gathered. Open the Deduction Board [H] or ascend the stairwell.";
        public string solvedObjective = "Board solved! Ascend the stairwell to return to the hub.";

        string _lastText;

        void Awake()
        {
            var core = CoreRuntime.Ensure();
            if (core.Session.InvestigationCaseId < 0)
                core.Session.InvestigationCaseId = caseId;
            if (core.Session.CurrentCase < caseId)
                core.Session.CurrentCase = caseId;
            core.Session.SetSceneContext(SceneMode.Mode3D,
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        void Start() => Refresh();
        void Update() => Refresh();

        void Refresh()
        {
            var hud = HUDManager.Instance;
            if (hud == null) return;

            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            var board = session != null ? session.GetOrCreateBoardState(caseId) : null;
            bool solved = board != null && board.BoardSolved;

            int found = 0;
            int total = requiredClueIds != null ? requiredClueIds.Length : 0;
            for (int i = 0; i < total; i++)
            {
                string id = requiredClueIds[i];
                bool has = (DiaryManager.Instance != null && DiaryManager.Instance.HasClue(id)) ||
                           (session != null && session.HasClue(id));
                if (has) found++;
            }

            string next = solved
                ? solvedObjective
                : (found >= total && total > 0 ? boardObjective : string.Format(gatherObjectiveFormat, found, total));

            if (next != _lastText)
            {
                _lastText = next;
                hud.SetObjective(next);
            }
        }
    }
}


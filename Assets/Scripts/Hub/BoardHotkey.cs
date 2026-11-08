using System.Collections.Generic;
using Morsoul.Board;
using Morsoul.Core;
using Morsoul.Data;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Wires lazy weighted-board initialisation for investigation contexts.
    /// Retained instances in 2D hubs deny opening and gameplay mutations;
    /// investigation composition must stamp the Core scene mode correctly.
    /// Attach beside a WeightedBoardController; its Update handles H.
    /// The visited investigation ID takes precedence over campaign progress.
    /// Weighted cases only; legacy slot/premise boards are scene-scoped.
    /// </summary>
    public class BoardHotkey : MonoBehaviour
    {
        /// <summary>The board panel to lazy-initialise (same GameObject by default).</summary>
        public WeightedBoardController board;

        void Awake()
        {
            if (board == null) board = GetComponent<WeightedBoardController>();
        }

        void OnEnable()
        {
            if (board == null)
            {
                Debug.LogWarning($"BoardHotkey '{name}': no WeightedBoardController assigned.", this);
                return;
            }
            board.CanUse = CanUseBoard;
            board.EnsureSession = EnsureBoard;
        }

        void OnDisable()
        {
            if (board != null && board.EnsureSession == (System.Func<bool>)EnsureBoard)
                board.EnsureSession = null;
            if (board != null) board.CanUse = () => false;
        }

        bool CanUseBoard() => CoreRuntime.Instance != null &&
            CoreRuntime.Instance.Session.CurrentSceneMode == SceneMode.Mode3D;

        /// <summary>
        /// Builds the board session for the session's current case. False
        /// when Core is absent, the case has no evidence graph, or the
        /// graph fails to build (the controller keeps the board closed).
        /// </summary>
        public bool EnsureBoard()
        {
            var core = CoreRuntime.Instance;
            if (board != null) board.CanUse = CanUseBoard;
            if (core == null || board == null || !CanUseBoard()) return false;
            int caseId = core.Session.InvestigationCaseId >= 0
                ? core.Session.InvestigationCaseId : core.Session.CurrentCase;
            var data = core.ResolveCase(caseId);
            if (data == null || data.evidenceGraph == null) return false;
            if (board.caseData != data || board.Session == null)
            {
                if (!board.SetCase(data, core.Session.GetOrCreateBoardState(data.caseId)))
                    return false;
            }

            var focus = core.Focus;
            board.FocusSource = () => (focus.Percent, focus.Maximum);
            board.FocusSink = (energy, max) => focus.Sync(energy, max);

            var clues = new List<ClueData>();
            if (data.preloadedClues != null && data.preloadedClues.Length > 0)
            {
                foreach (var clue in data.preloadedClues)
                    if (clue != null) clues.Add(clue);
            }
            else
            {
                foreach (string id in core.Session.ClueIds)
                {
                    var clue = core.ResolveClue(id);
                    if (clue != null) clues.Add(clue);
                }
            }
            board.SetClues(clues);
            return true;
        }
    }
}

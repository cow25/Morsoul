using System.Collections.Generic;
using Morsoul.Board;
using Morsoul.Core;
using Morsoul.Data;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Wires a weighted-board panel for lazy session initialisation so the
    /// global "H opens the board everywhere" shortcut works outside the
    /// CaseLoop scene (GDD §4.2 — the board opens from any scene). Attach
    /// next to a WeightedBoardController in a hub scene; the controller's
    /// own Update handles the H key, this only ensures a session exists.
    /// Weighted cases only — legacy slot/premise boards are scene-scoped.
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
            board.EnsureSession = EnsureBoard;
        }

        void OnDisable()
        {
            if (board != null && board.EnsureSession == (System.Func<bool>)EnsureBoard)
                board.EnsureSession = null;
        }

        /// <summary>
        /// Builds the board session for the session's current case. False
        /// when Core is absent, the case has no evidence graph, or the
        /// graph fails to build (the controller keeps the board closed).
        /// </summary>
        public bool EnsureBoard()
        {
            var core = CoreRuntime.Instance;
            if (core == null || board == null) return false;
            var data = core.ResolveCase(core.Session.CurrentCase);
            if (data == null || data.evidenceGraph == null) return false;
            if (board.caseData != data || board.Session == null)
            {
                if (!board.SetCase(data, core.Session.GetOrCreateBoardState(data.caseId)))
                    return false;
            }

            var focus = core.Focus;
            board.FocusSource = () => (focus.Percent, focus.Maximum);
            board.FocusSink = (energy, max) => focus.Sync(energy, max);

            // Re-push collected clues on every call: the session may have been
            // built before the player finished collecting in this scene.
            var clues = new List<ClueData>();
            foreach (string id in core.Session.ClueIds)
            {
                var clue = core.ResolveClue(id);
                if (clue != null) clues.Add(clue);
            }
            board.SetClues(clues);
            return true;
        }
    }
}

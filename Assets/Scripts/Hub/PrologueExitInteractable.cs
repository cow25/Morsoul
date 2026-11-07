using Morsoul.Core;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Prologue exit: requires the tutorial board to be solved (persisted
    /// BoardRuntimeState.BoardSolved for boardCaseId), then stamps
    /// Prologue_Completed and advances Session.CurrentCase so the hub case
    /// door begins Case 1 (spec outro sets CurrentCase = "CASE_1"). Travels
    /// like a normal carrier when open; plays lockedDialogue when not.
    /// </summary>
    public class PrologueExitInteractable : CarrierInteractable
    {
        /// <summary>Case the campaign advances to on exit (1 = Vault).</summary>
        public int nextCaseId = 1;
        /// <summary>Case whose board must be solved before leaving (0 = tutorial).</summary>
        public int boardCaseId;
        /// <summary>Played while the board is unsolved; optional.</summary>
        public Morsoul.Dialogue.DialogueData lockedDialogue;

        public override void Interact(HubContext ctx)
        {
            var core = CoreRuntime.Instance;
            var session = core?.Session;
            if (session != null)
            {
                var board = session.GetOrCreateBoardState(boardCaseId);
                if (board == null || !board.BoardSolved)
                {
                    if (lockedDialogue != null && ctx != null && ctx.dialogue != null)
                    {
                        if (ctx.flags != null) ctx.dialogue.SetFlags(ctx.flags.Set);
                        ctx.dialogue.SetDialogue(lockedDialogue);
                        ctx.dialogue.Play();
                    }
                    else
                    {
                        Debug.Log($"PrologueExitInteractable '{name}': tutorial board unsolved.", this);
                    }
                    return;
                }
                session.SetFlag("Prologue_Completed");
                session.CurrentCase = nextCaseId;
            }
            // No Core: permissive standalone fallback (same as carriers).
            base.Interact(ctx);
        }
    }
}

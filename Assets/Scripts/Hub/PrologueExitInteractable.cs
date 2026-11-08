using Morsoul.Core;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Prologue stand-in exit: requires the tutorial board to be solved, then
    /// performs the robe/gate transition into the dedicated 2D wilderness
    /// approach. Prologue completion and Case 1 advance happen only after the
    /// separate Eliza first-contact sequence finishes.
    /// </summary>
    public class PrologueExitInteractable : CarrierInteractable
    {
        /// <summary>Route resolved before the serialized targetScene fallback.</summary>
        public string routeKey = RouteKeys.PrologueWilderness;
        /// <summary>Case whose board must be solved before leaving (0 = tutorial).</summary>
        public int boardCaseId;
        /// <summary>Played while the board is unsolved; optional.</summary>
        public Morsoul.Dialogue.DialogueData lockedDialogue;
        /// <summary>Optional notice shown after the automatic robe pickup.</summary>
        public Morsoul.Dialogue.DialogueData robeDialogue;

        bool _waitingForRobeDialogue;

        public override void Interact(HubContext ctx)
        {
            if (_waitingForRobeDialogue) return;
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
                session.PrologueGateUnlocked = true;
                if (!session.PrologueRobeCollected)
                {
                    session.PrologueRobeCollected = true;
                    if (PlayRobeDialogue(ctx)) return;
                }
            }
            CompleteExit(ctx);
        }

        bool PlayRobeDialogue(HubContext ctx)
        {
            var controller = ctx != null ? ctx.dialogue : null;
            if (robeDialogue == null || controller == null) return false;
            if (ctx.flags != null) controller.SetFlags(ctx.flags.Set);
            _waitingForRobeDialogue = true;
            void OnEnded()
            {
                controller.DialogueEnded -= OnEnded;
                _waitingForRobeDialogue = false;
                CompleteExit(ctx);
            }
            controller.DialogueEnded += OnEnded;
            controller.SetDialogue(robeDialogue);
            controller.Play();
            return true;
        }

        void CompleteExit(HubContext ctx)
        {
            if (CoreRuntime.Instance != null) targetScene = ResolveTargetScene();
            // No Core: permissive standalone fallback (same as carriers).
            base.Interact(ctx);
        }

        string ResolveTargetScene()
        {
            if (!string.IsNullOrEmpty(routeKey) && GameStateManager.Instance != null)
            {
                string routed = GameStateManager.Instance.routes.Resolve(routeKey);
                if (!string.IsNullOrEmpty(routed)) return routed;
            }
            return targetScene;
        }
    }
}

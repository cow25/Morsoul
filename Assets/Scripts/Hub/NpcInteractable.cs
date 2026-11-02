using Morsoul.Data;
using Morsoul.Dialogue;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// NPC talk point. On interact, hands this NPC's DialogueData to the shared
    /// DialogueController, wires the hub flag set into the runner (so
    /// requiresFlag gating sees story flags) and forwards clue/flag side
    /// effects back into the HubJournal / HubFlags.
    /// </summary>
    public class NpcInteractable : Interactable2D
    {
        /// <summary>Dialogue graph played when this NPC is talked to.</summary>
        public DialogueData dialogue;

        DialogueController _wiredController;
        HubContext _wiredContext;

        void OnDestroy()
        {
            UnwireEvents();
        }

        public override void Interact(HubContext ctx)
        {
            base.Interact(ctx);
            if (ctx == null || ctx.dialogue == null)
            {
                Debug.LogWarning($"NpcInteractable '{name}': no HubContext.dialogue wired.", this);
                return;
            }
            if (dialogue == null)
            {
                Debug.LogWarning($"NpcInteractable '{name}': dialogue is null; nothing plays.", this);
                return;
            }

            WireEvents(ctx);
            // Share the scene flag set so the runner's requiresFlag gating works.
            if (ctx.flags != null) ctx.dialogue.SetFlags(ctx.flags.Set);
            ctx.dialogue.SetDialogue(dialogue);
            ctx.dialogue.Play();
        }

        void WireEvents(HubContext ctx)
        {
            if (_wiredController == ctx.dialogue && _wiredContext == ctx) return;
            UnwireEvents();
            ctx.dialogue.ClueRegistered += OnDialogueClueRegistered;
            ctx.dialogue.FlagSet += OnDialogueFlagSet;
            _wiredController = ctx.dialogue;
            _wiredContext = ctx;
        }

        void UnwireEvents()
        {
            if (_wiredController == null) return;
            _wiredController.ClueRegistered -= OnDialogueClueRegistered;
            _wiredController.FlagSet -= OnDialogueFlagSet;
            _wiredController = null;
            _wiredContext = null;
        }

        void OnDialogueClueRegistered(ClueData clue) => _wiredContext?.journal?.Register(clue);

        void OnDialogueFlagSet(string flag) => _wiredContext?.flags?.Add(flag);
    }
}

using Morsoul.Data;
using Morsoul.Dialogue;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// NPC talk point. When this NPC is the case's interrogation suspect and
    /// the session marks the encounter unlocked, enters the 2D interrogation;
    /// otherwise hands its DialogueData to HubContext.PlayDialogue — the
    /// scene's single effects binder (clues -> HubJournal, flags -> HubFlags).
    /// </summary>
    public class NpcInteractable : Interactable2D
    {
        /// <summary>Dialogue graph played when this NPC is talked to.</summary>
        public DialogueData dialogue;
        public int interrogationCaseId = -1;

        public override void Interact(HubContext ctx)
        {
            base.Interact(ctx);
            var core = Morsoul.Core.CoreRuntime.Instance;
            if (core != null && core.Session.CanStartInterrogation(interrogationCaseId))
            {
                if (!core.EnterInterrogation(interrogationCaseId))
                    Debug.LogWarning("NpcInteractable: the unlocked 2D interrogation could not be entered.", this);
                return;
            }
            if (ctx == null)
            {
                Debug.LogWarning($"NpcInteractable '{name}': no HubContext supplied.", this);
                return;
            }
            if (dialogue == null)
            {
                Debug.LogWarning($"NpcInteractable '{name}': dialogue is null; nothing plays.", this);
                return;
            }

            ctx.PlayDialogue(dialogue);
        }
    }
}

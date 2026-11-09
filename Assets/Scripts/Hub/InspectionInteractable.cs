using Morsoul.Data;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// World inspection point (notice board, corpse, landmark...). On interact,
    /// registers its clue into the HubJournal exactly once.
    /// </summary>
    public class InspectionInteractable : Interactable2D
    {
        /// <summary>Clue granted on first inspection.</summary>
        public ClueData clue;
        /// <summary>Optional feedback line/graph played after the clue registers.</summary>
        public Morsoul.Dialogue.DialogueData inspectionDialogue;

        bool _inspected;

        void Reset()
        {
            promptText = "[E] Inspect";
        }

        public override void Interact(HubContext ctx)
        {
            base.Interact(ctx);
            if (_inspected) return;
            if (clue == null)
            {
                // Flavor-only inspection (notice boards and the like): no
                // clue to register, just re-readable text.
                if (inspectionDialogue != null)
                {
                    if (ctx == null)
                        Debug.LogWarning($"InspectionInteractable '{name}': no HubContext supplied.", this);
                    else ctx.PlayDialogue(inspectionDialogue);
                }
                return;
            }
            if (ctx == null || ctx.journal == null)
            {
                Debug.LogWarning($"InspectionInteractable '{name}': no HubContext.journal wired.", this);
                return;
            }
            if (!ctx.journal.Register(clue)) return;
            _inspected = true;
            if (inspectionDialogue != null) ctx.PlayDialogue(inspectionDialogue);
        }
    }
}

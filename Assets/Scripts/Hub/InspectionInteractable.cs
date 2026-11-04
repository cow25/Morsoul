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

        bool _inspected;

        void Reset()
        {
            promptText = "[E] Inspect";
        }

        public override void Interact(HubContext ctx)
        {
            base.Interact(ctx);
            if (_inspected || clue == null) return;
            if (ctx == null || ctx.journal == null)
            {
                Debug.LogWarning($"InspectionInteractable '{name}': no HubContext.journal wired.", this);
                return;
            }
            if (ctx.journal.Register(clue)) _inspected = true;
        }
    }
}

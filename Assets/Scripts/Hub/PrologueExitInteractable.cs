using Morsoul.Core;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Prologue exit: stamps Prologue_Completed and advances
    /// Session.CurrentCase so the hub case door begins Case 1 (spec outro
    /// sets CurrentCase = "CASE_1"). Travels like a normal carrier.
    /// </summary>
    public class PrologueExitInteractable : CarrierInteractable
    {
        /// <summary>Case the campaign advances to on exit (1 = Vault).</summary>
        public int nextCaseId = 1;

        public override void Interact(HubContext ctx)
        {
            var session = CoreRuntime.Instance?.Session;
            if (session != null)
            {
                session.SetFlag("Prologue_Completed");
                session.CurrentCase = nextCaseId;
            }
            base.Interact(ctx);
        }
    }
}

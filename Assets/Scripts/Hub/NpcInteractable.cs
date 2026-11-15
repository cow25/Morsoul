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

        /// <summary>NPCs are solid: the player walks up to them, not through.</summary>
        protected override bool BlocksMovement => true;

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
            if (core != null && gameObject.name == "Ovan" &&
                GuildIntroRules.IsBriefingPending(core.Session, core.Session.CurrentCase))
            {
                int caseId = core.Session.CurrentCase;
                core.Session.SetFlag(GuildIntroRules.BriefingPendingFlag(caseId), false);
                ctx.PlayDialogue(BuildOvanBriefing(caseId, dialogue?.portraits));
                return;
            }
            if (dialogue == null)
            {
                Debug.LogWarning($"NpcInteractable '{name}': dialogue is null; nothing plays.", this);
                return;
            }

            ctx.PlayDialogue(dialogue);
        }

        static DialogueData BuildOvanBriefing(int caseId, SpeakerPortraitData[] portraits)
        {
            string line = caseId switch
            {
                2 => "Good work on the vault. Next assignment: the Cathedral of Saint Althea. " +
                     "Mother Lyra was silenced before she could deliver her ledger. " +
                     "Take the Carrier from Lower Market to the Cathedral Courtyard.",
                3 => "Another urgent dispatch from the frontier: Whisperwood Grove is dying from blight. " +
                     "Meet Ranger Kaelen via the Carrier and secure the Perimeter Seal.",
                4 => "The trail leads to Iron Hollow. Take the Carrier there, present Guild authority " +
                     "to Overseer Thrun, and reach Drift 09.",
                _ => "You have every piece of the puzzle now. Review the clues on your Deduction Board [H] " +
                     "to uncover what lies behind the Sanctum Core."
            };

            var data = ScriptableObject.CreateInstance<DialogueData>();
            data.hideFlags = HideFlags.DontSave;
            data.id = $"OvanBriefing_C{caseId}";
            data.startNodeId = "n1";
            data.portraits = portraits;
            data.nodes = new[]
            {
                new DialogueNodeData
                {
                    id = "n1",
                    speakerName = "Guildmaster Ovan",
                    text = line
                }
            };
            return data;
        }
    }
}

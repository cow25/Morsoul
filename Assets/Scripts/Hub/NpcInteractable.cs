using Morsoul.Core;
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

        void Start()
        {
            if (interrogationCaseId < 1 || interrogationCaseId > 4) return;
            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            if (session != null &&
                !session.HasFlag(PostInterrogationCutsceneController.PendingFlag(interrogationCaseId)) &&
                (session.CurrentCase > interrogationCaseId ||
                 (session.Grades.TryGetValue(interrogationCaseId, out var g) && g != Grade.Ungraded)))
            {
                gameObject.SetActive(false);
            }
        }

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
            if (core != null && gameObject.name == "Ovan")
            {
                int caseId = core.Session.CurrentCase;
                if (caseId >= 5)
                {
                    StartOvanCase5Prompt(ctx, core, dialogue?.portraits);
                    return;
                }
                if (GuildIntroRules.IsBriefingPending(core.Session, caseId))
                {
                    core.Session.SetFlag(GuildIntroRules.BriefingPendingFlag(caseId), false);
                    ctx.PlayDialogue(BuildOvanBriefing(caseId, dialogue?.portraits));
                    return;
                }
                if (caseId >= 2)
                {
                    ctx.PlayDialogue(BuildOvanBriefing(caseId, dialogue?.portraits));
                    return;
                }
            }
            if (dialogue == null)
            {
                Debug.LogWarning($"NpcInteractable '{name}': dialogue is null; nothing plays.", this);
                return;
            }

            ctx.PlayDialogue(dialogue);
        }

        static void StartOvanCase5Prompt(HubContext ctx, CoreRuntime core, SpeakerPortraitData[] portraits)
        {
            const string confirmFlag = "Guild.Case5Confirmed";
            core.Session.SetFlag(confirmFlag, false);

            var data = ScriptableObject.CreateInstance<DialogueData>();
            data.hideFlags = HideFlags.DontSave;
            data.id = "OvanCase5Prompt";
            data.startNodeId = "n1";
            data.portraits = portraits;
            data.nodes = new[]
            {
                new DialogueNodeData
                {
                    id = "n1",
                    speakerName = "Guildmaster Ovan",
                    text = "The Sanctum Core beneath Drift 09 awaits. Once you descend, there's no turning back until it's finished. Are you ready to continue to Case 5, and have you nothing else left to take care of?",
                    options = new[]
                    {
                        new DialogueOptionData { label = "I'm ready — continue to Case 5.", nextNodeId = "proceed" },
                        new DialogueOptionData { label = "Not yet — I still have things to take care of.", nextNodeId = "wait" }
                    }
                },
                new DialogueNodeData
                {
                    id = "proceed",
                    speakerName = "Guildmaster Ovan",
                    text = "Then go with everything you've got. May Asterveil endure.",
                    setsFlag = confirmFlag
                },
                new DialogueNodeData
                {
                    id = "wait",
                    speakerName = "Guildmaster Ovan",
                    text = "Take whatever time you need. Talk to me again whenever you're ready for Case 5."
                }
            };

            var dlg = ctx.dialogue;
            if (dlg != null)
            {
                void OnEnded()
                {
                    dlg.DialogueEnded -= OnEnded;
                    if (!core.Session.HasFlag(confirmFlag)) return;
                    core.Session.SetFlag(confirmFlag, false);
                    GameStateManager.Ensure().GoToRoute(RouteKeys.CaseLoop, GameFlowState.Board);
                }
                dlg.DialogueEnded += OnEnded;
            }
            ctx.PlayDialogue(data);
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

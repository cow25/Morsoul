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
            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            if (interrogationCaseId >= 1 && interrogationCaseId <= 4 && session != null &&
                !session.HasFlag(PostInterrogationCutsceneController.PendingFlag(interrogationCaseId)) &&
                (session.CurrentCase > interrogationCaseId ||
                 (session.Grades.TryGetValue(interrogationCaseId, out var g) && g != Grade.Ungraded)))
            {
                gameObject.SetActive(false);
                return;
            }
            if (HasUncollectedCaseClue()) InitClueGlint(0.55f);
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            TickClueGlint(HasUncollectedCaseClue());
        }

        bool HasUncollectedCaseClue()
        {
            if (dialogue == null || dialogue.nodes == null) return false;
            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            if (session == null) return false;
            foreach (var node in dialogue.nodes)
            {
                if (node != null && node.registersClue != null &&
                    node.registersClue.caseId == session.CurrentCase &&
                    !session.HasClue(node.registersClue.id))
                    return true;
            }
            return false;
        }

        public override void SetPromptVisible(bool visible)
        {
            if (visible && interrogationCaseId >= 1 && interrogationCaseId <= 4)
            {
                var core = CoreRuntime.Instance;
                if (core != null && core.Session.CanStartInterrogation(interrogationCaseId))
                    promptText = "[E] Confront";
            }
            base.SetPromptVisible(visible);
        }

        public override void Interact(HubContext ctx)
        {
            base.Interact(ctx);
            var core = Morsoul.Core.CoreRuntime.Instance;
            if (core != null && core.Session.CanStartInterrogation(interrogationCaseId))
            {
                if (core.Session.CurrentSceneMode != SceneMode.Mode2D)
                    core.Session.SetSceneContext(SceneMode.Mode2D,
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
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
            const string briefedFlag = "Guild.Case5Briefed";
            const string confirmFlag = "Guild.Case5Confirmed";
            core.Session.SetFlag(confirmFlag, false);

            var data = ScriptableObject.CreateInstance<DialogueData>();
            data.hideFlags = HideFlags.DontSave;
            data.portraits = portraits;

            if (!core.Session.HasFlag(briefedFlag))
            {
                core.Session.SetFlag(briefedFlag, true);
                data.id = "OvanCase5Briefing";
                data.startNodeId = "b1";
                data.nodes = new[]
                {
                    new DialogueNodeData
                    {
                        id = "b1",
                        speakerName = "Guildmaster Ovan",
                        text = "Every case you closed—from the Lower Market solvent to Iron Hollow's breach—points to one coordinated operation beneath Asterveil.",
                        nextNodeId = "b2"
                    },
                    new DialogueNodeData
                    {
                        id = "b2",
                        speakerName = "Eliza",
                        text = "He's right, {PlayerName}. None of those were isolated crimes. The Veiled Circle has been testing First-Age seal-breakers across the entire city.",
                        nextNodeId = "b3"
                    },
                    new DialogueNodeData
                    {
                        id = "b3",
                        speakerName = "Golem",
                        text = "[THREAT ASSESSMENT: CRITICAL] If their siphon network converges on the Sanctum Core, Asterveil's containment will fail.",
                        nextNodeId = "b4"
                    },
                    new DialogueNodeData
                    {
                        id = "b4",
                        speakerName = "Guildmaster Ovan",
                        text = "Before we draft our emergency report for the Empirical Guards, gather everything you still need—especially any First-Age Runic Logs your Aether Sight can uncover. Speak to me again when you're ready."
                    }
                };
                ctx.PlayDialogue(data);
                return;
            }

            int logs = core.Session.RunicLogCount;
            data.id = "OvanCase5Prompt";
            data.startNodeId = "n1";
            data.nodes = new[]
            {
                new DialogueNodeData
                {
                    id = "n1",
                    speakerName = "Guildmaster Ovan",
                    text = $"Our Guild scouts have compiled their own findings on the Veiled Circle (Runic Logs: {logs}/4). Are you ready to build a structured report alarming enough to mobilize the Empirical Guards?",
                    options = new[]
                    {
                        new DialogueOptionData { label = "I'm ready — let's draft the Guild report.", nextNodeId = "proceed" },
                        new DialogueOptionData { label = "Not yet — I still have things to gather.", nextNodeId = "wait" }
                    }
                },
                new DialogueNodeData
                {
                    id = "proceed",
                    speakerName = "Guildmaster Ovan",
                    text = "Good. Take the Guild's investigation dossier and lay out the proof—step by step—until the Guards have no choice but to act.",
                    setsFlag = confirmFlag
                },
                new DialogueNodeData
                {
                    id = "wait",
                    speakerName = "Guildmaster Ovan",
                    text = "Take whatever time you need. Revisit the districts via the Carrier and talk to me again when you're ready."
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

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
            if (clue == null)
            {
                var core = Morsoul.Core.CoreRuntime.Instance;
                if (core != null && gameObject.name == "GuildBoard" && core.Session.CurrentCase >= 2)
                {
                    int caseId = core.Session.CurrentCase;
                    core.Session.SetFlag($"Guild.CheckBoard.{caseId}", false);
                    core.Session.SetFlag(ElizaCompanionFollower.JoinedFlag);
                    if (caseId == 2)
                    {
                        core.Session.SetFlag(ElizaCarrierGuideController.PendingFlag(caseId));
                        if (ctx != null) ctx.PlayDialogue(BuildGuildBoardDialogue(caseId));
                        ElizaCompanionFollower.AttachIfJoined(ctx);
                    }
                    else
                    {
                        bool race = UnityEngine.Random.value < 0.5f;
                        core.Session.SetFlag(ElizaHubPresenceController.RaceToCarrierFlag, race);
                        if (ctx != null && ctx.dialogue != null)
                        {
                            if (race)
                            {
                                void OnBoardDone()
                                {
                                    ctx.dialogue.DialogueEnded -= OnBoardDone;
                                    ElizaHubPresenceController.StartRaceOutOfGuild(ctx);
                                }
                                ctx.dialogue.DialogueEnded += OnBoardDone;
                            }
                            ctx.PlayDialogue(BuildGuildBoardDialogue(caseId));
                        }
                    }
                    return;
                }
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
            bool newlyRegistered = !_inspected && ctx.journal.Register(clue);
            _inspected = true;
            if (inspectionDialogue != null)
                ctx.PlayDialogue(inspectionDialogue);
            else
                ctx.PlayDialogue(BuildFallbackDialogue(clue, newlyRegistered));
        }

        static Morsoul.Dialogue.DialogueData BuildGuildBoardDialogue(int caseId)
        {
            string posting = caseId switch
            {
                2 => "[REQUEST — CATHEDRAL OF SAINT ALTHEA]\n" +
                     "Mother Lyra was silenced before delivering her ledger. " +
                     "Investigators cleared for Tier-II Arcane Inquiry: take the Carrier from Lower Market to the Cathedral Courtyard.",
                3 => "[URGENT DISPATCH — WHISPERWOOD GROVE]\n" +
                     "Unexplained blight is spreading from the elder grove. " +
                     "Qualified investigators: meet Ranger Kaelen via the Carrier and secure the Perimeter Seal.",
                4 => "[PRIORITY CONTRACT — IRON HOLLOW]\n" +
                     "Unauthorized Veiled Circle activity reported near Drift 09. " +
                     "Take the Carrier to Iron Hollow and present Guild authority to Overseer Thrun.",
                _ => "[SEALED DOSSIER — SANCTUM CORE]\n" +
                     "All regional inquiries converge beneath Asterveil. " +
                     "Review your Deduction Board [H] to reconstruct the final truth."
            };

            var data = ScriptableObject.CreateInstance<Morsoul.Dialogue.DialogueData>();
            data.hideFlags = HideFlags.DontSave;
            data.id = $"GuildBoard_C{caseId}";
            data.startNodeId = "n1";
            if (caseId <= 4)
            {
                data.nodes = new[]
                {
                    new Morsoul.Dialogue.DialogueNodeData
                    {
                        id = "n1",
                        speakerName = "Guild Quest Board",
                        text = posting,
                        nextNodeId = "n2"
                    },
                    new Morsoul.Dialogue.DialogueNodeData
                    {
                        id = "n2",
                        speakerName = "Eliza",
                        text = "I'm joining you on this one, {PlayerName}. Let's head out to the Lower Market—I'll guide you straight to the Carrier."
                    }
                };
            }
            else
            {
                data.nodes = new[]
                {
                    new Morsoul.Dialogue.DialogueNodeData
                    {
                        id = "n1",
                        speakerName = "Guild Quest Board",
                        text = posting
                    }
                };
            }
            return data;
        }

        static Morsoul.Dialogue.DialogueData BuildFallbackDialogue(ClueData target, bool newlyRegistered)
        {
            string title = string.IsNullOrEmpty(target.displayName) ? target.id : target.displayName;
            string desc = string.IsNullOrEmpty(target.description) ? "" : $"\n{target.description}";
            string suffix = newlyRegistered ? "\n\n[Clue added to Journal]" : "";

            var data = ScriptableObject.CreateInstance<Morsoul.Dialogue.DialogueData>();
            data.hideFlags = HideFlags.DontSave;
            data.id = $"Inspect_{target.id}";
            data.startNodeId = "n1";
            data.nodes = new[]
            {
                new Morsoul.Dialogue.DialogueNodeData
                {
                    id = "n1",
                    speakerName = "Observation",
                    text = $"{title}{desc}{suffix}"
                }
            };
            return data;
        }
    }
}

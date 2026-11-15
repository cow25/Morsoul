using System;
using System.Collections;
using Morsoul.Core;
using Morsoul.Data;
using Morsoul.Dialogue;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Hub
{
    /// <summary>
    /// Coordinates Eliza's asymmetric hub presence across the campaign:
    /// stations her on independent investigation leads in Guild/Market/Cathedral/Iron Hollow,
    /// activates active-follower escort in Whisperwood, attaches direct talk interaction,
    /// fires Communicator Crystal check-ins + tool/Runic-Log reaction beats, and
    /// provides a pre-descent warning at Iron Hollow's Sanctum lift when logs remain.
    /// </summary>
    public class ElizaHubPresenceController : MonoBehaviour
    {
        public const string SanctumWarningFlag = "Sanctum.WarningShown";

        public HubContext context;

        bool _staged;
        bool _monitorRunning;

        void Start()
        {
            if (context == null) context = GetComponent<HubContext>() ?? FindAnyObjectByType<HubContext>();
            var core = CoreRuntime.Instance;
            if (core == null || context == null) return;

            TryStageEliza(core.Session);
            if (!_monitorRunning)
            {
                _monitorRunning = true;
                StartCoroutine(MonitorAsyncBeats(core));
            }
        }

        void Update()
        {
            var core = CoreRuntime.Instance;
            if (!_staged && core != null)
                TryStageEliza(core.Session);
        }

        void TryStageEliza(SessionState session)
        {
            if (session == null || context == null) return;
            string sceneName = SceneManager.GetActiveScene().name;
            int caseId = session.CurrentCase;

            if (IsCutsceneOrIntroBusy(session, sceneName, caseId))
                return;

            // In Hub_Guild, wait until Eliza finishes 2F intro/briefing AND player walks downstairs (so she never pops away mid-floor).
            if (string.Equals(sceneName, "Hub_Guild", StringComparison.OrdinalIgnoreCase))
            {
                if (!session.HasFlag(GuildIntroRules.IntroSeenFlag)) return;
                var playerCtrl = FindAnyObjectByType<HubPlayerController>();
                if (playerCtrl != null && playerCtrl.transform.position.y > 18f)
                    return;
            }
            else if (!session.HasFlag(GuildIntroRules.IntroSeenFlag) && caseId <= 1)
            {
                return;
            }

            var elizaGo = FindOrCreateEliza(context);
            if (elizaGo == null) return;

            bool followMode = ShouldFollowInScene(sceneName);
            if (followMode)
            {
                if (!elizaGo.activeSelf)
                {
                    var playerCtrl = FindAnyObjectByType<HubPlayerController>();
                    Vector3 basePos = playerCtrl != null ? playerCtrl.transform.position : Vector3.zero;
                    elizaGo.transform.position = basePos + new Vector3(-1.2f, 0f, 0f);
                }
                elizaGo.SetActive(true);
                ElizaCompanionFollower.AttachIfJoined(context, forceAttach: true);
            }
            else
            {
                var follower = elizaGo.GetComponent<ElizaCompanionFollower>();
                if (follower != null) follower.enabled = false;
                elizaGo.transform.position = ResolveStationPosition(sceneName, caseId);
                elizaGo.SetActive(true);
            }

            AttachTalkInteractable(elizaGo, sceneName, blocksMovement: !followMode);
            _staged = true;
        }

        static bool ShouldFollowInScene(string sceneName) =>
            string.Equals(sceneName, "Hub_Wilderness", StringComparison.OrdinalIgnoreCase);

        static Vector3 ResolveStationPosition(string sceneName, int caseId)
        {
            if (string.Equals(sceneName, "Hub_Guild", StringComparison.OrdinalIgnoreCase))
                return new Vector3(3.6f, 4.8f, 0f);
            if (string.Equals(sceneName, "Hub_LowerMarket", StringComparison.OrdinalIgnoreCase))
                return caseId >= 2 && caseId <= 4
                    ? new Vector3(-20.8f, -1.5f, 0f)
                    : new Vector3(-7.0f, 7.2f, 0f);
            if (string.Equals(sceneName, "Hub_CathedralNave", StringComparison.OrdinalIgnoreCase))
                return new Vector3(-2.0f, 1.5f, 0f);
            if (string.Equals(sceneName, "Hub_IronHollow", StringComparison.OrdinalIgnoreCase))
                return caseId >= 5
                    ? new Vector3(4.5f, 2.4f, 0f)
                    : new Vector3(-0.5f, 2.2f, 0f);
            return new Vector3(-2.0f, 1.0f, 0f);
        }

        static GameObject FindOrCreateEliza(HubContext ctx)
        {
            VisualProfile profile = ctx != null ? ctx.elizaProfile : null;
#if UNITY_EDITOR
            if (profile == null)
                profile = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualProfile>("Assets/Art/Profiles/Eliza.asset");
#endif
            var all = FindObjectsByType<EntityVisual>(FindObjectsInactive.Include);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                string n = all[i].gameObject.name;
                if (n == "Eliza" || n == "ArrivalEliza")
                {
                    if (all[i].profile == null && profile != null)
                        all[i].Apply(profile);
                    return all[i].gameObject;
                }
            }

            var go = new GameObject("Eliza");
            go.AddComponent<YSort2D>();
            var visual = go.AddComponent<EntityVisual>();
            if (profile != null)
            {
                visual.Apply(profile);
            }
            else
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.color = new Color(0.9f, 0.55f, 0.35f, 1f);
            }
            return go;
        }

        static void AttachTalkInteractable(GameObject elizaGo, string sceneName, bool blocksMovement)
        {
            bool hasTrigger = false;
            foreach (var col in elizaGo.GetComponents<BoxCollider2D>())
            {
                if (col.isTrigger) { hasTrigger = true; break; }
            }
            if (!hasTrigger)
            {
                var trigger = elizaGo.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
                trigger.size = new Vector2(1.5f, 1.5f);
            }

            var talk = elizaGo.GetComponent<ElizaTalkInteractable>() ?? elizaGo.AddComponent<ElizaTalkInteractable>();
            talk.hubSceneName = sceneName;
            talk.promptText = "[E] Talk to Eliza";
            talk.blocksMovement = blocksMovement;

            if (talk.promptIndicator == null)
            {
                var existingPrompt = elizaGo.transform.Find("Prompt");
                if (existingPrompt != null)
                {
                    talk.promptIndicator = existingPrompt.gameObject;
                }
                else
                {
                    var promptGo = new GameObject("Prompt");
                    promptGo.transform.SetParent(elizaGo.transform, false);
                    promptGo.transform.localPosition = new Vector3(0f, 1.35f, 0f);
                    var label = promptGo.AddComponent<InteractableLabel>();
                    label.text = "[E] Talk to Eliza";
                    label.color = new Color(1f, 0.85f, 0.2f);
                    label.fontSize = 15;
                    promptGo.SetActive(false);
                    talk.promptIndicator = promptGo;
                }
            }

            if (elizaGo.transform.Find("NameLabel") == null)
            {
                var nameGo = new GameObject("NameLabel");
                nameGo.transform.SetParent(elizaGo.transform, false);
                nameGo.transform.localPosition = new Vector3(0f, 1.05f, 0f);
                var label = nameGo.AddComponent<InteractableLabel>();
                label.text = "Eliza";
                label.color = new Color(1f, 1f, 1f, 0.85f);
                label.fontSize = 15;
            }
        }

        IEnumerator MonitorAsyncBeats(CoreRuntime core)
        {
            while (this != null && gameObject.activeInHierarchy)
            {
                yield return null;
                yield return null;

                var session = core.Session;
                string sceneName = SceneManager.GetActiveScene().name;
                if (context == null || context.dialogue == null) continue;
                if (context.UiBlocked || IsCutsceneOrIntroBusy(session, sceneName, session.CurrentCase)) continue;
                if (HasPendingInterlude(session)) continue;

                DialogueData pending = TryPickNextAsyncBeat(session, sceneName, out string flagToLatch);
                if (pending == null) continue;

                if (!string.IsNullOrEmpty(flagToLatch))
                    session.SetFlag(flagToLatch);

                if (context.PlayDialogue(pending))
                {
                    while (context != null && context.dialogue != null && context.dialogue.IsPlaying)
                        yield return null;
                }
            }
        }

        static bool IsCutsceneOrIntroBusy(SessionState session, string sceneName, int caseId)
        {
            if (session == null) return true;
            if (caseId >= 1 && caseId <= 4 && session.HasFlag(PostInterrogationCutsceneController.PendingFlag(caseId)))
                return true;
            if (caseId >= 2 && caseId <= 4 && session.HasFlag(ElizaCarrierGuideController.PendingFlag(caseId)) &&
                string.Equals(sceneName, "Hub_LowerMarket", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(sceneName, "Hub_LowerMarket", StringComparison.OrdinalIgnoreCase) &&
                GuildIntroRules.CanPlayMarketArrival(session))
                return true;
            if (string.Equals(sceneName, "Hub_Guild", StringComparison.OrdinalIgnoreCase) &&
                (GuildIntroRules.CanPlayGuildIntro(session) || GuildIntroRules.IsBriefingPending(session, caseId)))
                return true;
            return false;
        }

        static bool HasPendingInterlude(SessionState session)
        {
            if (session == null) return false;
            var triggers = FindObjectsByType<HubArrivalTrigger>(FindObjectsInactive.Exclude);
            for (int i = 0; i < triggers.Length; i++)
            {
                var t = triggers[i];
                if (t == null || t.dialogue == null) continue;
                if (session.CurrentCase < t.minCurrentCase) continue;
                if (session.HasFlag(t.playedFlag)) continue;
                if (!string.IsNullOrEmpty(t.requiredFlag) && !session.HasFlag(t.requiredFlag)) continue;
                return true;
            }
            return false;
        }

        static DialogueData TryPickNextAsyncBeat(SessionState session, string sceneName, out string flagToLatch)
        {
            flagToLatch = null;

            // 1. Case 1 Crystal check-in on Lower Market solvent discovery.
            if (string.Equals(sceneName, "Hub_LowerMarket", StringComparison.OrdinalIgnoreCase) &&
                session.CurrentCase == 1 &&
                session.HasFlag("Guild.CrystalReceived") &&
                (session.HasClue("C3_01") || session.HasClue("C1_C3")) &&
                !session.HasFlag("Crystal.C1.Solvent"))
            {
                flagToLatch = "Crystal.C1.Solvent";
                return MakeDialogue("D_Crystal_C1_Solvent",
                    ("[CRYSTAL LINK — Eliza]", "Wait—{PlayerName}, can you hear me? My Communicator Crystal just vibrated warm when your scan note crossed."),
                    ("[CRYSTAL LINK — Eliza]", "That solvent you pulled off Ignatius' vault floor—I checked every black-market fence between here and the wharf. Nobody in Asterveil distills a compound that leaves cold iron untouched."),
                    ("Golem", "[LINK RECEIVED: Correlating outside-broker supply with First-Age chemical grade. Cross-check Ignatius when ready.]"));
            }

            // 2. Case 3 Emotion Echo baseline resonance with Eliza.
            if (string.Equals(sceneName, "Hub_Wilderness", StringComparison.OrdinalIgnoreCase) &&
                HasUnlockedTool(session, Tool.EmotionEcho) &&
                !session.HasFlag("Crystal.C3.Echo"))
            {
                flagToLatch = "Crystal.C3.Echo";
                return MakeDialogue("D_Tool_C3_Echo",
                    ("Golem", "[EMOTION ECHO CALIBRATED: Scanning nearest human baseline (Eliza Astra)... Anomaly detected. Beneath surface focus lies an unindexed, centuries-old protective resonance directed toward this unit.]"),
                    ("Eliza", "(Blinks, resting a hand on her ribs) Hey—why did your lens flare violet at me just now? Quit staring like that... Wait. Why did the hair on my neck stand up?"),
                    ("Golem", "Calibration complete. Residual familiarity signature archived for later analysis."));
            }

            // 3. Case 4 Aether Sight architectural recognition & revisit prompt.
            if (string.Equals(sceneName, "Hub_IronHollow", StringComparison.OrdinalIgnoreCase) &&
                HasUnlockedTool(session, Tool.AetherSight) &&
                !session.HasFlag("Crystal.C4.AetherSight"))
            {
                flagToLatch = "Crystal.C4.AetherSight";
                return MakeDialogue("D_Tool_C4_AetherSight",
                    ("Golem", "[AETHER SIGHT ONLINE: Deep sub-laboratory wall geometry matches corrupted Memory Sector 00 by 98.4%.] \"Eliza. With these optics active, dormant inscriptions inside our earlier crime scenes will become legible.\""),
                    ("Eliza", "(Stares at the glowing cyan lens, brow furrowed) Sector Zero? Those old sites across Asterveil... with eyes like yours now, I wonder what we missed before we ride down to the Sanctum."));
            }

            // 4. Stage 4: Runic Log (L1–L4) reactions as each log is deciphered.
            if (!session.HasFlag("Guild.IntroPending") &&
                !(string.Equals(sceneName, "Hub_Guild", StringComparison.OrdinalIgnoreCase) && !session.HasFlag("Guild.CrystalReceived")))
            {
                for (int i = 0; i < session.RunicLogs.Count; i++)
                {
                    if (!session.RunicLogs[i]) continue;
                    string flag = $"RunicReaction.L{i + 1}";
                    if (session.HasFlag(flag)) continue;
                    flagToLatch = flag;
                    return BuildRunicLogReaction(i + 1, session.RunicLogCount);
                }
            }

            return null;
        }

        static DialogueData BuildRunicLogReaction(int logNumber, int totalLogs)
        {
            return logNumber switch
            {
                1 => MakeDialogue("D_RunicReaction_L1",
                    ("Golem", "[RUNIC LOG #1 LOGGED: Section Zero Solvent Volatility record decoded. First-Age containment teams burned volatile purge reagents beneath Asterveil before the Cataclysm.]"),
                    ("Eliza", "(Quietly) A reagent brewed to erase evidence before a disaster... Why does 'Section Zero' sound like cold water down my back? Whoever supplied Ignatius got their recipe from that tomb.")),
                2 => MakeDialogue("D_RunicReaction_L2",
                    ("Golem", "[RUNIC LOG #2 LOGGED: Acoustic Stabilization array. Saint Althea's lower belfry plate was forged centuries before the Church to soothe planetary mana tremors.]"),
                    ("Eliza", "No wonder that humming bell felt wrong inside my skull. The Orthodoxy raised a cathedral right over a First-Age warning chime—and the Circle knew how to invert it.")),
                3 => MakeDialogue("D_RunicReaction_L3",
                    ("Golem", "[RUNIC LOG #3 LOGGED: Sub-Facility Section Zero — Displacement Telemetry. Operative E. Cochran cut her own extraction tether to bind a dying investigator's fading soul into a brass containment vessel.]"),
                    ("Golem", "[RECOVERED AUDIO FRAGMENT]: 'Keep the lens focused. The mind forgets, but the soul remembers.'"),
                    ("Eliza", "(Goes dead still, pressing a hand tight against her breastbone as her voice drops to a whisper) 'The soul remembers'... {PlayerName}... why does my chest hurt reading a ghost's final ledger?"),
                    ("Golem", "[TELEMETRY: Subject pulse elevated to 118 BPM. Acute grief response detected. Cross-linking with Prologue Anomaly #01.]")),
                _ => MakeDialogue("D_RunicReaction_L4",
                    ("Golem", $"[RUNIC LOG #4 LOGGED: Planetary Stabilizer Console override formulas reconstructed ({totalLogs}/4 archived). Direct bridge requires an intact conscious anchor core.]"),
                    ("Eliza", totalLogs >= 4
                        ? "Four out of four ciphers unlocked. No more blind spots, partner. Let's take the Sanctum lift in Iron Hollow and finish what three thousand years couldn't."
                        : $"That blueprint matters—and we still have dormant First-Age markings waiting in the earlier districts ({totalLogs}/4 collected). Let's decide whether to sweep them via the Carrier before facing the Overseer."))
            };
        }

        /// <summary>
        /// Pauses first interaction with Iron Hollow's Sanctum lift (CaseDoor)
        /// when Case 5 is active and fewer than 4 Runic Logs have been collected.
        /// </summary>
        public static bool TryWarnBeforeSanctumDescent(HubContext ctx)
        {
            var core = CoreRuntime.Instance;
            if (ctx == null || ctx.dialogue == null || core == null) return false;
            var session = core.Session;
            if (session.CurrentCase != 5 || session.RunicLogCount >= 4 || session.HasFlag(SanctumWarningFlag))
                return false;

            session.SetFlag(SanctumWarningFlag);
            ctx.PlayDialogue(MakeDialogue("D_Sanctum_LiftWarning",
                ("Eliza", $"(Catches your iron sleeve beside the freight cage) \"Hold on, {{PlayerName}}! Once we ride this lift down into the Sanctum Core, there's no elevator back up.\""),
                ("Eliza", $"\"Your Aether Sight has only recovered {session.RunicLogCount} of 4 dormant First-Age Runic Logs across Asterveil. If you want the full truth from the Lower Market, Cathedral, or Whisperwood first, take the Carrier back.\""),
                ("Eliza", "\"Otherwise, step up to the cage again whenever you're ready—and we'll end this together.\"")));
            return true;
        }

        static bool HasUnlockedTool(SessionState session, Tool tool)
        {
            if (session == null || session.Tools == null) return false;
            foreach (var t in session.Tools)
                if (t == tool) return true;
            return false;
        }

        static DialogueData MakeDialogue(string id, params (string speaker, string text)[] lines)
        {
            var data = ScriptableObject.CreateInstance<DialogueData>();
            data.hideFlags = HideFlags.DontSave;
            data.id = id;
            data.startNodeId = "n0";
            data.nodes = new DialogueNodeData[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                data.nodes[i] = new DialogueNodeData
                {
                    id = $"n{i}",
                    speakerName = lines[i].speaker,
                    text = lines[i].text,
                    nextNodeId = i + 1 < lines.Length ? $"n{i + 1}" : ""
                };
            }
            return data;
        }
    }
}

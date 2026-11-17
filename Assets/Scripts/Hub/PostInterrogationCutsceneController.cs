using System.Collections;
using System.Collections.Generic;
using Morsoul.Core;
using Morsoul.Dialogue;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// In-world post-interrogation cutscene played in the suspect's hub right after
    /// Case 1–4 resolution is dismissed: guards march in and drag the culprit away,
    /// Eliza comments in person on the player's work, and the scene hands back to
    /// CaseCompletedEvent so GSM returns to the Guild for Ovan's debrief.
    /// </summary>
    public class PostInterrogationCutsceneController : MonoBehaviour
    {
        public static string PendingFlag(int caseId) => $"PostInterrogation.Pending.{caseId}";

        public HubContext context;
        [Min(0.1f)] public float timingScale = 1f;

        HubPlayerController _player;
        Rigidbody2D _playerBody;
        readonly List<GameObject> _tempActors = new List<GameObject>();

        void Start()
        {
            var core = CoreRuntime.Instance;
            if (core == null) return;

            int caseId = core.Session.CurrentCase;
            if (caseId < 1 || caseId > 4 || !core.Session.HasFlag(PendingFlag(caseId)))
                return;

            if (context == null) context = GetComponent<HubContext>() ?? FindAnyObjectByType<HubContext>();
            _player = FindAnyObjectByType<HubPlayerController>();
            _playerBody = _player != null ? _player.GetComponent<Rigidbody2D>() : null;
            if (_player != null) _player.enabled = false;

            StartCoroutine(Run(core, caseId));
        }

        void OnDestroy()
        {
            for (int i = 0; i < _tempActors.Count; i++)
            {
                if (_tempActors[i] != null) Destroy(_tempActors[i]);
            }
            _tempActors.Clear();
        }

        IEnumerator Run(CoreRuntime core, int caseId)
        {
            var dialogue = context != null ? context.dialogue : null;
            yield return FadeTo(dialogue, 1f, 0f);

            var suspectNpc = FindSuspect(caseId);
            Vector3 suspectPos = suspectNpc != null
                ? suspectNpc.transform.position
                : (_player != null ? _player.transform.position + new Vector3(2f, 0f, 0f) : Vector3.zero);

            if (suspectNpc != null)
            {
                suspectNpc.enabled = false;
                var col = suspectNpc.GetComponent<Collider2D>();
                if (col != null) col.enabled = false;
            }

            // Stage player & camera facing the arrest.
            Vector3 playerPos = suspectPos + new Vector3(-2.1f, -0.5f, 0f);
            if (_player != null) MoveTarget(_player.transform, playerPos);
            var cam = FindAnyObjectByType<CameraFollow2D>();
            if (cam != null && _player != null) cam.SnapToTarget(_player.transform);

            // Stage Eliza in person beside the player.
            Transform eliza = PrepareEliza(playerPos + new Vector3(-1.3f, 0.1f, 0f));

            // Optional case-specific bystanders (e.g., Thrun, Kaelen, Sylvan, Valerius, Brokk).
            Transform thrun = caseId == 4 ? FindActor("Thrun") : null;
            if (thrun != null) thrun.position = suspectPos + new Vector3(-1.8f, 1.1f, 0f);
            Transform kaelen = caseId == 3 ? FindActor("Kaelen") : null;
            if (kaelen != null) kaelen.position = playerPos + new Vector3(-2.8f, -1.8f, 0f);
            Transform sylvan = caseId == 2 ? FindActor("Sylvan") : null;
            Transform valerius = caseId == 2
                ? SpawnTempGuard("CaptainValerius", suspectPos + new Vector3(-1.8f, 1.3f, 0f))
                : null;

            // Stage two knights/guards flanking the suspect from off-side.
            Transform guardL = SpawnTempGuard("ArrestGuard_L", suspectPos + new Vector3(-4.0f, 0.5f, 0f));
            Transform guardR = SpawnTempGuard("ArrestGuard_R", suspectPos + new Vector3(4.0f, 0.5f, 0f));

            yield return FadeTo(dialogue, 0f, 0.5f);

            // Case-specific confrontation / desperation beat before custody lock-in.
            yield return RunPreArrestAction(caseId, suspectNpc != null ? suspectNpc.transform : null,
                suspectPos, playerPos, eliza, thrun, valerius);

            // Guards march in to flank the culprit (with Kaelen in Case 3).
            const float walkSpeed = 3.2f;
            var leftIn = StartCoroutine(MoveAtSpeed(guardL, suspectPos + new Vector3(-0.95f, 0f, 0f), walkSpeed));
            var rightIn = StartCoroutine(MoveAtSpeed(guardR, suspectPos + new Vector3(0.95f, 0f, 0f), walkSpeed));
            var kaelenIn = kaelen != null
                ? StartCoroutine(MoveAtSpeed(kaelen, playerPos + new Vector3(-0.2f, -1.5f, 0f), walkSpeed))
                : null;
            yield return leftIn;
            yield return rightIn;
            if (kaelenIn != null) yield return kaelenIn;

            yield return Play(BuildArrestDialogue(caseId));

            // Step player, Eliza, and bystanders aside so the guards and culprit pass cleanly without pushing them.
            Vector3 asideOffset = caseId == 2 ? new Vector3(-1.6f, 0f, 0f) : new Vector3(0f, -1.8f, 0f);
            Vector3 asidePlayerPos = playerPos + asideOffset;
            var stepPlayer = _player != null ? StartCoroutine(MoveAtSpeed(_player.transform, asidePlayerPos, 2.8f)) : null;
            var stepEliza = eliza != null ? StartCoroutine(MoveAtSpeed(eliza, asidePlayerPos + new Vector3(-1.2f, 0f, 0f), 2.8f)) : null;
            var stepKaelen = kaelen != null ? StartCoroutine(MoveAtSpeed(kaelen, asidePlayerPos + new Vector3(1.2f, 0f, 0f), 2.8f)) : null;
            if (stepPlayer != null) yield return stepPlayer;
            if (stepEliza != null) yield return stepEliza;
            if (stepKaelen != null) yield return stepKaelen;
            playerPos = asidePlayerPos;

            // Guards drag the culprit away together along a clear exit route.
            const float escortSpeed = 2.6f;
            if (caseId == 2)
            {
                // Cathedral Nave: walk west to the center aisle (x = 0.5), then south out CathedralExit (y = -10.8).
                yield return EscortTrioTo(guardL, suspectNpc != null ? suspectNpc.transform : null, guardR,
                    new Vector3(0.5f, suspectPos.y, 0f), escortSpeed);
                yield return EscortTrioTo(guardL, suspectNpc != null ? suspectNpc.transform : null, guardR,
                    new Vector3(0.5f, -10.8f, 0f), escortSpeed);
            }
            else if (caseId == 3)
            {
                // Whisperwood: walk west down the trail toward the Asterveil road (away from BriarGate).
                yield return EscortTrioTo(guardL, suspectNpc != null ? suspectNpc.transform : null, guardR,
                    new Vector3(-11.5f, 0.0f, 0f), escortSpeed);
            }
            else
            {
                Vector3 exitDelta = new Vector3(10.0f, 0f, 0f);
                yield return EscortTrioTo(guardL, suspectNpc != null ? suspectNpc.transform : null, guardR,
                    suspectPos + exitDelta, escortSpeed);
            }

            if (suspectNpc != null) suspectNpc.gameObject.SetActive(false);
            if (guardL != null) guardL.gameObject.SetActive(false);
            if (guardR != null) guardR.gameObject.SetActive(false);

            // Case-specific post-arrest resolution (free Sylvan, shut siphon, unseal Brokk).
            yield return RunPostArrestResolution(caseId, suspectPos, playerPos, eliza, sylvan, thrun);

            // Eliza steps closer and comments in person on our work + overarching lead.
            if (eliza != null)
                yield return MoveAtSpeed(eliza, playerPos + new Vector3(-1.05f, 0.05f, 0f), 2.5f);

            yield return Play(BuildElizaDebriefDialogue(caseId));

            if (caseId == 1)
            {
                core.Session.UnlockTool(Morsoul.Data.Tool.EmotionEcho);
                core.Session.SetFlag("Crystal.C3.Echo");
                yield return Play(BuildEmotionEchoAwakeningDialogue());
                yield return SkillUnlockModal.Show(Morsoul.Data.Tool.EmotionEcho, context);
            }
            else if (caseId == 3)
            {
                core.Session.UnlockTool(Morsoul.Data.Tool.AetherSight);
                core.Session.SetFlag("Crystal.C4.AetherSight");
                yield return Play(BuildAetherSightAwakeningDialogue());
                yield return SkillUnlockModal.Show(Morsoul.Data.Tool.AetherSight, context);
            }

            yield return FadeTo(dialogue, 1f, 0.5f);
            core.Session.SetFlag(PendingFlag(caseId), false);
            core.Bus.Publish(new CaseCompletedEvent { CaseId = caseId, Ending = 0 });
        }

        IEnumerator EscortTrioTo(Transform guardL, Transform suspect, Transform guardR,
            Vector3 centerDest, float speed)
        {
            var dragL = StartCoroutine(MoveAtSpeed(guardL, centerDest + new Vector3(-0.75f, 0f, 0f), speed));
            var dragS = suspect != null
                ? StartCoroutine(MoveAtSpeed(suspect, centerDest, speed))
                : null;
            var dragR = StartCoroutine(MoveAtSpeed(guardR, centerDest + new Vector3(0.75f, 0f, 0f), speed));
            yield return dragL;
            if (dragS != null) yield return dragS;
            yield return dragR;
        }

        IEnumerator RunPreArrestAction(int caseId, Transform suspect, Vector3 suspectPos,
            Vector3 playerPos, Transform eliza, Transform thrun, Transform valerius)
        {
            if (caseId == 3)
            {
                // Vane draws a trench dagger and lunges; Eliza intercepts and pins him.
                yield return Play(MakeChain("D_C3_VaneDesperate",
                    ("Vane", "Wait until the Sanctum opens! Ignorant puppets—every impure mutation on this continent will be washed clean! I won't let Guild hounds block our destiny!")));
                if (suspect != null)
                    yield return MoveAtSpeed(suspect, playerPos + new Vector3(0.95f, 0.25f, 0f), 4.6f);
                if (eliza != null)
                    yield return MoveAtSpeed(eliza, playerPos + new Vector3(0.55f, 0.05f, 0f), 6.5f);
                yield return Play(MakeChain("D_C3_ElizaParry",
                    ("Eliza", "(Intercepts Vane's blade in a flash, sweeping his boots onto the dirt and pressing steel to his throat) \"Try it, zealot. Give me an excuse.\""),
                    ("Vane", "(Gasping beneath her knee) \"You... street filth... The Overseer already holds the keys... The First Age will rise...\"")));
                yield break;
            }

            if (caseId == 4)
            {
                // Harlan reaches for a magnesium blasting cap; Eliza & Thrun disarm him.
                yield return Play(MakeChain("D_C4_HarlanDetonator",
                    ("Harlan", "(Pulling a crackling magnesium blasting cap from his boot) \"If I can't deliver the Drift records, I'll bury you all in the dark! Long live the Purified Earth!\"")));
                if (thrun != null)
                    yield return MoveAtSpeed(thrun, suspectPos + new Vector3(-0.75f, 0.1f, 0f), 5.0f);
                if (eliza != null)
                    yield return MoveAtSpeed(eliza, suspectPos + new Vector3(-1.35f, -0.35f, 0f), 5.2f);
                yield return Play(MakeChain("D_C4_HarlanDisarmed",
                    ("Overseer Thrun", "(Brings his brass hammer down flat onto Harlan's wrist, knocking the fuse into a drainage trench) \"You mangy cave-rat! You don't blow up my crew and my mountain on the same shift!\"")));
                yield break;
            }

            if (caseId == 2)
            {
                yield return Play(MakeChain("D_C2_AccordWeep",
                    ("Accord", "(Dropping to his knees beside the chancel stones) \"Forgive me, Mother Lyra! The masked patron with the Eclipsed Eye swore it was only sleeping mist! They swore they'd drag my sister Marian into the salt pits if I didn't silence your voice!\"")));
                yield break;
            }

            yield return Play(MakeChain("D_C1_IgnatiusCollapse",
                ("Master Alchemist Ignatius", "(Slumping back against his satchel in despair) \"The river barges sank in the spring thaw... If the lenders went to the Council, my Guild standing was dead! Nobody was supposed to get hurt!\"")));
        }

        IEnumerator RunPostArrestResolution(int caseId, Vector3 suspectPos, Vector3 playerPos,
            Transform eliza, Transform sylvan, Transform thrun)
        {
            if (caseId == 2)
            {
                if (sylvan != null)
                {
                    sylvan.gameObject.SetActive(true);
                    yield return MoveAtSpeed(sylvan, playerPos + new Vector3(1.1f, -1.1f, 0f), 2.8f);
                }
                yield return Play(MakeChain("D_C2_LyraGrace",
                    ("Captain Valerius", "(Reading softly from High Priestess Lyra's chalk slate) \"'Law must answer for the crime, but compassion must answer for the coercion. Keep Accord's sister under Templar sanctuary—and strike the cold iron from the Spirit-folk youth at once.'\""),
                    ("Acolyte Sylvan", "(Rubbing his bruised wrists, bowing deeply) \"Thank you, {PlayerName}... thank you, Eliza. If you hadn't climbed that tower, they would have hanged me before watch-change.\""),
                    ("High Priestess Lyra (Slate)", "\"Hear this truth before you depart: that Eclipsed Eye seal belongs to the Veiled Circle—an ancient order I last read of in First-Age archives. Someone wanted my voice silenced before I finished charting the deep tremors beneath Asterveil.\"")));
                yield break;
            }

            if (caseId == 3)
            {
                if (_player != null)
                    yield return MoveAtSpeed(_player.transform, suspectPos + new Vector3(0.4f, 0.2f, 0f), 2.8f);
                yield return Play(MakeChain("D_C3_ValveShut",
                    ("Golem", "[ACTUATOR OVERRIDE: Wrenching primary brass shutoff valve clockwise... Siphon conduit sealed. Elder Tree sap flow stabilizing.]"),
                    ("Ranger Scout Kaelen", "\"Listen... the woodland spirits stopped screaming. You saved the entire eastern province, both of you!\"")));
                yield break;
            }

            if (caseId == 4)
            {
                if (_player != null)
                    yield return MoveAtSpeed(_player.transform, suspectPos + new Vector3(0.2f, 1.2f, 0f), 2.8f);
                Transform brokk = SpawnTempMiner("ChiefEngineerBrokk", suspectPos + new Vector3(1.6f, 2.6f, 0f));
                if (brokk != null)
                    yield return MoveAtSpeed(brokk, suspectPos + new Vector3(1.2f, 0.9f, 0f), 2.4f);
                yield return Play(MakeChain("D_C4_BrokkFreed",
                    ("Golem", "[ACTUATOR ENGAGED: Applying maximum radial torque to Drift 09 stasis bulkhead... Vault unsealed.]"),
                    ("Chief Engineer Brokk", "(Staggering out through white steam, clutching his welding visor) \"By the mountain's heart... just down to my last breaths of tanked air! Bless you, construct!\""),
                    ("Overseer Thrun", "\"Brokk! You stubborn old mule—you're alive!\""),
                    ("Chief Engineer Brokk", "\"Listen close—those cultists weren't after ore! They brought deep bores straight through the bedrock! The Grand Overseer just rode the heavy freight lift down into the Sanctum Core with Whisperwood's sap—heading right for the ancient Stabilizer Pylons!\"")));
            }
        }

        static NpcInteractable FindSuspect(int caseId)
        {
            var npcs = FindObjectsByType<NpcInteractable>();
            for (int i = 0; i < npcs.Length; i++)
            {
                if (npcs[i] != null && npcs[i].interrogationCaseId == caseId)
                    return npcs[i];
            }
            return null;
        }

        Transform PrepareEliza(Vector3 position)
        {
            var all = FindObjectsByType<EntityVisual>(FindObjectsInactive.Include);
            EntityVisual source = null;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                string n = all[i].gameObject.name;
                if (n == "ArrivalEliza" || n == "Eliza")
                {
                    source = all[i];
                    break;
                }
            }

            if (source != null)
            {
                if (source.profile == null)
                {
                    var profile = ResolveElizaProfile();
                    if (profile != null) source.Apply(profile);
                }
                source.transform.position = position;
                source.gameObject.SetActive(true);
                return source.transform;
            }

            var go = new GameObject("CutsceneEliza");
            go.transform.position = position;
            _tempActors.Add(go);
            go.AddComponent<YSort2D>();
            var fallbackProfile = ResolveElizaProfile();
            if (fallbackProfile != null)
            {
                go.AddComponent<EntityVisual>().Apply(fallbackProfile);
            }
            else
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.color = new Color(0.9f, 0.55f, 0.35f, 1f);
            }
            return go.transform;
        }

        VisualProfile ResolveElizaProfile()
        {
            if (context != null && context.elizaProfile != null) return context.elizaProfile;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<VisualProfile>("Assets/Art/Profiles/Eliza.asset");
#else
            return null;
#endif
        }

        static Transform FindActor(string exactName)
        {
            var all = FindObjectsByType<EntityVisual>(FindObjectsInactive.Include);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].gameObject.name == exactName)
                {
                    all[i].gameObject.SetActive(true);
                    return all[i].transform;
                }
            }
            return null;
        }

        Transform SpawnTempMiner(string actorName, Vector3 position)
        {
            var all = FindObjectsByType<EntityVisual>(FindObjectsInactive.Include);
            EntityVisual template = null;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                string n = all[i].gameObject.name;
                if (n.Contains("Miner") || n.Contains("Thrun"))
                {
                    template = all[i];
                    break;
                }
            }
            return SpawnFromTemplate(template, actorName, position, new Color(0.55f, 0.45f, 0.35f, 1f));
        }

        Transform SpawnTempGuard(string actorName, Vector3 position) =>
            SpawnFromTemplate(FindGuardTemplate(), actorName, position, new Color(0.45f, 0.5f, 0.62f, 1f));

        Transform SpawnFromTemplate(EntityVisual template, string actorName, Vector3 position, Color fallbackTint)
        {
            GameObject go;
            if (template != null)
            {
                go = Instantiate(template.gameObject, position, Quaternion.identity);
                go.name = actorName;
                var npc = go.GetComponent<NpcInteractable>();
                if (npc != null) Destroy(npc);
                foreach (var c in go.GetComponentsInChildren<Collider2D>(true))
                    Destroy(c);
                var stroller = go.GetComponent<AmbientStroller2D>();
                if (stroller != null) Destroy(stroller);
                go.SetActive(true);
            }
            else
            {
                go = new GameObject(actorName);
                go.transform.position = position;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.color = fallbackTint;
                go.AddComponent<YSort2D>();
            }
            _tempActors.Add(go);
            return go.transform;
        }

        static EntityVisual FindGuardTemplate()
        {
            var all = FindObjectsByType<EntityVisual>(FindObjectsInactive.Include);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                string n = all[i].gameObject.name;
                string pn = all[i].profile != null ? all[i].profile.name : "";
                if (n.Contains("Guard") || n.Contains("Watchman") || n.Contains("Ranger") ||
                    pn.Contains("Guard") || pn.Contains("Watchman") || pn.Contains("Ranger"))
                    return all[i];
            }
            return null;
        }

        static DialogueData BuildArrestDialogue(int caseId)
        {
            (string speaker, string line) = caseId switch
            {
                1 => ("Guild Wardens",
                      "Master Alchemist Ignatius, your license is forfeit. You are under arrest for staging the vault sabotage. Move!"),
                2 => ("Orthodoxy Knights",
                      "Accord, by order of High Priestess Lyra and the Cathedral Tribunal, come quietly to the cloister cells."),
                3 => ("Ranger Scout Kaelen",
                      "Vane, your blight catalyst is forfeit and the Elder Root is safe. In irons—you're coming back to Asterveil!"),
                _ => ("Iron Hollow Guard",
                      "Harlan, keep your hands where we can see them! You're under arrest for the Drift 09 sabotage.")
            };

            return MakeChain($"D_PostInterrogation_Arrest_C{caseId}", (speaker, line));
        }

        static DialogueData BuildElizaDebriefDialogue(int caseId)
        {
            return caseId switch
            {
                1 => MakeChain("D_PostInterrogation_Eliza_C1",
                    ("Eliza", "That was sharp work, {PlayerName}. You cornered Ignatius with the vault evidence before he could cover his ledger trails."),
                    ("Golem", "[DIAGNOSTIC NOTE]: Commercial fraud is resolved, yet one anomaly remains. Ignatius lacked the equipment to distill solvent of First-Age purity—he bought it from a phantom broker."),
                    ("Eliza", "Which means somebody fed him the exact reagent to imitate ancient ruin smoke... and let him test it on Asterveil. Come on—let's get back to the Guild and show Ovan our findings.")),
                2 => MakeChain("D_PostInterrogation_Eliza_C2",
                    ("Eliza", "You didn't flinch once up there, {PlayerName}. Accord broke the moment you laid Mother Lyra's chalk slate against his story."),
                    ("Golem", "[ARCHIVE CORRELATION: Case 1 Solvent + Case 2 Acoustic Plate = First-Age seal countermeasures. Frequency pattern accelerating.]"),
                    ("Eliza", "So the same Veiled Circle using fake brokers in the Lower Market just silenced a Cathedral oracle. Come on—we need Ovan to see how these threads tie together.")),
                3 => MakeChain("D_PostInterrogation_Eliza_C3",
                    ("Golem", "Diagnostic check: Subject Eliza Astra intercepted lethal close-range strike in 0.4 seconds. Thank you... partner."),
                    ("Eliza", "(Wiping dirt off her cuff, a quick grin crossing her face) Hey, somebody has to keep stray blades off that shiny core of yours. And look at this crate Vane dropped—the brass valve carries an Iron Hollow foundry mark."),
                    ("Eliza", "Wait... why are twelve thousand gallons of Elder sap heading into Dwarven mining shafts? Let's report back to Ovan right now.")),
                _ => MakeChain("D_PostInterrogation_Eliza_C4",
                    ("Eliza", "Well played, {PlayerName}. Exposing Harlan saved Chief Brokk—but Cases 1 through 4 were just their supply chain. The Grand Overseer is already down in the Sanctum Core."),
                    ("Golem", "[AETHER SIGHT STATUS]: Dormant First-Age inscriptions across Asterveil are now legible. Recommend reporting to Ovan and reviewing earlier scenes before final descent."),
                    ("Eliza", "And your new eyes might catch what we walked straight past in the Lower Market, the Cathedral, and Whisperwood. Let's check in with the Guild first—then end this."))
            };
        }

        static DialogueData BuildEmotionEchoAwakeningDialogue()
        {
            return MakeChain("D_PostInterrogation_C1_EmotionEcho",
                ("Golem", "[CHASSIS ALERT]: Dormant subroutine awakening. Observing human distress and sustained proximity to Eliza Astra has unlocked a locked module."),
                ("Eliza", "Wait—your chest core just pulsed violet! Are you alright, {PlayerName}?"),
                ("Golem", "[EMOTION ECHO ONLINE]: Scanning nearest baseline (Eliza Astra)... Anomaly. Beneath her focus lies a centuries-old protective resonance toward this unit."),
                ("Eliza", "(Blinks, resting a hand over her heart) Centuries-old? Why does it feel like I've guarded your back a hundred times before?"),
                ("Golem", "Resonance archived. I can now read the emotional tremors behind spoken lies during interrogations."));
        }

        static DialogueData BuildAetherSightAwakeningDialogue()
        {
            return MakeChain("D_PostInterrogation_C3_AetherSight",
                ("Golem", "[CORE SATURATION]: Residual mana absorbed at the Whisperwood ritual site has reached optical threshold."),
                ("Eliza", "Wait—your lens just flared cyan! Did that Elder-root siphon leave something inside your core?"),
                ("Golem", "[AETHER SIGHT ONLINE]: The ritual energy recharged a dormant First-Age prism. Concealed mana conduits and ancient runes are now visible."),
                ("Eliza", "So the very magic the Veiled Circle tried to siphon just gave us eyes to see everything they buried—even in our earlier cases."));
        }

        static DialogueData MakeChain(string id, params (string speaker, string text)[] lines)
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

        IEnumerator Play(DialogueData data)
        {
            var dialogue = context != null ? context.dialogue : null;
            if (data == null || dialogue == null || context == null) yield break;
            if (!context.PlayDialogue(data)) yield break;
            while (dialogue.IsPlaying) yield return null;
        }

        IEnumerator FadeTo(DialogueController dialogue, float target, float seconds)
        {
            if (dialogue != null) yield return dialogue.FadeTo(target, seconds / timingScale);
            else yield return new WaitForSeconds(Mathf.Max(0f, seconds / timingScale));
        }

        IEnumerator MoveAtSpeed(Transform target, Vector3 destination, float unitsPerSecond)
        {
            if (target == null) yield break;
            Vector3 from = target.position;
            float dist = Mathf.Abs(destination.x - from.x) + Mathf.Abs(destination.y - from.y);
            float duration = Mathf.Max(0.2f, dist / Mathf.Max(0.5f, unitsPerSecond)) / timingScale;
            yield return AxisMove.Move(target, destination, duration,
                (to, legDuration) => MoveLeg(target, to, legDuration));
        }

        IEnumerator MoveLeg(Transform target, Vector3 destination, float duration)
        {
            if (target == null) yield break;
            Vector3 start = target.position;
            Vector2 dir = (Vector2)(destination - start);
            var anim = target.GetComponent<HubCharacterAnimator>();
            if (dir.sqrMagnitude > 0.0001f) anim?.Tick(dir.normalized);
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (dir.sqrMagnitude > 0.0001f) anim?.Tick(dir.normalized);
                MoveTarget(target, Vector3.Lerp(start, destination, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            MoveTarget(target, destination);
            anim?.Tick(Vector2.zero);
        }

        void MoveTarget(Transform target, Vector3 position)
        {
            if (_player != null && target == _player.transform && _playerBody != null)
                _playerBody.position = position;
            target.position = position;
        }
    }
}


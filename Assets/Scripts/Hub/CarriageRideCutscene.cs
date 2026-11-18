using System;
using System.Collections;
using System.Collections.Generic;
using Morsoul.Core;
using Morsoul.Data;
using Morsoul.Dialogue;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Plays a scenic 2D carriage-ride cutscene (animated Horse + Carrier
    /// managing the reins + rolling wagon + scrolling road) on first-time
    /// outbound trips to new regions and on post-case returns to Hub_Guild.
    /// </summary>
    public class CarriageRideCutscene : MonoBehaviour
    {
        public static bool TryPlayBeforeTravel(HubContext ctx, string destinationScene, Action proceed)
        {
            var core = CoreRuntime.Instance;
            if (core == null || string.IsNullOrEmpty(destinationScene) || proceed == null)
                return false;

            var session = core.Session;
            DialogueData script = PickCutsceneDialogue(session, destinationScene, out string flagToLatch);
            if (script == null) return false;

            session.SetFlag(flagToLatch, true);

            var host = new GameObject("CarriageRideCutsceneHost");
            var runner = host.AddComponent<CarriageRideCutscene>();
            runner.StartCoroutine(runner.PlaySequence(ctx, script, proceed));
            return true;
        }

        static DialogueData PickCutsceneDialogue(SessionState session, string dest, out string flag)
        {
            flag = null;

            // 1. After-case return ride to Hub_Guild (only once per solved case).
            if (string.Equals(dest, "Hub_Guild", StringComparison.OrdinalIgnoreCase))
            {
                for (int c = 4; c >= 1; c--)
                {
                    if (!session.Grades.TryGetValue(c, out var g) || g == Grade.Ungraded) continue;
                    string retFlag = $"CarriageCutscene.Return.{c}";
                    if (session.HasFlag(retFlag)) continue;
                    flag = retFlag;
                    return BuildReturnDialogue(c);
                }
                return null;
            }

            // 2. First-time outbound ride to each regional hub.
            string outFlag = $"CarriageCutscene.Outbound.{dest}";
            if (session.HasFlag(outFlag)) return null;

            DialogueData outbound = dest switch
            {
                "Hub_LowerMarket" => BuildOutboundDialogue(
                    "D_Carriage_Out_LowerMarket",
                    ("Eliza", "First time riding out of the Guild gates, {PlayerName}? Asterveil's Lower Market never sleeps—keep your optics sharp."),
                    ("Carrier", "Mind the cobblestones, folks. Since the Cataclysm, half the old First-Age drains under the market still groan when wagons roll over."),
                    ("Golem", "[STABILITY CHECK]: Suspension variance nominal. Though the sub-street resonance feels... oddly familiar."),
                    ("Eliza", "(Glances sideways at your core) Familiar, huh? Funny thing for a brand-new Guild construct to say.")),

                "Hub_CathedralCourtyard" => BuildOutboundDialogue(
                    "D_Carriage_Out_Cathedral",
                    ("Carrier", "Steep climb up Saint Althea's ridge. Bell-tower pilgrims say the bronze clapper hummed by itself all night."),
                    ("Eliza", "First-Age solvent in the market, now a silenced oracle in the Cathedral... Someone's testing old-world tricks."),
                    ("Golem", "[OBSERVATION]: Subject Eliza's pulse elevates near consecrated stone. Fear, or recognition?"),
                    ("Eliza", "Call it bad history with locked doors. Stay close to me once we step inside.")),

                "Hub_Wilderness" => BuildOutboundDialogue(
                    "D_Carriage_Out_Wilderness",
                    ("Carrier", "Whoa there, steady girl—the Whisperwood boundary stones just flickered blue as we crossed."),
                    ("Eliza", "Wait... those ward-stones haven't reacted to a Guild carriage in thirty years. Why did they pulse when you leaned out?"),
                    ("Golem", "[SCAN]: First-Age perimeter ward acknowledged my core frequency as... authorized."),
                    ("Eliza", "Authorized by a dead civilization? When this case is over, we're finding out who really built you.")),

                "Hub_IronHollow" => BuildOutboundDialogue(
                    "D_Carriage_Out_IronHollow",
                    ("Carrier", "Last stretch down into Iron Hollow. Dwarven blast-crews cracked into ruins older than the Guild itself."),
                    ("Eliza", "Three districts, three First-Age countermeasures. Whatever they're digging up down there, it's the heart of the Veiled Circle."),
                    ("Golem", "My optical prism is still humming from Whisperwood. If they hid anything beneath the mines, I will find it."),
                    ("Eliza", "Good. Because at this point, I trust you more than anyone sitting on the Guild Council.")),

                _ => null
            };

            if (outbound == null) return null;
            flag = outFlag;
            return outbound;
        }

        static DialogueData BuildReturnDialogue(int caseId) => caseId switch
        {
            1 => BuildOutboundDialogue(
                "D_Carriage_Ret_C1",
                ("Eliza", "Ignatius crumbled the moment you cornered his ledger. Not bad for our first real case together."),
                ("Golem", "[SYSTEM NOTE]: Emotional telemetry recorded during human confrontation. Core efficiency... improved near you, Eliza."),
                ("Eliza", "(Smiles faintly, watching the market lanterns fade) Careful, tin man—keep talking like that and I might think you actually like working with me.")),

            2 => BuildOutboundDialogue(
                "D_Carriage_Ret_C2",
                ("Eliza", "Accord kept talking about 'the Architect's era' like it was yesterday. Did that phrase mean anything to you?"),
                ("Golem", "[MEMORY SECTOR 00]: Query returns corrupted fragments—tall spires, a woman's voice, and a promise to return."),
                ("Eliza", "(Quietly) A promise... Funny. For a second up in that belfry, you sounded just like someone I used to wait for.")),

            3 => BuildOutboundDialogue(
                "D_Carriage_Ret_C3",
                ("Eliza", "You took Vane's ambush without flinching—and now your eye's glowing cyan from that ritual siphon."),
                ("Golem", "And you stepped between a lethal blade and my chassis in 0.4 seconds. Statistically irrational."),
                ("Eliza", "Partners don't run the odds, {PlayerName}. Let's get back to Ovan before Iron Hollow blows wide open.")),

            _ => BuildOutboundDialogue(
                "D_Carriage_Ret_C4",
                ("Eliza", "Four cases, and every trail leads straight down into the Sanctum Core beneath Asterveil."),
                ("Golem", "Once we descend, my core may not survive another planetary stabilization cycle."),
                ("Eliza", "Don't talk like that. Whatever's waiting down there, we face it together—just like we always have."))
        };

        IEnumerator PlaySequence(HubContext ctx, DialogueData script, Action proceed)
        {
            Func<bool> blocker = () => true;
            ctx?.AddUiBlocker(blocker);
            PauseMenuController.RegisterExternalModal(blocker);

            var createdAssets = new List<UnityEngine.Object>();
            var stage = BuildStage(out var propsToScroll, createdAssets);

            var dialogue = ctx != null && ctx.dialogue != null
                ? ctx.dialogue
                : FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);

            bool done = false;
            if (dialogue != null)
            {
                if (CoreRuntime.Instance != null)
                    dialogue.playerName = CoreRuntime.Instance.Session.PlayerName;
                void OnEnd() => done = true;
                dialogue.DialogueEnded += OnEnd;
                dialogue.SetDialogue(script);
                dialogue.Play();

                while (!done && dialogue.IsPlaying)
                {
                    ScrollBackdrop(propsToScroll);
                    yield return null;
                }
                dialogue.DialogueEnded -= OnEnd;
            }
            else
            {
                float timer = 0f;
                while (timer < 3.5f)
                {
                    timer += Time.unscaledDeltaTime;
                    ScrollBackdrop(propsToScroll);
                    yield return null;
                }
            }

            ctx?.RemoveUiBlocker(blocker);
            PauseMenuController.UnregisterExternalModal(blocker);
            PauseMenuController.NotifyExternalModalClosed();

            if (stage != null) Destroy(stage);
            for (int i = 0; i < createdAssets.Count; i++)
                if (createdAssets[i] != null) Destroy(createdAssets[i]);
            if (script != null) Destroy(script);
            proceed?.Invoke();
            Destroy(gameObject);
        }

        static GameObject BuildStage(out List<Transform> scrollItems, List<UnityEngine.Object> createdAssets)
        {
            scrollItems = new List<Transform>();
            var stage = new GameObject("CarriageCutsceneStage");
            stage.transform.position = new Vector3(0f, -600f, 0f);

            var camGo = new GameObject("CarriageCam");
            camGo.transform.SetParent(stage.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.2f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 3.8f;
            cam.depth = 50;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.14f, 1f);

            // Road strip
            var road = CreateSolidQuad("RoadStrip", stage.transform,
                new Vector3(0f, -0.8f, 0f), new Vector3(24f, 2.6f, 1f),
                new Color(0.18f, 0.15f, 0.13f, 1f), 1, createdAssets);

            // Grass / verge
            CreateSolidQuad("VergeStrip", stage.transform,
                new Vector3(0f, 0.8f, 0f), new Vector3(24f, 1.0f, 1f),
                new Color(0.10f, 0.16f, 0.12f, 1f), 0, createdAssets);

            // Scrolling roadside milestones & silhouette trees
            for (int i = 0; i < 8; i++)
            {
                float x = -10f + i * 3.0f;
                var post = CreateSolidQuad($"Post_{i}", stage.transform,
                    new Vector3(x, 0.55f, 0f), new Vector3(0.22f, 1.1f, 1f),
                    new Color(0.14f, 0.12f, 0.10f, 1f), 2, createdAssets);
                scrollItems.Add(post.transform);
            }

            // Animated Carriage Rig (Wagon + seated Carrier + trotting Horse + reins)
            var rigGo = new GameObject("CarriageRig");
            rigGo.transform.SetParent(stage.transform, false);
            rigGo.transform.localPosition = new Vector3(-1.2f, 0.15f, 0f);
            rigGo.transform.localScale = new Vector3(1.15f, 1.15f, 1f);
            var rig = rigGo.AddComponent<CarriageVisualRig>();
            rig.Initialize(moving: true, order: 10);

            return stage;
        }

        static void ScrollBackdrop(List<Transform> items)
        {
            if (items == null) return;
            float dx = Time.unscaledDeltaTime * 3.6f;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null) continue;
                var p = items[i].localPosition;
                p.x -= dx;
                if (p.x < -11f) p.x += 24f;
                items[i].localPosition = p;
            }
        }

        static GameObject CreateSolidQuad(string name, Transform parent, Vector3 localPos,
            Vector3 scale, Color color, int sortingOrder, List<UnityEngine.Object> createdAssets)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            var tex = new Texture2D(2, 2);
            tex.SetPixels(new[] { color, color, color, color });
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            createdAssets?.Add(tex);
            createdAssets?.Add(sr.sprite);
            sr.sortingOrder = sortingOrder;
            return go;
        }

        static DialogueData BuildOutboundDialogue(string id, params (string speaker, string text)[] lines)
        {
            var data = ScriptableObject.CreateInstance<DialogueData>();
            data.hideFlags = HideFlags.DontSave;
            data.id = id;
            data.startNodeId = "n0";
            var nodes = new DialogueNodeData[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                nodes[i] = new DialogueNodeData
                {
                    id = $"n{i}",
                    speakerName = lines[i].speaker,
                    text = lines[i].text,
                    nextNodeId = i + 1 < lines.Length ? $"n{i + 1}" : ""
                };
            }
            data.nodes = nodes;
            return data;
        }
    }
}

using Morsoul.Core;
using Morsoul.Hub;
using NUnit.Framework;
using UnityEngine;

namespace Morsoul.Tests
{
    /// <summary>
    /// Covers the prologue -> Lower Market -> Guild first-visit chain: the
    /// gating flags, the dynamic Y-sort, and the world-space collider
    /// footprint math that feeds the dressing colliders.
    /// </summary>
    public class GuildProgressionTests
    {
        SessionState _session;

        [SetUp]
        public void SetUp() => _session = new SessionState();

        [Test]
        public void MarketArrival_OnlyPlaysOnceStraightOutOfPrologue()
        {
            Assert.IsFalse(GuildIntroRules.CanPlayMarketArrival(_session));

            _session.PrologueCompleted = true;
            _session.CurrentCase = 1;
            Assert.IsTrue(GuildIntroRules.CanPlayMarketArrival(_session));

            // Veteran saves already past Case 1 never get the guided tour.
            _session.CurrentCase = 2;
            Assert.IsFalse(GuildIntroRules.CanPlayMarketArrival(_session));
            _session.CurrentCase = 1;

            // Once latched it never replays.
            _session.SetFlag(GuildIntroRules.ArrivedFlag);
            Assert.IsFalse(GuildIntroRules.CanPlayMarketArrival(_session));
        }

        [Test]
        public void GuildIntro_RequiresArrivalHandoff_AndPlaysOnce()
        {
            Assert.IsFalse(GuildIntroRules.CanPlayGuildIntro(_session));

            _session.SetFlag(GuildIntroRules.IntroPendingFlag);
            Assert.IsTrue(GuildIntroRules.CanPlayGuildIntro(_session));

            // Interrupt/resume replays until the seen flag latches.
            _session.SetFlag(GuildIntroRules.IntroSeenFlag);
            Assert.IsFalse(GuildIntroRules.CanPlayGuildIntro(_session));
        }

        [Test]
        public void YSort_SortsDynamicActorsByY()
        {
            var go = new GameObject("Sortable", typeof(YSort2D));
            try
            {
                var sort = go.GetComponent<YSort2D>();
                var lateUpdate = typeof(YSort2D).GetMethod("LateUpdate",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);

                // A southern actor draws in front of a northern one; bias
                // shifts the whole curve without changing the order.
                go.transform.position = new Vector3(0f, -3f, 0f);
                lateUpdate.Invoke(sort, null);
                Assert.AreEqual(-3f * YSort2D.DepthPerUnitY,
                    go.transform.position.z, 1e-5f);

                go.transform.position = new Vector3(0f, 4f, 0f);
                lateUpdate.Invoke(sort, null);
                Assert.AreEqual(4f * YSort2D.DepthPerUnitY,
                    go.transform.position.z, 1e-5f);

                sort.bias = -0.005f;
                lateUpdate.Invoke(sort, null);
                Assert.AreEqual(4f * YSort2D.DepthPerUnitY - 0.005f,
                    go.transform.position.z, 1e-5f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SceneEntryPoint_PendingEntry_UsesNamedMarker()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            var core = CoreRuntime.Ensure();
            var spawn = new GameObject("PlayerSpawn");
            spawn.tag = "PlayerSpawn";
            var marker = new GameObject("Spawn_GuildDoor");
            marker.AddComponent<SceneSpawnMarker>().entryId = "guild_door";
            marker.transform.position = new Vector3(3f, 2.4f, 0f);
            var playerGo = new GameObject("Player");
            playerGo.tag = "Player";
            var entryGo = new GameObject("SceneEntryPoint");
            var entry = entryGo.AddComponent<SceneEntryPoint>();
            entry.player = playerGo.transform;
            try
            {
                core.Session.PendingEntryPointId = "guild_door";
                typeof(SceneEntryPoint).GetMethod("Start",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                    .Invoke(entry, null);

                // The door marker wins over the default spawn, then clears.
                Assert.AreEqual(new Vector3(3f, 2.4f, 0f), playerGo.transform.position);
                Assert.AreEqual("", core.Session.PendingEntryPointId);
            }
            finally
            {
                foreach (var go in new[] { entryGo, playerGo, marker, spawn, core.gameObject })
                    Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SceneEntryPoint_PreservesInvestigationMode()
        {
            // EnterInvestigationRoute stamps Mode3D; the spawn-stamp must
            // not downgrade it or the deduction board's CanUse gate closes.
            var core = CoreRuntime.Ensure();
            var spawn = new GameObject("PlayerSpawn");
            spawn.tag = "PlayerSpawn";
            var playerGo = new GameObject("Player");
            playerGo.tag = "Player";
            var entryGo = new GameObject("SceneEntryPoint");
            entryGo.AddComponent<SceneEntryPoint>();
            try
            {
                core.Session.SetSceneContext(SceneMode.Mode3D, "Investigation_01_Placeholder");
                typeof(SceneEntryPoint).GetMethod("Start",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                    .Invoke(entryGo.GetComponent<SceneEntryPoint>(), null);

                Assert.AreEqual(SceneMode.Mode3D, core.Session.CurrentSceneMode,
                    "Spawn stamping must preserve the investigation scene mode.");
            }
            finally
            {
                core.Session.SetSceneContext(SceneMode.Mode2D, "");
                foreach (var go in new[] { entryGo, playerGo, spawn, core.gameObject })
                    Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SceneEntryPoint_DoesNotRestorePositionFromOtherScene()
        {
            var core = CoreRuntime.Ensure();
            var playerGo = new GameObject("Player");
            playerGo.tag = "Player";
            var spawn = new GameObject("PlayerSpawn");
            spawn.tag = "PlayerSpawn";
            spawn.transform.position = new Vector3(-6.2f, -0.3f, 0f);
            var entryGo = new GameObject("SceneEntryPoint");
            var entry = entryGo.AddComponent<SceneEntryPoint>();
            entry.player = playerGo.transform;
            try
            {
                string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                var expectedSpawn = GameObject.FindWithTag("PlayerSpawn").transform.position;
                core.Session.Reset();
                core.Session.SetPlayerContext(SceneMode.Mode2D, activeScene + "_Other",
                    new Vector3(-20.25f, -2.84f, 0f), PlayerFacing2D.Left);
                typeof(SceneEntryPoint).GetMethod("Start",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(entry, null);

                Assert.AreEqual(expectedSpawn, playerGo.transform.position,
                    "Player must spawn at PlayerSpawn instead of inheriting another scene's coordinates.");
                Assert.AreEqual(activeScene, core.Session.CurrentSceneId);
            }
            finally
            {
                core.Session.SetSceneContext(SceneMode.Mode2D, "");
                foreach (var go in new[] { entryGo, playerGo, spawn, core.gameObject })
                    Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void NpcInteractable_BlocksMovementWithSolidCollider()
        {
            // NPCs get a second, non-trigger collider so the player walks up
            // to them instead of through them; the detection trigger stays.
            var go = new GameObject("Npc", typeof(BoxCollider2D));
            go.GetComponent<BoxCollider2D>().isTrigger = true;
            go.AddComponent<NpcInteractable>();
            try
            {
                var solids = 0;
                foreach (var col in go.GetComponents<BoxCollider2D>())
                    if (!col.isTrigger) solids++;
                Assert.AreEqual(1, solids,
                    "NPC should carry exactly one solid block collider.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Interactable2D_OptInBlockingOnly()
        {
            var go = new GameObject("Board", typeof(BoxCollider2D));
            go.GetComponent<BoxCollider2D>().isTrigger = true;
            go.AddComponent<InspectionInteractable>();
            try
            {
                var inspection = go.GetComponent<InspectionInteractable>();
                foreach (var col in go.GetComponents<BoxCollider2D>())
                    Assert.IsTrue(col.isTrigger,
                        "Non-blocking interactable should stay trigger-only.");

                // Awake ran already with blocking off; add the collider the
                // same way the scene's next load would.
                inspection.blocksMovement = true;
                typeof(Interactable2D).GetMethod("EnsureBlockCollider",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                    .Invoke(inspection, null);
                var solids = 0;
                foreach (var col in go.GetComponents<BoxCollider2D>())
                    if (!col.isTrigger) solids++;
                Assert.AreEqual(1, solids);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PropCollider_KeepsAuthoredWorldFootprintUnderScale()
        {
            // A 0.7x0.5 trunk at (0,-0.75) under a 1.8x2.2-scaled quad must
            // land at exactly those world bounds after Unity multiplies the
            // local values back by the scale.
            var worldSize = new Vector2(0.7f, 0.5f);
            var worldOffset = new Vector2(0f, -0.75f);
            var scale = new Vector2(1.8f, 2.2f);

            var localSize = PropFootprint.LocalSize(worldSize, scale);
            var localOffset = PropFootprint.LocalOffset(worldOffset, scale);

            Assert.AreEqual(worldSize.x, localSize.x * scale.x, 1e-5f);
            Assert.AreEqual(worldSize.y, localSize.y * scale.y, 1e-5f);
            Assert.AreEqual(worldOffset.x, localOffset.x * scale.x, 1e-5f);
            Assert.AreEqual(worldOffset.y, localOffset.y * scale.y, 1e-5f);
        }

        [Test]
        public void HubObjective_GatesCaseOneBehindArrivalAndGuildIntro()
        {
            var core = CoreRuntime.Ensure();
            var hudGo = new GameObject("HudPanel");
            var hud = hudGo.AddComponent<HubHudController>();
            hud.caseText = "Case 1: The Poisoned Vault";
            hud.objectiveText = "Interview Merchant Guildmaster Ovan for storage clearance.";
            hud.objectiveCaseId = 1;
            hud.postInvestigationObjective = "Confront Master Alchemist Ignatius.";

            try
            {
                core.Session.Reset();
                core.Session.CurrentCase = 1;
                core.Session.PrologueCompleted = true;

                // Step 1: Entering market, arrival not yet seen -> Case text hidden, arrival objective.
                hud.RefreshObjective();
                Assert.AreEqual("", hud.AppliedCaseText, "Case label should be hidden during market arrival.");
                Assert.AreEqual("Follow Eliza to the Investigator's Guild.", hud.AppliedObjective);

                // Step 2: Market arrival seen, guild intro not yet seen -> Guild entrance objective.
                core.Session.SetFlag(GuildIntroRules.ArrivedFlag);
                hud.RefreshObjective();
                Assert.AreEqual("", hud.AppliedCaseText, "Case label should be hidden during guild intro.");
                Assert.AreEqual("Follow Eliza to meet Guildmaster Ovan.", hud.AppliedObjective);

                // Step 3: Guild intro seen, Case 1 briefing not yet played -> Meet Ovan objective.
                core.Session.SetFlag(GuildIntroRules.IntroSeenFlag);
                hud.RefreshObjective();
                Assert.AreEqual("", hud.AppliedCaseText, "Case label should be hidden before Case 1 briefing.");
                Assert.AreEqual("Speak with Guildmaster Ovan upstairs in the Guild hall.", hud.AppliedObjective);

                // Step 4: Ovan delivers Case 1 briefing -> Case 1 unlocks!
                core.Session.SetFlag(GuildIntroRules.BriefingSeenFlag);
                hud.RefreshObjective();
                Assert.AreEqual("Case 1: The Poisoned Vault", hud.AppliedCaseText);
                Assert.AreEqual("Interview Merchant Guildmaster Ovan for storage clearance.", hud.AppliedObjective);

                // Step 5: Warrant granted -> Cellar grate objective.
                core.Session.SetFlag(GuildIntroRules.WarrantFlag);
                hud.RefreshObjective();
                Assert.AreEqual("Case 1: The Poisoned Vault", hud.AppliedCaseText);
                Assert.AreEqual("Investigate the poisoned vault via the cellar grate.", hud.AppliedObjective);

                // Step 6: Board solved -> Interrogation confront suspect objective.
                core.Session.GetOrCreateBoardState(1).BoardSolved = true;
                hud.RefreshObjective();
                Assert.AreEqual("Case 1: The Poisoned Vault", hud.AppliedCaseText);
                Assert.AreEqual("Confront Master Alchemist Ignatius.", hud.AppliedObjective);
            }
            finally
            {
                Object.DestroyImmediate(hudGo);
                Object.DestroyImmediate(core.gameObject);
            }
        }

        [Test]
        public void HubObjective_DoesNotLeakFutureHubGoalWhenCurrentCaseIsEarlier()
        {
            var core = CoreRuntime.Ensure();
            var hudGo = new GameObject("HudPanel");
            var hud = hudGo.AddComponent<HubHudController>();
            hud.caseText = "Case 2: The Muted Belfry";
            hud.objectiveText = "Examine Mother Lyra and secure the Belfry Key.";
            hud.objectiveCaseId = 2;

            try
            {
                core.Session.Reset();
                core.Session.CurrentCase = 0;
                hud.RefreshObjective();
                Assert.AreEqual("Prologue", hud.AppliedCaseText);
                Assert.AreNotEqual(hud.objectiveText, hud.AppliedObjective);

                core.Session.CurrentCase = 1;
                core.Session.SetFlag(GuildIntroRules.WarrantFlag);
                hud.RefreshObjective();
                Assert.AreEqual("Case 1: The Poisoned Vault", hud.AppliedCaseText);
                Assert.AreEqual("Investigate the poisoned vault via the cellar grate.", hud.AppliedObjective);

                core.Session.CurrentCase = 2;
                core.Session.SetFlag("Guild.CheckBoard.2");
                hud.RefreshObjective();
                Assert.AreEqual("Case 2: The Muted Belfry", hud.AppliedCaseText);
                Assert.AreEqual("Check the Quest Board downstairs in the Investigator's Guild for potential cases.", hud.AppliedObjective);
            }
            finally
            {
                Object.DestroyImmediate(hudGo);
                Object.DestroyImmediate(core.gameObject);
            }
        }

        [Test]
        public void IgnatiusDialogue_HasWorkingConversationAndClaim()
        {
            var data = UnityEditor.AssetDatabase.LoadAssetAtPath<Morsoul.Dialogue.DialogueData>(
                "Assets/Data/Dialogue/Case01/D_Ignatius_Claim_01.asset");
            Assert.IsNotNull(data, "D_Ignatius_Claim_01.asset must exist.");

            var flags = new System.Collections.Generic.HashSet<string>();
            var runner = new Morsoul.Dialogue.DialogueRunner(data, flags);

            // First run: starts at ignatius_repeat, but since Ignatius_ClaimHeard is unset, skips to greeting.
            Assert.AreEqual("ignatius_greeting", runner.Current.id);
            Assert.IsTrue(runner.Current.text.Contains("quarter-century of alchemical mastery"));

            // Advance to formal claim.
            runner.Advance();
            Assert.AreEqual("ignatius_claim", runner.Current.id);
            Assert.IsNotNull(runner.Current.registersClue, "Claim node must register clue T2_01.");
            Assert.AreEqual("T2_01", runner.Current.registersClue.id);
            Assert.AreEqual("Ignatius_ClaimHeard", runner.Current.setsFlag);

            // Advance to inquiry menu.
            runner.Advance();
            Assert.AreEqual("ignatius_menu", runner.Current.id);
            Assert.IsTrue(runner.HasOptions);
            Assert.AreEqual(3, runner.CurrentOptions.Count);

            // Select Option 0: Destroyed vault details.
            runner.Choose(0);
            Assert.AreEqual("ignatius_vault", runner.Current.id);
            runner.Advance();
            Assert.AreEqual("ignatius_menu", runner.Current.id);

            // Select Option 2: Conclude inquiry.
            runner.Choose(2);
            Assert.AreEqual("ignatius_leave", runner.Current.id);
            runner.Advance();
            Assert.IsTrue(runner.IsEnded);

            // Second run: now that Ignatius_ClaimHeard is set, repeat greeting shows with options immediately.
            var runnerRepeat = new Morsoul.Dialogue.DialogueRunner(data, flags);
            Assert.AreEqual("ignatius_repeat", runnerRepeat.Current.id);
            Assert.IsTrue(runnerRepeat.HasOptions);
        }

        [Test]
        public void LowerMarket_RestSpot_RelocatedBesideNoticeBoard()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Scenes/Hub_LowerMarket.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);
            try
            {
                var restSpot = GameObject.Find("RestSpot");
                var noticeBoard = GameObject.Find("NoticeBoard");
                Assert.IsNotNull(restSpot, "RestSpot must exist in Hub_LowerMarket.");
                Assert.IsNotNull(noticeBoard, "NoticeBoard must exist in Hub_LowerMarket.");
                Assert.AreEqual(-4.07f, restSpot.transform.position.x, 0.5f, "RestSpot X must sit beside NoticeBoard.");
                Assert.AreEqual(2.3f, restSpot.transform.position.y, 0.5f, "RestSpot Y must sit beside NoticeBoard.");
            }
            finally
            {
                // Leave scene open
            }
        }

        [Test]
        public void LowerMarket_MarketCrowd_HasMultipleIndividualNpcSprites()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Scenes/Hub_LowerMarket.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);
            try
            {
                var central = GameObject.Find("MarketCrowd");
                Assert.IsNotNull(central, "Central MarketCrowd must exist.");

                var member1 = GameObject.Find("MarketCrowd_Vendor");
                var member2 = GameObject.Find("MarketCrowd_Townsperson");
                var member3 = GameObject.Find("MarketCrowd_Adventurer");

                Assert.IsNotNull(member1, "MarketCrowd_Vendor must exist.");
                Assert.IsNotNull(member2, "MarketCrowd_Townsperson must exist.");
                Assert.IsNotNull(member3, "MarketCrowd_Adventurer must exist.");

                foreach (var npc in new[] { central, member1, member2, member3 })
                {
                    var interactable = npc.GetComponent<NpcInteractable>();
                    Assert.IsNotNull(interactable, $"{npc.name} must have NpcInteractable.");
                    Assert.AreEqual("[E] Listen", interactable.promptText);
                    Assert.IsTrue(interactable.blocksMovement, $"{npc.name} must block movement.");
                    Assert.IsNotNull(npc.GetComponent<YSort2D>(), $"{npc.name} must carry YSort2D.");
                }

                // Check positions are distinct and not overlapping.
                var positions = new[] { central.transform.position, member1.transform.position, member2.transform.position, member3.transform.position };
                for (int i = 0; i < positions.Length; i++)
                {
                    for (int j = i + 1; j < positions.Length; j++)
                    {
                        float dist = Vector3.Distance(positions[i], positions[j]);
                        Assert.Greater(dist, 0.5f, $"Crowd NPCs {i} and {j} should not overlap (dist={dist:F2}).");
                    }
                }
            }
            finally
            {
                // Leave scene open
            }
        }

        [Test]
        public void LowerMarket_ExpandedPopulation_HasInteractablesAndAmbientStrollers()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Scenes/Hub_LowerMarket.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            string[] extraInteractables =
            {
                "MarketNpc_Smith",
                "MarketNpc_Apothecary",
                "MarketNpc_Chef",
                "MarketNpc_Guard",
                "MarketNpc_Courier",
            };

            foreach (string name in extraInteractables)
            {
                var go = GameObject.Find(name);
                Assert.IsNotNull(go, $"{name} must exist in expanded Hub_LowerMarket.");
                var npc = go.GetComponent<NpcInteractable>();
                Assert.IsNotNull(npc, $"{name} must carry NpcInteractable.");
                Assert.IsNotNull(npc.dialogue, $"{name} must have assigned flavor dialogue.");
                Assert.IsNotNull(go.GetComponent<YSort2D>(), $"{name} must carry YSort2D.");
            }

            string[] strollers =
            {
                "AmbientMarket_Stroll_North",
                "AmbientMarket_Stroll_Plaza",
                "AmbientMarket_Stroll_West",
                "AmbientMarket_Stroll_East",
                "AmbientMarket_Stroll_South",
                "AmbientMarket_WatchPatrol",
            };

            foreach (string name in strollers)
            {
                var go = GameObject.Find(name);
                Assert.IsNotNull(go, $"{name} must exist in expanded Hub_LowerMarket.");
                Assert.IsNull(go.GetComponent<Interactable2D>(), $"{name} must remain non-interactable.");
                var stroller = go.GetComponent<AmbientStroller2D>();
                Assert.IsNotNull(stroller, $"{name} must carry AmbientStroller2D.");
                Assert.IsNotNull(stroller.waypoints, $"{name} must have patrol waypoints.");
                Assert.GreaterOrEqual(stroller.waypoints.Length, 2, $"{name} must have >= 2 waypoints.");
                Assert.IsNotNull(go.GetComponent<YSort2D>(), $"{name} must carry YSort2D.");
            }

            // Confirm expanded spread reaches both far West Caravan (-16..-18) and far East Market (12..16).
            var guard = GameObject.Find("MarketNpc_Guard");
            var chef = GameObject.Find("MarketNpc_Chef");
            Assert.Greater(chef.transform.position.x - guard.transform.position.x, 25f,
                "Expanded Lower Market NPC spread must span >= 25 units horizontally.");
        }

        [Test]
        public void LowerMarket_HasGuildEntrance_MarketArrival_AndOutdoorOvanDisabled()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Scenes/Hub_LowerMarket.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            GameObject FindRoot(string name)
            {
                foreach (var root in scene.GetRootGameObjects())
                    if (root.name == name) return root;
                return null;
            }

            var ovan = FindRoot("Ovan");
            if (ovan != null)
                Assert.IsFalse(ovan.activeSelf, "Outdoor Ovan must be inactive once moved upstairs into Hub_Guild.");

            var guildEntrance = FindRoot("GuildEntrance");
            Assert.IsNotNull(guildEntrance, "GuildEntrance door must exist in Hub_LowerMarket.");
            Assert.IsTrue(guildEntrance.activeSelf, "GuildEntrance must be active.");
            var gate = guildEntrance.GetComponent<GateInteractable>();
            Assert.IsNotNull(gate, "GuildEntrance must carry GateInteractable.");
            Assert.AreEqual("Hub_Guild", gate.targetScene);

            var guildSpawn = FindRoot("Spawn_GuildDoor");
            Assert.IsNotNull(guildSpawn, "Spawn_GuildDoor marker must exist for Guild returns.");
            var marker = guildSpawn.GetComponent<SceneSpawnMarker>();
            Assert.IsNotNull(marker);
            Assert.AreEqual("guild_door", marker.entryId);

            var arrivalEliza = FindRoot("ArrivalEliza");
            Assert.IsNotNull(arrivalEliza, "ArrivalEliza stand-in must exist in Hub_LowerMarket.");
            Assert.IsFalse(arrivalEliza.activeSelf, "ArrivalEliza must start inactive until arrival runs.");

            var arrival = Object.FindAnyObjectByType<MarketArrivalController>();
            Assert.IsNotNull(arrival, "MarketArrivalController must exist in Hub_LowerMarket.");
            Assert.IsNotNull(arrival.context, "MarketArrivalController.context must be wired.");
            Assert.IsNotNull(arrival.player, "MarketArrivalController.player must be wired.");
            Assert.IsNotNull(arrival.eliza, "MarketArrivalController.eliza must be wired.");
            Assert.IsNotNull(arrival.entryDialogue, "MarketArrivalController.entryDialogue must be wired.");
            Assert.IsNotNull(arrival.doorDialogue, "MarketArrivalController.doorDialogue must be wired.");
            Assert.AreEqual("Hub_Guild", arrival.guildSceneName);
        }
    }
}

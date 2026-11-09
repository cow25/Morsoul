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
            var core = CoreRuntime.Ensure();
            var spawn = new GameObject("PlayerSpawn");
            spawn.tag = "PlayerSpawn";
            var marker = new GameObject("Spawn_GuildDoor");
            marker.AddComponent<SceneSpawnMarker>().entryId = "guild_door";
            marker.transform.position = new Vector3(3f, 2.3f, 0f);
            var playerGo = new GameObject("Player");
            playerGo.tag = "Player";
            var entryGo = new GameObject("SceneEntryPoint");
            entryGo.AddComponent<SceneEntryPoint>();
            try
            {
                core.Session.PendingEntryPointId = "guild_door";
                typeof(SceneEntryPoint).GetMethod("Start",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                    .Invoke(entryGo.GetComponent<SceneEntryPoint>(), null);

                // The door marker wins over the default spawn, then clears.
                Assert.AreEqual(new Vector3(3f, 2.3f, 0f), playerGo.transform.position);
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
    }
}

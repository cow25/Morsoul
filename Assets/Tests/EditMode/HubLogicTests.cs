using System.Collections.Generic;
using Morsoul.Board;
using Morsoul.Data;
using Morsoul.Hub;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Morsoul.Tests
{
    /// <summary>
    /// EditMode coverage for the 2D hub framework. Exercises the pure helpers
    /// (GateRule, HubTargeting) and the wiring-level behaviours that can run
    /// without a scene load (HubFlags, HubJournal, InspectionInteractable).
    /// GateInteractable/CarrierInteractable scene loading itself is not
    /// testable in EditMode - that is exactly why the pass/fail decision lives
    /// in GateRule.CanPass.
    /// </summary>
    public class HubLogicTests
    {
        readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _objects)
                if (obj != null)
                    Object.DestroyImmediate(obj);
            _objects.Clear();
        }

        T NewComponent<T>(string name = "TestGO") where T : Component
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go.AddComponent<T>();
        }

        ClueData NewClue(string id)
        {
            var clue = ScriptableObject.CreateInstance<ClueData>();
            clue.id = id;
            clue.displayName = id;
            _objects.Add(clue);
            return clue;
        }

        HubContext NewContext(out HubFlags flags, out HubJournal journal)
        {
            var go = new GameObject("HubContext");
            _objects.Add(go);
            var context = go.AddComponent<HubContext>();
            flags = go.AddComponent<HubFlags>();
            journal = go.AddComponent<HubJournal>();
            context.flags = flags;
            context.journal = journal;
            return context;
        }

        // --- HubContext -------------------------------------------------------

        [Test]
        public void HubContext_OpenWeightedBoard_BlocksHubUi()
        {
            var context = NewContext(out _, out _);
            var board = NewComponent<WeightedBoardController>("WeightedBoardPanel");
            var data = AssetDatabase.LoadAssetAtPath<CaseData>(
                "Assets/Data/Cases/Case01/Case_01.asset");
            Assert.NotNull(data);
            Assert.IsTrue(board.SetCase(data));
            context.boardPanel = board;

            Assert.IsFalse(context.UiBlocked);
            board.Open();
            Assert.IsTrue(context.UiBlocked);
            board.Close();
            Assert.IsFalse(context.UiBlocked);
        }

        // --- HubFlags ---------------------------------------------------------

        [Test]
        public void HubFlags_Add_Has_Set()
        {
            var flags = NewComponent<HubFlags>();

            Assert.That(flags.Has("metOvan"), Is.False);
            flags.Add("metOvan");
            Assert.That(flags.Has("metOvan"), Is.True);

            flags.Add("metOvan"); // idempotent
            Assert.That(flags.Set.Count, Is.EqualTo(1));

            flags.Add(null);
            flags.Add("");
            Assert.That(flags.Set.Count, Is.EqualTo(1));
        }

        [Test]
        public void HubFlags_Set_IsBackingSet_ForDialogueGating()
        {
            var flags = NewComponent<HubFlags>();
            flags.Add("C1_WarrantGranted");

            // DialogueController.SetFlags takes this same set, so flags added
            // by a running dialogue are visible to the hub immediately.
            flags.Set.Add("seenByRunner");
            Assert.That(flags.Has("seenByRunner"), Is.True);
            Assert.That(flags.Set.Contains("C1_WarrantGranted"), Is.True);
        }

        [Test]
        public void HubFlags_Matching_IsOrdinal()
        {
            var flags = NewComponent<HubFlags>();
            flags.Add("CaseFlag");
            Assert.That(flags.Has("caseflag"), Is.False);
            Assert.That(flags.Has("CaseFlag"), Is.True);
        }

        // --- HubJournal -------------------------------------------------------

        [Test]
        public void HubJournal_Register_Dedups_AndFiresOnce()
        {
            var journal = NewComponent<HubJournal>();
            var clue = NewClue("C_TEST");
            int fired = 0;
            ClueData seen = null;
            journal.ClueRegistered += c => { fired++; seen = c; };

            Assert.That(journal.Register(clue), Is.True);
            Assert.That(journal.Register(clue), Is.False); // duplicate dropped
            Assert.That(journal.Register(null), Is.False);

            Assert.That(journal.Collected.Count, Is.EqualTo(1));
            Assert.That(journal.Collected[0], Is.SameAs(clue));
            Assert.That(fired, Is.EqualTo(1));
            Assert.That(seen, Is.SameAs(clue));
            Assert.That(journal.Contains(clue), Is.True);
        }

        // --- GateRule ---------------------------------------------------------

        [Test]
        public void GateRule_EmptyRequirement_AlwaysPasses()
        {
            Assert.That(GateRule.CanPass(null, null), Is.True);
            Assert.That(GateRule.CanPass("", null), Is.True);
            Assert.That(GateRule.CanPass("", new HashSet<string>()), Is.True);
        }

        [Test]
        public void GateRule_RequiredFlag_MustBePresent()
        {
            var flags = new HashSet<string>();
            Assert.That(GateRule.CanPass("C1_WarrantGranted", flags), Is.False);
            Assert.That(GateRule.CanPass("C1_WarrantGranted", null), Is.False);

            flags.Add("C1_WarrantGranted");
            Assert.That(GateRule.CanPass("C1_WarrantGranted", flags), Is.True);
        }

        // --- HubTargeting -------------------------------------------------------

        [Test]
        public void HubTargeting_Empty_ReturnsMinusOne()
        {
            Assert.That(HubTargeting.Nearest(Vector2.zero, new List<Vector2>()), Is.EqualTo(-1));
            Assert.That(HubTargeting.Nearest(Vector2.zero, null), Is.EqualTo(-1));
        }

        [Test]
        public void HubTargeting_Nearest_PicksClosestIndex()
        {
            var positions = new List<Vector2>
            {
                new Vector2(5f, 0f),
                new Vector2(-1f, 0f), // nearest to origin
                new Vector2(0f, 3f),
            };
            Assert.That(HubTargeting.Nearest(Vector2.zero, positions), Is.EqualTo(1));
            // Ties resolve to the lower index.
            Assert.That(HubTargeting.Nearest(new Vector2(2f, 0f),
                new List<Vector2> { new Vector2(1f, 0f), new Vector2(3f, 0f) }), Is.EqualTo(0));
        }

        // --- InspectionInteractable ---------------------------------------------

        [Test]
        public void InspectionInteractable_RegistersClue_Once()
        {
            var context = NewContext(out _, out HubJournal journal);
            var inspection = NewComponent<InspectionInteractable>("NoticeBoard");
            inspection.clue = NewClue("C_BOARD");

            inspection.Interact(context);
            inspection.Interact(context); // second inspection is a no-op

            Assert.That(journal.Collected.Count, Is.EqualTo(1));
            Assert.That(journal.Contains(inspection.clue), Is.True);
        }

        [Test]
        public void InspectionInteractable_NoClue_OrNoJournal_IsSafe()
        {
            var inspection = NewComponent<InspectionInteractable>();
            inspection.clue = null;
            Assert.That(() => inspection.Interact(null), Throws.Nothing);
        }

        // --- TravelRules --------------------------------------------------------

        Morsoul.Core.SceneRouteTable HubRoutes()
        {
            var routes = new Morsoul.Core.SceneRouteTable();
            routes.Set("hub.0", "Hub_LowerMarket");
            routes.Set("hub.1", "Hub_LowerMarket");
            routes.Set("hub.2", "Hub_CathedralNave");
            routes.Set("hub.3", "Hub_Wilderness");
            routes.Set("hub.4", "Hub_IronHollow");
            return routes;
        }

        [Test]
        public void TravelRules_IsHubUnlocked_ProgressionAndBounds()
        {
            Assert.That(TravelRules.IsHubUnlocked(1, 0), Is.False);
            Assert.That(TravelRules.IsHubUnlocked(0, 0), Is.True);
            Assert.That(TravelRules.IsHubUnlocked(1, 1), Is.True);
            Assert.That(TravelRules.IsHubUnlocked(3, 1), Is.False);
            // Completed cases keep their hubs open (backtracking).
            Assert.That(TravelRules.IsHubUnlocked(1, 5), Is.True);
            Assert.That(TravelRules.IsHubUnlocked(-1, 5), Is.False);
        }

        [Test]
        public void TravelRules_CollectDestinations_FiltersUnlocksCurrentAndDupes()
        {
            var routes = HubRoutes();
            var ids = new List<int> { 0, 1, 2, 3, 4 };
            var destinations = TravelRules.CollectDestinations(
                routes, ids, null, currentCase: 3, currentScene: "Hub_Wilderness",
                canLoad: null);

            // hub.4 locked (case 4 not reached); hub.3 is the current scene;
            // hub.0 and hub.1 collapse into one LowerMarket destination.
            Assert.That(destinations.Count, Is.EqualTo(2));
            Assert.That(destinations[0].sceneName, Is.EqualTo("Hub_LowerMarket"));
            Assert.That(destinations[1].sceneName, Is.EqualTo("Hub_CathedralNave"));
        }

        [Test]
        public void TravelRules_CollectDestinations_HonoursCanLoadAndLabels()
        {
            var routes = HubRoutes();
            var ids = new List<int> { 0, 1 };
            var labels = new Dictionary<int, string> { { 0, "Market" }, { 1, "Guild Square" } };
            var destinations = TravelRules.CollectDestinations(
                routes, ids, labels, currentCase: 1, currentScene: "Other",
                canLoad: s => s != "Hub_LowerMarket");

            Assert.That(destinations.Count, Is.EqualTo(0));

            destinations = TravelRules.CollectDestinations(
                routes, ids, labels, currentCase: 1, currentScene: "Other",
                canLoad: _ => true);
            Assert.That(destinations.Count, Is.EqualTo(1));
            Assert.That(destinations[0].label, Is.EqualTo("Market")); // first id's label wins
        }

        [Test]
        public void TravelRules_CollectDestinations_NullInputs_AreSafe()
        {
            Assert.That(TravelRules.CollectDestinations(null, null, null, 5, "x", null).Count,
                Is.EqualTo(0));
        }

        [Test]
        public void AxisLock_DiagonalInput_CollapsesToHorizontal()
        {
            Assert.AreEqual(new Vector2(1f, 0f), HubPlayerController.AxisLock(new Vector2(1f, 1f)));
            Assert.AreEqual(new Vector2(-1f, 0f), HubPlayerController.AxisLock(new Vector2(-1f, 0.4f)));
            Assert.AreEqual(new Vector2(0f, 1f), HubPlayerController.AxisLock(new Vector2(0.3f, 1f)));
            Assert.AreEqual(new Vector2(0f, -1f), HubPlayerController.AxisLock(new Vector2(0f, -1f)));
            Assert.AreEqual(Vector2.zero, HubPlayerController.AxisLock(Vector2.zero));
        }

        [Test]
        public void AxisMove_Diagonal_DecomposesIntoTwoAxisLegs()
        {
            var from = new Vector3(1f, 2f, 0f);
            var to = new Vector3(4f, 6f, 0f);

            var cornerH = AxisMove.Corner(from, to);
            Assert.AreEqual(new Vector3(4f, 2f, 0f), cornerH, "horizontal leg first");

            var cornerV = AxisMove.Corner(from, to, horizontalFirst: false);
            Assert.AreEqual(new Vector3(1f, 6f, 0f), cornerV, "vertical leg first");

            // Already axis-aligned: no corner inserted.
            Assert.AreEqual(to, AxisMove.Corner(from, new Vector3(4f, 2f, 0f)));
            Assert.AreEqual(to, AxisMove.Corner(from, new Vector3(1f, 6f, 0f)));

            Assert.AreEqual(7f, AxisMove.PathLength(from, to));
        }
    }
}

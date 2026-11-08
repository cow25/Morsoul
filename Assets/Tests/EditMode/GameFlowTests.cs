using System.Collections.Generic;
using System.Linq;
using Morsoul.Board;
using Morsoul.Data;
using Morsoul.Flow;
using Morsoul.Hub;
using Morsoul.Interrogation;
using Morsoul.Resolution;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Morsoul.Tests
{
    /// <summary>
    /// Drives GameFlow through its public handler surface (the same methods it
    /// subscribes to controller events). Controllers run on bare GameObjects
    /// without PanelRenderers - all UI work inside them is null-tolerant, so
    /// the session/grade/routing logic is exercised for real.
    /// </summary>
    public class GameFlowTests
    {
        readonly List<GameObject> _objects = new List<GameObject>();

        GameFlow _flow;
        BoardController _board;
        InterrogationController _interrogation;
        ResolutionController _resolution;
        HubJournal _journal;

        T New<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go.AddComponent<T>();
        }

        [SetUp]
        public void SetUp()
        {
            _board = New<BoardController>("Board");
            _interrogation = New<InterrogationController>("Interrogation");
            _resolution = New<ResolutionController>("Resolution");
            _journal = New<HubJournal>("Journal");
            _flow = New<GameFlow>("GameFlow");
            _flow.board = _board;
            _flow.interrogation = _interrogation;
            _flow.resolution = _resolution;
            _flow.hubJournal = _journal;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
                if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
            TestCases.DestroyAll();
        }

        // --- BeginCase ---------------------------------------------------------

        [Test]
        public void BeginCase_CreatesSession_AndPushesCaseToBoard()
        {
            var f = TestCases.Build(1, withInterrogation: true);
            _flow.BeginCase(f.caseData);

            Assert.NotNull(_flow.Session);
            Assert.AreSame(f.caseData, _flow.Session.Case);
            Assert.AreSame(f.caseData, _board.caseData);
        }

        [Test]
        public void BeginCase_NullCase_IsIgnored()
        {
            _flow.BeginCase(null);
            Assert.IsNull(_flow.Session);
            Assert.AreEqual(Grade.Ungraded, _flow.CurrentGrade);
        }

        // --- Clue rack rule ------------------------------------------------------

        [Test]
        public void ResolveBoardClues_PrefersPreloadedClues()
        {
            var f = TestCases.Build(5, withInterrogation: false, boardOnly: true);
            var clues = GameFlow.ResolveBoardClues(f.caseData);

            Assert.AreEqual(4, clues.Count);
            CollectionAssert.AreEquivalent(f.caseData.preloadedClues, clues);
            CollectionAssert.DoesNotContain(clues, f.rh1);
        }

        [Test]
        public void ResolveBoardClues_NoPreload_TakesAllNonLatentCaseClues()
        {
            var f = TestCases.Build(1, withInterrogation: true);
            var latent = ScriptableObject.CreateInstance<ClueData>();
            latent.id = "L1_01";
            latent.displayName = "Latent";
            latent.type = ClueType.Latent;
            latent.isBacktrackingOnly = true;
            latent.caseId = 1;
            f.assets.Add(latent);
            var clues = new List<ClueData>(f.caseData.clues) { latent };
            f.caseData.clues = clues.ToArray();

            var resolved = GameFlow.ResolveBoardClues(f.caseData);

            CollectionAssert.DoesNotContain(resolved, latent);
            Assert.AreEqual(5, resolved.Count); // c1 c2 t1 t2 rh1
        }

        // --- Grading -------------------------------------------------------------

        [Test]
        public void HandleBoardAnswer_Wrong_RecordsWrongAnswer_AndDropsGrade()
        {
            var f = TestCases.Build(1, withInterrogation: true);
            _flow.BeginCase(f.caseData);

            _flow.HandleBoardAnswer(new AnswerResult(
                AnswerOutcome.Wrong, f.n1, null, 10f, 0, 1, "hint"));

            Assert.AreEqual(1, _flow.Session.WrongCausalAnswers);
            Assert.AreEqual(Grade.Standard, _flow.CurrentGrade);
        }

        [Test]
        public void HandleBoardAnswer_Correct_KeepsCleanGrade()
        {
            var f = TestCases.Build(1, withInterrogation: true);
            _flow.BeginCase(f.caseData);

            _flow.HandleBoardAnswer(new AnswerResult(
                AnswerOutcome.Correct, f.n1, f.p1, 0f, 0, 0, null));

            Assert.AreEqual(0, _flow.Session.WrongCausalAnswers);
            Assert.AreEqual(Grade.Clean, _flow.CurrentGrade);
        }

        // --- Ending routing -------------------------------------------------------

        [Test]
        public void RoutesToEnding_FourLogs_RoutesToEnding3()
        {
            var f = TestCases.Build(5, withInterrogation: false, boardOnly: true);
            _flow.BeginCase(f.caseData);
            _flow.runicLogsCollected = 4;
            int? routed = null;
            _flow.EndingRouteRequested += e => routed = e;

            _flow.HandleRoutesToEnding();

            Assert.AreEqual(3, routed);
        }

        [Test]
        public void RoutesToEnding_ZeroLogs_RoutesToEnding2()
        {
            var f = TestCases.Build(5, withInterrogation: false, boardOnly: true);
            _flow.BeginCase(f.caseData);
            _flow.runicLogsCollected = 0;
            int? routed = null;
            _flow.EndingRouteRequested += e => routed = e;

            _flow.HandleRoutesToEnding();

            Assert.AreEqual(2, routed);
        }

        [Test]
        public void StrikeCosted_ThreeStrikes_RoutesToEnding1()
        {
            var f = TestCases.Build(5, withInterrogation: false, boardOnly: true);
            _flow.BeginCase(f.caseData);
            int? routed = null;
            _flow.EndingRouteRequested += e => routed = e;

            _flow.HandleBoardStrikeCosted(1);
            Assert.IsNull(routed);
            _flow.HandleBoardStrikeCosted(2);

            Assert.AreEqual(3, _flow.Session.StrikesLost);
            Assert.AreEqual(1, routed);
        }

        // --- Interrogation wiring --------------------------------------------------

        [Test]
        public void InterrogationUnlocked_ConfiguresAndOpensPanel()
        {
            var f = TestCases.Build(1, withInterrogation: true);
            _flow.BeginCase(f.caseData);
            _board.Open();
            _flow.HandleClueRegistered(f.t1); // journal evidence

            _flow.HandleInterrogationUnlocked(f.interrogation);

            Assert.IsTrue(_interrogation.IsOpen);
            Assert.IsFalse(_board.IsOpen);
            Assert.AreSame(f.interrogation, _interrogation.interrogationData);
            Assert.IsTrue(_interrogation.guidedFirstPhase, "caseId 1 -> guided");
            Assert.IsFalse(_interrogation.emotionEchoUnlocked, "echo requires caseId >= 3");
        }

        [Test]
        public void InterrogationUnlocked_Case3_EnablesEmotionEcho()
        {
            var f = TestCases.Build(3, withInterrogation: true);
            _flow.BeginCase(f.caseData);

            _flow.HandleInterrogationUnlocked(f.interrogation);

            Assert.IsTrue(_interrogation.emotionEchoUnlocked);
            Assert.IsFalse(_interrogation.guidedFirstPhase);
        }

        [Test]
        public void InterrogationEnded_Confessed_ShowsResolutionAndClosesPanel()
        {
            var f = TestCases.Build(1, withInterrogation: true);
            _flow.BeginCase(f.caseData);
            _flow.HandleInterrogationUnlocked(f.interrogation);

            _flow.HandleInterrogationEnded(InterrogationStatus.Confessed);

            Assert.IsTrue(_resolution.IsVisible);
            Assert.IsFalse(_interrogation.IsOpen);
            Assert.AreEqual(Grade.Clean, _flow.CurrentGrade);
            Assert.IsFalse(_resolution.UneaseVisible, "no latentLog -> no unease");
        }

        [Test]
        public void InterrogationEnded_Confessed_UncollectedLatentLog_ShowsUnease()
        {
            var f = TestCases.Build(1, withInterrogation: true);
            var log = ScriptableObject.CreateInstance<RunicLogData>();
            log.id = 1;
            log.location = "Vault alcove";
            f.assets.Add(log);
            f.caseData.latentLog = log;
            f.caseData.uneaseFlagKey = "Case1_UneaseActive";
            var latentClue = ScriptableObject.CreateInstance<ClueData>();
            latentClue.id = "L1_01";
            latentClue.displayName = "Runic Log #1";
            latentClue.caseId = f.caseData.caseId;
            latentClue.type = ClueType.Latent;
            latentClue.isBacktrackingOnly = true;
            f.assets.Add(latentClue);
            f.caseData.clues = f.caseData.clues.Concat(new[] { latentClue }).ToArray();
            _flow.BeginCase(f.caseData);
            _flow.latentLogCollected = false;

            _flow.HandleInterrogationEnded(InterrogationStatus.Confessed);

            Assert.IsTrue(_resolution.UneaseVisible);
        }

        // --- Mistrial ----------------------------------------------------------------

        [Test]
        public void Mistrial_RecordsPenalty_ReloadWaitsForRetryConfirm()
        {
            var f = TestCases.Build(1, withInterrogation: true);
            _flow.BeginCase(f.caseData);
            int calls = 0;
            _flow.MistrialRetryRequested += () => calls++;

            // The controller fires MistrialRequested AND InterrogationEnded(Mistrial)
            // for a single mistrial; both feed the same deduplicated handler.
            _flow.HandleMistrialRequested();
            _flow.HandleInterrogationEnded(InterrogationStatus.Mistrial);

            Assert.IsTrue(_flow.Session.MistrialRetried);
            Assert.AreEqual(0, _flow.Session.StrikesLost);
            Assert.AreEqual(0, calls,
                "the checkpoint reload waits for the player's retry confirmation");
        }

        [Test]
        public void RetryConfirm_FiresEvent_AndReopensPanelWithoutCore()
        {
            var f = TestCases.Build(1, withInterrogation: true);
            _flow.BeginCase(f.caseData);
            _flow.HandleInterrogationUnlocked(f.interrogation);
            _interrogation.Close();
            int calls = 0;
            _flow.MistrialRetryRequested += () => calls++;

            _flow.HandleMistrialRequested();
            _flow.HandleRetryRequested(); // no CoreRuntime -> in-scene retry

            Assert.AreEqual(1, calls);
            Assert.IsTrue(_interrogation.IsOpen);
        }

        [Test]
        public void RetryConfirm_WithoutMistrial_DoesNothing()
        {
            var f = TestCases.Build(1, withInterrogation: true);
            _flow.BeginCase(f.caseData);
            int calls = 0;
            _flow.MistrialRetryRequested += () => calls++;

            _flow.HandleRetryRequested();

            Assert.AreEqual(0, calls);
            Assert.IsFalse(_interrogation.IsOpen);
        }

        [Test]
        public void RetryInterrogation_ResetsAndReopensPanel()
        {
            var f = TestCases.Build(1, withInterrogation: true);
            _flow.BeginCase(f.caseData);
            _flow.HandleInterrogationUnlocked(f.interrogation);
            _interrogation.Close();

            _flow.HandleMistrialRequested();
            _flow.RetryInterrogation();

            Assert.IsTrue(_interrogation.IsOpen);
        }

        [Test]
        public void InterrogationStrikes_DeltaFeedsGrade()
        {
            var f = TestCases.Build(1, withInterrogation: true);
            _flow.BeginCase(f.caseData);

            _flow.HandleInterrogationStrikes(2);
            Assert.AreEqual(2, _flow.Session.StrikesLost);
            Assert.AreEqual(Grade.Standard, _flow.CurrentGrade);

            _flow.HandleInterrogationStrikes(2); // repeated total: no double count
            Assert.AreEqual(2, _flow.Session.StrikesLost);
        }

        [Test]
        public void RestoreValidatedNodes_FiltersForeignCaseNodes()
        {
            var f = TestCases.Build(1, withInterrogation: true);
            _board.SetCase(f.caseData);

            _board.RestoreValidatedNodes(new[] { f.n1.id, "IN9_99" });

            CollectionAssert.AreEquivalent(new[] { f.n1.id }, _board.ValidatedNodeIds);
            CollectionAssert.Contains(_board.Premises, f.p1);
        }

        [Test]
        public void LatentClue_Registration_LatchesActiveCaseFlag()
        {
            var f = TestCases.Build(1, withInterrogation: true);
            _flow.BeginCase(f.caseData);
            Assert.IsFalse(_flow.latentLogCollected);

            var latent = ScriptableObject.CreateInstance<ClueData>();
            latent.id = "L1_01";
            latent.displayName = "Runic Log #1";
            latent.type = ClueType.Latent;
            latent.caseId = 1;
            f.assets.Add(latent);

            _flow.HandleClueRegistered(latent);

            Assert.IsTrue(_flow.latentLogCollected);
        }

        // --- Clue / focus pass-through ------------------------------------------------

        [Test]
        public void ClueRegistered_FeedsJournalAndReemits()
        {
            var f = TestCases.Build(1, withInterrogation: true);
            _flow.BeginCase(f.caseData);
            ClueData received = null;
            _flow.ClueRegistered += c => received = c;

            _flow.HandleClueRegistered(f.c1);

            Assert.IsTrue(_journal.Contains(f.c1));
            Assert.AreSame(f.c1, received);
        }

        [Test]
        public void FocusCosted_ReemitsAsFocusSpent()
        {
            var f = TestCases.Build(1, withInterrogation: true);
            _flow.BeginCase(f.caseData);
            float cost = 0f;
            string source = null;
            _flow.FocusSpent += (c, s) => { cost = c; source = s; };

            _flow.HandleBoardFocusCosted(12.5f);

            Assert.AreEqual(12.5f, cost);
            Assert.AreEqual("Board", source);
        }

        // --- Campaign bootstrap ------------------------------------------------

        [Test]
        public void CaseDatabase_Find_ResolvesByCaseId()
        {
            var f = TestCases.Build(3, withInterrogation: true);
            var db = ScriptableObject.CreateInstance<CaseDatabase>();
            f.assets.Add(db);
            db.cases.Add(null);
            db.cases.Add(f.caseData);

            Assert.AreSame(f.caseData, db.Find(3));
            Assert.IsNull(db.Find(4));
        }

        [Test]
        public void SessionCase_ResolvesThroughCaseDatabase()
        {
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            core.Session.CurrentCase = 5;
            var f = TestCases.Build(5, withInterrogation: false, boardOnly: true);
            var db = ScriptableObject.CreateInstance<CaseDatabase>();
            f.assets.Add(db);
            db.cases.Add(f.caseData);
            _flow.caseDatabase = db;

            Assert.IsTrue(_flow.TryBeginSessionCase());

            Assert.AreSame(f.caseData, _flow.Session.Case);
            Assert.AreEqual(5, core.Session.CurrentCase);
        }

        [Test]
        public void SessionCase_UnknownId_ReturnsFalse()
        {
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            core.Session.CurrentCase = 99;
            var f = TestCases.Build(1, withInterrogation: true);
            var db = ScriptableObject.CreateInstance<CaseDatabase>(); // empty
            f.assets.Add(db);
            _flow.caseDatabase = db;

            Assert.IsFalse(_flow.TryBeginSessionCase());
            Assert.IsNull(_flow.Session);
        }

        [Test]
        public void CaseLoopRoute_IsSeeded()
        {
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            var gsm = Core.GameStateManager.Ensure();

            gsm.SeedDefaultRoutes();

            Assert.AreEqual("CaseLoop", gsm.routes.Resolve(Core.RouteKeys.CaseLoop));
        }

        [Test]
        public void CaseCompleted_AdvancesSessionCurrentCase()
        {
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            var gsm = Core.GameStateManager.Ensure();
            gsm.routes.routes.Clear(); // unregistered routes -> warnings only, no scene loads
            core.Session.CurrentCase = 1;

            gsm.OnCaseCompleted(new Core.CaseCompletedEvent { CaseId = 1, Ending = 0 });

            Assert.AreEqual(2, core.Session.CurrentCase);
        }

        // --- Case 1 route/session integration -----------------------------------

        static CaseData LoadCaseOne() =>
            AssetDatabase.LoadAssetAtPath<CaseData>(
                "Assets/Data/Cases/Case01/Case_01.asset");

        [Test]
        public void CaseOneHubAndInvestigationRoutes_AreSeeded()
        {
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            var gsm = Core.GameStateManager.Ensure();

            gsm.SeedDefaultRoutes();

            Assert.AreEqual("Hub_LowerMarket",
                gsm.routes.Resolve(Core.RouteKeys.Hub(1)));
            Assert.AreEqual("Investigation_01_Placeholder",
                gsm.routes.Resolve(Core.RouteKeys.Investigation(1)));
        }

        [Test]
        public void RouteBackedDestinations_PreferRouteTableOverFallback()
        {
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            var gsm = Core.GameStateManager.Ensure();
            gsm.SeedDefaultRoutes();

            var gate = New<GateInteractable>("VaultGate");
            gate.routeKey = Core.RouteKeys.Investigation(1);
            gate.targetScene = "Missing_Vault";
            var carrier = New<CarrierInteractable>("CaseDoor");
            carrier.routeKey = Core.RouteKeys.CaseLoop;
            carrier.targetScene = "Missing_CaseLoop";

            Assert.AreEqual("Investigation_01_Placeholder", gate.ResolveTargetScene());
            Assert.AreEqual("CaseLoop", carrier.ResolveTargetScene());
        }

        [Test]
        public void BoardHotkey_CaseOne_LoadsSessionCollectedClues()
        {
            var case1 = LoadCaseOne();
            Assert.NotNull(case1);
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            core.Session.Reset();
            core.Session.CurrentCase = 1;
            foreach (var clue in case1.clues) core.Session.RegisterClue(clue);

            var board = New<WeightedBoardController>("WeightedBoard");
            var hotkey = New<BoardHotkey>("BoardHotkey");
            hotkey.board = board;

            Assert.IsTrue(hotkey.EnsureBoard());
            Assert.AreSame(case1, board.caseData);
            Assert.NotNull(board.Session);
            Assert.AreEqual(6, board.Session.State.CollectedClueIds.Count);
            CollectionAssert.Contains(board.Session.State.CollectedClueIds, "C1_01");
            CollectionAssert.Contains(board.Session.State.CollectedClueIds, "T2_01");
            CollectionAssert.DoesNotContain(board.Session.State.CollectedClueIds, "L1_01");
        }

        [Test]
        public void SolvedCaseOneBoard_ResumesIntoInterrogationWithSessionEvidence()
        {
            var case1 = LoadCaseOne();
            Assert.NotNull(case1);
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            core.Session.Reset();
            core.Session.CurrentCase = 1;
            foreach (var clue in case1.clues) core.Session.RegisterClue(clue);
            var state = core.Session.GetOrCreateBoardState(1);
            state.BoardSolved = true;

            var weighted = New<WeightedBoardController>("WeightedBoard");
            _flow.weightedBoard = weighted;

            _flow.BeginCase(case1);

            Assert.IsFalse(weighted.IsOpen);
            Assert.IsTrue(_interrogation.IsOpen);
            Assert.AreSame(case1.interrogation, _interrogation.interrogationData);
            var evidence = _flow.BuildEvidence();
            CollectionAssert.Contains(evidence, case1.inquiryNodes[0].producesPremise);
            CollectionAssert.Contains(evidence, case1.inquiryNodes[1].producesPremise);
            CollectionAssert.Contains(evidence, case1.clues[1]); // C2_01 physical evidence
        }

        [Test]
        public void CaseOneConfession_PersistsGradeAndUneaseFlag()
        {
            var case1 = LoadCaseOne();
            Assert.NotNull(case1);
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            core.Session.Reset();

            _flow.BeginCase(case1);
            _flow.HandleInterrogationEnded(InterrogationStatus.Confessed);

            Assert.IsTrue(_resolution.IsVisible);
            Assert.IsTrue(_resolution.UneaseVisible);
            Assert.AreEqual(Grade.Clean, core.Session.Grades[1]);
            Assert.IsTrue(core.Session.HasFlag(case1.uneaseFlagKey));
        }
    }
}

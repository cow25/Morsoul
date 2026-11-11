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
        readonly List<Object> _tempAssets = new List<Object>();

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
            // JournalController pauses via Time.timeScale; a failed assert can
            // strand it at 0 and poison both later tests and TimeManager.asset.
            Time.timeScale = 1f;
            foreach (var go in _objects)
                if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
            foreach (var asset in _tempAssets)
                if (asset != null) Object.DestroyImmediate(asset);
            _tempAssets.Clear();
            TestCases.DestroyAll();
        }

        /// <summary>
        /// A one-shot cutscene that stays playing in EditMode: the shot needs
        /// a visual to be playable and waitForInput so it holds until Advance.
        /// </summary>
        Cutscene.CutsceneData BuildWaitForInputCutscene()
        {
            var tex = new Texture2D(1, 1);
            _tempAssets.Add(tex);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.zero);
            _tempAssets.Add(sprite);
            var data = ScriptableObject.CreateInstance<Cutscene.CutsceneData>();
            _tempAssets.Add(data);
            data.shots = new[]
            {
                new Cutscene.CutsceneShotData
                {
                    image = sprite,
                    caption = "Echoes.",
                    waitForInput = true,
                },
            };
            return data;
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

        static CaseData LoadCase(int caseId) =>
            AssetDatabase.LoadAssetAtPath<CaseData>(
                $"Assets/Data/Cases/Case0{caseId}/Case_0{caseId}.asset");

        [Test]
        public void InvestigationGate_UsesEntryAdapterWithoutLoadingSerializedStandIn()
        {
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            int calls = 0;
            core.InvestigationEntryHandler = route =>
            {
                Assert.AreEqual("investigation.2", route);
                calls++;
                return new Core.InvestigationEntryResult { Success = true, Message = "Test investigation accepted." };
            };
            var gate = New<GateInteractable>("InvestigationGate");
            gate.routeKey = "investigation.2";
            gate.targetScene = "This_StandIn_Must_Not_Load";
            gate.Interact(null);
            Assert.AreEqual(1, calls);
            Assert.AreEqual("Test investigation accepted.", core.InvestigationStatusMessage);
            gate.requiredFlag = "Missing_Clearance";
            gate.Interact(null);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void CampaignRoutes_SeedOnlyDeliveredScenes()
        {
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            var gsm = Core.GameStateManager.Ensure();

            gsm.SeedDefaultRoutes();

            Assert.AreEqual("Hub_LowerMarket", gsm.routes.Resolve(Core.RouteKeys.Hub(1)));
            Assert.AreEqual("Hub_CathedralNave", gsm.routes.Resolve(Core.RouteKeys.Hub(2)));
            Assert.AreEqual("Hub_Wilderness", gsm.routes.Resolve(Core.RouteKeys.Hub(3)));
            Assert.AreEqual("Hub_IronHollow", gsm.routes.Resolve(Core.RouteKeys.Hub(4)));
            Assert.IsNull(gsm.routes.Resolve(Core.RouteKeys.Hub(5)));
            Assert.AreEqual("PrologueRuins", gsm.routes.Resolve(Core.RouteKeys.Opening));
            Assert.IsNull(gsm.routes.Resolve(Core.RouteKeys.Investigation(1)));
            Assert.IsNull(gsm.routes.Resolve(Core.RouteKeys.Investigation(2)));
            Assert.IsNull(gsm.routes.Resolve(Core.RouteKeys.Investigation(3)));
            Assert.IsNull(gsm.routes.Resolve(Core.RouteKeys.Investigation(4)));
        }

        [Test]
        public void ContinueScene_MissingCheckpoint_FallsBackToCaseHubThenTitle()
        {
            var routes = new Core.SceneRouteTable();
            routes.Set(Core.RouteKeys.Title, "Title");
            routes.Set(Core.RouteKeys.Hub(2), "Hub_CathedralNave");

            Assert.AreEqual("Hub_CathedralNave",
                Core.GameStateManager.ResolveContinueScene("Missing_Scene", 2, routes,
                    scene => scene != "Missing_Scene"));
            Assert.AreEqual("Title",
                Core.GameStateManager.ResolveContinueScene("Missing_Scene", 5, routes,
                    scene => scene != "Missing_Scene"));
            Assert.AreEqual("Hub_CathedralNave",
                Core.GameStateManager.ResolveContinueScene("Hub_CathedralNave", 5, routes,
                    _ => true));
            Assert.IsNull(
                Core.GameStateManager.ResolveContinueScene("Missing_Scene", 5, routes,
                    _ => false));
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
            gsm.routes.Set(Core.RouteKeys.Investigation(1), "Delivered_Vault");
            var carrier = New<CarrierInteractable>("CaseDoor");
            carrier.routeKey = Core.RouteKeys.CaseLoop;
            carrier.targetScene = "Missing_CaseLoop";

            Assert.AreEqual("Delivered_Vault", gate.ResolveTargetScene());
            Assert.AreEqual("CaseLoop", carrier.ResolveTargetScene());
        }

        [Test]
        public void InvestigationRevisit_DoesNotChangeCampaignCase()
        {
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            core.Session.CurrentCase = 5;
            core.Session.InvestigationCaseId = 1;
            core.Session.CurrentSceneMode = Core.SceneMode.Mode3D;
            _flow.investigationScene = true;
            _flow.weightedBoard = New<WeightedBoardController>("InvestigationBoard");
            _flow.BeginCase(LoadCaseOne());
            Assert.AreEqual(5, core.Session.CurrentCase);
            Assert.AreEqual(1, core.Session.InvestigationCaseId);
            Assert.AreEqual(1, _flow.weightedBoard.Session.State.CaseId);
        }

        [Test]
        public void AcceptedBoard_NotifiesObserversAfterFocusRewardIsCommitted()
        {
            var data = LoadCaseOne();
            var board = New<WeightedBoardController>("WeightedBoard");
            board.SetCase(data);
            board.SetClues(data.clues);
            float energy = 25f, maximum = 100f;
            board.FocusSource = () => (energy, maximum);
            board.FocusSink = (value, cap) => { energy = value; maximum = cap; };
            foreach (var edge in data.evidenceGraph.solutionEdges)
                board.DrawLink(edge.sourceId, edge.targetId);
            Assert.IsTrue(board.SelectConclusion(data.evidenceGraph.completeConclusionId));
            bool notified = false;
            board.BoardSolved += certificate =>
            {
                notified = true;
                Assert.AreEqual(110f, energy);
                Assert.AreEqual(110f, maximum);
            };
            board.SubmitConclusion();
            Assert.IsTrue(notified);
        }

        [Test]
        public void WeightedBoardStart_PreservesAlreadyBoundPersistentState()
        {
            var board = New<WeightedBoardController>("WeightedBoard");
            var state = new Morsoul.Engine.BoardRuntimeState { CaseId = 1, CaseErrorCount = 7 };
            board.SetCase(LoadCaseOne(), state);
            typeof(WeightedBoardController).GetMethod("Start",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(board, null);
            Assert.AreSame(state, board.Session.State);
            Assert.AreEqual(7, board.Session.State.CaseErrorCount);
        }

        [Test]
        public void HubBoard_RejectsOpenAndMutationEvenWithAnExistingSession()
        {
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            core.Session.CurrentCase = 1;
            var board = New<WeightedBoardController>("HubBoard");
            board.SetCase(LoadCaseOne(), core.Session.GetOrCreateBoardState(1));
            board.SetClues(LoadCaseOne().clues);
            var hook = New<BoardHotkey>("HubBoardHook");
            hook.board = board;
            Assert.IsFalse(hook.EnsureBoard());
            board.Open();
            Assert.IsFalse(board.IsOpen);
            board.DrawLink("C1_01", "C2_01");
            Assert.IsEmpty(board.Session.State.Links);
            Assert.IsFalse(board.SelectConclusion(LoadCaseOne().evidenceGraph.completeConclusionId));
            Assert.IsEmpty(board.SubmitConclusion());
        }

        [Test]
        public void MatchingSuspect_RequiresAcceptedCurrentCaseAndRejectsResolvedCases()
        {
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            core.Session.CurrentCase = 1;
            core.Session.GetOrCreateBoardState(1).BoardSolved = true;
            int calls = 0;
            core.InterrogationEntryHandler = caseId => { calls++; return true; };
            var suspect = New<NpcInteractable>("Suspect");
            suspect.interrogationCaseId = 1;
            suspect.Interact(null);
            Assert.AreEqual(1, calls);
            Assert.IsFalse(core.EnterInterrogation(2));
            core.Session.SetGrade(1, Grade.Standard);
            Assert.IsFalse(core.EnterInterrogation(1));
            Assert.AreEqual(1, calls);
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

            core.Session.CurrentSceneMode = Core.SceneMode.Mode3D;
            Assert.IsTrue(hotkey.EnsureBoard());
            Assert.AreSame(case1, board.caseData);
            Assert.NotNull(board.Session);
            Assert.AreEqual(6, board.Session.State.CollectedClueIds.Count);
            CollectionAssert.Contains(board.Session.State.CollectedClueIds, "C1_01");
            CollectionAssert.Contains(board.Session.State.CollectedClueIds, "T2_01");
            CollectionAssert.DoesNotContain(board.Session.State.CollectedClueIds, "L1_01");
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void BoardHotkey_CampaignCase_LoadsItsAuthoredWeightedSession(int caseId)
        {
            var data = LoadCase(caseId);
            Assert.NotNull(data);
            Assert.NotNull(data.evidenceGraph);
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            core.Session.Reset();
            core.Session.CurrentCase = caseId;
            foreach (var clue in data.clues) core.Session.RegisterClue(clue);

            var board = New<WeightedBoardController>("WeightedBoard");
            var hotkey = New<BoardHotkey>("BoardHotkey");
            hotkey.board = board;

            core.Session.CurrentSceneMode = Core.SceneMode.Mode3D;
            Assert.IsTrue(hotkey.EnsureBoard());
            Assert.AreSame(data, board.caseData);
            Assert.NotNull(board.Session);
            Assert.AreEqual(caseId, board.Session.State.CaseId);
            Assert.Greater(board.Session.State.CollectedClueIds.Count, 0);
        }

        [Test]
        public void FinaleFlow_UsesPreloadedCluesInDedicatedContext()
        {
            var data = LoadCase(5);
            Assert.NotNull(data);
            Assert.NotNull(data.evidenceGraph);
            Assert.NotNull(data.preloadedClues);
            Assert.Greater(data.preloadedClues.Length, 0);
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            core.Session.Reset();
            core.Session.CurrentCase = 5;

            var board = New<WeightedBoardController>("WeightedBoard");
            _flow.weightedBoard = board;
            _flow.BeginCase(data);

            Assert.IsTrue(board.IsOpen);
            Assert.AreSame(data, board.caseData);
            Assert.NotNull(board.Session);
            CollectionAssert.AreEquivalent(
                data.preloadedClues.Where(c => c != null).Select(c => c.id),
                board.Session.State.CollectedClueIds);
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
            if (case1.inquiryNodes != null && case1.inquiryNodes.Length >= 2)
            {
                CollectionAssert.DoesNotContain(evidence, case1.inquiryNodes[0].producesPremise);
                CollectionAssert.DoesNotContain(evidence, case1.inquiryNodes[1].producesPremise);
            }
            CollectionAssert.Contains(evidence, case1.clues[1]); // C2_01 physical evidence
        }

        [Test]
        public void CaseOneConfession_ToolGatedLog_PersistsGradeWithoutUnease()
        {
            // GDD §2.4: Case 1's log needs Aether Sight (unlocked in Case 4),
            // so it is beyond current tools and no banner/flag fires.
            var case1 = LoadCaseOne();
            Assert.NotNull(case1);
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            core.Session.Reset();

            _flow.BeginCase(case1);
            _flow.HandleInterrogationEnded(InterrogationStatus.Confessed);

            Assert.IsTrue(_resolution.IsVisible);
            Assert.IsFalse(_resolution.UneaseVisible);
            Assert.AreEqual(Grade.Clean, core.Session.Grades[1]);
            Assert.IsFalse(core.Session.HasFlag(case1.uneaseFlagKey));
        }

        [Test]
        public void Confession_ReachableMissedLog_ShowsUneaseAndSetsFlag()
        {
            var core = EnsureEncounterSession(1);
            var f = TestCases.Build(1, withInterrogation: true);
            var log = ScriptableObject.CreateInstance<RunicLogData>();
            log.id = 1;
            log.caseId = 1;
            log.location = "Vault alcove";
            f.assets.Add(log);
            f.caseData.latentLog = log;
            f.caseData.uneaseFlagKey = "Case1_UneaseActive";
            var latentClue = ScriptableObject.CreateInstance<ClueData>();
            latentClue.id = "L1_01";
            latentClue.displayName = "Runic Log #1";
            latentClue.caseId = 1;
            latentClue.type = ClueType.Latent;
            f.assets.Add(latentClue);
            f.caseData.clues = f.caseData.clues.Concat(new[] { latentClue }).ToArray();
            _flow.BeginCase(f.caseData);

            _flow.HandleInterrogationEnded(InterrogationStatus.Confessed);

            Assert.IsTrue(_resolution.UneaseVisible);
            Assert.IsTrue(core.Session.HasFlag("Case1_UneaseActive"));
        }

        [Test]
        public void LatentClue_ResolvesUneaseFlagOnCollection()
        {
            var case1 = LoadCaseOne();
            Assert.NotNull(case1);
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            core.Session.Reset();
            core.Session.SetFlag(case1.uneaseFlagKey);
            var latentClue = case1.clues.First(c => c != null && c.type == ClueType.Latent);
            _flow.activeCase = case1;

            _flow.HandleClueRegistered(latentClue);

            Assert.IsTrue(core.Session.IsLatentLogCollected(1));
            Assert.IsFalse(core.Session.HasFlag(case1.uneaseFlagKey));
        }

        // --- Interrogation encounter snapshots -------------------------------------

        [Test]
        public void EngineRestore_ResumesPhaseSegmentsStrikes()
        {
            var f = TestCases.Build(1, withInterrogation: true);
            var engine = new InterrogationEngine(f.interrogation, new EvidenceData[0]);

            engine.RestoreState(1, 2, 1);
            Assert.AreEqual(1, engine.PhaseIndex);
            Assert.AreEqual(2, engine.SegmentsRemaining);
            Assert.AreEqual(1, engine.StrikesLost);
            Assert.AreEqual(InterrogationStatus.InProgress, engine.Status);

            engine.RestoreState(3, 0, 0); // phase index past the last phase
            Assert.AreEqual(InterrogationStatus.Confessed, engine.Status);
            Assert.IsNull(engine.CurrentPhase);

            engine.RestoreState(0, 3, 3);
            Assert.AreEqual(InterrogationStatus.Mistrial, engine.Status);
        }

        Core.CoreRuntime EnsureEncounterSession(int caseId, int entryStrikes = 0)
        {
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            core.Session.Reset();
            core.Session.CurrentCase = caseId;
            core.Session.GetOrCreateBoardState(caseId).BoardSolved = true;
            if (entryStrikes > 0) core.Session.SetInterrogationStrikeCount(caseId, entryStrikes);
            return core;
        }

        [Test]
        public void EncounterOpen_CreatesSnapshot_ConfessionClearsIt()
        {
            var core = EnsureEncounterSession(1, entryStrikes: 1);
            var f = TestCases.Build(1, withInterrogation: true);

            _flow.BeginCase(f.caseData); // encounter path auto-opens the interrogation

            var snap = core.Session.ActiveInterrogation;
            Assert.NotNull(snap);
            Assert.AreEqual(1, snap.caseId);
            Assert.AreEqual(0, snap.phaseIndex);
            Assert.AreEqual(3, snap.composureSegmentsRemaining);
            Assert.AreEqual(0, snap.strikeCount);
            Assert.AreEqual(1, snap.entryStrikeCount, "entry strikes capture committed history");

            _flow.HandleInterrogationEnded(InterrogationStatus.Confessed);
            Assert.IsNull(core.Session.ActiveInterrogation);
        }

        [Test]
        public void EncounterOpen_RestoresPersistedSnapshot()
        {
            var core = EnsureEncounterSession(1);
            core.Session.SetActiveInterrogation(new Core.ActiveInterrogationSave
            {
                caseId = 1,
                phaseIndex = 1,
                composureSegmentsRemaining = 2,
                strikeCount = 1,
            });
            var f = TestCases.Build(1, withInterrogation: true);

            _flow.BeginCase(f.caseData);

            Assert.IsTrue(_interrogation.IsOpen);
            Assert.AreEqual(1, _interrogation.EnginePhaseIndex);
            Assert.AreEqual(2, _interrogation.EngineSegmentsRemaining);
            Assert.AreEqual(1, _interrogation.EngineStrikesLost);
        }

        [Test]
        public void EncounterProgress_UpdatesSnapshot()
        {
            var core = EnsureEncounterSession(1);
            var f = TestCases.Build(1, withInterrogation: true);
            _flow.BeginCase(f.caseData);

            _interrogation.RestoreState(1, 2, 1); // fires StateChanged -> handler updates the snapshot

            var snap = core.Session.ActiveInterrogation;
            Assert.NotNull(snap);
            Assert.AreEqual(1, snap.phaseIndex);
            Assert.AreEqual(2, snap.composureSegmentsRemaining);
            Assert.AreEqual(1, snap.strikeCount);
        }

        [Test]
        public void InterrogationForfeit_RewindsAttemptStrikesAndClearsSnapshot()
        {
            var core = EnsureEncounterSession(1, entryStrikes: 1);
            var f = TestCases.Build(1, withInterrogation: true);
            _flow.BeginCase(f.caseData); // snapshot.entryStrikeCount = 1

            _flow.HandleInterrogationStrikes(2); // aborted attempt records strikes
            Assert.AreEqual(2, core.Session.GetInterrogationStrikeCount(1));

            _flow.HandleInterrogationForfeited();

            Assert.IsNull(core.Session.ActiveInterrogation);
            Assert.AreEqual(1, core.Session.GetInterrogationStrikeCount(1),
                "aborted attempt strikes are voided; committed history survives");
        }

        [Test]
        public void Mistrial_ClearsSnapshotAndResetsPersistedStrikes()
        {
            var core = EnsureEncounterSession(1);
            var f = TestCases.Build(1, withInterrogation: true);
            _flow.BeginCase(f.caseData);
            Assert.NotNull(core.Session.ActiveInterrogation);

            _flow.HandleMistrialRequested();

            Assert.IsNull(core.Session.ActiveInterrogation);
            Assert.AreEqual(0, core.Session.GetInterrogationStrikeCount(1));
            Assert.IsTrue(core.Session.HasFlag(Core.SessionState.MistrialFlagFor(1)));
        }

        // --- Modal coordination -------------------------------------------------------

        [Test]
        public void Journal_RefusesToOpenWhileUiBlocked_AndBlocksUiWhileOpen()
        {
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            core.Session.Reset();
            var context = New<HubContext>("Context");
            var journal = New<Journal.JournalController>("Journal");
            // EditMode fires no OnEnable — mirror its context/blocker wiring.
            journal.context = context;
            context.AddUiBlocker(() => journal.IsOpen);

            bool extraBlock = false;
            context.AddUiBlocker(() => extraBlock);

            journal.Open();
            Assert.IsTrue(journal.IsOpen);
            Assert.IsTrue(context.UiBlocked, "the open journal blocks hub interaction");

            journal.Close();
            Assert.IsFalse(context.UiBlocked);

            extraBlock = true;
            Assert.IsFalse(journal.OpenAllowed());
            journal.Open();
            Assert.IsFalse(journal.IsOpen);

            extraBlock = false;
            journal.CanOpen = () => false;
            Assert.IsFalse(journal.OpenAllowed());
            journal.Open();
            Assert.IsFalse(journal.IsOpen);
        }

        [Test]
        public void Journal_RefusesToOpenDuringCutscene()
        {
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            core.Session.Reset();
            var context = New<HubContext>("Context");
            var journal = New<Journal.JournalController>("Journal");
            journal.context = context;

            core.Session.SetCutsceneContext("Cutscene.Test", "");

            Assert.IsFalse(journal.OpenAllowed());
            journal.Open();
            Assert.IsFalse(journal.IsOpen);
        }

        [Test]
        public void Journal_SyncsRunicLogsFromSession()
        {
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            core.Session.Reset();
            core.Session.MarkRunicLogById(1);
            var f = TestCases.Build(1, withInterrogation: false);
            var journal = New<Journal.JournalController>("Journal");
            var log = ScriptableObject.CreateInstance<RunicLogData>();
            log.id = 1;
            f.assets.Add(log);
            journal.allLogs = new[] { log };

            journal.SyncLogsFromSession();

            CollectionAssert.Contains(journal.collectedLogs, log);
        }

        [Test]
        public void HubObjective_SwapsToSuspectTextWhenInterrogationUnlocked()
        {
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            core.Session.Reset();
            core.Session.CurrentCase = 1;
            var hud = New<HubHudController>("Hud");
            hud.objectiveText = "Base objective.";
            hud.objectiveCaseId = 1;
            hud.postInvestigationObjective = "Confront the suspect.";

            hud.RefreshObjective();
            Assert.AreEqual("Base objective.", hud.AppliedObjective);

            core.Session.GetOrCreateBoardState(1).BoardSolved = true;
            hud.RefreshObjective();
            Assert.AreEqual("Confront the suspect.", hud.AppliedObjective);
        }

        [Test]
        public void PlayDialogue_BindsEffectsNameAndFlagsOnce()
        {
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            core.Session.Reset();
            core.Session.PlayerName = "Vessel";
            var context = New<HubContext>("Context");
            var journal = New<HubJournal>("JournalStore");
            var flags = New<HubFlags>("Flags");
            var dialogue = New<Dialogue.DialogueController>("Dialogue");
            context.journal = journal;
            context.flags = flags;
            context.dialogue = dialogue;

            var f = TestCases.Build(1, withInterrogation: false);
            var data = ScriptableObject.CreateInstance<Dialogue.DialogueData>();
            f.assets.Add(data);
            data.id = "D_TEST";
            data.startNodeId = "n1";
            data.nodes = new[]
            {
                new Dialogue.DialogueNodeData
                {
                    id = "n1",
                    text = "Hello.",
                    registersClue = f.c1,
                    setsFlag = "Test.Flag",
                },
            };

            Assert.IsTrue(context.PlayDialogue(data));

            Assert.IsTrue(journal.Contains(f.c1));
            Assert.IsTrue(flags.Has("Test.Flag"));
            Assert.IsTrue(core.Session.HasFlag("Test.Flag"));
            Assert.AreEqual("Vessel", dialogue.playerName);
        }

        [Test]
        public void EntryCutscene_DelaysFinaleBoard_ThenOpensItAfterPlayback()
        {
            var data = LoadCase(5);
            Assert.NotNull(data);
            Assert.IsFalse(data.hasInterrogation);
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            core.Session.Reset();
            core.Session.CurrentCase = 5;

            var board = New<WeightedBoardController>("WeightedBoard");
            _flow.weightedBoard = board;
            var player = New<Cutscene.CutscenePlayerController>("CutscenePlayer");
            _flow.cutscenePlayer = player;
            _flow.entryCutscene = BuildWaitForInputCutscene();
            _flow.Bind();

            _flow.BeginCase(data);

            Assert.IsTrue(player.IsPlaying);
            Assert.IsFalse(board.IsOpen);

            _flow.HandleCutsceneEnded();

            Assert.IsTrue(board.IsOpen);
            Assert.IsFalse(_interrogation.IsOpen);
        }

        [Test]
        public void EntryCutscene_LatchesOncePerSave_SecondBeginCaseOpensBoardDirectly()
        {
            var data = LoadCase(5);
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            core.Session.Reset();
            core.Session.CurrentCase = 5;

            var board = New<WeightedBoardController>("WeightedBoard");
            _flow.weightedBoard = board;
            var player = New<Cutscene.CutscenePlayerController>("CutscenePlayer");
            _flow.cutscenePlayer = player;
            _flow.entryCutscene = BuildWaitForInputCutscene();
            _flow.Bind();

            _flow.BeginCase(data);
            Assert.IsTrue(player.IsPlaying);
            _flow.HandleCutsceneEnded();
            Assert.IsTrue(board.IsOpen);

            player.Stop();
            board.Close();
            _flow.BeginCase(data);

            Assert.IsFalse(player.IsPlaying);
            Assert.IsTrue(board.IsOpen);
        }

        [Test]
        public void EntryCutscene_Unassigned_OpensFinaleBoardDirectly()
        {
            var data = LoadCase(5);
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            core.Session.Reset();
            core.Session.CurrentCase = 5;

            var board = New<WeightedBoardController>("WeightedBoard");
            _flow.weightedBoard = board;
            _flow.cutscenePlayer = New<Cutscene.CutscenePlayerController>("CutscenePlayer");
            _flow.entryCutscene = null;
            _flow.Bind();

            _flow.BeginCase(data);

            Assert.IsTrue(board.IsOpen);
        }

        [Test]
        public void AudioCoordinator_PersistsChannelVolumes_AndDucksForCinematics()
        {
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            var audio = core.Audio;
            Assert.NotNull(audio);

            audio.SetMasterVolume(0.5f);
            audio.SetMusicVolume(0.25f);
            audio.SetSfxVolume(2f); // clamps to 1
            Assert.AreEqual(0.5f, audio.MasterVolume);
            Assert.AreEqual(0.25f, audio.MusicVolume);
            Assert.AreEqual(1f, audio.SfxVolume);

            audio.SetDucked(true);
            Assert.IsTrue(audio.IsDucked);
            audio.SetDucked(false);
            Assert.IsFalse(audio.IsDucked);

            audio.PlaySfx(null); // null-safe no-op

            audio.SetMasterVolume(1f);
            audio.SetMusicVolume(1f);
            audio.SetSfxVolume(1f);
        }

        [Test]
        public void SceneAudioLoop_StartsOwnedLoops_AndReleasesThemOnUnload()
        {
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            var audio = core.Audio;
            var music = AudioClip.Create("loopMusic", 44100, 1, 44100, false);
            var amb = AudioClip.Create("loopAmb", 44100, 1, 44100, false);
            _tempAssets.Add(music);
            _tempAssets.Add(amb);

            var loop = New<Core.SceneAudioLoop>("SceneAudio");
            loop.music = music;
            loop.ambience = amb;
            loop.Play();

            Assert.AreSame(music, audio.CurrentMusic);
            Assert.AreSame(amb, audio.CurrentAmbience);

            // OnDestroy doesn't fire for non-ExecuteAlways components in
            // EditMode — invoke it the way a scene unload would.
            typeof(Core.SceneAudioLoop).GetMethod("OnDestroy",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).Invoke(loop, null);

            Assert.IsNull(audio.CurrentMusic);
            Assert.IsNull(audio.CurrentAmbience);
        }

        [Test]
        public void SceneAudioLoop_UnassignedChannels_LeaveExistingMixUntouched()
        {
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            var audio = core.Audio;
            var music = AudioClip.Create("keepMusic", 44100, 1, 44100, false);
            _tempAssets.Add(music);
            audio.PlayMusic(music);

            var loop = New<Core.SceneAudioLoop>("EmptyLoop");
            loop.Play(); // nothing assigned — must not kill the current track

            Assert.AreSame(music, audio.CurrentMusic);
            typeof(Core.SceneAudioLoop).GetMethod("OnDestroy",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).Invoke(loop, null);
            Assert.AreSame(music, audio.CurrentMusic);
        }

        [Test]
        public void CaseZero_InCaseLoop_OpensWeightedBoardDirectly()
        {
            var data = LoadCase(0);
            Assert.NotNull(data);
            Assert.NotNull(data.evidenceGraph);
            var core = Core.CoreRuntime.Ensure();
            _objects.Add(core.gameObject);
            core.Session.Reset();
            core.Session.CurrentCase = 0;

            var board = New<WeightedBoardController>("WeightedBoard");
            _flow.weightedBoard = board;
            _flow.investigationScene = false;
            _flow.BeginCase(data);

            Assert.IsNotNull(board.CanUse);
            Assert.IsTrue(board.CanUse());
            Assert.IsTrue(board.IsOpen);
        }
    }
}

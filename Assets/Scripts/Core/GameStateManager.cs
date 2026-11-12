using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Core
{
    /// <summary>
    /// Scene-flow driver. Lives on the CoreRuntime object (DontDestroyOnLoad);
    /// <see cref="Ensure"/> attaches it lazily so any scene can boot the game
    /// loop without a dedicated Bootstrap scene.
    ///
    /// Responsibilities:
    /// - Owns the <see cref="FlowStateMachine"/> and the data-driven
    ///   <see cref="SceneRouteTable"/> (missing routes fail cleanly).
    /// - Consumes bus events (NewGame, Continue, checkpoint and scene-load
    ///   requests) so panels/interactables never call SceneManager directly
    ///   once wired.
    /// - Writes the JSON checkpoint on every transition through
    ///   <see cref="CurrentSave"/> — the live save systems (journal, flags,
    ///   session) write into before asking for a checkpoint.
    /// </summary>
    public class GameStateManager : MonoBehaviour
    {
        static GameStateManager _instance;

        [Header("Scene routes (key -> scene name)")]
        [Tooltip("Keys: title, opening, caseloop, hub.<caseId>, investigation.<caseId>.")]
        public SceneRouteTable routes = new SceneRouteTable();

        /// <summary>The case-loop FSM; transitions are advisory (logged, never block a load).</summary>
        public FlowStateMachine Fsm { get; } = new FlowStateMachine();

        /// <summary>
        /// The live save record. Systems merge their state into it and the
        /// GSM stamps case/scene/focus before persisting.
        /// </summary>
        public SaveState CurrentSave { get; private set; } = new SaveState();

        public static GameStateManager Instance => _instance;

        /// <summary>
        /// Returns the live GSM, creating CoreRuntime first if needed.
        /// Safe from any Awake/Start in any scene.
        /// </summary>
        public static GameStateManager Ensure()
        {
            if (_instance != null) return _instance;
            var core = CoreRuntime.Ensure();
            _instance = core.GetComponent<GameStateManager>();
            if (_instance == null)
                _instance = core.gameObject.AddComponent<GameStateManager>();
            // Awake normally wires this; editor-driven Ensure calls (tests,
            // batch tooling) never get an Awake.
            if (_instance._core == null) _instance._core = core;
            return _instance;
        }

        CoreRuntime _core;
        bool _runStateLoaded;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;
            _core = GetComponent<CoreRuntime>() ?? CoreRuntime.Ensure();
            SeedDefaultRoutes();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void OnEnable() => Subscribe();
        void OnDisable() => Unsubscribe();

        void Subscribe()
        {
            _core.Bus.Subscribe<NewGameEvent>(OnNewGame);
            _core.Bus.Subscribe<ContinueGameEvent>(OnContinue);
            _core.Bus.Subscribe<CheckpointRequestedEvent>(OnCheckpointRequested);
            _core.Bus.Subscribe<SceneLoadRequestedEvent>(OnSceneLoadRequested);
            _core.Bus.Subscribe<CaseCompletedEvent>(OnCaseCompleted);
            _core.Bus.Subscribe<MistrialRetryEvent>(OnMistrialRetry);
        }

        void Unsubscribe()
        {
            _core.Bus.Unsubscribe<NewGameEvent>(OnNewGame);
            _core.Bus.Unsubscribe<ContinueGameEvent>(OnContinue);
            _core.Bus.Unsubscribe<CheckpointRequestedEvent>(OnCheckpointRequested);
            _core.Bus.Unsubscribe<SceneLoadRequestedEvent>(OnSceneLoadRequested);
            _core.Bus.Unsubscribe<CaseCompletedEvent>(OnCaseCompleted);
            _core.Bus.Unsubscribe<MistrialRetryEvent>(OnMistrialRetry);
        }

        // --- Public entry points (also wired to bus events) -------------------

        /// <summary>Fresh run: resets the save, restores Focus, goes to the opening.</summary>
        public void NewGame(string playerName)
        {
            _core.Session.Reset();
            _core.Session.PlayerName = playerName;
            _core.Session.UnlockTool(Morsoul.Data.Tool.GolemEye);
            CurrentSave = new SaveState { playerName = playerName };
            _core.Focus.Restore(FocusTuning.FocusMax, false);
            _runStateLoaded = true;
            WriteCheckpoint("NewGame", null);
            if (_core.InvestigationEntryHandler != null)
            {
                var entry = _core.EnterInvestigation(RouteKeys.Opening);
                if (!entry.Success) Debug.LogWarning(entry.Message, this);
                return;
            }
            GoToRoute(RouteKeys.Opening, GameFlowState.Opening,
                fallbackKey: RouteKeys.Hub(0), fallbackState: GameFlowState.Hub);
        }

        /// <summary>Resume from the sealed checkpoint; false when none is readable.</summary>
        public bool ContinueGame()
        {
            if (!_core.Save.TryLoad(out var state, out string error))
            {
                Debug.LogWarning($"GameStateManager: continue failed ({error}).", this);
                return false;
            }
            CurrentSave = state;
            _runStateLoaded = true;
            _core.Session.RestoreFrom(state, _core.ResolveClue);
            if (_core.Session.ResumeInterruptedElizaIntro())
                state.checkpointSceneName = SessionState.PrologueWildernessSceneId;
            int resumedCase = state.currentSceneMode == SceneMode.Mode3D && state.investigationCaseId >= 0
                ? state.investigationCaseId : state.currentCase;
            bool strained = resumedCase >= 1 && resumedCase <= 4 &&
                            state.strainedScanUsedByCase != null &&
                            state.strainedScanUsedByCase.Count >= resumedCase &&
                            state.strainedScanUsedByCase[resumedCase - 1];
            _core.Focus.Restore(state.focusEnergy, strained,
                maximum: state.focusMax > 0f ? state.focusMax : (float?)null,
                restRemainderSeconds: state.restElapsedRemainderSeconds,
                closedAtUtc: ParseUtc(state.closedAtUtc),
                lastTrustedUtc: ParseUtc(state.lastTrustedUtc),
                lastOfflineRecoveryAppliedAtUtc: ParseUtc(state.lastOfflineRecoveryAppliedAtUtc));
            _core.Focus.ApplyOfflineRecovery(DateTime.UtcNow);
            // Consume the close marker and persist the trusted watermark even
            // if scene resolution later falls back or fails.
            PersistCurrentSave("Continue");
            string checkpointScene = CurrentSave.checkpointSceneName;
            string scene = ResolveContinueScene(checkpointScene, state.currentCase,
                routes, Application.CanStreamedLevelBeLoaded);
            if (string.IsNullOrEmpty(scene))
            {
                Debug.LogWarning("GameStateManager: save has no resumable scene.", this);
                return false;
            }
            if (scene != checkpointScene)
            {
                _core.Session.SetSceneContext(SceneMode.Mode2D, scene);
                _core.Session.InvestigationCaseId = -1;
                _core.Session.InvestigationReturn = null;
                _core.Focus.SetStrainedScanUsed(_core.Session.IsStrainedScanUsed(state.currentCase));
                if (!string.IsNullOrEmpty(checkpointScene))
                    Debug.LogWarning($"GameStateManager: checkpointed scene '{checkpointScene}' not loadable; using '{scene}' instead.", this);
                else
                    Debug.LogWarning($"GameStateManager: save has no checkpoint scene; using '{scene}'.", this);
            }
            return LoadSceneInternal(scene,
                preserveRestoredPose: scene == checkpointScene);
        }

        public bool EnterInvestigationRoute(string routeKey, int caseId)
        {
            if (caseId < 0 || caseId > 4) return false;
            string scene = routes.Resolve(routeKey);
            if (string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene)) return false;
            var session = _core.Session;
            var mode = session.CurrentSceneMode;
            string origin = session.CurrentSceneId;
            var position = session.PlayerPosition;
            var facing = session.PlayerFacing2D;
            bool hadPosition = session.HasPlayerPosition;
            session.CaptureInvestigationReturn();
            int previousCase = session.InvestigationCaseId;
            session.InvestigationCaseId = caseId;
            bool previousStrained = _core.Focus.StrainedScanUsed;
            _core.Focus.SetStrainedScanUsed(session.IsStrainedScanUsed(caseId));
            bool loaded = false;
            try
            {
                session.SetSceneContext(SceneMode.Mode3D, scene);
                loaded = GoToRoute(routeKey, GameFlowState.Investigation);
                return loaded;
            }
            finally
            {
                if (!loaded)
                {
                    session.InvestigationCaseId = previousCase;
                    _core.Focus.SetStrainedScanUsed(previousStrained);
                    if (hadPosition) session.SetPlayerContext(mode, origin, position, facing);
                    else session.SetSceneContext(mode, origin);
                }
            }
        }

        public bool ReturnFromInvestigation(int caseId)
        {
            var session = _core.Session;
            var entry = session.InvestigationReturn;
            bool restore = entry != null && entry.position != null &&
                           !string.IsNullOrEmpty(entry.sceneName) && Application.CanStreamedLevelBeLoaded(entry.sceneName);
            string scene = restore ? entry.sceneName : routes.Resolve(RouteKeys.Hub(caseId));
            if (string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene)) return false;
            int previousCase = session.InvestigationCaseId;
            bool previousStrained = _core.Focus.StrainedScanUsed;
            if (previousStrained && previousCase >= 1 && previousCase <= 4)
                session.MarkStrainedScan(previousCase);
            var previousMode = session.CurrentSceneMode;
            string previousScene = session.CurrentSceneId;
            var previousPosition = session.PlayerPosition;
            var previousFacing = session.PlayerFacing2D;
            bool hadPosition = session.HasPlayerPosition;
            bool loaded = false;
            try
            {
                if (restore)
                    session.SetPlayerContext(SceneMode.Mode2D, scene,
                        new Vector3(entry.position.x, entry.position.y, entry.position.z), entry.facing);
                else session.SetSceneContext(SceneMode.Mode2D, scene);
                session.InvestigationReturn = null;
                session.InvestigationCaseId = -1;
                _core.Focus.SetStrainedScanUsed(session.IsStrainedScanUsed(session.CurrentCase));
                loaded = LoadSceneInternal(scene, preserveRestoredPose: restore);
                return loaded;
            }
            finally
            {
                if (!loaded)
                {
                    session.InvestigationReturn = entry;
                    session.InvestigationCaseId = previousCase;
                    _core.Focus.SetStrainedScanUsed(previousStrained);
                    if (hadPosition) session.SetPlayerContext(previousMode, previousScene, previousPosition, previousFacing);
                    else session.SetSceneContext(previousMode, previousScene);
                }
            }
        }

        internal static string ResolveContinueScene(string checkpointScene,
            int currentCase, SceneRouteTable routeTable, Func<string, bool> canLoad)
        {
            bool CanLoad(string scene) =>
                !string.IsNullOrEmpty(scene) && (canLoad == null || canLoad(scene));
            if (CanLoad(checkpointScene)) return checkpointScene;

            string hub = routeTable != null
                ? routeTable.Resolve(RouteKeys.Hub(currentCase)) : null;
            if (CanLoad(hub)) return hub;

            string title = routeTable != null
                ? routeTable.Resolve(RouteKeys.Title) : null;
            return CanLoad(title) ? title : null;
        }

        /// <summary>
        /// Loads the scene registered for a route key. False when the key is
        /// unregistered OR its scene cannot load, so caller fallbacks engage.
        /// </summary>
        public bool GoToRoute(string key, GameFlowState stateOnSuccess)
        {
            string scene = routes.Resolve(key);
            if (scene == null)
            {
                Debug.LogWarning($"GameStateManager: no route for '{key}'.", this);
                return false;
            }
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogWarning($"GameStateManager: route '{key}' -> '{scene}' not loadable (missing or not in Build Profiles).", this);
                return false;
            }
            Advance(stateOnSuccess);
            return LoadSceneInternal(scene);
        }

        /// <summary>Route with fallback when the primary key isn't registered.</summary>
        public bool GoToRoute(string key, GameFlowState state,
            string fallbackKey, GameFlowState fallbackState)
        {
            return GoToRoute(key, state) || GoToRoute(fallbackKey, fallbackState);
        }

        /// <summary>Writes the checkpoint, stamping trusted time, focus and scene.</summary>
        public void WriteCheckpoint(string reason, string nextSceneName,
            bool preserveRestoredPose = false)
        {
            StampRuntimeState(markClosed: false, nextSceneName,
                updateCheckpointScene: true, preserveRestoredPose);
            _core.Save.Save(CurrentSave);
            Debug.Log($"GameStateManager: checkpoint written ({reason}) -> {CurrentSave.checkpointSceneName}", this);
        }

        /// <summary>Writes a graceful-close checkpoint with a sealed close timestamp.</summary>
        public void WriteCloseCheckpoint(string reason)
        {
            StampRuntimeState(markClosed: true, nextSceneName: null,
                updateCheckpointScene: true, preserveRestoredPose: true);
            _core.Save.Save(CurrentSave);
            Debug.Log($"GameStateManager: close checkpoint written ({reason}) -> {CurrentSave.checkpointSceneName}", this);
        }

        void OnApplicationQuit()
        {
            if (_core != null && _runStateLoaded && _core.Save.HasSave)
                WriteCloseCheckpoint("Quit");
        }

        /// <summary>Persists runtime state without changing the resume scene.</summary>
        void PersistCurrentSave(string reason)
        {
            StampRuntimeState(markClosed: false, nextSceneName: null,
                updateCheckpointScene: false, preserveRestoredPose: true);
            _core.Save.Save(CurrentSave);
            Debug.Log($"GameStateManager: save state stamped ({reason}).", this);
        }

        void StampRuntimeState(bool markClosed, string nextSceneName,
            bool updateCheckpointScene, bool preserveRestoredPose)
        {
            if (updateCheckpointScene && !preserveRestoredPose &&
                !string.IsNullOrEmpty(nextSceneName) &&
                nextSceneName != SceneManager.GetActiveScene().name)
            {
                // A travel checkpoint resumes at the destination's entry point;
                // the loaded scene stamps its real spawn pose on entry.
                _core.Session.SetSceneContext(
                    _core.Session.CurrentSceneMode, nextSceneName);
            }
            int focusCase = _core.Session.CurrentSceneMode == SceneMode.Mode3D &&
                            _core.Session.InvestigationCaseId >= 0
                ? _core.Session.InvestigationCaseId : _core.Session.CurrentCase;
            if (_core.Focus.StrainedScanUsed && focusCase >= 1 && focusCase <= 4)
                _core.Session.MarkStrainedScan(focusCase);
            _core.Session.ApplyTo(CurrentSave);
            if (markClosed) _core.Focus.MarkClosed(DateTime.UtcNow);
            else _core.Focus.StampTrustedNow(DateTime.UtcNow);

            CurrentSave.focusEnergy = _core.Focus.Percent;
            CurrentSave.focusMax = _core.Focus.Maximum;
            CurrentSave.restElapsedRemainderSeconds = _core.Focus.RestRemainderSeconds;
            CurrentSave.closedAtUtc = FormatUtc(_core.Focus.ClosedAtUtc);
            CurrentSave.lastTrustedUtc = FormatUtc(_core.Focus.LastTrustedUtc);
            CurrentSave.lastOfflineRecoveryAppliedAtUtc =
                FormatUtc(_core.Focus.LastOfflineRecoveryAppliedAtUtc);
            if (updateCheckpointScene)
            {
                CurrentSave.checkpointSceneName = !string.IsNullOrEmpty(nextSceneName)
                    ? nextSceneName
                    : SceneManager.GetActiveScene().name;
            }
            if (string.IsNullOrEmpty(CurrentSave.currentSceneId))
                CurrentSave.currentSceneId = CurrentSave.checkpointSceneName;
        }

        /// <summary>Records an FSM transition; illegal jumps warn but never block.</summary>
        public void Advance(GameFlowState next)
        {
            if (!Fsm.TryTransition(next))
                Debug.LogWarning($"GameStateManager: illegal transition {Fsm.Current} -> {next} (allowed anyway).", this);
        }

        static string FormatUtc(DateTime? utc) =>
            utc.HasValue ? utc.Value.ToString("o") : "";

        static DateTime? ParseUtc(string iso) =>
            !string.IsNullOrEmpty(iso) &&
            DateTime.TryParse(iso, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
                ? dt
                : (DateTime?)null;

        /// <summary>
        /// Adds built-in routes for scenes that exist today; only fills keys
        /// that are absent, so a Bootstrap scene's inspector table always wins.
        /// Remap as the 3D investigation scenes land.
        /// </summary>
        internal void SeedDefaultRoutes()
        {
            void Seed(string key, string sceneName)
            {
                if (!routes.Contains(key)) routes.Set(key, sceneName);
            }
            Seed(RouteKeys.Title, "Title");
            Seed(RouteKeys.Opening, "PrologueRuins"); // Integration catalog guards the 3D opening
            Seed(RouteKeys.PrologueWilderness, SessionState.PrologueWildernessSceneId);
            Seed(RouteKeys.CaseLoop, "CaseLoop");
            // Investigation routes are registered only when their real levels are delivered.
            Seed(RouteKeys.Hub(0), "Hub_LowerMarket");   // tutorial case shares the market hub
            Seed(RouteKeys.Hub(1), "Hub_LowerMarket");
            Seed(RouteKeys.Hub(2), "Hub_CathedralCourtyard"); // Cathedral of Saint Althea (Exterior Drop-off)
            Seed(RouteKeys.Hub(3), "Hub_Wilderness");    // Whisperwood hub
            Seed(RouteKeys.Hub(4), "Hub_IronHollow");    // Iron Hollow railhead
        }

        // --- Bus handlers -----------------------------------------------------

        void OnNewGame(NewGameEvent e) => NewGame(e.PlayerName);
        void OnContinue(ContinueGameEvent _) => ContinueGame();
        void OnCheckpointRequested(CheckpointRequestedEvent e)
        {
            string next = e.NextSceneName;
            if (!string.IsNullOrEmpty(next) && !Application.CanStreamedLevelBeLoaded(next))
            {
                // Destination can't load (missing scene / not in Build
                // Profiles) — stamp the checkpoint at the current scene so a
                // later Continue never strands on an unloadable scene.
                Debug.LogWarning($"GameStateManager: checkpoint target '{next}' not loadable; stamping current scene instead.", this);
                next = null;
            }
            WriteCheckpoint(e.Reason, next);
        }
        void OnSceneLoadRequested(SceneLoadRequestedEvent e)
        {
            if (!string.IsNullOrEmpty(e.SceneName)) LoadSceneInternal(e.SceneName);
        }

        internal void OnCaseCompleted(CaseCompletedEvent e)
        {
            if (e.Ending != 0)
            {
                // Game-ending route: resolution dismiss -> ending -> title.
                Advance(GameFlowState.Ending);
                GoToRoute(RouteKeys.Title, GameFlowState.Title);
                return;
            }
            // Normal advance: the session case moves on whether or not the
            // next hub exists yet — the case door always opens the loop for
            // Session.CurrentCase. Route chain: next hub -> current hub ->
            // title, so an unregistered route never strands the run.
            _core.Session.CurrentCase = e.CaseId + 1;
            var nextCase = _core.ResolveCase(_core.Session.CurrentCase);
            if (nextCase != null && nextCase.toolUnlocked != Morsoul.Data.Tool.None)
                _core.Session.UnlockTool(nextCase.toolUnlocked);
            if (GoToRoute(RouteKeys.Hub(e.CaseId + 1), GameFlowState.Hub)) return;
            if (GoToRoute(RouteKeys.Hub(e.CaseId), GameFlowState.Hub)) return;
            GoToRoute(RouteKeys.Title, GameFlowState.Title);
        }

        void OnMistrialRetry(MistrialRetryEvent _)
        {
            // "Retry from Checkpoint": reload the Zone B entry checkpoint —
            // composure and strikes reset with the scene.
            string scene = CurrentSave.checkpointSceneName;
            if (string.IsNullOrEmpty(scene)) scene = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(scene))
            {
                Debug.LogWarning("GameStateManager: mistrial retry but no scene to reload.", this);
                return;
            }
            LoadSceneInternal(scene);
        }

        // --- Internals --------------------------------------------------------

        /// <summary>
        /// Loads the scene through the guarded path, stamping the checkpoint
        /// with the destination only once loadability is confirmed; false when
        /// it can't load so caller fallbacks engage.
        /// </summary>
        bool LoadSceneInternal(string sceneName, bool preserveRestoredPose = false)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                // Scene doesn't exist or isn't in Build Profiles — keep the
                // checkpoint pointed at the CURRENT scene so Continue doesn't
                // strand the player on an unloadable scene.
                Debug.LogWarning($"GameStateManager: scene '{sceneName}' is not loadable (missing or not in Build Profiles).", this);
                return false;
            }
            WriteCheckpoint("SceneTransition", sceneName, preserveRestoredPose);
            SceneManager.LoadScene(sceneName);
            return true;
        }
    }
}

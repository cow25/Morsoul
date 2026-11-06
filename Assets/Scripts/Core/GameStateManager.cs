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
            CurrentSave = new SaveState { playerName = playerName };
            _core.Focus.Restore(FocusTuning.FocusMax, false);
            WriteCheckpoint("NewGame", null);
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
            _core.Session.RestoreFrom(state, _core.ResolveClue);
            _core.Focus.Restore(state.focusEnergy, state.strainedScanUsed,
                maximum: state.focusMax > 0f ? state.focusMax : (float?)null,
                restRemainderSeconds: state.restElapsedRemainderSeconds,
                closedAtUtc: ParseUtc(state.closedAtUtc),
                lastOfflineRecoveryAppliedAtUtc: ParseUtc(state.lastOfflineRecoveryAppliedAtUtc));
            _core.Focus.ApplyOfflineRecovery(DateTime.UtcNow);
            string scene = state.checkpointSceneName;
            if (string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene))
            {
                // Checkpointed scene missing (e.g. a seam gate to a scene that
                // hasn't landed yet) — resume at the case's hub instead.
                if (!string.IsNullOrEmpty(scene))
                    Debug.LogWarning($"GameStateManager: checkpointed scene '{scene}' not loadable; falling back to case hub.", this);
                scene = routes.Resolve(RouteKeys.Hub(state.currentCase));
            }
            if (string.IsNullOrEmpty(scene))
            {
                Debug.LogWarning("GameStateManager: save has no resumable scene.", this);
                return false;
            }
            LoadSceneInternal(scene);
            return true;
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

        /// <summary>Writes the checkpoint, stamping focus + scene.</summary>
        public void WriteCheckpoint(string reason, string nextSceneName)
        {
            _core.Session.ApplyTo(CurrentSave);
            CurrentSave.focusEnergy = _core.Focus.Percent;
            CurrentSave.focusMax = _core.Focus.Maximum;
            CurrentSave.restElapsedRemainderSeconds = _core.Focus.RestRemainderSeconds;
            _core.Focus.MarkClosed(DateTime.UtcNow);
            CurrentSave.closedAtUtc = FormatUtc(_core.Focus.ClosedAtUtc);
            CurrentSave.lastOfflineRecoveryAppliedAtUtc =
                FormatUtc(_core.Focus.LastOfflineRecoveryAppliedAtUtc);
            CurrentSave.strainedScanUsed = _core.Focus.StrainedScanUsed;
            CurrentSave.checkpointSceneName = !string.IsNullOrEmpty(nextSceneName)
                ? nextSceneName
                : SceneManager.GetActiveScene().name;
            _core.Save.Save(CurrentSave);
            Debug.Log($"GameStateManager: checkpoint written ({reason}) -> {CurrentSave.checkpointSceneName}", this);
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
            Seed(RouteKeys.Opening, "Prologue_2D"); // 2D stand-in until PrologueRuins ships
            Seed(RouteKeys.CaseLoop, "CaseLoop");
            Seed(RouteKeys.Investigation(1), "Investigation_01_Placeholder"); // vault stand-in
            Seed(RouteKeys.Hub(0), "Hub_LowerMarket");   // tutorial case shares the market hub
            Seed(RouteKeys.Hub(1), "Hub_LowerMarket");
            Seed(RouteKeys.Hub(3), "Hub_Wilderness");    // Whisperwood hub
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
        bool LoadSceneInternal(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                // Scene doesn't exist or isn't in Build Profiles — keep the
                // checkpoint pointed at the CURRENT scene so Continue doesn't
                // strand the player on an unloadable scene.
                Debug.LogWarning($"GameStateManager: scene '{sceneName}' is not loadable (missing or not in Build Profiles).", this);
                return false;
            }
            WriteCheckpoint("SceneTransition", sceneName);
            SceneManager.LoadScene(sceneName);
            return true;
        }
    }
}

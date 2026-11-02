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
        [Tooltip("Keys: title, opening, hub.<caseId>, investigation.<caseId>.")]
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
            return _instance;
        }

        CoreRuntime _core;
        string _pendingScene;

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
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
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
        }

        void Unsubscribe()
        {
            _core.Bus.Unsubscribe<NewGameEvent>(OnNewGame);
            _core.Bus.Unsubscribe<ContinueGameEvent>(OnContinue);
            _core.Bus.Unsubscribe<CheckpointRequestedEvent>(OnCheckpointRequested);
            _core.Bus.Unsubscribe<SceneLoadRequestedEvent>(OnSceneLoadRequested);
        }

        // --- Public entry points (also wired to bus events) -------------------

        /// <summary>Fresh run: resets the save, restores Focus, goes to the opening.</summary>
        public void NewGame(string playerName)
        {
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
            _core.Focus.Restore(state.focusPercent, state.strainedScanUsed);
            string scene = !string.IsNullOrEmpty(state.checkpointSceneName)
                ? state.checkpointSceneName
                : routes.Resolve(RouteKeys.Hub(state.currentCase));
            if (string.IsNullOrEmpty(scene))
            {
                Debug.LogWarning("GameStateManager: save has no resumable scene.", this);
                return false;
            }
            LoadSceneInternal(scene);
            return true;
        }

        /// <summary>Loads the scene registered for a route key; false when unregistered.</summary>
        public bool GoToRoute(string key, GameFlowState stateOnSuccess)
        {
            string scene = routes.Resolve(key);
            if (scene == null)
            {
                Debug.LogWarning($"GameStateManager: no route for '{key}'.", this);
                return false;
            }
            Advance(stateOnSuccess);
            LoadSceneInternal(scene);
            return true;
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
            CurrentSave.focusPercent = _core.Focus.Percent;
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

        /// <summary>
        /// Adds built-in routes for scenes that exist today; only fills keys
        /// that are absent, so a Bootstrap scene's inspector table always wins.
        /// Remap as the 3D investigation scenes land.
        /// </summary>
        void SeedDefaultRoutes()
        {
            void Seed(string key, string sceneName)
            {
                if (!routes.Contains(key)) routes.Set(key, sceneName);
            }
            Seed(RouteKeys.Title, "Title");
            Seed(RouteKeys.Hub(0), "Hub_LowerMarket");   // tutorial case shares the market hub
            Seed(RouteKeys.Hub(1), "Hub_LowerMarket");
            Seed(RouteKeys.Hub(3), "Hub_Wilderness");    // Whisperwood hub
        }

        // --- Bus handlers -----------------------------------------------------

        void OnNewGame(NewGameEvent e) => NewGame(e.PlayerName);
        void OnContinue(ContinueGameEvent _) => ContinueGame();
        void OnCheckpointRequested(CheckpointRequestedEvent e)
            => WriteCheckpoint(e.Reason, e.NextSceneName);
        void OnSceneLoadRequested(SceneLoadRequestedEvent e)
        {
            if (!string.IsNullOrEmpty(e.SceneName)) LoadSceneInternal(e.SceneName);
        }

        // --- Internals --------------------------------------------------------

        void LoadSceneInternal(string sceneName)
        {
            _pendingScene = sceneName;
            WriteCheckpoint("SceneTransition", sceneName);
            SceneManager.LoadScene(sceneName);
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _pendingScene = null;
        }
    }
}

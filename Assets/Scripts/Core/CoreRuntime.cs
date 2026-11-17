using UnityEngine;

namespace Morsoul.Core
{
    /// <summary>
    /// Persistent runtime owner of the shared Core services. Created once by
    /// the Bootstrap scene (or lazily via <see cref="Ensure"/>) and kept alive
    /// across scene loads. Holds the global <see cref="IEventBus"/>; save,
    /// focus and session services attach here as they land.
    /// </summary>
    public class CoreRuntime : MonoBehaviour
    {
        static CoreRuntime _instance;

        /// <summary>The live CoreRuntime, or null if the game hasn't booted.</summary>
        public static CoreRuntime Instance => _instance;

        /// <summary>The shared event bus.</summary>
        public IEventBus Bus { get; } = new EventBus();

        /// <summary>Checkpoint read/write against persistentDataPath.</summary>
        public SaveService Save => SaveService.Persistent;

        /// <summary>
        /// Persistent Focus bar — lives here so it survives scene loads
        /// without auto-refill (GDD §6.6). Scan controllers call TickScan,
        /// rest spots call TickRest, the board calls the cost methods.
        /// </summary>
        public FocusModel Focus { get; } = new FocusModel();

        /// <summary>
        /// Live cross-scene session truth (clues, flags, board edges, logs,
        /// grades, tools). Scene-scoped stores sync against this; the GSM
        /// flattens it into the checkpoint on every write.
        /// </summary>
        public SessionState Session { get; } = new SessionState();

        /// <summary>
        /// Shared audio coordinator — auto-attached in Awake alongside the
        /// GSM; the accessor lazily attaches too because EditMode callers can
        /// reach this before Awake runs.
        /// </summary>
        public AudioCoordinator Audio
        {
            get
            {
                var audio = GetComponent<AudioCoordinator>();
                return audio != null ? audio : gameObject.AddComponent<AudioCoordinator>();
            }
        }

        public bool DevelopmentInvestigationFallbackEnabled { get; set; }
        public bool AutoCompleteCasesEnabled { get; set; }
        public bool ZeroScanFocusCost { get; set; }
        public string InvestigationStatusMessage { get; set; } = "";
        public System.Func<string, InvestigationEntryResult> InvestigationEntryHandler { get; set; }
        public System.Func<int, bool> InterrogationEntryHandler { get; set; }

        public bool EnterInterrogation(int caseId) => Session.CanStartInterrogation(caseId) &&
            Session.CurrentSceneMode == SceneMode.Mode2D && InterrogationEntryHandler != null &&
            InterrogationEntryHandler(caseId);

        public InvestigationEntryResult EnterInvestigation(string routeKey)
        {
            var result = InvestigationEntryHandler != null
                ? InvestigationEntryHandler(routeKey)
                : new InvestigationEntryResult { Message = "Investigation entry adapter is unavailable." };
            InvestigationStatusMessage = result.Message ?? "";
            return result;
        }

        Morsoul.Data.ClueRegistry _clueRegistry;
        bool _clueRegistryLoaded;

        /// <summary>
        /// Resolves a saved clue id back to its ClueData asset via the
        /// generated Resources/ClueRegistry index; null when unregistered.
        /// </summary>
        public Morsoul.Data.ClueData ResolveClue(string clueId)
        {
            if (!_clueRegistryLoaded)
            {
                _clueRegistry = Resources.Load<Morsoul.Data.ClueRegistry>("ClueRegistry");
                _clueRegistryLoaded = true;
            }
            return _clueRegistry != null ? _clueRegistry.Find(clueId) : null;
        }

        Morsoul.Data.CaseDatabase _caseDatabase;
        bool _caseDatabaseLoaded;

        /// <summary>
        /// Resolves a case id to its CaseData asset via the generated
        /// Resources/CaseDatabase index; null when unregistered. The case
        /// loop uses this to play Session.CurrentCase.
        /// </summary>
        public Morsoul.Data.CaseData ResolveCase(int caseId)
        {
            if (!_caseDatabaseLoaded)
            {
                _caseDatabase = Resources.Load<Morsoul.Data.CaseDatabase>("CaseDatabase");
                _caseDatabaseLoaded = true;
            }
            return _caseDatabase != null ? _caseDatabase.Find(caseId) : null;
        }

        /// <summary>
        /// Returns the existing instance, or creates a DontDestroyOnLoad
        /// CoreRuntime GameObject. Safe to call from any scene's Awake.
        /// </summary>
        public static CoreRuntime Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("CoreRuntime");
            _instance = go.AddComponent<CoreRuntime>();
            if (Application.isPlaying) DontDestroyOnLoad(go);
            return _instance;
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            // Auto-attach the scene-flow driver so CoreRuntime.Ensure()
            // always yields the full stack (bus + save + focus + session +
            // GSM + audio) for any publisher that boots Core lazily.
            if (GetComponent<GameStateManager>() == null)
                gameObject.AddComponent<GameStateManager>();
            if (GetComponent<AudioCoordinator>() == null)
                gameObject.AddComponent<AudioCoordinator>();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}

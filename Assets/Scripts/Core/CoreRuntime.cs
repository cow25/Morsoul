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
        /// Returns the existing instance, or creates a DontDestroyOnLoad
        /// CoreRuntime GameObject. Safe to call from any scene's Awake.
        /// </summary>
        public static CoreRuntime Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("CoreRuntime");
            _instance = go.AddComponent<CoreRuntime>();
            DontDestroyOnLoad(go);
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
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}

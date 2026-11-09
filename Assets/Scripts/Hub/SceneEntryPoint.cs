using Morsoul.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Hub
{
    /// <summary>
    /// Places the player at the scene's spawn point on Start, or at the saved
    /// pose when this scene is the checkpoint being resumed. Standalone
    /// replacement for the 3D PlayerSpawner: finds the GameObject tagged with
    /// spawnPointTag and moves the player transform to it. When several entry
    /// points exist, give each a distinct tag and set spawnPointTag accordingly
    /// (scene factory defaults to "PlayerSpawn").
    /// </summary>
    public class SceneEntryPoint : MonoBehaviour
    {
        /// <summary>Tag of the GameObject marking the spawn position.</summary>
        public string spawnPointTag = "PlayerSpawn";
        /// <summary>Transform moved to the spawn point; falls back to the "Player" tag.</summary>
        public Transform player;

        void Start()
        {
            var target = player;
            if (target == null)
            {
                var playerGo = GameObject.FindWithTag("Player");
                target = playerGo != null ? playerGo.transform : null;
            }
            if (target == null)
            {
                Debug.LogWarning("SceneEntryPoint: no player transform assigned or tagged 'Player'.", this);
                return;
            }

            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            var save = GameStateManager.Instance != null
                ? GameStateManager.Instance.CurrentSave
                : null;
            string sceneName = SceneManager.GetActiveScene().name;

            // A gate that named a destination spawn (entryId) wins over both
            // resume and default spawn — doors deposit you at their doorway.
            if (session != null && !string.IsNullOrEmpty(session.PendingEntryPointId))
            {
                string pending = session.PendingEntryPointId;
                session.PendingEntryPointId = "";
                foreach (var marker in FindObjectsByType<SceneSpawnMarker>(
                    FindObjectsInactive.Include))
                {
                    if (marker == null || marker.entryId != pending) continue;
                    target.position = marker.transform.position;
                    session.SetPlayerContext(SceneMode.Mode2D, sceneName,
                        target.position, session.PlayerFacing2D);
                    return;
                }
                Debug.LogWarning($"SceneEntryPoint: no SceneSpawnMarker for " +
                                 $"entry '{pending}'; falling back.", this);
            }

            bool isResumeScene = session != null && session.HasPlayerPosition &&
                ((save != null && save.checkpointSceneName == sceneName) ||
                 session.CurrentSceneId == sceneName);
            if (isResumeScene)
            {
                target.position = session.PlayerPosition;
                session.SetPlayerContext(SceneMode.Mode2D, sceneName,
                    target.position, session.PlayerFacing2D);
                return;
            }

            var spawn = GameObject.FindWithTag(spawnPointTag);
            if (spawn == null)
            {
                Debug.LogWarning($"SceneEntryPoint: no GameObject tagged '{spawnPointTag}'.", this);
                return;
            }
            target.position = spawn.transform.position;
            session?.SetPlayerContext(SceneMode.Mode2D, sceneName,
                target.position, session.PlayerFacing2D);
        }
    }
}

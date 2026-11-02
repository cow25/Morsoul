using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Places the player at the scene's spawn point on Start. Standalone
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
            var spawn = GameObject.FindWithTag(spawnPointTag);
            if (spawn == null)
            {
                Debug.LogWarning($"SceneEntryPoint: no GameObject tagged '{spawnPointTag}'.", this);
                return;
            }
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
            target.position = spawn.transform.position;
        }
    }
}

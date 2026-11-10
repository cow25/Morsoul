using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Named spawn marker resolved by <see cref="SceneEntryPoint"/> when an
    /// exit carried a matching <see cref="GateInteractable.entryId"/> — e.g.
    /// leaving the Guild places the player at the market's guild-door marker
    /// instead of the scene's default spawn.
    /// </summary>
    public class SceneSpawnMarker : MonoBehaviour
    {
        /// <summary>Matches the exit's entryId.</summary>
        public string entryId;
    }
}

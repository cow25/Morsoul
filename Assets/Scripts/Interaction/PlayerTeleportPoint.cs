using UnityEngine;

namespace Morsoul.Interaction
{
    /// <summary>
    /// Moves the player safely across vertical obstacles (e.g., Case 2 maintenance ladder)
    /// while temporarily disabling CharacterController so the transform update sticks.
    /// </summary>
    public class PlayerTeleportPoint : MonoBehaviour
    {
        public Transform destination;

        public void TeleportPlayer()
        {
            if (destination == null) return;
            var player = GameObject.FindWithTag("Player");
            if (player == null) return;

            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = destination.position;
            player.transform.rotation = destination.rotation;
            if (cc != null) cc.enabled = true;
        }
    }
}


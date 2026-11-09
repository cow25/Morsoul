using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Intra-scene teleport (stairs/ladders between stacked floor regions).
    /// Interacting snaps the player to the destination transform — the usual
    /// RPG stair transition without a scene load.
    /// </summary>
    public class StairInteractable : Interactable2D
    {
        /// <summary>Where the player reappears.</summary>
        public Transform destination;

        void Reset()
        {
            promptText = "[E] Stairs";
        }

        public override void Interact(HubContext ctx)
        {
            base.Interact(ctx);
            if (destination == null)
            {
                Debug.LogWarning($"StairInteractable '{name}': no destination wired.", this);
                return;
            }
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            var pos = destination.position;
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.position = pos;
                rb.linearVelocity = Vector2.zero;
            }
            player.transform.position = pos;
        }
    }
}

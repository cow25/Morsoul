using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Hub
{
    /// <summary>
    /// Ungated scene transition interactable (ferryman, carriage, trail exit).
    /// Always requests a checkpoint and loads targetScene on interact.
    /// </summary>
    public class CarrierInteractable : Interactable2D
    {
        /// <summary>Scene loaded on interact.</summary>
        public string targetScene;

        /// <summary>Fired just before scene load; argument is the checkpoint reason.</summary>
        public event Action<string> CheckpointRequested;

        void Reset()
        {
            promptText = "[E] Travel";
        }

        public override void Interact(HubContext ctx)
        {
            base.Interact(ctx);
            CheckpointRequested?.Invoke("Carrier");
            if (string.IsNullOrEmpty(targetScene))
            {
                Debug.LogWarning($"CarrierInteractable '{name}': targetScene is empty.", this);
                return;
            }
            SceneManager.LoadScene(targetScene);
        }
    }
}

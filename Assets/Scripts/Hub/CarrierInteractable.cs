using System;
using Morsoul.Core;
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
        /// <summary>Reason stamped on the travel checkpoint ("Carrier", "CaseDoor", ...).</summary>
        public string checkpointReason = "Carrier";

        /// <summary>Fired just before scene load; argument is the checkpoint reason.</summary>
        public event Action<string> CheckpointRequested;

        void Reset()
        {
            promptText = "[E] Travel";
        }

        public override void Interact(HubContext ctx)
        {
            base.Interact(ctx);
            CheckpointRequested?.Invoke(checkpointReason);
            CoreRuntime.Instance?.Bus.Publish(new CheckpointRequestedEvent
            {
                Reason = checkpointReason,
                NextSceneName = targetScene
            });
            if (string.IsNullOrEmpty(targetScene))
            {
                Debug.LogWarning($"CarrierInteractable '{name}': targetScene is empty.", this);
                return;
            }
            // Single load authority: the GSM guards missing scenes and stamps
            // the checkpoint; direct load only without Core.
            if (CoreRuntime.Instance != null)
                CoreRuntime.Instance.Bus.Publish(new SceneLoadRequestedEvent
                {
                    SceneName = targetScene
                });
            else
                SceneManager.LoadScene(targetScene);
        }
    }
}

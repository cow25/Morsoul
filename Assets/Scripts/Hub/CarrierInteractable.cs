using System;
using System.Collections.Generic;
using Morsoul.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Hub
{
    /// <summary>
    /// Carrier fast-travel node (GDD §3 Transit Node). On interact it offers
    /// every unlocked regional hub through the scene's
    /// <see cref="TravelMenuController"/>; a single reachable destination
    /// travels immediately, preserving the original fixed-target behaviour
    /// when no menu is wired. Travelling always requests a checkpoint and
    /// loads through the GameStateManager (direct load only without Core).
    /// </summary>
    public class CarrierInteractable : Interactable2D
    {
        /// <summary>Fallback scene loaded when no travel menu is available.</summary>
        public string targetScene;
        /// <summary>Reason stamped on the travel checkpoint ("Carrier", "CaseDoor", ...).</summary>
        public string checkpointReason = "Carrier";
        /// <summary>
        /// When true, interacting opens the carrier destination menu of
        /// unlocked hubs (via HubContext.travelMenu). False keeps the fixed
        /// targetScene behaviour — used by CaseDoor and the prologue exit.
        /// </summary>
        public bool offerTravelMenu = false;

        /// <summary>Fired just before scene load; argument is the checkpoint reason.</summary>
        public event Action<string> CheckpointRequested;

        void Reset()
        {
            promptText = "[E] Travel";
        }

        public override void Interact(HubContext ctx)
        {
            base.Interact(ctx);

            // Destination picker: unlocked hubs from the route table. Only
            // used when the scene wires a travel menu AND Core is live —
            // otherwise the serialized targetScene fallback keeps old scenes
            // and non-campaign carriers (CaseDoor) working unchanged.
            var menu = ctx != null ? ctx.travelMenu : null;
            if (offerTravelMenu && menu != null && CoreRuntime.Instance != null)
            {
                var destinations = menu.CollectDestinations(SceneManager.GetActiveScene().name);
                if (destinations.Count > 1)
                {
                    void OnSelected(TravelDestination d)
                    {
                        menu.DestinationSelected -= OnSelected;
                        menu.Cancelled -= OnCancelled;
                        TravelTo(d.sceneName);
                    }
                    void OnCancelled()
                    {
                        menu.DestinationSelected -= OnSelected;
                        menu.Cancelled -= OnCancelled;
                    }
                    menu.DestinationSelected += OnSelected;
                    menu.Cancelled += OnCancelled;
                    menu.Show(destinations);
                    return;
                }
                if (destinations.Count == 1)
                {
                    TravelTo(destinations[0].sceneName);
                    return;
                }
                // No unlocked destination: fall through to the fixed target.
            }

            TravelTo(targetScene);
        }

        /// <summary>Checkpoints and loads <paramref name="scene"/> through the GSM.</summary>
        protected void TravelTo(string scene)
        {
            CheckpointRequested?.Invoke(checkpointReason);
            CoreRuntime.Instance?.Bus.Publish(new CheckpointRequestedEvent
            {
                Reason = checkpointReason,
                NextSceneName = scene
            });
            if (string.IsNullOrEmpty(scene))
            {
                Debug.LogWarning($"CarrierInteractable '{name}': targetScene is empty.", this);
                return;
            }
            // Single load authority: the GSM guards missing scenes and stamps
            // the checkpoint; direct load only without Core.
            if (CoreRuntime.Instance != null)
                CoreRuntime.Instance.Bus.Publish(new SceneLoadRequestedEvent
                {
                    SceneName = scene
                });
            else
                SceneManager.LoadScene(scene);
        }
    }
}

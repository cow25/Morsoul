using System;
using Morsoul.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Hub
{
    /// <summary>
    /// Flag-gated scene exit (e.g. the Vault seam). When the required flag is
    /// present, requests a checkpoint save and loads targetScene; otherwise it
    /// plays the lockedDialogue through the shared DialogueController.
    /// Checkpoint persistence is plumbed later — for now subscribers listen to
    /// CheckpointRequested.
    /// </summary>
    public class GateInteractable : Interactable2D
    {
        /// <summary>Flag required to pass; empty = always open.</summary>
        public string requiredFlag;
        /// <summary>Scene loaded when the gate is open.</summary>
        public string targetScene;
        /// <summary>
        /// Optional named spawn in the destination scene (matches a
        /// SceneSpawnMarker's entryId). Empty = the scene's default spawn.
        /// Used so doors deposit the player at their own doorway.
        /// </summary>
        public string entryId;
        /// <summary>
        /// Optional route-table key (e.g. "investigation.3") resolved through
        /// the GameStateManager before targetScene. Lets a Zone B seam point
        /// at a route whose scene is delivered by another lane — the seam
        /// needs no rewiring when the real scene registers.
        /// </summary>
        public string routeKey;
        /// <summary>Played when the gate is locked; optional.</summary>
        public Morsoul.Dialogue.DialogueData lockedDialogue;

        /// <summary>Fired just before scene load; argument is the checkpoint reason.</summary>
        public event Action<string> CheckpointRequested;

        void Reset()
        {
            promptText = "[E] Enter";
        }

        public override void Interact(HubContext ctx)
        {
            base.Interact(ctx);

            var flags = ctx != null && ctx.flags != null ? ctx.flags.Set : null;
            if (GateRule.CanPass(requiredFlag, flags))
            {
                if (!string.IsNullOrEmpty(routeKey) && routeKey.StartsWith("investigation.", StringComparison.Ordinal))
                {
                    var entry = CoreRuntime.Ensure().EnterInvestigation(routeKey);
                    Debug.Log(entry.Message, this);
                    return;
                }
                string scene = ResolveTargetScene();
                if (string.IsNullOrEmpty(scene))
                {
                    Debug.LogWarning($"GateInteractable '{name}': open but no destination " +
                                     $"(routeKey '{routeKey}' unresolved, targetScene empty).", this);
                    return;
                }
                CheckpointRequested?.Invoke("Seam");
                // Named-spawn handoff survives the travel checkpoint's pose
                // clear and is consumed by SceneEntryPoint in the next scene.
                if (CoreRuntime.Instance != null && !string.IsNullOrEmpty(entryId))
                    CoreRuntime.Instance.Session.PendingEntryPointId = entryId;
                // Single load authority: the GSM guards missing scenes and
                // stamps the checkpoint; direct load only without Core.
                if (CoreRuntime.Instance != null)
                    CoreRuntime.Instance.Bus.Publish(new SceneLoadRequestedEvent
                    {
                        SceneName = scene
                    });
                else
                    SceneManager.LoadScene(scene);
                return;
            }

            if (lockedDialogue == null)
            {
                Debug.Log($"GateInteractable '{name}': locked (missing flag '{requiredFlag}').", this);
                return;
            }
            if (ctx == null || ctx.dialogue == null)
            {
                Debug.LogWarning($"GateInteractable '{name}': locked but no HubContext.dialogue wired.", this);
                return;
            }
            ctx.PlayDialogue(lockedDialogue);
        }

        /// <summary>
        /// Route-table lookup first (when routeKey is set and Core is live),
        /// serialized targetScene as fallback. Null/empty when neither
        /// resolves.
        /// </summary>
        public string ResolveTargetScene()
        {
            if (!string.IsNullOrEmpty(routeKey) && GameStateManager.Instance != null)
            {
                string routed = GameStateManager.Instance.routes.Resolve(routeKey);
                if (!string.IsNullOrEmpty(routed)) return routed;
            }
            return targetScene;
        }
    }
}

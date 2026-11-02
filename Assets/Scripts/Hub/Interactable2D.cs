using System;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Base class for 2D hub interactables. The HubPlayerController picks the
    /// nearest instance inside its interact radius, toggles its promptIndicator
    /// while in range and calls Interact on the interact key. Subclasses must
    /// call base.Interact(ctx) so the Interacted event fires.
    /// </summary>
    public class Interactable2D : MonoBehaviour
    {
        /// <summary>Prompt hint shown next to the interactable (scene-driven label).</summary>
        public string promptText = "[E] Talk";
        /// <summary>Optional world-space prompt (sprite/[E] child) toggled while the player is in range.</summary>
        public GameObject promptIndicator;

        /// <summary>Raised each time Interact is invoked.</summary>
        public event Action Interacted;

        /// <summary>Interaction entry point. Base implementation only raises Interacted.</summary>
        public virtual void Interact(HubContext ctx)
        {
            Interacted?.Invoke();
        }

        /// <summary>Shows/hides the assigned prompt indicator, if any.</summary>
        public void SetPromptVisible(bool visible)
        {
            if (promptIndicator != null && promptIndicator.activeSelf != visible)
                promptIndicator.SetActive(visible);
        }

        void OnDisable()
        {
            SetPromptVisible(false);
        }
    }
}

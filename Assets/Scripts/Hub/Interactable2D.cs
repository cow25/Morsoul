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
    [RequireComponent(typeof(YSort2D))]
    public class Interactable2D : MonoBehaviour
    {
        /// <summary>Prompt hint shown next to the interactable (scene-driven label).</summary>
        public string promptText = "[E] Talk";
        /// <summary>Optional world-space prompt (sprite/[E] child) toggled while the player is in range.</summary>
        public GameObject promptIndicator;

        /// <summary>Raised each time Interact is invoked.</summary>
        public event Action Interacted;

        void Awake()
        {
            // RequireComponent covers editor additions; this guarantees the
            // sorter exists on objects serialized before the requirement.
            if (GetComponent<YSort2D>() == null)
                gameObject.AddComponent<YSort2D>();
        }

        /// <summary>Interaction entry point. Base implementation only raises Interacted.</summary>
        public virtual void Interact(HubContext ctx)
        {
            Interacted?.Invoke();
        }

        /// <summary>Shows/hides the assigned prompt indicator, if any.</summary>
        public void SetPromptVisible(bool visible)
        {
            if (promptIndicator == null) return;
            // Text prompts (TMP labels) mirror promptText whenever shown, so
            // installers only need to assign promptText once.
            if (visible)
            {
                var label = promptIndicator.GetComponent<InteractableLabel>();
                if (label != null && label.text != promptText) label.text = promptText;
            }
            if (promptIndicator.activeSelf != visible)
                promptIndicator.SetActive(visible);
        }

        void OnDisable()
        {
            SetPromptVisible(false);
        }
    }
}

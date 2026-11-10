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
    [ExecuteAlways]
    [RequireComponent(typeof(YSort2D))]
    public class Interactable2D : MonoBehaviour
    {
        /// <summary>Prompt hint shown next to the interactable (scene-driven label).</summary>
        public string promptText = "[E] Talk";
        /// <summary>Optional world-space prompt (sprite/[E] child) toggled while the player is in range.</summary>
        public GameObject promptIndicator;
        /// <summary>
        /// When set, a solid BoxCollider2D is added so the player cannot walk
        /// through the object. Gates/stairs leave it off — you interact with
        /// them, you don't bump into them. NPCs always block.
        /// </summary>
        public bool blocksMovement;
        /// <summary>World-space size of the solid body added when blocking.</summary>
        public Vector2 blockSize = new Vector2(0.55f, 0.5f);

        /// <summary>Raised each time Interact is invoked.</summary>
        public event Action Interacted;

        /// <summary>Whether this interactable physically blocks the player.</summary>
        protected virtual bool BlocksMovement => blocksMovement;

        void Awake()
        {
            // RequireComponent covers editor additions; this guarantees the
            // sorter exists on objects serialized before the requirement.
            if (GetComponent<YSort2D>() == null)
                gameObject.AddComponent<YSort2D>();
            if (BlocksMovement) EnsureBlockCollider();
        }

        void OnEnable()
        {
            if (BlocksMovement) EnsureBlockCollider();
        }

        /// <summary>
        /// Adds a small non-trigger collider (kept separate from the
        /// detection trigger the player scans with OverlapCircleAll).
        /// </summary>
        void EnsureBlockCollider()
        {
            foreach (var col in GetComponents<BoxCollider2D>())
                if (!col.isTrigger) return;
            var body = gameObject.AddComponent<BoxCollider2D>();
            body.isTrigger = false;
            body.size = blockSize;
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

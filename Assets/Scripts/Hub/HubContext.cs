using Morsoul.Dialogue;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Scene-level wiring hub for 2D hub scenes. Holds the shared services that
    /// <see cref="Interactable2D"/> implementations need when interacted with:
    /// the dialogue overlay controller, the hub clue journal and the scene flag
    /// set. No static singletons — every interactable receives this context
    /// through <see cref="Interactable2D.Interact"/>.
    /// </summary>
    public class HubContext : MonoBehaviour
    {
        public DialogueController dialogue;
        public HubJournal journal;
        public HubFlags flags;
        /// <summary>Optional carrier travel menu; opened by CarrierInteractable.</summary>
        public TravelMenuController travelMenu;

        /// <summary>True when a dialogue is currently playing (blocks movement/interaction).</summary>
        public bool DialoguePlaying => dialogue != null && dialogue.IsPlaying;

        /// <summary>True while any blocking overlay (dialogue, travel menu) is open.</summary>
        public bool UiBlocked =>
            DialoguePlaying || (travelMenu != null && travelMenu.IsOpen);
    }
}

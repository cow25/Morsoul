using UnityEngine;
using UnityEngine.InputSystem; 

public class InteractionProbe : MonoBehaviour
{
    public float range = 3f;
    public LayerMask interactLayer;
    
    [Tooltip("Drag your MainCamera here")]
    [SerializeField] private Camera playerCamera; 

    private void Start()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
    }

    private void Update()
    {
        if (Morsoul.Core.PauseMenuController.AnyModalOrPauseOpen ||
            Morsoul.Dev.DevMenuOverlay.IsOpen)
        {
            HUDManager.Instance?.HideInteractionPrompt();
            return;
        }
        if (playerCamera == null) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, range, interactLayer))
        {
            if (hit.collider.TryGetComponent(out ClueNode clue))
            {
                HUDManager.Instance.ShowInteractionPrompt("[L-Click] Inspect Clue", clue.defaultDescription);
                
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    InspectManager.Instance.OpenInspect(clue);
                }
            }
            else if (hit.collider.TryGetComponent(out DialogueTrigger dialogue))
            {
                HUDManager.Instance.ShowInteractionPrompt("[E] Talk", dialogue.dialogueText);
            }
            else if (hit.collider.TryGetComponent(out LockableInteractable interactable))
            {
                if (interactable.IsLocked())
                {
                    HUDManager.Instance.ShowInteractionPrompt("[Locked]", interactable.lockedDescription);
                }
                else
                {
                    HUDManager.Instance.ShowInteractionPrompt("[L-Click] Interact", interactable.unlockedDescription);
                    
                    if (Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        interactable.Interact();
                    }
                }
            }
        }
        else
        {
            HUDManager.Instance.HideInteractionPrompt();
        }
    }
}
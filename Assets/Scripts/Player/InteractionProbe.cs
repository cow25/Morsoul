using System.Linq;
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
                bool needsAether = clue.requiredTool == ClueNode.Tool.AetherSight || clue.requiredTool == ClueNode.Tool.Both;
                var session = Morsoul.Core.CoreRuntime.Instance?.Session;
                bool hasAether = session != null && session.Tools.Contains(Morsoul.Data.Tool.AetherSight);
                if (needsAether && !hasAether)
                {
                    HUDManager.Instance.ShowInteractionPrompt("[Locked] Dormant Runic Inscription", "Requires Aether Sight (unlocked after Case 4) to decipher.");
                    return;
                }

                bool needsScan = clue.requiredTool == ClueNode.Tool.GolemEye && clue.state == ClueNode.State.Hidden;
                if (needsScan)
                {
                    HUDManager.Instance.ShowInteractionPrompt("[Hold Tab / L-Click] Scan with Golem Eye", "Unreadable modern script. Scan with Golem Eye [Tab] to decipher.");
                    if (Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        var scanner = FindAnyObjectByType<ScanController>();
                        if (scanner != null) scanner.TriggerAccidentalScan(1.8f);
                        clue.state = ClueNode.State.Revealed;
                        InspectManager.Instance.OpenInspect(clue);
                    }
                }
                else
                {
                    HUDManager.Instance.ShowInteractionPrompt("[L-Click] Inspect Clue", clue.defaultDescription);
                    if (Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        InspectManager.Instance.OpenInspect(clue);
                    }
                }
            }
            else if (hit.collider.TryGetComponent(out DialogueTrigger dialogue))
            {
                HUDManager.Instance.ShowInteractionPrompt("[E] Talk", dialogue.dialogueText);
            }
            else if (hit.collider.TryGetComponent(out LockableInteractable interactable))
            {
                HUDManager.Instance.ShowInteractionPrompt(interactable.GetPromptAction(), interactable.GetPromptDescription());
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    interactable.Interact();
                }
            }
        }
        else
        {
            HUDManager.Instance.HideInteractionPrompt();
        }
    }
}
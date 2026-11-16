using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem; 

public class InteractionProbe : MonoBehaviour
{
    public float range = 3f;
    public LayerMask interactLayer;
    
    [Tooltip("Drag your MainCamera here")]
    [SerializeField] private Camera playerCamera; 

    private ScanController _scanController;

    private void Start()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
        _scanController = FindAnyObjectByType<ScanController>();
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
                    HUDManager.Instance?.HideInteractionPrompt();
                    return;
                }

                if (_scanController == null) _scanController = FindAnyObjectByType<ScanController>();
                bool isScanning = _scanController != null && _scanController.IsScanning;
                if (!clue.IsScanned && !isScanning)
                {
                    HUDManager.Instance?.HideInteractionPrompt();
                    return;
                }

                if (!clue.IsScanned && isScanning)
                    clue.MarkScanned();

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
                HUDManager.Instance.ShowInteractionPrompt(interactable.GetPromptAction(), interactable.GetPromptDescription());
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    interactable.Interact();
                }
            }
        }
        else if (HUDManager.Instance != null)
        {
            HUDManager.Instance.HideInteractionPrompt();
        }
    }
}
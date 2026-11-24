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
                _scanController ??= FindAnyObjectByType<ScanController>();
                var core = Morsoul.Core.CoreRuntime.Instance;
                var session = core?.Session;

                bool needsAether = clue.requiredTool == ClueNode.Tool.AetherSight || clue.requiredTool == ClueNode.Tool.Both;
                bool hasAether = session != null && session.Tools.Contains(Morsoul.Data.Tool.AetherSight);
                bool aetherActive = hasAether && _scanController != null && _scanController.IsAetherSightActive;
                bool needsGolemEye = clue.requiredTool == ClueNode.Tool.GolemEye || clue.requiredTool == ClueNode.Tool.Both;
                bool hasGolemEye = session != null && session.Tools.Contains(Morsoul.Data.Tool.GolemEye);
                if ((needsAether && !aetherActive) || (needsGolemEye && !hasGolemEye))
                {
                    HUDManager.Instance?.HideInteractionPrompt();
                    return;
                }

                if (core != null && session != null && !string.IsNullOrEmpty(clue.id))
                {
                    var clueData = core.ResolveClue(clue.id);
                    if (clueData != null)
                    {
                        var log = clueData.type == Morsoul.Data.ClueType.Latent
                            ? core.ResolveCase(clueData.caseId)?.latentLog
                            : null;
                        if (!Morsoul.Core.ClueAcquisition.CanAcquire(clueData, session, log, out _))
                        {
                            HUDManager.Instance?.HideInteractionPrompt();
                            return;
                        }
                    }
                }

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
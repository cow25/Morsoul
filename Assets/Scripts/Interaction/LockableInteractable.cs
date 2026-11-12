using UnityEngine;
using UnityEngine.Events;

public class LockableInteractable : MonoBehaviour
{
    [Header("Interaction Settings")]
    [Tooltip("Leave empty if the door is never locked.")]
    public string requiredItemId = "I1_Key"; 
    public string lockedDescription = "Looks like it can be opened by something.";
    public string unlockedDescription = "It's unlocked. [L-Click] to open.";

    [Header("Events")]
    [Tooltip("What happens when the player interacts successfully?")]
    public UnityEvent onInteractSuccess;

    public bool IsLocked()
    {
        if (string.IsNullOrEmpty(requiredItemId)) return false;
        if (DiaryManager.Instance == null) return true;

        return !DiaryManager.Instance.HasClue(requiredItemId); 
    }

    public void Interact()
    {
        if (!IsLocked())
        {
            Debug.Log("Door Unlocked! Triggering event...");
            onInteractSuccess?.Invoke();
            
            requiredItemId = ""; 
        }
        else
        {
            Debug.Log("Door is locked. You need: " + requiredItemId);
        }
    }
}
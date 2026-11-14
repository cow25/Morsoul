using UnityEngine;
using UnityEngine.Events;

public class LockableInteractable : MonoBehaviour
{
    [Header("Interaction Settings")]
    [Tooltip("Leave empty if the door is never locked.")]
    public string requiredItemId = "C3_00"; 
    public string lockedDescription = "Sealed hydraulic blast door. Requires the Ancient Chamber Key and a solved Deduction Board [H].";
    public string unlockedDescription = "Manual override calibrated. Unlock the gate and collect the nearby traveler's robe before exiting.";
    public bool requireBoardSolved = false;
    public int boardCaseId = 0;

    [Header("Events")]
    [Tooltip("What happens when the player interacts successfully?")]
    public UnityEvent onInteractSuccess;

    public bool HasRequiredItem()
    {
        if (string.IsNullOrEmpty(requiredItemId)) return true;
        if (DiaryManager.Instance != null && DiaryManager.Instance.HasClue(requiredItemId)) return true;
        var session = Morsoul.Core.CoreRuntime.Instance?.Session;
        return session != null && session.HasClue(requiredItemId);
    }

    public bool IsBoardReady()
    {
        if (!requireBoardSolved) return true;
        var session = Morsoul.Core.CoreRuntime.Instance?.Session;
        if (session == null) return false;
        var board = session.GetOrCreateBoardState(boardCaseId);
        return board != null && board.BoardSolved;
    }

    public bool IsLocked()
    {
        return !HasRequiredItem() || !IsBoardReady();
    }

    public string GetPromptAction()
    {
        return IsLocked() ? "[Locked] Sealed Stone Gate" : "[L-Click] Unlock & Exit Ruins";
    }

    public string GetPromptDescription()
    {
        if (!HasRequiredItem() && requireBoardSolved && !IsBoardReady())
            return "Failsafe lockdown active. Recover the Ancient Chamber Key and solve the Deduction Board [H].";
        if (!HasRequiredItem())
            return "Requires the Ancient Chamber Key from the equipment rubble.";
        if (requireBoardSolved && !IsBoardReady())
            return "Key recovered, but manual bypass is uncalibrated. Open the Deduction Board [H] and submit Manual Override [Space].";
        return unlockedDescription;
    }

    public void Interact()
    {
        if (!IsLocked())
        {
            var core = Morsoul.Core.CoreRuntime.Instance;
            if (core != null && requireBoardSolved && boardCaseId == 0)
            {
                core.Session.PrologueGateUnlocked = true;
                core.Session.PrologueRobeCollected = true;
                core.Session.PrologueExitLoaded = true;
                core.Session.SetSceneContext(Morsoul.Core.SceneMode.Mode2D, Morsoul.Core.SessionState.PrologueWildernessSceneId);
            }
            Debug.Log("Door Unlocked! Triggering event...");
            onInteractSuccess?.Invoke();
            requiredItemId = ""; 
        }
        else
        {
            Debug.Log("Door is locked: " + GetPromptDescription());
        }
    }
}
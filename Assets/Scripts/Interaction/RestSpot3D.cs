using Morsoul.Core;
using Morsoul.Dialogue;
using UnityEngine;

/// <summary>
/// 3D rest bench/campfire interactable: pressing [E] confirms resting (or
/// emergency reserve when rests are exhausted and Focus is critically low),
/// then locks movement until Focus Energy refills to maximum.
/// </summary>
public class RestSpot3D : MonoBehaviour
{
    [SerializeField] private string spotLabel = "Bench";

    public bool IsResting { get; private set; }
    bool _prompting;
    MonoBehaviour _playerController;
    ScanController _scanController;

    public string GetPromptAction()
    {
        if (IsResting) return "Resting... Recovering Focus Energy";
        var core = CoreRuntime.Instance;
        if (core == null) return "[E] Rest";
        if (core.Focus.Percent >= core.Focus.Maximum) return "Focus Full";
        int caseId = core.Session.InvestigationCaseId >= 0 ? core.Session.InvestigationCaseId : core.Session.CurrentCase;
        int rem = core.Session.GetRemainingRests(caseId);
        if (rem > 0)
        {
            float cd = core.Session.RestCooldownRemaining(Time.unscaledTime);
            return cd > 0.5f ? $"Rest cooling down ({Mathf.CeilToInt(cd)}s)" : $"[E] Rest ({rem}/{FocusTuning.RestMaxPerCase} left)";
        }
        return core.Focus.Percent < Morsoul.Engine.ProgressionConstants.LinkingUnlockFocus
            ? "[E] Emergency Reboot (Out of Rests)"
            : "Rest exhausted for case";
    }

    public string GetPromptDescription()
    {
        var focus = CoreRuntime.Ensure()?.Focus;
        return focus != null
            ? $"{spotLabel} — Focus {Mathf.RoundToInt(focus.Percent)}/{Mathf.RoundToInt(focus.Maximum)}"
            : $"{spotLabel} — Recover Focus";
    }

    public void Interact()
    {
        if (IsResting || _prompting) return;
        var core = CoreRuntime.Ensure();
        if (core == null || core.Focus.Percent >= core.Focus.Maximum) return;

        int caseId = core.Session.InvestigationCaseId >= 0 ? core.Session.InvestigationCaseId : core.Session.CurrentCase;
        int rem = core.Session.GetRemainingRests(caseId);
        if (rem <= 0)
        {
            if (core.Focus.Percent < Morsoul.Engine.ProgressionConstants.LinkingUnlockFocus)
                ShowOrApply(isEmergency: true, remaining: 0);
            return;
        }

        if (core.Session.RestCooldownRemaining(Time.unscaledTime) > 0.05f) return;
        ShowOrApply(isEmergency: false, remaining: rem);
    }

    void ShowOrApply(bool isEmergency, int remaining)
    {
        var core = CoreRuntime.Ensure();
        var dlg = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
        if (dlg == null)
        {
            if (isEmergency) TriggerEmergencyReserve();
            else StartResting();
            return;
        }

        const string confirmFlag = "RestSpot3D.Confirmed";
        core.Session.SetFlag(confirmFlag, false);
        var data = ScriptableObject.CreateInstance<DialogueData>();
        data.hideFlags = HideFlags.DontSave;
        data.id = isEmergency ? "RestSpot3D_Emergency" : "RestSpot3D_Confirm";
        data.startNodeId = "n1";
        string body = isEmergency
            ? $"All rests for this case are used up and your Focus Energy is critically low ({Mathf.RoundToInt(core.Focus.Percent)}/{Mathf.RoundToInt(core.Focus.Maximum)}). Triggering an Emergency Reboot will wipe your current Deduction Board links, reset rests for this case, and forfeit a Clean grade (Flawed). Proceed?"
            : $"Rest here until your Focus Energy is completely full ({Mathf.RoundToInt(core.Focus.Percent)}/{Mathf.RoundToInt(core.Focus.Maximum)})? ({remaining}/{FocusTuning.RestMaxPerCase} rests left)";
        data.nodes = new[]
        {
            new DialogueNodeData
            {
                id = "n1",
                speakerName = spotLabel,
                text = body,
                options = new[]
                {
                    new DialogueOptionData { label = isEmergency ? "Yes, reboot case (forfeit Clean & board)." : "Yes, rest until full.", nextNodeId = "yes" },
                    new DialogueOptionData { label = "No, not now.", nextNodeId = "no" }
                }
            },
            new DialogueNodeData { id = "yes", speakerName = spotLabel, text = isEmergency ? "Emergency reboot initiated — board links cleared." : "Settling in to recover Focus Energy...", setsFlag = confirmFlag },
            new DialogueNodeData { id = "no", speakerName = spotLabel, text = "You step away." }
        };

        _prompting = true;
        void OnEnded()
        {
            dlg.DialogueEnded -= OnEnded;
            _prompting = false;
            Destroy(data);
            if (!core.Session.HasFlag(confirmFlag)) return;
            core.Session.SetFlag(confirmFlag, false);
            if (isEmergency) TriggerEmergencyReserve();
            else StartResting();
        }
        dlg.DialogueEnded += OnEnded;
        dlg.SetDialogue(data);
    }

    void TriggerEmergencyReserve()
    {
        var core = CoreRuntime.Ensure();
        if (core == null) return;
        int caseId = core.Session.InvestigationCaseId >= 0 ? core.Session.InvestigationCaseId : core.Session.CurrentCase;
        core.Session.ApplyEmergencyCaseReboot(caseId);
        float target = Mathf.Min(core.Focus.Maximum,
            Mathf.Max(core.Focus.Percent, Morsoul.Engine.ProgressionConstants.LinkingUnlockFocus));
        core.Focus.Sync(target, core.Focus.Maximum);
    }

    void StartResting()
    {
        var core = CoreRuntime.Ensure();
        if (core == null || core.Focus.Percent >= core.Focus.Maximum) return;
        int caseId = core.Session.InvestigationCaseId >= 0 ? core.Session.InvestigationCaseId : core.Session.CurrentCase;
        if (core.Session.GetRemainingRests(caseId) <= 0) return;

        core.Session.RecordRestUse(caseId, Time.unscaledTime);
        IsResting = true;
        if (_playerController == null) _playerController = FindAnyObjectByType<PlayerController>();
        if (_playerController != null) _playerController.enabled = false;
        if (_scanController == null) _scanController = FindAnyObjectByType<ScanController>();
        if (_scanController != null) _scanController.enabled = false;
    }

    void StopResting()
    {
        if (!IsResting) return;
        IsResting = false;
        if (_playerController != null) _playerController.enabled = true;
        if (_scanController != null) _scanController.enabled = true;
    }

    void Update()
    {
        if (!IsResting) return;
        var core = CoreRuntime.Instance;
        if (core == null) { StopResting(); return; }
        core.Focus.TickRest(Time.deltaTime * 2f);
        if (core.Focus.Percent >= core.Focus.Maximum)
            StopResting();
    }

    void OnDisable() => StopResting();
}


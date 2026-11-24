using Morsoul.Core;
using Morsoul.Dialogue;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Bench/seat rest spot: pressing [E] opens a confirmation dialogue;
    /// confirming locks movement until Focus Energy refills to maximum.
    /// When out of rests and critically low on Focus (&lt; LinkingUnlockFocus),
    /// offers an emergency reserve recharge fallback to prevent soft-locks.
    /// </summary>
    public class RestSpot : Interactable2D
    {
        /// <summary>True while the player is seated and recovering Focus.</summary>
        public bool IsResting { get; private set; }

        /// <summary>Optional overlay toggled while resting (e.g. a dim panel).</summary>
        public GameObject restingIndicator;
        /// <summary>Optional one-shot played when the player sits down.</summary>
        public AudioClip restCue;

        HubPlayerController _player;
        HubContext _context;
        System.Func<bool> _restBlocker;
        float _sessionTargetFocus;
        string _statusMessage;
        bool _prompting;

        void Reset()
        {
            promptText = "[E] Rest";
        }

        void Awake()
        {
            _player = FindAnyObjectByType<HubPlayerController>();
        }

        public override void SetPromptVisible(bool visible)
        {
            if (visible)
            {
                promptText = BuildDynamicPrompt();
                TutorialOverlayController.TryShow("TUT_REST_SPOT");
            }
            base.SetPromptVisible(visible);
        }

        string BuildDynamicPrompt()
        {
            if (IsResting) return "Resting... Recovering Focus Energy";
            var core = CoreRuntime.Instance;
            if (core == null) return "[E] Rest";
            int caseId = core.Session.CurrentCase;
            int remaining = core.Session.GetRemainingRests(caseId);
            if (!string.IsNullOrEmpty(_statusMessage)) return _statusMessage;
            if (core.Focus.Percent >= core.Focus.Maximum) return "Focus Full";
            if (remaining <= 0)
            {
                return core.Focus.Percent < Morsoul.Engine.ProgressionConstants.LinkingUnlockFocus
                    ? "[E] Emergency Reboot (Out of Rests)"
                    : "Rest used up for this case";
            }
            float cd = core.Session.RestCooldownRemaining(Time.unscaledTime);
            if (cd > 0.5f) return $"Rest cooling down ({Mathf.CeilToInt(cd)}s)";
            return $"[E] Rest ({remaining}/{FocusTuning.RestMaxPerCase} left)";
        }

        public override void Interact(HubContext ctx)
        {
            base.Interact(ctx);
            if (IsResting || _prompting) return;
            if (ctx != null && _restBlocker == null)
            {
                _restBlocker = () => IsResting;
                ctx.AddUiBlocker(_restBlocker);
                _context = ctx;
            }

            var core = CoreRuntime.Instance;
            if (core == null) { StartResting(); return; }

            if (core.Focus.Percent >= core.Focus.Maximum)
            {
                _statusMessage = "Focus already full";
                SetPromptVisible(true);
                return;
            }

            int caseId = core.Session.CurrentCase;
            int remaining = core.Session.GetRemainingRests(caseId);
            if (remaining <= 0)
            {
                if (core.Focus.Percent < Morsoul.Engine.ProgressionConstants.LinkingUnlockFocus)
                    ShowConfirmation(ctx, isEmergency: true, remaining: 0);
                else
                {
                    _statusMessage = $"No rests left for Case {caseId}";
                    SetPromptVisible(true);
                    TutorialOverlayController.TryShow("TUT_REST_SPOT");
                }
                return;
            }

            float cd = core.Session.RestCooldownRemaining(Time.unscaledTime);
            if (cd > 0.05f)
            {
                _statusMessage = $"Rest cooling down ({Mathf.CeilToInt(cd)}s)";
                SetPromptVisible(true);
                return;
            }

            if (ctx != null && ctx.dialogue != null)
                ShowConfirmation(ctx, isEmergency: false, remaining: remaining);
            else
                StartResting();
        }

        void ShowConfirmation(HubContext ctx, bool isEmergency, int remaining)
        {
            var core = CoreRuntime.Instance;
            if (core == null || ctx == null || ctx.dialogue == null) return;

            const string confirmFlag = "RestSpot.Confirmed";
            core.Session.SetFlag(confirmFlag, false);

            var data = ScriptableObject.CreateInstance<DialogueData>();
            data.hideFlags = HideFlags.DontSave;
            data.id = isEmergency ? "RestSpot_Emergency" : "RestSpot_Confirm";
            data.startNodeId = "n1";

            string promptBody = isEmergency
                ? $"All rests for this case are used up and your Focus Energy is critically low ({Mathf.RoundToInt(core.Focus.Percent)}/{Mathf.RoundToInt(core.Focus.Maximum)}). Triggering an Emergency Reboot will wipe your current Deduction Board links, reset rests for this case, and forfeit a Clean grade (Flawed). Proceed?"
                : $"Rest here until your Focus Energy is completely full ({Mathf.RoundToInt(core.Focus.Percent)}/{Mathf.RoundToInt(core.Focus.Maximum)})? ({remaining}/{FocusTuning.RestMaxPerCase} rests left)";

            data.nodes = new[]
            {
                new DialogueNodeData
                {
                    id = "n1",
                    speakerName = "Rest Spot",
                    text = promptBody,
                    options = new[]
                    {
                        new DialogueOptionData { label = isEmergency ? "Yes, reboot case (forfeit Clean & board)." : "Yes, rest until full.", nextNodeId = "yes" },
                        new DialogueOptionData { label = "No, not now.", nextNodeId = "no" }
                    }
                },
                new DialogueNodeData
                {
                    id = "yes",
                    speakerName = "Rest Spot",
                    text = isEmergency ? "Emergency reboot initiated — board links cleared." : "Settling in to recover Focus Energy...",
                    setsFlag = confirmFlag
                },
                new DialogueNodeData
                {
                    id = "no",
                    speakerName = "Rest Spot",
                    text = "You step away from the rest spot."
                }
            };

            _prompting = true;
            var dlg = ctx.dialogue;
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
            ctx.PlayDialogue(data);
        }

        /// <summary>Fallback when out of both Focus and rests: wipes unsolved board links, marks Flawed, and resets rests.</summary>
        public void TriggerEmergencyReserve()
        {
            var core = CoreRuntime.Instance;
            if (core == null) return;
            int caseId = core.Session.CurrentCase;
            core.Session.ApplyEmergencyCaseReboot(caseId);
            _statusMessage = null;
            StartResting();
        }

        /// <summary>Begins the rest loop; enforces per-case limit, cooldown, and fills to maximum.</summary>
        public void StartResting()
        {
            if (IsResting) return;
            _statusMessage = null;
            var core = CoreRuntime.Instance;
            if (core == null || core.Focus.Percent >= core.Focus.Maximum)
            {
                _statusMessage = "Focus already full";
                SetPromptVisible(true);
                return;
            }

            int caseId = core.Session.CurrentCase;
            if (core.Session.GetRemainingRests(caseId) <= 0)
            {
                _statusMessage = $"No rests left for Case {caseId}";
                SetPromptVisible(true);
                TutorialOverlayController.TryShow("TUT_REST_SPOT");
                return;
            }

            float cd = core.Session.RestCooldownRemaining(Time.unscaledTime);
            if (cd > 0.05f)
            {
                _statusMessage = $"Rest cooling down ({Mathf.CeilToInt(cd)}s)";
                SetPromptVisible(true);
                return;
            }

            core.Session.RecordRestUse(caseId, Time.unscaledTime);
            _sessionTargetFocus = core.Focus.Maximum;

            IsResting = true;
            if (restCue != null) core.Audio?.PlaySfx(restCue);
            if (_player == null) _player = FindAnyObjectByType<HubPlayerController>();
            if (_player != null) _player.enabled = false;
            if (restingIndicator != null) restingIndicator.SetActive(true);
            SetPromptVisible(true);
            TutorialOverlayController.TryShow("TUT_REST_SPOT");
        }

        /// <summary>Stops resting; safe to call even when not resting.</summary>
        public void StopResting()
        {
            if (!IsResting) return;
            IsResting = false;
            _statusMessage = null;
            if (_player != null) _player.enabled = true;
            if (restingIndicator != null) restingIndicator.SetActive(false);
        }

        void Update()
        {
            if (!IsResting) return;
            var core = CoreRuntime.Instance;
            if (core == null) { StopResting(); return; }
            core.Focus.TickRest(Time.deltaTime * 2f);

            if (core.Focus.Percent >= _sessionTargetFocus || core.Focus.Percent >= core.Focus.Maximum)
            {
                if (core.Focus.Percent > _sessionTargetFocus)
                    core.Focus.Sync(_sessionTargetFocus, core.Focus.Maximum);
                StopResting();
            }
        }

        void OnDisable()
        {
            StopResting();
            if (_context != null && _restBlocker != null)
            {
                _context.RemoveUiBlocker(_restBlocker);
                _restBlocker = null;
            }
        }
    }
}

using Morsoul.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Morsoul.Hub
{
    /// <summary>
    /// Bench/seat rest spot (Algorithm.md B2, GDD §6.4): interact to start
    /// resting, Focus regenerates +1 per Focus tick while resting, any
    /// movement key, the interact key or Escape cancels. Resting blocks
    /// player movement (no auto-refill — capped at the current maximum).
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
        int _restStartFrame = -1;
        float _sessionTargetFocus;
        string _statusMessage;

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
            var core = CoreRuntime.Instance;
            if (core == null) return "[E] Rest";
            int caseId = core.Session.CurrentCase;
            int remaining = core.Session.GetRemainingRests(caseId);
            if (!string.IsNullOrEmpty(_statusMessage)) return _statusMessage;
            if (remaining <= 0) return "Rest used up for this case";
            float cd = core.Session.RestCooldownRemaining(Time.unscaledTime);
            if (cd > 0.5f) return $"Rest cooling down ({Mathf.CeilToInt(cd)}s)";
            if (core.Focus.Percent >= core.Focus.Maximum) return "Focus Full";
            return $"[E] Rest ({remaining}/{FocusTuning.RestMaxPerCase} left)";
        }

        public override void Interact(HubContext ctx)
        {
            base.Interact(ctx);
            // Register once so UiBlocked (and the journal's open gate) covers
            // the resting state — resting blocks interaction like an overlay.
            if (ctx != null && _restBlocker == null)
            {
                _restBlocker = () => IsResting;
                ctx.AddUiBlocker(_restBlocker);
                _context = ctx;
            }
            if (IsResting) StopResting();
            else StartResting();
        }

        /// <summary>Begins the rest loop; enforces per-case limit, cooldown, and gain cap.</summary>
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
            _sessionTargetFocus = Mathf.Min(
                core.Focus.Maximum,
                core.Focus.Percent + FocusTuning.RestGainCapPerUse);

            IsResting = true;
            _restStartFrame = Time.frameCount;
            if (restCue != null) core.Audio?.PlaySfx(restCue);
            if (_player == null) _player = FindAnyObjectByType<HubPlayerController>();
            if (_player != null) _player.enabled = false;
            if (restingIndicator != null) restingIndicator.SetActive(true);
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
            core.Focus.TickRest(Time.deltaTime);

            // Skip the frame rest started on — the interact press that
            // opened it would otherwise cancel immediately.
            if (Time.frameCount == _restStartFrame) return;
            var kb = Keyboard.current;
            if (kb != null && (kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame ||
                               kb.wKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame ||
                               kb.sKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame ||
                               kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame ||
                               kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame))
            {
                StopResting();
                return;
            }

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

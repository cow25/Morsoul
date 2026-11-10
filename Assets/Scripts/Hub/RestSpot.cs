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

        void Reset()
        {
            promptText = "[E] Rest";
        }

        void Awake()
        {
            _player = FindAnyObjectByType<HubPlayerController>();
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

        /// <summary>Begins the rest loop; no-op when already resting or Focus is full.</summary>
        public void StartResting()
        {
            if (IsResting) return;
            var core = CoreRuntime.Instance;
            if (core == null || core.Focus.Percent >= core.Focus.Maximum) return;
            IsResting = true;
            _restStartFrame = Time.frameCount;
            if (restCue != null) core.Audio?.PlaySfx(restCue);
            if (_player == null) _player = FindAnyObjectByType<HubPlayerController>();
            if (_player != null) _player.enabled = false;
            if (restingIndicator != null) restingIndicator.SetActive(true);
        }

        /// <summary>Stops resting; safe to call even when not resting.</summary>
        public void StopResting()
        {
            if (!IsResting) return;
            IsResting = false;
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

            if (core.Focus.Percent >= core.Focus.Maximum)
            {
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

using Morsoul.Core;
using Morsoul.Dialogue;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// One-shot arrival beat for a hub scene (Narrative interludes I.1–I.3):
    /// on the first frame after scene load, when the required session state
    /// is met (default: the campaign reached Case 5, i.e. post-Case-4
    /// backtracking) and the once-flag is unset, plays the assigned
    /// DialogueData through the shared hub dialogue controller and latches
    /// the flag so it never repeats.
    /// </summary>
    public class HubArrivalTrigger : MonoBehaviour
    {
        /// <summary>Dialogue played on arrival (e.g. the I.1 Guildmaster's Audit).</summary>
        public DialogueData dialogue;
        /// <summary>Session flag latched after the beat plays.</summary>
        public string playedFlag = "Interlude.Played";
        /// <summary>Arrival only triggers once Session.CurrentCase reaches this value (5 = after Case 4).</summary>
        public int minCurrentCase = 5;
        /// <summary>Optional additional session flag required before playing.</summary>
        public string requiredFlag;
        /// <summary>Frame delay before starting so panels/spawns finish initializing.</summary>
        public int delayFrames = 1;

        HubContext _context;
        int _armedFrame = -1;
        bool _fired;

        void Awake()
        {
            _context = FindAnyObjectByType<HubContext>();
        }

        void OnEnable()
        {
            _armedFrame = Time.frameCount + Mathf.Max(0, delayFrames);
        }

        void Update()
        {
            if (_fired || _armedFrame < 0 || Time.frameCount < _armedFrame) return;
            _fired = true;

            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            if (session == null || dialogue == null) return;
            if (session.CurrentCase < minCurrentCase) return;
            if (session.HasFlag(playedFlag)) return;
            if (!string.IsNullOrEmpty(requiredFlag) && !session.HasFlag(requiredFlag)) return;

            var controller = _context != null ? _context.dialogue : null;
            if (controller == null)
            {
                Debug.LogWarning($"HubArrivalTrigger '{name}': no HubContext.dialogue wired.", this);
                return;
            }
            // Latch the once-flag only after the beat actually completes so a
            // failed or interrupted playback can still retry on the next load.
            void OnEnded()
            {
                controller.DialogueEnded -= OnEnded;
                session.SetFlag(playedFlag);
            }
            controller.DialogueEnded += OnEnded;

            if (!_context.PlayDialogue(dialogue))
            {
                controller.DialogueEnded -= OnEnded;
                Debug.LogWarning($"HubArrivalTrigger '{name}': arrival dialogue failed to start.", this);
            }
        }
    }
}

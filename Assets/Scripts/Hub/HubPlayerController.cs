using System.Collections.Generic;
using Morsoul.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Morsoul.Hub
{
    /// <summary>
    /// Top-down 2D player controller for hub scenes. WASD movement via the
    /// Input System (Keyboard.current), LeftShift sprint, E to interact with
    /// the nearest <see cref="Interactable2D"/> found by an OverlapCircleAll
    /// cast. While the shared dialogue panel is playing, movement and
    /// interaction are suspended (E then advances dialogue instead).
    /// Camera smoothing is intentionally NOT handled here — use Cinemachine
    /// or CameraFollow2D.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(YSort2D))]
    public class HubPlayerController : MonoBehaviour
    {
        public float moveSpeed = 3.5f;
        public float sprintMultiplier = 1.5f;
        /// <summary>Shared hub services handed to interactables.</summary>
        public HubContext context;
        /// <summary>Radius of the interactable search circle.</summary>
        public float interactRadius = 1.5f;
        /// <summary>Layers searched by the interactable cast; defaults to everything.</summary>
        public LayerMask interactLayers = ~0;
        [Header("Audio")]
        [Tooltip("Footstep one-shots picked at random while moving; empty stays silent.")]
        public AudioClip[] footstepClips;
        /// <summary>Seconds between footstep clips while walking (sprint shortens it).</summary>
        public float stepIntervalSeconds = 0.42f;

        Rigidbody2D _rb;
        readonly List<Interactable2D> _candidates = new List<Interactable2D>();
        readonly List<Vector2> _candidatePositions = new List<Vector2>();
        Interactable2D _prompted;
        PlayerFacing2D _facing = PlayerFacing2D.Down;
        float _stepTimer;

        /// <summary>Current normalized move input (zero while dialogue plays).</summary>
        public Vector2 MoveInput { get; private set; }
        /// <summary>Last non-zero movement direction, persisted for save/restore.</summary>
        public PlayerFacing2D CurrentFacing => _facing;
        /// <summary>Nearest interactable currently in range, if any.</summary>
        public Interactable2D CurrentTarget { get; private set; }

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            var body = GetComponent<BoxCollider2D>();
            if (body == null)
            {
                body = gameObject.AddComponent<BoxCollider2D>();
                body.size = new Vector2(0.8f, 0.8f);
            }
            if (GetComponent<YSort2D>() == null)
                gameObject.AddComponent<YSort2D>();
            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            if (session != null) _facing = session.PlayerFacing2D;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Also treat the frame a blocking overlay ended as busy: the same
            // E press that closed the panel must not re-trigger the interactable.
            bool dialoguePlaying = context != null && context.UiBlocked
                || context != null && context.dialogue != null
                   && context.dialogue.LastEndFrame == Time.frameCount
                || context != null && context.travelMenu != null
                   && context.travelMenu.LastCloseFrame == Time.frameCount;
            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            bool cutsceneActive = session != null && !string.IsNullOrEmpty(session.ActiveCutsceneId);
            MoveInput = dialoguePlaying || cutsceneActive
                ? Vector2.zero
                : ReadMoveInput(keyboard);
            if (!MoveInput.Equals(Vector2.zero)) _facing = FacingFrom(MoveInput);
            SyncPlayerContext();

            UpdatePromptTarget();
            TickFootsteps(keyboard);

            if (!dialoguePlaying && !cutsceneActive && keyboard.eKey.wasPressedThisFrame &&
                CurrentTarget != null)
                CurrentTarget.Interact(context);
        }

        void FixedUpdate()
        {
            var keyboard = Keyboard.current;
            bool sprint = !MoveInput.Equals(Vector2.zero)
                          && keyboard != null
                          && keyboard.leftShiftKey.isPressed;
            float speed = moveSpeed * (sprint ? sprintMultiplier : 1f);
            _rb.linearVelocity = MoveInput.normalized * speed;
        }

        static Vector2 ReadMoveInput(Keyboard keyboard)
        {
            var input = Vector2.zero;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.wKey.isPressed) input.y += 1f;
            return input;
        }

        static PlayerFacing2D FacingFrom(Vector2 input)
        {
            return Mathf.Abs(input.x) > Mathf.Abs(input.y)
                ? (input.x > 0f ? PlayerFacing2D.Right : PlayerFacing2D.Left)
                : (input.y > 0f ? PlayerFacing2D.Up : PlayerFacing2D.Down);
        }

        void SyncPlayerContext()
        {
            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            session?.SetPlayerContext(SceneMode.Mode2D,
                SceneManager.GetActiveScene().name, transform.position, _facing);
        }

        void UpdatePromptTarget()
        {
            _candidates.Clear();
            _candidatePositions.Clear();

            // No targeting while a blocking overlay is open; the prompt hides too.
            if (context != null && context.UiBlocked)
            {
                if (_prompted != null)
                {
                    _prompted.SetPromptVisible(false);
                    _prompted = null;
                }
                CurrentTarget = null;
                return;
            }

            var hits = Physics2D.OverlapCircleAll(transform.position, interactRadius, interactLayers);
            foreach (var hit in hits)
            {
                if (hit == null) continue;
                var interactable = hit.GetComponentInParent<Interactable2D>();
                if (interactable == null || _candidates.Contains(interactable)) continue;
                _candidates.Add(interactable);
                _candidatePositions.Add(interactable.transform.position);
            }

            int index = HubTargeting.Nearest(transform.position, _candidatePositions);
            var nearest = index >= 0 ? _candidates[index] : null;

            if (nearest != _prompted)
            {
                if (_prompted != null) _prompted.SetPromptVisible(false);
                _prompted = nearest;
                if (_prompted != null) _prompted.SetPromptVisible(true);
            }
            CurrentTarget = nearest;
        }

        void TickFootsteps(Keyboard keyboard)
        {
            if (MoveInput.Equals(Vector2.zero) ||
                footstepClips == null || footstepClips.Length == 0)
            {
                _stepTimer = 0f;
                return;
            }
            _stepTimer -= Time.deltaTime;
            if (_stepTimer > 0f) return;
            bool sprint = keyboard.leftShiftKey.isPressed;
            _stepTimer = stepIntervalSeconds / (sprint ? sprintMultiplier : 1f);
            var clip = footstepClips[Random.Range(0, footstepClips.Length)];
            if (clip != null) CoreRuntime.Instance?.Audio?.PlaySfx(clip, 0.8f);
        }

        void OnDisable()
        {
            if (_prompted != null)
            {
                _prompted.SetPromptVisible(false);
                _prompted = null;
            }
            CurrentTarget = null;
            MoveInput = Vector2.zero;
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
        }
    }
}

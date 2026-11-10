using System;
using System.Collections;
using Morsoul.Core;
using Morsoul.Dialogue;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Morsoul.Hub
{
    /// <summary>
    /// Trigger + in-world sequencer for the approved Eliza first contact. It
    /// captures the pre-contact checkpoint, locks movement/camera, stages the
    /// authored beats, then unlocks the eastern route and advances to Case 1.
    /// Opens with a cutaway of Eliza surveying and hiding, then holds the
    /// camera on the protagonist for the whole approach.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PrologueElizaIntroController : MonoBehaviour
    {
        public HubContext context;
        public HubPlayerController player;
        public CameraFollow2D cameraFollow;
        [FormerlySerializedAs("elizaWalkStart")]
        public Transform elizaSurveyMarker;
        public Transform eliza;
        public VisualProfile elizaProfile;
        public Transform bushProp;
        [FormerlySerializedAs("diagnosticBeacon")]
        public Transform eyeHighlight;
        public InteractableLabel elizaEmote;
        public InteractableLabel playerEmote;
        public GameObject routeBlocker;
        [Tooltip("Fallback rewind pose used only when the trigger collider is missing.")]
        public Vector3 safeResumePosition = new Vector3(1.5f, 0f, 0f);
        public string nextSceneName = "Hub_LowerMarket";
        public int nextCaseId = 1;
        public bool autoLoadNextScene = true;
        [Min(0.1f)] public float timingScale = 1f;

        [Header("Choreography")]
        [Tooltip("Gap kept between the camera's right edge and the bush when the trigger fires.")]
        public float revealMargin = 0.4f;
        [Tooltip("Never place the trigger east of this X, whatever the aspect; " +
                 "keeps the rewind pose west of its west edge.")]
        public float triggerMaxX = 4.6f;
        [Tooltip("How far west of the trigger's west edge the rewind pose sits.")]
        public float resumeLead = 1.5f;
        [Tooltip("Where the protagonist stops his approach, facing the bush.")]
        public float playerApproachX = 10.4f;
        [Tooltip("The cautious step closer after the Eye flags the bush.")]
        public float playerCloseX = 10.9f;
        public float approachSpeed = 2.6f;
        [Tooltip("How long the camera holds on Eliza surveying before she notices him.")]
        public float surveyHoldSeconds = 1.4f;
        [Tooltip("Seconds of black between the Eliza cutaway and the approach.")]
        public float cutawayFadeSeconds = 0.35f;
        [Tooltip("Concealed spot behind the bush's east edge.")]
        public Vector2 elizaHidePosition = new Vector2(13.35f, 0.5f);
        [Tooltip("Where Eliza stands after stepping back out of cover, defensive.")]
        public Vector2 elizaDefensivePosition = new Vector2(14.15f, 0.6f);
        [Tooltip("South waypoint so her lunge clears the bush instead of clipping it.")]
        public Vector2 elizaRushDetour = new Vector2(13.5f, -0.6f);
        [Tooltip("Where the lunge ends when uninterrupted — just short of the protagonist.")]
        public Vector2 elizaAttackPosition = new Vector2(11.8f, -0.6f);
        public float rushSpeed = 4f;
        public float exitSpeed = 1.7f;
        [Tooltip("How long the protagonist waits before following her out.")]
        public float exitFollowDelay = 0.3f;
        [Tooltip("Waypoint south of the bush so his exit path walks around it.")]
        public Vector2 playerExitBypass = new Vector2(12.4f, -0.65f);
        public float exitX = 24.5f;

        bool _running;
        Rigidbody2D _playerBody;

        void Awake()
        {
            if (context == null) context = FindAnyObjectByType<HubContext>();
            if (player == null) player = FindAnyObjectByType<HubPlayerController>();
            if (cameraFollow == null) cameraFollow = FindAnyObjectByType<CameraFollow2D>();
            if (player != null) _playerBody = player.GetComponent<Rigidbody2D>();
            // Eliza moves during the sequence — she needs the same dynamic
            // Y-sort the player and interactables carry, and her visual profile.
            if (eliza != null)
            {
                if (eliza.GetComponent<YSort2D>() == null)
                    eliza.gameObject.AddComponent<YSort2D>();
                if (eliza.GetComponent<EntityVisual>() == null)
                {
                    var visual = eliza.gameObject.AddComponent<EntityVisual>();
                    visual.profile = elizaProfile;
#if UNITY_EDITOR
                    if (visual.profile == null)
                        visual.profile = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualProfile>("Assets/Art/Profiles/Eliza.asset");
#endif
                    visual.Apply();
                }
            }

            PositionTriggerForReveal();

            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            if (session != null && session.CurrentCase == 0 && !session.PrologueCompleted)
                session.PrologueExitLoaded = true;
            if (routeBlocker != null && session != null)
                routeBlocker.SetActive(!(session.ElizaIntroSeen && session.EasternRouteUnlocked));
        }

        /// <summary>
        /// Slides the trigger so the player crosses it just before the bush's
        /// left edge enters the camera's right edge — Eliza and the bush are
        /// never revealed by ordinary movement before the cutscene staging.
        /// Aspect-aware: narrower views fire later, wider views fire earlier.
        /// </summary>
        internal void PositionTriggerForReveal()
        {
            if (bushProp == null || cameraFollow == null) return;
            var cam = cameraFollow.GetComponent<Camera>();
            var col = GetComponent<Collider2D>();
            if (cam == null || !cam.orthographic || col == null) return;

            float bushLeft = bushProp.position.x;
            var bushRenderer = bushProp.GetComponentInChildren<Renderer>();
            if (bushRenderer != null) bushLeft = bushRenderer.bounds.min.x;

            float playerHalf = 0.3f;
            if (player != null)
            {
                var playerRenderer = player.GetComponentInChildren<Renderer>();
                if (playerRenderer != null) playerHalf = playerRenderer.bounds.extents.x;
            }

            float halfView = cam.orthographicSize * cam.aspect;
            // Contact happens when the player's leading edge reaches the
            // trigger's west edge; at that moment the camera's right edge
            // must still be margin short of the bush.
            float contactX = bushLeft - halfView - revealMargin;
            float triggerHalf = col.bounds.extents.x;
            float centerX = contactX + triggerHalf + playerHalf;
            var position = transform.position;
            transform.position = new Vector3(Mathf.Min(centerX, triggerMaxX), position.y, position.z);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_running || other == null) return;
            if (player != null && other.GetComponentInParent<HubPlayerController>() != player) return;
            if (player == null && !other.CompareTag("Player")) return;
            Begin();
        }

        public void Begin()
        {
            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            if (_running || !PrologueTransitionRules.CanTriggerElizaIntro(session))
            {
                if (!_running)
                    Debug.LogWarning($"PrologueElizaIntroController '{name}': trigger ignored " +
                                     $"(session={session != null}, exitLoaded={session != null && session.PrologueExitLoaded}, " +
                                     $"seen={session != null && session.ElizaIntroSeen}, " +
                                     $"activeCutscene='{(session != null ? session.ActiveCutsceneId : "")}').", this);
                return;
            }
            _running = true;
            var collider = GetComponent<Collider2D>();
            if (collider != null) collider.enabled = false;
            CapturePreContactCheckpoint(session);
            LockPlayer();
            StartCoroutine(RunSequence(session));
        }

        void CapturePreContactCheckpoint(SessionState session)
        {
            // Record the rewind pose in the session WITHOUT moving the player:
            // teleporting the transform here produced a visible backward
            // "leap" on trigger. The pose stays west of the trigger's west
            // edge so an interrupted run replays by simply walking east again.
            session.SetPlayerContext(SceneMode.Mode2D,
                SceneManager.GetActiveScene().name,
                ResumePosition(), PlayerFacing2D.Right);
            session.SetCutsceneContext("", SessionState.ElizaPreContactCheckpointId);
            GameStateManager.Ensure().WriteCheckpoint(
                SessionState.ElizaPreContactCheckpointId, null, preserveRestoredPose: true);

            session.ElizaIntroTriggered = true;
            session.SetCutsceneContext(SessionState.ElizaIntroCutsceneId,
                SessionState.ElizaPreContactCheckpointId);
            GameStateManager.Ensure().WriteCheckpoint(
                "ElizaIntroStart", null, preserveRestoredPose: true);
        }

        Vector3 ResumePosition()
        {
            var col = GetComponent<Collider2D>();
            if (col != null)
                return new Vector3(col.bounds.min.x - resumeLead, 0f, 0f);
            return safeResumePosition;
        }

        void LockPlayer()
        {
            if (_playerBody == null && player != null)
                _playerBody = player.GetComponent<Rigidbody2D>();
            if (_playerBody != null) _playerBody.linearVelocity = Vector2.zero;
            if (player != null) player.enabled = false;
        }

        IEnumerator RunSequence(SessionState session)
        {
            var dialogue = context != null ? context.dialogue : null;
            var playerTransform = player != null ? player.transform : null;

            // A. Cutaway — Eliza surveying the area near the bush. The player
            // watches her notice him and dive into cover before the camera
            // comes back and he walks in.
            StageEncounter();
            yield return FadeTo(dialogue, 1f, cutawayFadeSeconds);
            if (cameraFollow != null && eliza != null)
                cameraFollow.SnapToTarget(eliza);
            yield return FadeTo(dialogue, 0f, cutawayFadeSeconds);
            yield return Wait(surveyHoldSeconds);

            // Movement to the west — she startles and hides in the bush.
            yield return Emote(elizaEmote, "!", 1.0f);
            yield return MoveTo(eliza,
                new Vector3(elizaHidePosition.x, elizaHidePosition.y, 0f), 0.3f);
            SetActive(eliza, false);
            yield return Wait(0.5f);

            // B. Back to the protagonist — a normal walk toward the bush with
            // the camera on him the whole way.
            yield return FadeTo(dialogue, 1f, cutawayFadeSeconds);
            if (cameraFollow != null && player != null)
                cameraFollow.SnapToTarget(player.transform);
            yield return FadeTo(dialogue, 0f, cutawayFadeSeconds);
            yield return MoveToSpeed(playerTransform,
                new Vector3(playerApproachX, 0f, 0f), approachSpeed);

            yield return Emote(playerEmote, "...", 1.2f);
            yield return Line("Golem (thought)", "Everything looks different through this eye.");

            // The Eye detects her concealment; the Golem cautiously approaches.
            SetActive(eyeHighlight, true);
            yield return Line("Golem (thought)", "Someone is hiding in that bush.");
            yield return MoveToSpeed(playerTransform,
                new Vector3(playerCloseX, 0f, 0f), approachSpeed * 0.5f);

            // Alarmed, she emerges from behind the bush and steps back, defensive.
            SetActive(eliza, true);
            yield return MoveTo(eliza,
                new Vector3(elizaDefensivePosition.x, elizaDefensivePosition.y, 0f), 0.45f);
            yield return Emote(elizaEmote, "!", 1.2f);
            yield return Line("???", "⟦▓▒ ▓▓▓ ▒▓▓▓ ▓▒!⟧");

            // The Golem attempts to communicate; she cannot parse him either.
            yield return Line("Golem", "Look, wait! Don't hurt me!");
            yield return Emote(elizaEmote, "?", 0.9f);

            // She lunges — and freezes mid-stride the moment the familiar
            // language reaches her.
            var rush = StartCoroutine(ElizaRushes());
            yield return Line("Golem (thought)",
                "Those sounds... the words in the notes. I can make sense of them now.");
            yield return Line("Golem", "Wait. I mean no harm.",
                () => { if (rush != null) StopCoroutine(rush); });
            if (rush != null) StopCoroutine(rush);
            yield return Emote(elizaEmote, "?", 1.5f);
            yield return Line("???", "Why should I listen to a monster like you?");
            yield return Line("Golem",
                "I have no weapons on me! At least hear me out first!");
            yield return Emote(elizaEmote, "...", 1.5f);
            SetActive(eyeHighlight, false);

            // B. Narrative bridge — fade to black, explanation card, fade back in.
            yield return FadeTo(dialogue, 1f, 0.8f);
            if (dialogue != null)
                dialogue.SetFadeText("You explained your situation to the wary stranger.\n\n(E) continue");
            yield return Line("", "You explained your situation to the wary stranger.");
            if (dialogue != null) dialogue.SetFadeText("");
            yield return FadeTo(dialogue, 0f, 0.8f);

            // C. First dialogue — anonymous until she names herself in-line.
            yield return Line("???",
                "Ohh~ Fascinating! The ruins have been inaccessible for as long as I can " +
                "remember. No one knows how to get inside, nor do they have the means " +
                "to open them.");
            yield return Line("???", "You're pretty clever, huh?");
            yield return Line("???",
                "Name's Eliza. I'm an information broker at the Guild.");
            yield return Line("Eliza", "What's yours? Do you even have one?");

            if (dialogue != null && dialogue.ShowNamePrompt())
            {
                string enteredName = null;
                void OnName(string n) => enteredName = n;
                dialogue.NameSubmitted += OnName;
                try
                {
                    while (enteredName == null) yield return null;
                    if (session != null) session.PlayerName = enteredName;
                }
                finally
                {
                    dialogue.NameSubmitted -= OnName;
                    dialogue.HideNamePrompt();
                }
            }

            yield return Line("Eliza", "{PlayerName}, huh? Nice to meet you!");
            yield return Line("Eliza",
                "I don't know why, but I had a strong urge to survey the ruins today...");
            yield return Line("Eliza", "Must be fate, meeting you here!");
            yield return Line("Eliza",
                "Come on, follow me back to the Guild. I have to report my findings.");
            yield return Line("Eliza",
                "I'll answer your questions along the way. We've got time.");

            // D. Exit — she leads, he follows a step behind at matching pace,
            // both on sharp axis-aligned paths around the bush.
            var elizaExit = StartCoroutine(MoveToSpeed(eliza,
                new Vector3(exitX, 0f, 0f), exitSpeed));
            yield return Wait(exitFollowDelay);
            var golemExit = StartCoroutine(GolemExits(playerTransform));
            yield return elizaExit;
            yield return golemExit;
            yield return FadeTo(dialogue, 1f, 0.8f);

            session.ElizaIntroSeen = true;
            session.EasternRouteUnlocked = true;
            session.PrologueCompleted = true;
            session.CurrentCase = nextCaseId;
            session.SetFlag("Prologue_Completed");
            session.SetCutsceneContext("", "");
            if (routeBlocker != null) routeBlocker.SetActive(false);
            session.SetPlayerContext(SceneMode.Mode2D,
                SceneManager.GetActiveScene().name,
                player != null ? player.transform.position : safeResumePosition,
                PlayerFacing2D.Right);

            if (dialogue != null)
                dialogue.SetFadeText("PROLOGUE COMPLETED — PROCEEDING TO ZONE A: ASTERVEIL LOWER MARKET");
            yield return Wait(3f);
            if (autoLoadNextScene && !string.IsNullOrEmpty(nextSceneName))
                RequestNextScene();
            else
            {
                if (dialogue != null) yield return dialogue.FadeTo(0f, 0.5f);
                if (cameraFollow != null && player != null)
                    cameraFollow.target = player.transform;
                if (player != null) player.enabled = true;
            }
            _running = false;
        }

        /// <summary>
        /// Eliza is already out surveying (off the right edge of the view);
        /// the camera just needs to be centred on the protagonist. No fade,
        /// no teleport — the scripted walk continues the player's own motion.
        /// </summary>
        internal void StageEncounter()
        {
            if (eliza != null)
            {
                eliza.position = elizaSurveyMarker != null
                    ? elizaSurveyMarker.position
                    : new Vector3(14f, 0f, 0f);
                eliza.gameObject.SetActive(true);
            }
            if (cameraFollow != null && player != null)
                cameraFollow.SnapToTarget(player.transform);
        }

        /// <summary>The hostile lunge: south around the bush so she doesn't
        /// clip through it, then straight at the protagonist. Stopped wherever
        /// it is when the familiar language lands.</summary>
        IEnumerator ElizaRushes()
        {
            yield return MoveToSpeed(eliza,
                new Vector3(elizaRushDetour.x, elizaRushDetour.y, 0f), rushSpeed);
            yield return MoveToSpeed(eliza,
                new Vector3(elizaAttackPosition.x, elizaAttackPosition.y, 0f), rushSpeed);
        }

        /// <summary>He skirts the bush's south edge (vertical leg first) then
        /// walks straight east out of the frame.</summary>
        IEnumerator GolemExits(Transform playerTransform)
        {
            yield return MoveToSpeed(playerTransform,
                new Vector3(playerExitBypass.x, playerExitBypass.y, 0f),
                exitSpeed, horizontalFirst: false);
            yield return MoveToSpeed(playerTransform,
                new Vector3(exitX, playerExitBypass.y, 0f), exitSpeed);
        }

        IEnumerator FadeTo(DialogueController dialogue, float targetOpacity, float seconds)
        {
            if (dialogue != null) yield return dialogue.FadeTo(targetOpacity, seconds / timingScale);
            else yield return Wait(seconds);
        }

        void RequestNextScene()
        {
            var core = CoreRuntime.Instance;
            if (core == null)
            {
                SceneManager.LoadScene(nextSceneName);
                return;
            }
            core.Bus.Publish(new CheckpointRequestedEvent
            {
                Reason = "PrologueCompleted",
                NextSceneName = nextSceneName,
            });
            core.Bus.Publish(new SceneLoadRequestedEvent { SceneName = nextSceneName });
        }

        IEnumerator Line(string speaker, string text, Action whenShown = null)
        {
            var dialogue = context != null ? context.dialogue : null;
            if (dialogue == null) yield break;
            var previous = dialogue.dialogueData;
            var data = ScriptableObject.CreateInstance<DialogueData>();
            data.id = SessionState.ElizaIntroCutsceneId;
            data.startNodeId = "line";
            data.portraits = previous != null ? previous.portraits : null;
            data.nodes = new[]
            {
                new DialogueNodeData { id = "line", speakerName = speaker, text = text },
            };
            try
            {
                if (!context.PlayDialogue(data)) yield break;
                whenShown?.Invoke();
                while (dialogue.IsPlaying) yield return null;
            }
            finally
            {
                dialogue.Stop();
                dialogue.SetDialogue(previous);
                if (Application.isPlaying) Destroy(data);
                else DestroyImmediate(data);
            }
        }

        IEnumerator Emote(InteractableLabel label, string text, float seconds)
        {
            if (label != null)
            {
                label.text = text;
                label.gameObject.SetActive(true);
            }
            yield return Wait(seconds);
            if (label != null) label.gameObject.SetActive(false);
        }

        IEnumerator MoveToSpeed(Transform target, Vector3 destination, float speed,
            bool horizontalFirst = true)
        {
            float path = target == null ? 0f : AxisMove.PathLength(target.position, destination);
            yield return MoveTo(target, destination,
                speed > 0.01f ? path / speed : 0.01f, horizontalFirst);
        }

        /// <summary>Axis-aligned only: diagonal destinations run as an L-path
        /// (horizontal leg first unless told otherwise) at constant speed.</summary>
        IEnumerator MoveTo(Transform target, Vector3 destination, float duration,
            bool horizontalFirst = true)
        {
            yield return AxisMove.Move(target, destination,
                Mathf.Max(0.01f, duration / timingScale),
                (to, legDuration) => MoveLeg(target, to, legDuration),
                horizontalFirst);
        }

        IEnumerator MoveLeg(Transform target, Vector3 destination, float duration)
        {
            if (target == null) yield break;
            Vector3 start = target.position;
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                // Linear, not eased: constant leg speed is what makes the
                // 90° walk read as classic tile-RPG movement and keeps two
                // walkers at equal speeds actually synchronized.
                MoveTarget(target, Vector3.Lerp(start, destination,
                    Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            MoveTarget(target, destination);
        }

        void MoveTarget(Transform target, Vector3 position)
        {
            if (player != null && target == player.transform && _playerBody != null)
                _playerBody.position = position;
            target.position = position;
        }

        IEnumerator Wait(float seconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, seconds / timingScale));
        }

        static void SetActive(Transform target, bool active)
        {
            if (target != null) target.gameObject.SetActive(active);
        }
    }
}

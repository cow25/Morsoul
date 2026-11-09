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
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PrologueElizaIntroController : MonoBehaviour
    {
        public HubContext context;
        public HubPlayerController player;
        public CameraFollow2D cameraFollow;
        public Transform cameraTarget;
        public Transform elizaCameraTarget;
        public Transform eliza;
        public Transform elizaWalkStart;
        public Transform bushProp;
        [FormerlySerializedAs("diagnosticBeacon")]
        public Transform eyeHighlight;
        public InteractableLabel elizaEmote;
        public InteractableLabel playerEmote;
        public GameObject routeBlocker;
        public Vector3 safeResumePosition = new Vector3(6f, 0f, 0f);
        public string nextSceneName = "Hub_LowerMarket";
        public int nextCaseId = 1;
        public bool autoLoadNextScene = true;
        [Min(0.1f)] public float timingScale = 1f;

        bool _running;
        Rigidbody2D _playerBody;

        void Awake()
        {
            if (context == null) context = FindAnyObjectByType<HubContext>();
            if (player == null) player = FindAnyObjectByType<HubPlayerController>();
            if (cameraFollow == null) cameraFollow = FindAnyObjectByType<CameraFollow2D>();
            if (player != null) _playerBody = player.GetComponent<Rigidbody2D>();

            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            if (session != null && session.CurrentCase == 0 && !session.PrologueCompleted)
                session.PrologueExitLoaded = true;
            if (routeBlocker != null && session != null)
                routeBlocker.SetActive(!(session.ElizaIntroSeen && session.EasternRouteUnlocked));
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
            if (cameraFollow != null && cameraTarget != null)
                cameraFollow.target = cameraTarget;
            StartCoroutine(RunSequence(session));
        }

        void CapturePreContactCheckpoint(SessionState session)
        {
            SetPlayerPosition(safeResumePosition);
            session.SetPlayerContext(SceneMode.Mode2D,
                SceneManager.GetActiveScene().name,
                safeResumePosition, PlayerFacing2D.Right);
            session.SetCutsceneContext("", SessionState.ElizaPreContactCheckpointId);
            GameStateManager.Ensure().WriteCheckpoint(
                SessionState.ElizaPreContactCheckpointId, null, preserveRestoredPose: true);

            session.ElizaIntroTriggered = true;
            session.SetCutsceneContext(SessionState.ElizaIntroCutsceneId,
                SessionState.ElizaPreContactCheckpointId);
            GameStateManager.Ensure().WriteCheckpoint(
                "ElizaIntroStart", null, preserveRestoredPose: true);
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

            // A. Cutaway — Eliza surveying the path toward the ruins.
            yield return FadeTo(dialogue, 1f, 0.5f);
            if (cameraFollow != null && elizaCameraTarget != null)
                cameraFollow.target = elizaCameraTarget;
            yield return FadeTo(dialogue, 0f, 0.5f);

            SetActive(eliza, true);
            if (elizaWalkStart != null && eliza != null)
                eliza.position = elizaWalkStart.position;
            yield return MoveTo(eliza, new Vector3(12f, 0f, 0f), 2f);
            yield return Emote(elizaEmote, "!", 1.5f);
            // She mistakes the approaching figure for a monster and dives into the bush.
            yield return MoveTo(eliza,
                bushProp != null ? bushProp.position : new Vector3(12f, 0f, 0f), 0.5f);
            SetActive(eliza, false);

            yield return FadeTo(dialogue, 1f, 0.4f);
            if (cameraFollow != null && cameraTarget != null)
                cameraFollow.target = cameraTarget;
            yield return FadeTo(dialogue, 0f, 0.4f);

            // B. The protagonist enters, still acclimating to the Golem Eye.
            yield return MoveTo(player != null ? player.transform : null,
                new Vector3(9f, 0f, 0f), 2f);
            yield return Emote(playerEmote, "...", 1.5f);
            yield return Line("System",
                "[OCULAR CALIBRATION: ambient mana saturation nominal. Terrain survey in progress.]", 3f);

            // The Eye detects her concealment; the Golem cautiously approaches.
            SetActive(eyeHighlight, true);
            yield return Line("System",
                "[ANOMALY DETECTED: biological signature concealed — vegetation, east. " +
                "Heart rate: elevated. Posture: watchful.]", 4f);
            yield return MoveTo(player != null ? player.transform : null,
                new Vector3(10f, 0f, 0f), 1.5f);

            // Alarmed, she bursts from the bush, ready to attack.
            SetActive(eliza, true);
            yield return MoveTo(eliza, new Vector3(11f, 0f, 0f), 0.7f);
            yield return Emote(elizaEmote, "!", 1.2f);
            yield return Line("???", "\u27E6\u2593\u2592 \u2593\u2593\u2593 \u2592\u2593\u2593\u2593 \u2593\u2592!\u27E7", 3f);

            // The Golem attempts to communicate; neither can parse the other.
            yield return Line("Golem", "\u27E6\u2593\u2593\u2592 \u2593 \u2593\u2593\u2592 \u2592\u2593\u27E7", 3f);
            yield return Emote(elizaEmote, "?", 1.5f);

            // The Eye completes spoken decipherment against the recovered log corpus.
            yield return Line("System",
                "[LINGUISTIC DECRYPTION: correlating phoneme stream against recovered " +
                "textual corpus (Torn Desk Logs, C0_C1)... PATTERN MATCH: modern Agarthan. " +
                "Spoken comprehension: ONLINE.]", 5f);

            // Just as she moves to strike, it answers in her language.
            yield return Line("Golem", "Wait. I mean no harm.", 3f);
            yield return Emote(elizaEmote, "?", 1.5f);
            yield return Line("Eliza", "It... it talks? Relics don't talk—", 3f);
            yield return Line("Golem",
                "I awoke inside those ruins. I do not remember what I am.", 4f);
            yield return Emote(elizaEmote, "...", 1.5f);
            SetActive(eyeHighlight, false);

            // C. Narrative bridge — fade to black, explanation card, fade back in.
            yield return FadeTo(dialogue, 1f, 0.8f);
            if (dialogue != null)
                dialogue.SetFadeText("You explained your situation to the wary stranger.");
            yield return Wait(2.5f);
            if (dialogue != null) dialogue.SetFadeText("");
            yield return FadeTo(dialogue, 0f, 0.8f);

            // D. First dialogue — wary, then the broker's curiosity wins.
            yield return Line("Eliza",
                "Ohh~ Fascinating! The ruins have been inaccessible for as long as I can " +
                "remember. No one knows how to get inside, nor do they have the means " +
                "to open them.", 6f);
            yield return Line("Eliza", "You're pretty clever, huh?", 3f);
            yield return Line("Eliza",
                "Name's Eliza. I'm an information broker at the Guild.", 4f);
            yield return Line("Eliza", "What's yours? Do you even have one?", 3f);

            if (dialogue != null && dialogue.ShowNamePrompt())
            {
                string enteredName = null;
                void OnName(string n) => enteredName = n;
                dialogue.NameSubmitted += OnName;
                while (enteredName == null) yield return null;
                dialogue.NameSubmitted -= OnName;
                if (session != null) session.PlayerName = enteredName;
            }

            yield return Line("Eliza", "{PlayerName}, huh? Nice to meet you!", 3f);
            yield return Line("Eliza",
                "I don't know why, but I had a strong urge to survey the ruins today...", 5f);
            yield return Line("Eliza", "Must be fate, meeting you here!", 3f);
            yield return Line("Eliza",
                "Come on, follow me back to the Guild. I have to report my findings.", 5f);
            yield return Line("Eliza",
                "I'll answer your questions along the way. We've got time.", 4f);

            // E. Exit — she walks out; the Golem follows two paces behind.
            yield return MoveTo(eliza, new Vector3(13f, 0f, 0f), 1f);
            var follow = StartCoroutine(MoveTo(player != null ? player.transform : null,
                new Vector3(24f, 0f, 0f), 7f));
            yield return MoveTo(eliza, new Vector3(24f, 0f, 0f), 5f);
            yield return follow;
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

        IEnumerator Line(string speaker, string text, float seconds)
        {
            var dialogue = context != null ? context.dialogue : null;
            if (dialogue != null) dialogue.ShowStandaloneLine(speaker, text);
            yield return Wait(seconds);
            if (dialogue != null) dialogue.Stop();
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

        IEnumerator MoveTo(Transform target, Vector3 destination, float duration)
        {
            if (target == null) yield break;
            Vector3 start = target.position;
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration / timingScale);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                MoveTarget(target, Vector3.Lerp(start, destination,
                    Mathf.SmoothStep(0f, 1f, elapsed / duration)));
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

        void SetPlayerPosition(Vector3 position)
        {
            if (player == null) return;
            if (_playerBody != null)
            {
                _playerBody.position = position;
                _playerBody.linearVelocity = Vector2.zero;
            }
            player.transform.position = position;
        }

        static void SetActive(Transform target, bool active)
        {
            if (target != null) target.gameObject.SetActive(active);
        }
    }
}

using System.Collections;
using Morsoul.Core;
using Morsoul.Dialogue;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        public Transform eliza;
        public Transform keyProp;
        public Transform diagnosticBeacon;
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
            if (_running || !PrologueTransitionRules.CanTriggerElizaIntro(session)) return;
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
            yield return Wait(5f);

            SetActive(eliza, true);
            yield return MoveTo(eliza, new Vector3(10f, 0f, 0f), 2f);
            yield return Emote(elizaEmote, "!", 1.5f);
            yield return Line("Eliza",
                "Hold it right there, scrap-heap! Hands where I can see them!", 4f);

            yield return Emote(playerEmote, "...", 2f);
            yield return Line("Eliza",
                "Don't play dumb. Machines don't dig themselves out of sealed ruins. " +
                "Who sent you, and why were you buried down there?", 6f);
            yield return MoveTo(eliza, new Vector3(9f, 0f, 0f), 2f);

            SetActive(keyProp, true);
            SetActive(diagnosticBeacon, true);
            yield return Line("Golem",
                "[DIAGNOSTIC BEACON ACTIVE: Unit Type: Forensic & Analytical " +
                "Assistance Construct. Combat Subroutines: OFFLINE. Core Identity: UNKNOWN.]",
                5f);

            yield return MoveTo(eliza, new Vector3(8.5f, 0f, 0f), 2f);
            yield return Emote(elizaEmote, "?", 1.5f);
            yield return Line("Eliza",
                "Forensic assistance...? You don't have an attack core at all, do you?", 4f);
            yield return Line("Eliza",
                "Wait... that ocular frequency. Why does looking at you make my chest ache...?", 5f);

            yield return Line("Eliza",
                "An old surveyor key? You didn't smash your way through that seal... " +
                "you solved the manual lock mechanism.", 5f);
            SetActive(keyProp, false);
            SetActive(diagnosticBeacon, false);
            yield return Line("Eliza",
                "An ancient machine that solves logic puzzles instead of ripping heads off. " +
                "If the City Watch catches you lumbering down the Imperial Road, they'll " +
                "seize you for scrap metal or sell your mana core to the Alchemist Guild.", 7f);

            yield return Line("Eliza",
                "An old forensic construct with working optical translation... " +
                "Looks like I just found myself an ancient detective.", 5f);
            yield return Emote(elizaEmote, "...", 1.5f);
            yield return Line("Eliza",
                "...Why did saying that feel so natural?", 3f);
            yield return Line("Eliza",
                "Lucky for you, my pockets are empty and the Investigator's Guild pays " +
                "bounties for sharp eyes. Pull that hood down over your brass plates, " +
                "keep your mouth shut, and follow me.", 7f);
            yield return Line("Eliza",
                "Name's Eliza Astra. Let's see what Asterveil makes of you... partner.", 5f);

            yield return MoveTo(eliza, new Vector3(24f, 0f, 0f), 6f);
            yield return MoveTo(player != null ? player.transform : null,
                new Vector3(8f, 0f, 0f), 2f);

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

            yield return Line("",
                "PROLOGUE COMPLETED — PROCEEDING TO ZONE A: ASTERVEIL LOWER MARKET", 4f);
            if (autoLoadNextScene && !string.IsNullOrEmpty(nextSceneName))
                RequestNextScene();
            else
            {
                if (cameraFollow != null && player != null)
                    cameraFollow.target = player.transform;
                if (player != null) player.enabled = true;
            }
            _running = false;
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

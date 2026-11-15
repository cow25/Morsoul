using System.Collections;
using Morsoul.Core;
using Morsoul.Dialogue;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Hub
{
    /// <summary>
    /// First-visit arrival beat for the Lower Market: straight out of the
    /// prologue, Eliza leads the player in from the west entry, establishes
    /// the market and the Guild hall through ordinary (player-paced) dialogue,
    /// then escorts them inside — latching the flags that trigger the Guild
    /// introduction. Replays cleanly if the save was made mid-sequence: the
    /// arrived flag only latches once the transition is underway.
    /// </summary>
    public class MarketArrivalController : MonoBehaviour
    {
        public HubContext context;
        public HubPlayerController player;
        /// <summary>Eliza stand-in quad staged in the scene; activated for the walk.</summary>
        public Transform eliza;
        /// <summary>Played once at the market entry (welcome beats).</summary>
        public DialogueData entryDialogue;
        /// <summary>Played at the Guild door (escort-in beats).</summary>
        public DialogueData doorDialogue;
        public string guildSceneName = "Hub_Guild";
        [Min(0.1f)] public float timingScale = 1f;

        [Header("Staging")]
        // All waypoints sit on the drawn paths: entry on the west street
        // (y = -1), door step on the guild branch (x = 3).
        public Vector3 playerEntry = new Vector3(-8.5f, -1f, 0f);
        public Vector3 elizaEntry = new Vector3(-7.4f, -1f, 0f);
        public Vector3 elizaLead = new Vector3(-4.5f, -1f, 0f);
        public Vector3 elizaDoor = new Vector3(3f, 2.6f, 0f);
        public Vector3 playerDoor = new Vector3(3.4f, 2.35f, 0f);
        public Vector3 elizaInside = new Vector3(3f, 3.4f, 0f);

        Rigidbody2D _playerBody;

        void Start()
        {
            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            if (!GuildIntroRules.CanPlayMarketArrival(session))
            {
                // Latch on any post-prologue visit too so it can never fire
                // on a later market return or a veteran save.
                if (session != null && session.PrologueCompleted)
                    session.SetFlag(GuildIntroRules.ArrivedFlag);
                return;
            }
            if (context == null) context = FindAnyObjectByType<HubContext>();
            if (player == null) player = FindAnyObjectByType<HubPlayerController>();
            _playerBody = player != null ? player.GetComponent<Rigidbody2D>() : null;
            if (player != null) player.enabled = false;
            StartCoroutine(Run(session));
        }

        IEnumerator Run(SessionState session)
        {
            var dialogue = context != null ? context.dialogue : null;

            // Stage under black: Eliza beside the player at the west entry
            // (the plaza spawn stays the default for non-arrival entries).
            yield return FadeTo(dialogue, 1f, 0f);
            if (player != null) MoveTarget(player.transform, playerEntry);
            var cameraFollow = FindAnyObjectByType<CameraFollow2D>();
            if (cameraFollow != null && player != null)
                cameraFollow.SnapToTarget(player.transform);
            if (eliza != null)
            {
                eliza.position = elizaEntry;
                eliza.gameObject.SetActive(true);
            }
            yield return FadeTo(dialogue, 0f, 0.6f);

            // She leads the player in down the street at a matching walking pace,
            // staying just a step ahead, then turns back for the welcome beat.
            const float walkSpeed = 3.5f;
            Vector3 leadOffset = playerEntry - elizaEntry;
            if (leadOffset.sqrMagnitude < 0.25f) leadOffset = new Vector3(-1.4f, 0f, 0f);
            Vector3 playerLead = elizaLead + leadOffset;

            var leadEliza = StartCoroutine(MoveAtSpeed(eliza, elizaEntry, elizaLead, walkSpeed));
            yield return Wait(0.15f);
            var leadPlayer = StartCoroutine(MoveAtSpeed(
                player != null ? player.transform : null, playerEntry, playerLead, walkSpeed));
            yield return leadEliza;
            yield return leadPlayer;
            yield return Play(entryDialogue);

            // Down the street to the Guild hall's bannered door: same lane,
            // the Golem trailing a step behind at the exact same walking speed.
            if (elizaDoor.y > 4f && elizaLead.x < -7.5f)
            {
                // Route along the cobblestone West & North park ring around Park_TownWell (0, -0.3).
                Vector3 elizaWest = new Vector3(-7.8f, elizaLead.y, 0f);
                Vector3 playerWest = new Vector3(-8.3f, playerLead.y, 0f);
                Vector3 elizaNorth = new Vector3(-7.8f, 7.5f, 0f);
                Vector3 playerNorth = new Vector3(-7.2f, 7.5f, 0f);
                Vector3 elizaForecourt = new Vector3(elizaDoor.x, 7.5f, 0f);
                Vector3 playerForecourt = new Vector3(playerDoor.x, 7.0f, 0f);
                yield return WalkPair(eliza, elizaLead, elizaWest, playerLead, playerWest, walkSpeed);
                yield return WalkPair(eliza, elizaWest, elizaNorth, playerWest, playerNorth, walkSpeed);
                yield return WalkPair(eliza, elizaNorth, elizaForecourt, playerNorth, playerForecourt, walkSpeed);
                yield return WalkPair(eliza, elizaForecourt, elizaDoor, playerForecourt, playerDoor, walkSpeed);
            }
            else
            {
                yield return WalkPair(eliza, elizaLead, elizaDoor, playerLead, playerDoor, walkSpeed);
            }
            yield return Play(doorDialogue);

            // She steps inside; the Golem follows — the Guild scene takes over.
            yield return MoveTo(eliza, elizaInside, 0.8f);
            yield return FadeTo(dialogue, 1f, 0.6f);

            session.SetFlag(GuildIntroRules.ArrivedFlag);
            session.SetFlag(GuildIntroRules.IntroPendingFlag);
            RequestGuild();
        }

        void RequestGuild()
        {
            var core = CoreRuntime.Instance;
            if (core != null)
            {
                core.Bus.Publish(new SceneLoadRequestedEvent { SceneName = guildSceneName });
            }
            else
            {
                SceneManager.LoadScene(guildSceneName);
            }
        }

        IEnumerator Play(DialogueData data)
        {
            var dialogue = context != null ? context.dialogue : null;
            if (data == null || dialogue == null) yield break;
            if (!context.PlayDialogue(data)) yield break;
            while (dialogue.IsPlaying) yield return null;
        }

        IEnumerator FadeTo(DialogueController dialogue, float target, float seconds)
        {
            if (dialogue != null) yield return dialogue.FadeTo(target, seconds / timingScale);
            else yield return Wait(seconds);
        }

        IEnumerator WalkPair(Transform elizaTarget, Vector3 elizaFrom, Vector3 elizaTo,
            Vector3 playerFrom, Vector3 playerTo, float walkSpeed)
        {
            var escort = StartCoroutine(MoveAtSpeed(elizaTarget, elizaFrom, elizaTo, walkSpeed));
            yield return Wait(0.15f);
            var follow = StartCoroutine(MoveAtSpeed(
                player != null ? player.transform : null, playerFrom, playerTo, walkSpeed));
            yield return escort;
            yield return follow;
        }

        IEnumerator MoveAtSpeed(Transform target, Vector3 from, Vector3 destination, float unitsPerSecond)
        {
            float dist = Mathf.Abs(destination.x - from.x) + Mathf.Abs(destination.y - from.y);
            float duration = Mathf.Max(0.25f, dist / Mathf.Max(0.5f, unitsPerSecond));
            yield return MoveTo(target, destination, duration);
        }

        /// <summary>Axis-aligned only: diagonal destinations run as an L-path
        /// (horizontal leg first) at constant speed — sharp 90° movement.</summary>
        IEnumerator MoveTo(Transform target, Vector3 destination, float duration)
        {
            yield return AxisMove.Move(target, destination,
                Mathf.Max(0.01f, duration / timingScale),
                (to, legDuration) => MoveLeg(target, to, legDuration));
        }

        IEnumerator MoveLeg(Transform target, Vector3 destination, float duration)
        {
            if (target == null) yield break;
            Vector3 start = target.position;
            Vector2 dir = (Vector2)(destination - start);
            var anim = target.GetComponent<HubCharacterAnimator>();
            if (dir.sqrMagnitude > 0.0001f) anim?.Tick(dir.normalized);
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (dir.sqrMagnitude > 0.0001f) anim?.Tick(dir.normalized);
                MoveTarget(target, Vector3.Lerp(start, destination,
                    Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            MoveTarget(target, destination);
            anim?.Tick(Vector2.zero);
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
    }
}

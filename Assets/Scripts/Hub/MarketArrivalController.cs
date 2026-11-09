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
        public Vector3 playerEntry = new Vector3(-8f, 0f, 0f);
        public Vector3 elizaEntry = new Vector3(-9f, 0.2f, 0f);
        public Vector3 elizaLead = new Vector3(-4.5f, -0.8f, 0f);
        public Vector3 elizaDoor = new Vector3(2.6f, 2.6f, 0f);
        public Vector3 playerDoor = new Vector3(3.6f, 2.6f, 0f);
        public Vector3 elizaInside = new Vector3(3f, 3.3f, 0f);

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
            if (eliza != null)
            {
                eliza.position = elizaEntry;
                eliza.gameObject.SetActive(true);
            }
            yield return FadeTo(dialogue, 0f, 0.6f);

            // She leads a few steps in and turns back to the Golem.
            yield return MoveTo(eliza, elizaLead, 1.6f);
            yield return Play(entryDialogue);

            // Down the street together, to the Guild hall's bannered door.
            var follow = StartCoroutine(MoveTo(
                player != null ? player.transform : null, playerDoor, 4.5f));
            yield return MoveTo(eliza, elizaDoor, 4f);
            yield return follow;
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
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
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
    }
}

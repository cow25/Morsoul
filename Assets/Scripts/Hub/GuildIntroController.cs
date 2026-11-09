using System.Collections;
using Morsoul.Core;
using Morsoul.Dialogue;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// One-shot Guild introduction inside Hub_Guild: played only when the
    /// market arrival handed off control (IntroPending set, IntroSeen unset).
    /// Eliza walks the player past the front desk, up the stairs and into
    /// Ovan's office; all spoken beats run through the shared dialogue
    /// controller at the player's pace. Completing it restores normal hub
    /// exploration — the first mission stays interaction-driven (Ovan's
    /// ordinary NpcInteractable), never auto-granted here.
    /// </summary>
    public class GuildIntroController : MonoBehaviour
    {
        public HubContext context;
        public HubPlayerController player;
        public Transform eliza;
        /// <summary>Played at the front desk (clerk greeting).</summary>
        public DialogueData receptionDialogue;
        /// <summary>Played in Ovan's office (Guildmaster introduction).</summary>
        public DialogueData ovanDialogue;
        [Min(0.1f)] public float timingScale = 1f;

        [Header("Staging")]
        public Vector3 elizaEntry = new Vector3(0.8f, -3f, 0f);
        public Vector3 elizaDesk = new Vector3(1.6f, 0.6f, 0f);
        public Vector3 playerDesk = new Vector3(-0.4f, 0.2f, 0f);
        public Vector3 elizaStairs = new Vector3(6.3f, 2.6f, 0f);
        public Vector3 playerStairs = new Vector3(5.9f, 2.2f, 0f);
        public Vector3 upstairsEliza = new Vector3(6.3f, 21.6f, 0f);
        public Vector3 upstairsPlayer = new Vector3(5.7f, 21.2f, 0f);
        public Vector3 officeEliza = new Vector3(0.9f, 23.6f, 0f);
        public Vector3 officePlayer = new Vector3(-0.3f, 23.1f, 0f);

        Rigidbody2D _playerBody;

        void Start()
        {
            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            if (!GuildIntroRules.CanPlayGuildIntro(session)) return;
            if (context == null) context = FindAnyObjectByType<HubContext>();
            if (player == null) player = FindAnyObjectByType<HubPlayerController>();
            _playerBody = player != null ? player.GetComponent<Rigidbody2D>() : null;
            if (player != null) player.enabled = false;
            StartCoroutine(Run(session));
        }

        IEnumerator Run(SessionState session)
        {
            var dialogue = context != null ? context.dialogue : null;

            // Eliza walks in beside the player, fresh from the market door.
            yield return FadeTo(dialogue, 1f, 0f);
            if (eliza != null)
            {
                eliza.position = elizaEntry;
                eliza.gameObject.SetActive(true);
            }
            yield return FadeTo(dialogue, 0f, 0.6f);

            // To the front desk — clerk greeting.
            var playerFollow = StartCoroutine(MoveTo(
                player != null ? player.transform : null, playerDesk, 2.4f));
            yield return MoveTo(eliza, elizaDesk, 2f);
            yield return playerFollow;
            yield return Play(receptionDialogue);

            // Up the stairs to the Guildmaster's office.
            var playerToStairs = StartCoroutine(MoveTo(
                player != null ? player.transform : null, playerStairs, 2.8f));
            yield return MoveTo(eliza, elizaStairs, 2.6f);
            yield return playerToStairs;
            yield return FadeTo(dialogue, 1f, 0.4f);
            Teleport(eliza, upstairsEliza);
            Teleport(player != null ? player.transform : null, upstairsPlayer);
            yield return FadeTo(dialogue, 0f, 0.5f);

            // Into the office.
            var playerToOffice = StartCoroutine(MoveTo(
                player != null ? player.transform : null, officePlayer, 2.2f));
            yield return MoveTo(eliza, officeEliza, 2f);
            yield return playerToOffice;
            yield return Play(ovanDialogue);

            session.SetFlag(GuildIntroRules.IntroSeenFlag);
            session.SetFlag(GuildIntroRules.IntroPendingFlag, false);
            if (player != null) player.enabled = true;
        }

        void Teleport(Transform target, Vector3 position)
        {
            if (target == null) return;
            MoveTarget(target, position);
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

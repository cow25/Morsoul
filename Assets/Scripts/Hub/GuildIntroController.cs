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
        public Vector3 elizaEntry = new Vector3(1.1f, -5.4f, 0f);
        public Vector3 elizaDesk = new Vector3(1.2f, 1.9f, 0f);
        public Vector3 playerDesk = new Vector3(-0.8f, 1.9f, 0f);
        public Vector3 elizaStairs = new Vector3(10.8f, 3.2f, 0f);
        public Vector3 playerStairs = new Vector3(9.8f, 2.8f, 0f);
        public Vector3 upstairsEliza = new Vector3(9.0f, 24.2f, 0f);
        public Vector3 upstairsPlayer = new Vector3(8.2f, 23.6f, 0f);
        public Vector3 officeEliza = new Vector3(1.0f, 26.5f, 0f);
        public Vector3 officePlayer = new Vector3(-0.6f, 26.5f, 0f);

        Rigidbody2D _playerBody;
        bool _introRunning;
        bool _crystalRunning;

        void Start()
        {
            if (context == null) context = FindAnyObjectByType<HubContext>();
            if (player == null) player = FindAnyObjectByType<HubPlayerController>();
            _playerBody = player != null ? player.GetComponent<Rigidbody2D>() : null;

            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            if (!GuildIntroRules.CanPlayGuildIntro(session)) return;
            _introRunning = true;
            if (player != null) player.enabled = false;
            StartCoroutine(Run(session));
        }

        void Update()
        {
            if (_introRunning || _crystalRunning || player == null) return;
            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            if (session == null || !GuildIntroRules.IsCaseOneActive(session)) return;
            if (session.HasFlag("Guild.CrystalReceived")) return;

            // Trigger before descending from Ovan's 2F floor toward StairsDown (x=10.5, y=24.5).
            Vector3 p = player.transform.position;
            if (p.y > 18f && p.x >= 6.0f)
            {
                _crystalRunning = true;
                player.enabled = false;
                StartCoroutine(RunCrystalHandoff(session));
            }
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

            // Up the stairs to the Guildmaster's office: align at the bottom entrance, then climb North up the carpet.
            var playerFoot = StartCoroutine(MoveTo(
                player != null ? player.transform : null, new Vector3(11.15f, 2.35f, 0f), 2.4f));
            yield return MoveTo(eliza, new Vector3(11.85f, 2.45f, 0f), 2.2f);
            yield return playerFoot;

            var playerClimb = StartCoroutine(MoveTo(
                player != null ? player.transform : null, new Vector3(11.15f, 5.05f, 0f), 1.0f));
            yield return MoveTo(eliza, new Vector3(11.85f, 5.15f, 0f), 1.0f);
            yield return playerClimb;

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
            _introRunning = false;
            if (player != null) player.enabled = true;
        }

        IEnumerator RunCrystalHandoff(SessionState session)
        {
            if (eliza != null && player != null)
            {
                if (eliza.position.y < 18f) eliza.position = officeEliza;
                eliza.gameObject.SetActive(true);
                Vector3 dest = player.transform.position + new Vector3(-1.3f, 0f, 0f);
                yield return MoveTo(eliza, dest, 1.1f);
            }

            yield return Play(BuildCrystalDialogue());
            session.SetFlag("Guild.CrystalReceived");
            _crystalRunning = false;
            if (player != null) player.enabled = true;
        }

        static DialogueData BuildCrystalDialogue()
        {
            var data = ScriptableObject.CreateInstance<DialogueData>();
            data.hideFlags = HideFlags.DontSave;
            data.id = "D_Guild_CrystalHandoff";
            data.startNodeId = "n1";
            data.nodes = new[]
            {
                new DialogueNodeData
                {
                    id = "n1",
                    speakerName = "Eliza",
                    text = "(Rushes up to you, holding out a glowing blue shard) " +
                           "\"Wait—before you head downstairs! Take this Communicator Crystal. " +
                           "If we get separated during an investigation, we can still reach each other from afar.\"",
                    nextNodeId = "n2"
                },
                new DialogueNodeData
                {
                    id = "n2",
                    speakerName = "Eliza",
                    text = "\"Keep it safe. Now let's head down and get to work.\""
                }
            };
            return data;
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

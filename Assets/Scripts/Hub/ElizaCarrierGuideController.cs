using System.Collections;
using Morsoul.Core;
using Morsoul.Dialogue;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Guides the player from the Guild door in Lower Market to the Carrier
    /// wagon when a new case (<c>CurrentCase &gt;= 2</c>) has been accepted on the
    /// Guild Quest Board (<see cref="PendingFlag"/>). Walks at a matched pace
    /// along the clear street so Eliza stays one step ahead rather than dashing.
    /// </summary>
    public class ElizaCarrierGuideController : MonoBehaviour
    {
        public static string PendingFlag(int caseId) => $"Guild.ElizaGuidingToCarrier.{caseId}";

        public HubContext context;
        [Min(0.1f)] public float timingScale = 1f;

        HubPlayerController _player;
        Rigidbody2D _playerBody;

        void Start()
        {
            var core = CoreRuntime.Instance;
            if (core == null) return;

            int caseId = core.Session.CurrentCase;
            if (caseId != 2 || !core.Session.HasFlag(PendingFlag(caseId)))
                return;

            var carrier = FindAnyObjectByType<CarrierInteractable>();
            if (carrier == null) return;

            if (context == null) context = GetComponent<HubContext>() ?? FindAnyObjectByType<HubContext>();
            _player = FindAnyObjectByType<HubPlayerController>();
            _playerBody = _player != null ? _player.GetComponent<Rigidbody2D>() : null;
            if (_player != null) _player.enabled = false;

            StartCoroutine(Run(core, caseId, carrier.transform.position));
        }

        IEnumerator Run(CoreRuntime core, int caseId, Vector3 carrierPos)
        {
            var dialogue = context != null ? context.dialogue : null;
            var eliza = FindEliza();
            if (eliza == null)
            {
                core.Session.SetFlag(PendingFlag(caseId), false);
                if (_player != null) _player.enabled = true;
                yield break;
            }

            var follower = eliza.GetComponent<ElizaCompanionFollower>();
            follower?.SetPaused(true);

            var camFollow = FindAnyObjectByType<CameraFollow2D>();
            if (camFollow != null && _player != null)
                camFollow.SnapToTarget(_player.transform);

            Vector3 startPos = _player != null ? _player.transform.position : new Vector3(0.5f, 11.7f, 0f);
            Vector3 elizaStart = startPos + new Vector3(-0.6f, -0.7f, 0f);
            eliza.position = elizaStart;
            eliza.gameObject.SetActive(true);

            yield return Play(BuildStartGuideDialogue(caseId));

            // Follow the cobblestone outer park ring around Park_TownWell (0,-0.3):
            // south to the north ring (y=7.5), west to the west ring (x=-7.5), south to the Carrier avenue, then west to the wagon.
            const float northRingY = 7.5f;
            const float westRingX = -7.5f;
            Vector3 elizaStop = new Vector3(carrierPos.x + 1.4f, carrierPos.y + 0.5f, 0f);
            Vector3 playerStop = new Vector3(carrierPos.x + 1.9f, carrierPos.y - 0.3f, 0f);

            Vector3 elizaCorner1 = new Vector3(elizaStart.x, northRingY, 0f);
            Vector3 playerCorner1 = new Vector3(startPos.x, northRingY + 0.5f, 0f);
            Vector3 elizaCorner2 = new Vector3(westRingX - 0.3f, northRingY, 0f);
            Vector3 playerCorner2 = new Vector3(westRingX + 0.3f, northRingY + 0.5f, 0f);
            Vector3 elizaCorner3 = new Vector3(westRingX - 0.3f, elizaStop.y, 0f);
            Vector3 playerCorner3 = new Vector3(westRingX + 0.3f, playerStop.y, 0f);

            const float walkSpeed = 3.5f;
            yield return WalkPair(eliza, elizaStart, elizaCorner1, startPos, playerCorner1, walkSpeed);
            yield return WalkPair(eliza, elizaCorner1, elizaCorner2, playerCorner1, playerCorner2, walkSpeed);
            yield return WalkPair(eliza, elizaCorner2, elizaCorner3, playerCorner2, playerCorner3, walkSpeed);
            yield return WalkPair(eliza, elizaCorner3, elizaStop, playerCorner3, playerStop, walkSpeed);

            yield return Play(BuildArrivedAtCarrierDialogue(caseId));

            core.Session.SetFlag(PendingFlag(caseId), false);
            if (_player != null) _player.enabled = true;

            if (follower != null) follower.SetPaused(false);
            else ElizaCompanionFollower.AttachIfJoined(context);
        }

        static Transform FindEliza()
        {
            var all = FindObjectsByType<EntityVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                string n = all[i].gameObject.name;
                if (n == "ArrivalEliza" || n == "Eliza")
                    return all[i].transform;
            }
            return null;
        }

        static DialogueData BuildStartGuideDialogue(int caseId)
        {
            string dest = caseId switch
            {
                2 => "the Cathedral Courtyard",
                3 => "Whisperwood Grove",
                _ => "Iron Hollow"
            };
            return MakeDialogue($"D_Eliza_GuideStart_C{caseId}",
                $"We're officially on the case, {{PlayerName}}. Follow me—I'll lead us to the Carrier's wagon so we can head to {dest}.");
        }

        static DialogueData BuildArrivedAtCarrierDialogue(int caseId)
        {
            string dest = caseId switch
            {
                2 => "the Cathedral Courtyard",
                3 => "Whisperwood Grove",
                _ => "Iron Hollow"
            };
            return MakeDialogue($"D_Eliza_GuideEnd_C{caseId}",
                $"Here's the Carrier. Talk to him whenever you're ready to depart for {dest}—I'll be right behind you.");
        }

        static DialogueData MakeDialogue(string id, string text)
        {
            var data = ScriptableObject.CreateInstance<DialogueData>();
            data.hideFlags = HideFlags.DontSave;
            data.id = id;
            data.startNodeId = "n1";
            data.nodes = new[]
            {
                new DialogueNodeData { id = "n1", speakerName = "Eliza", text = text }
            };
            return data;
        }

        IEnumerator Play(DialogueData data)
        {
            var dialogue = context != null ? context.dialogue : null;
            if (data == null || dialogue == null || context == null) yield break;
            if (!context.PlayDialogue(data)) yield break;
            while (dialogue.IsPlaying) yield return null;
        }

        IEnumerator WalkPair(Transform eliza, Vector3 elizaFrom, Vector3 elizaTo, Vector3 playerFrom, Vector3 playerTo, float walkSpeed)
        {
            var elizaMove = StartCoroutine(MoveAtSpeed(eliza, elizaFrom, elizaTo, walkSpeed));
            yield return Wait(0.15f);
            var playerMove = StartCoroutine(MoveAtSpeed(
                _player != null ? _player.transform : null, playerFrom, playerTo, walkSpeed));
            yield return elizaMove;
            yield return playerMove;
        }

        IEnumerator MoveAtSpeed(Transform target, Vector3 from, Vector3 destination, float unitsPerSecond, bool horizontalFirst = true)
        {
            if (target == null) yield break;
            float dist = Mathf.Abs(destination.x - from.x) + Mathf.Abs(destination.y - from.y);
            float duration = Mathf.Max(0.2f, dist / Mathf.Max(0.5f, unitsPerSecond)) / timingScale;
            yield return AxisMove.Move(target, destination, duration,
                (to, legDuration) => MoveLeg(target, to, legDuration), horizontalFirst);
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
                MoveTarget(target, Vector3.Lerp(start, destination, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            MoveTarget(target, destination);
            anim?.Tick(Vector2.zero);
        }

        void MoveTarget(Transform target, Vector3 position)
        {
            if (_player != null && target == _player.transform && _playerBody != null)
                _playerBody.position = position;
            target.position = position;
        }

        IEnumerator Wait(float seconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, seconds / timingScale));
        }
    }
}


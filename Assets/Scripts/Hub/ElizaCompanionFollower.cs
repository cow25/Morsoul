using Morsoul.Core;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Keeps Eliza following a step behind the player in 2D hubs once she has
    /// joined the player's adventures (<see cref="JoinedFlag"/>). Moves strictly
    /// along one cardinal axis at a time (90° Undertale-style) and pauses
    /// automatically while a scripted cutscene or UI modal is active.
    /// </summary>
    public class ElizaCompanionFollower : MonoBehaviour
    {
        public const string JoinedFlag = "Guild.ElizaJoined";

        public Transform player;
        public HubContext context;
        public float followDistance = 1.35f;
        public float stopDistance = 1.05f;
        public float moveSpeed = 4.2f;

        HubCharacterAnimator _anim;
        bool _paused;

        public void SetPaused(bool paused)
        {
            _paused = paused;
            if (paused) _anim?.Tick(Vector2.zero);
        }

        void Awake()
        {
            _anim = GetComponent<HubCharacterAnimator>();
        }

        void Update()
        {
            if (_paused || player == null)
            {
                _anim?.Tick(Vector2.zero);
                return;
            }

            if (context != null && context.UiBlocked)
            {
                _anim?.Tick(Vector2.zero);
                return;
            }

            Vector2 delta = (Vector2)(player.position - transform.position);
            float dist = delta.magnitude;
            if (dist <= stopDistance)
            {
                _anim?.Tick(Vector2.zero);
                return;
            }

            if (dist <= followDistance && _anim == null)
                return;

            // Snap if wildly separated (e.g. floor teleport inside Guild).
            if (dist > 10f)
            {
                transform.position = player.position + new Vector3(-1.1f, 0f, 0f);
                _anim?.Tick(Vector2.zero);
                return;
            }

            // Sharp 90-degree axis-locked step toward the player.
            Vector2 stepDir = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? new Vector2(Mathf.Sign(delta.x), 0f)
                : new Vector2(0f, Mathf.Sign(delta.y));

            float step = Mathf.Min(moveSpeed * Time.deltaTime, Mathf.Max(0f, dist - stopDistance));
            if (step > 0.0001f)
            {
                transform.position += (Vector3)(stepDir * step);
                _anim?.Tick(stepDir);
            }
            else
            {
                _anim?.Tick(Vector2.zero);
            }
        }

        public static ElizaCompanionFollower AttachIfJoined(HubContext ctx, bool forceAttach = false)
        {
            var core = CoreRuntime.Instance;
            if (core == null || (!forceAttach && !core.Session.HasFlag(JoinedFlag))) return null;

            if (!forceAttach)
            {
                string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                bool followHere = string.Equals(sceneName, "Hub_Wilderness", System.StringComparison.OrdinalIgnoreCase) ||
                    (string.Equals(sceneName, "Hub_LowerMarket", System.StringComparison.OrdinalIgnoreCase) &&
                     core.Session.HasFlag(ElizaCarrierGuideController.PendingFlag(core.Session.CurrentCase)));
                if (!followHere) return null;
            }

            var playerCtrl = FindAnyObjectByType<HubPlayerController>();
            if (playerCtrl == null) return null;

            var all = FindObjectsByType<EntityVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject elizaGo = null;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                string n = all[i].gameObject.name;
                if (n == "Eliza" || n == "ArrivalEliza")
                {
                    elizaGo = all[i].gameObject;
                    break;
                }
            }
            if (elizaGo == null) return null;

            if (!elizaGo.activeSelf)
            {
                elizaGo.transform.position = playerCtrl.transform.position + new Vector3(-1.1f, 0f, 0f);
                elizaGo.SetActive(true);
            }

            var follower = elizaGo.GetComponent<ElizaCompanionFollower>() ?? elizaGo.AddComponent<ElizaCompanionFollower>();
            follower.player = playerCtrl.transform;
            follower.context = ctx;
            follower.enabled = true;
            return follower;
        }
    }
}


using Morsoul.Core;
using Morsoul.Dialogue;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Hub
{
    /// <summary>
    /// 3D-facing adapter for the ruins gate. The 3D scene owns its interaction
    /// and trigger placement; this component only applies the approved Session
    /// state, checkpoints, and asks the GSM for the guarded wilderness load.
    /// </summary>
    public class PrologueGateSeam : MonoBehaviour
    {
        public int boardCaseId;
        public string routeKey = RouteKeys.PrologueWilderness;
        public string fallbackSceneName = SessionState.PrologueWildernessSceneId;
        public string playerTag = "Player";
        public bool triggerOnPlayerEnter = true;
        public DialogueController dialogue;
        public DialogueData lockedDialogue;
        public DialogueData robeDialogue;

        bool _waitingForRobeDialogue;
        bool _loading;

        public void MarkGateUnlocked()
        {
            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            var board = session != null ? session.GetOrCreateBoardState(boardCaseId) : null;
            if (session != null && board != null && board.BoardSolved)
                session.PrologueGateUnlocked = true;
        }

        public void CollectRobe()
        {
            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            if (session != null) session.PrologueRobeCollected = true;
        }

        public void TryCrossGate() => TryCrossGate(null);

        void OnTriggerEnter(Collider other)
        {
            if (triggerOnPlayerEnter) TryCrossGate(other);
        }

        void TryCrossGate(Collider other)
        {
            if (_loading || _waitingForRobeDialogue) return;
            if (other != null && !IsPlayerCollider(other)) return;

            var core = CoreRuntime.Instance;
            var session = core != null ? core.Session : null;
            if (session == null || !PrologueTransitionRules.CanCrossRuinsGate(session, boardCaseId))
            {
                PlayLockedDialogue();
                return;
            }
            if (!session.PrologueRobeCollected)
            {
                session.PrologueRobeCollected = true;
                if (PlayRobeDialogue()) return;
            }
            LoadWilderness();
        }

        bool IsPlayerCollider(Collider other)
        {
            if (string.IsNullOrEmpty(playerTag)) return true;
            if (other.CompareTag(playerTag)) return true;
            if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(playerTag))
                return true;
            return other.transform.root.CompareTag(playerTag);
        }

        bool PlayRobeDialogue()
        {
            if (robeDialogue == null) return false;
            var controller = dialogue != null ? dialogue : FindAnyObjectByType<DialogueController>();
            if (controller == null) return false;
            _waitingForRobeDialogue = true;
            void OnEnded()
            {
                controller.DialogueEnded -= OnEnded;
                _waitingForRobeDialogue = false;
                LoadWilderness();
            }
            controller.DialogueEnded += OnEnded;
            controller.SetDialogue(robeDialogue);
            controller.Play();
            return true;
        }

        void PlayLockedDialogue()
        {
            if (lockedDialogue == null) return;
            var controller = dialogue != null ? dialogue : FindAnyObjectByType<DialogueController>();
            if (controller == null) return;
            controller.SetDialogue(lockedDialogue);
            controller.Play();
        }

        void LoadWilderness()
        {
            if (_loading) return;
            string scene = ResolveWildernessScene();
            if (string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogWarning($"PrologueGateSeam '{name}': wilderness scene '{scene}' is not loadable.", this);
                return;
            }
            _loading = true;
            var core = CoreRuntime.Instance;
            if (core != null)
            {
                core.Session.SetSceneContext(SceneMode.Mode2D, scene);
                core.Bus.Publish(new CheckpointRequestedEvent
                {
                    Reason = "PrologueGate",
                    NextSceneName = scene,
                });
                core.Bus.Publish(new SceneLoadRequestedEvent { SceneName = scene });
            }
            else
            {
                SceneManager.LoadScene(scene);
            }
        }

        string ResolveWildernessScene()
        {
            if (GameStateManager.Instance != null && !string.IsNullOrEmpty(routeKey))
            {
                string routed = GameStateManager.Instance.routes.Resolve(routeKey);
                if (!string.IsNullOrEmpty(routed)) return routed;
            }
            return fallbackSceneName;
        }
    }
}

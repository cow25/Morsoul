using Morsoul.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Hub
{
    /// <summary>
    /// Walk-in 2D trigger tile that transitions the player to <see cref="targetScene"/>
    /// on <see cref="OnTriggerEnter2D"/> without requiring an [E] interact prompt.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class SceneExitTrigger : MonoBehaviour
    {
        public string targetScene;
        public string entryId;

        bool _triggered;

        void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_triggered || other == null || !other.CompareTag("Player"))
                return;
            if (string.IsNullOrEmpty(targetScene))
                return;

            _triggered = true;
            if (CoreRuntime.Instance != null && !string.IsNullOrEmpty(entryId))
                CoreRuntime.Instance.Session.PendingEntryPointId = entryId;

            if (CoreRuntime.Instance != null)
                CoreRuntime.Instance.Bus.Publish(new SceneLoadRequestedEvent { SceneName = targetScene });
            else
                SceneManager.LoadScene(targetScene);
        }
    }
}


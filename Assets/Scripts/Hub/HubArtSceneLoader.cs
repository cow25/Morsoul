using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Hub
{
    public class HubArtSceneLoader : MonoBehaviour
    {
        public string artSceneName;

        bool _owns;
        bool _quitting;

        void Start()
        {
            if (string.IsNullOrEmpty(artSceneName)) return;
            var existing = SceneManager.GetSceneByName(artSceneName);
            if (existing.IsValid() && existing.isLoaded) return;
            if (!Application.CanStreamedLevelBeLoaded(artSceneName))
            {
                Debug.LogWarning($"HubArtSceneLoader: '{artSceneName}' is not loadable " +
                                 "(missing from Build Settings?); art overlay skipped.");
                return;
            }
            SceneManager.LoadScene(artSceneName, LoadSceneMode.Additive);
            _owns = true;
        }

        void OnApplicationQuit()
        {
            _quitting = true;
        }

        void OnDestroy()
        {
            if (!_owns || _quitting) return;
            _owns = false;
            var scene = SceneManager.GetSceneByName(artSceneName);
            if (scene.IsValid() && scene.isLoaded)
                SceneManager.UnloadSceneAsync(scene);
        }
    }
}

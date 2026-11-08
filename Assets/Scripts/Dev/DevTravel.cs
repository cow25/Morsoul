using Morsoul.Core;
using UnityEngine.SceneManagement;

namespace Morsoul.Dev
{
    /// <summary>
    /// Shared scene-load path for dev tools: identical authority to
    /// CarrierInteractable — checkpoint + guarded GSM load when Core is
    /// live, direct SceneManager load only as a standalone fallback.
    /// </summary>
    public static class DevTravel
    {
        public static void Go(string sceneName, string reason = "DevMenu")
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            var core = CoreRuntime.Instance;
            if (core != null)
            {
                core.Bus.Publish(new CheckpointRequestedEvent
                {
                    Reason = reason,
                    NextSceneName = sceneName
                });
                core.Bus.Publish(new SceneLoadRequestedEvent { SceneName = sceneName });
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
        }

        /// <summary>Resolve a route key through the GSM route table.</summary>
        public static string Resolve(string routeKey)
        {
            var gsm = GameStateManager.Instance;
            return gsm != null ? gsm.routes.Resolve(routeKey) : null;
        }
    }
}

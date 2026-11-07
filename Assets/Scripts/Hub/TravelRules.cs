using System.Collections.Generic;
using Morsoul.Core;

namespace Morsoul.Hub
{
    /// <summary>
    /// One fast-travel destination offered by the carrier: a hub route key,
    /// its resolved scene and the player-facing label.
    /// </summary>
    public struct TravelDestination
    {
        public int caseId;
        public string routeKey;
        public string sceneName;
        public string label;
    }

    /// <summary>
    /// Pure travel/unlock rules (GDD §3 Transit Node): a regional hub becomes
    /// reachable once its case has been reached, i.e. hub.k is unlocked when
    /// Session.CurrentCase &gt;= k. Backtracking to earlier hubs is always
    /// allowed — completed cases leave CurrentCase ahead of their hub id.
    /// Kept Unity-free so EditMode tests can drive every rule.
    /// </summary>
    public static class TravelRules
    {
        /// <summary>True when the hub for <paramref name="hubCaseId"/> has been reached.</summary>
        public static bool IsHubUnlocked(int hubCaseId, int currentCase) =>
            hubCaseId >= 0 && hubCaseId <= currentCase;

        /// <summary>
        /// Builds the carrier's destination list: every unlocked, loadable
        /// hub except the scene the player is standing in. Scenes that
        /// resolve to the same name collapse into a single destination.
        /// </summary>
        /// <param name="routes">GSM route table (hub.&lt;n&gt; keys).</param>
        /// <param name="hubCaseIds">Hub case ids to consider, in menu order.</param>
        /// <param name="labels">Per-case display labels; missing entries fall back to the scene name.</param>
        /// <param name="currentCase">Session.CurrentCase.</param>
        /// <param name="currentScene">Active scene name — excluded from the list.</param>
        /// <param name="canLoad">Scene-loadability predicate (Application.CanStreamedLevelBeLoaded).</param>
        public static List<TravelDestination> CollectDestinations(
            SceneRouteTable routes,
            IReadOnlyList<int> hubCaseIds,
            IReadOnlyDictionary<int, string> labels,
            int currentCase,
            string currentScene,
            System.Func<string, bool> canLoad)
        {
            var result = new List<TravelDestination>();
            if (routes == null || hubCaseIds == null) return result;

            for (int i = 0; i < hubCaseIds.Count; i++)
            {
                int caseId = hubCaseIds[i];
                if (!IsHubUnlocked(caseId, currentCase)) continue;
                string key = RouteKeys.Hub(caseId);
                string scene = routes.Resolve(key);
                if (string.IsNullOrEmpty(scene) || scene == currentScene) continue;
                if (canLoad != null && !canLoad(scene)) continue;
                if (ContainsScene(result, scene)) continue;

                string label = null;
                labels?.TryGetValue(caseId, out label);
                result.Add(new TravelDestination
                {
                    caseId = caseId,
                    routeKey = key,
                    sceneName = scene,
                    label = string.IsNullOrEmpty(label) ? scene : label,
                });
            }
            return result;
        }

        static bool ContainsScene(List<TravelDestination> list, string scene)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].sceneName == scene) return true;
            return false;
        }
    }
}

using System;
using System.Collections.Generic;

namespace Morsoul.Core
{
    /// <summary>Route keys the state machine asks for.</summary>
    public static class RouteKeys
    {
        public const string Title = "title";
        public const string Opening = "opening";
        /// <summary>2D approach loaded by the ruins gate, before the Eliza trigger.</summary>
        public const string PrologueWilderness = "prologue.wilderness";
        /// <summary>Shared case-loop scene; resolves the session's current case.</summary>
        public const string CaseLoop = "caseloop";
        /// <summary>Hub scene for a case: hub.0, hub.1, ...</summary>
        public static string Hub(int caseId) => $"hub.{caseId}";
        /// <summary>Investigation scene for a case: investigation.1, ...</summary>
        public static string Investigation(int caseId) => $"investigation.{caseId}";
    }

    /// <summary>One route-table row; edited in the inspector.</summary>
    [Serializable]
    public class SceneRoute
    {
        /// <summary>Route key, e.g. "title", "opening", "hub.1", "investigation.1".</summary>
        public string key;
        /// <summary>Scene name as listed in Build Settings.</summary>
        public string sceneName;
    }

    /// <summary>
    /// Data-driven scene route table: key -> scene name. Lives on the
    /// GameStateManager component; missing scenes simply have no entry and
    /// callers get a clean false/error instead of a scene-load exception.
    /// </summary>
    [Serializable]
    public class SceneRouteTable
    {
        public List<SceneRoute> routes = new List<SceneRoute>();

        /// <summary>Looks up a route; null when unregistered.</summary>
        public string Resolve(string key)
        {
            if (string.IsNullOrEmpty(key) || routes == null) return null;
            for (int i = 0; i < routes.Count; i++)
            {
                var r = routes[i];
                if (r != null && r.key == key && !string.IsNullOrEmpty(r.sceneName))
                    return r.sceneName;
            }
            return null;
        }

        /// <summary>Registers or replaces a route. No-op on blank key.</summary>
        public void Set(string key, string sceneName)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (routes == null) routes = new List<SceneRoute>();
            for (int i = 0; i < routes.Count; i++)
            {
                if (routes[i] != null && routes[i].key == key)
                {
                    routes[i].sceneName = sceneName;
                    return;
                }
            }
            routes.Add(new SceneRoute { key = key, sceneName = sceneName });
        }

        public bool Contains(string key) => Resolve(key) != null;
    }
}

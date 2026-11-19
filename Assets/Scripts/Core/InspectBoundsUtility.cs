using UnityEngine;

namespace Morsoul.Core
{
    /// <summary>
    /// Shared geometric bounds &amp; framing helper for 3D Inspect cloning so props
    /// are centered by Renderer bounds and scaled cleanly into camera view.
    /// </summary>
    public static class InspectBoundsUtility
    {
        public static bool TryComputeRendererBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            if (root == null) return false;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasAny = false;
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled) continue;
                if (r is ParticleSystemRenderer) continue;
                if (!hasAny)
                {
                    bounds = r.bounds;
                    hasAny = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }
            return hasAny;
        }
    }
}


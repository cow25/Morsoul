using System;
using System.Collections;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Shared sharp-90° path helper for every scripted 2D mover — gameplay
    /// and in-world cutscenes move axis-aligned only (Undertale-style), never
    /// diagonally. A diagonal destination decomposes into two legs meeting at
    /// a corner; callers pick the corner order per move.
    /// </summary>
    public static class AxisMove
    {
        /// <summary>
        /// The corner that turns a diagonal move into an L-path. Horizontal
        /// leg first by default; pass false when the vertical leg must come
        /// first (e.g. stepping around an obstacle).
        /// </summary>
        public static Vector3 Corner(Vector3 from, Vector3 to, bool horizontalFirst = true)
        {
            bool dx = Mathf.Abs(to.x - from.x) > 0.001f;
            bool dy = Mathf.Abs(to.y - from.y) > 0.001f;
            if (dx && dy)
                return horizontalFirst
                    ? new Vector3(to.x, from.y, to.z)
                    : new Vector3(from.x, to.y, to.z);
            return to;
        }

        /// <summary>Manhattan length of the axis-aligned path.</summary>
        public static float PathLength(Vector3 from, Vector3 to) =>
            Mathf.Abs(to.x - from.x) + Mathf.Abs(to.y - from.y);

        /// <summary>
        /// Splits a fixed-duration move into axis-aligned legs, sharing the
        /// duration budget by leg length so per-leg speed stays constant.
        /// <paramref name="leg"/> performs one straight axis-aligned move and
        /// is supplied by the caller (rigidbody sync, timing scale, etc.).
        /// </summary>
        public static IEnumerator Move(Transform target, Vector3 destination, float duration,
            Func<Vector3, float, IEnumerator> leg, bool horizontalFirst = true)
        {
            if (target == null || leg == null) yield break;
            Vector3 via = Corner(target.position, destination, horizontalFirst);
            if (via == destination)
            {
                yield return leg(destination, duration);
                yield break;
            }
            float d1 = Vector3.Distance(target.position, via);
            float d2 = Vector3.Distance(via, destination);
            float total = d1 + d2;
            yield return leg(via, duration * (d1 / total));
            yield return leg(destination, duration * (d2 / total));
        }
    }
}

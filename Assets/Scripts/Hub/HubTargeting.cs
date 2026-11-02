using System.Collections.Generic;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Pure helpers for choosing which interactable the player is targeting.
    /// Extracted from HubPlayerController so the nearest-target rule can be
    /// exercised in EditMode tests without Physics2D.
    /// </summary>
    public static class HubTargeting
    {
        /// <summary>
        /// Index of the position nearest to origin, or -1 for an empty list.
        /// Ties resolve to the lower index.
        /// </summary>
        public static int Nearest(Vector2 origin, IReadOnlyList<Vector2> positions)
        {
            if (positions == null || positions.Count == 0) return -1;
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < positions.Count; i++)
            {
                float sqr = (positions[i] - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }
            return best;
        }
    }
}

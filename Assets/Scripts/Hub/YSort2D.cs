using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Dynamic Y-sort for 2D actors and world objects. In a top-down scene the
    /// depth order must follow world Y — objects farther south (lower Y) draw
    /// in front. Static dressing bakes this formula into its Z; anything that
    /// can move (player, NPCs, cutscene actors) carries this component so the
    /// same rule holds while they walk. Z is only a render depth here — it has
    /// no effect on 2D physics.
    /// </summary>
    [DisallowMultipleComponent]
    public class YSort2D : MonoBehaviour
    {
        /// <summary>World units of Z per unit of Y. Matches the dressing formula.</summary>
        public const float DepthPerUnitY = 0.02f;

        /// <summary>
        /// Small fixed Z offset applied after Y-sorting. Dressing props use a
        /// slight negative bias so a prop at the same Y overhangs the actor in
        /// front of it; actors use zero.
        /// </summary>
        public float bias;

        void LateUpdate()
        {
            var p = transform.position;
            float z = p.y * DepthPerUnitY + bias;
            if (p.z != z)
            {
                p.z = z;
                transform.position = p;
            }
        }
    }
}

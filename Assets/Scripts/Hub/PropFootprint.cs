using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Converts authored world-space collider footprints into the local-space
    /// values a scaled blockout quad needs. BoxCollider2D size/offset multiply
    /// by the transform's lossy scale, so authoring in world units requires
    /// dividing back out — the source of the oversized "invisible barrier"
    /// bug on scaled dressing props.
    /// </summary>
    public static class PropFootprint
    {
        public static Vector2 LocalSize(Vector2 worldSize, Vector2 scale) =>
            new Vector2(Div(worldSize.x, scale.x), Div(worldSize.y, scale.y));

        public static Vector2 LocalOffset(Vector2 worldOffset, Vector2 scale) =>
            new Vector2(Div(worldOffset.x, scale.x), Div(worldOffset.y, scale.y));

        static float Div(float value, float scale) =>
            Mathf.Approximately(scale, 0f) ? value : value / scale;
    }
}

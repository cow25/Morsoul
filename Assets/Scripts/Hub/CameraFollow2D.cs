using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Minimal SmoothDamp camera follow for 2D hub scenes. Follows the target
    /// on XY in LateUpdate while preserving the camera's Z depth. Cinemachine
    /// remains an option; this covers blockout scenes with no package needs.
    /// </summary>
    public class CameraFollow2D : MonoBehaviour
    {
        /// <summary>Transform followed on the XY plane (usually the player).</summary>
        public Transform target;
        /// <summary>SmoothDamp time constant.</summary>
        public float smoothTime = 0.25f;
        [Header("Bounds")]
        [Tooltip("Clamp the camera position inside this world-space rect; " +
                 "disabled keeps the legacy unclamped follow.")]
        public bool useBounds;
        public Vector2 boundsMin;
        public Vector2 boundsMax;
        [Tooltip("Also inset the clamp by the orthographic half-size so the " +
                 "view edge never leaves the rect.")]
        public bool insetByView = true;

        Vector2 _velocity;

        void LateUpdate()
        {
            if (target == null) return;
            var position = transform.position;
            var goal = (Vector2)target.position;
            var smoothed = new Vector2(
                Mathf.SmoothDamp(position.x, goal.x, ref _velocity.x, smoothTime),
                Mathf.SmoothDamp(position.y, goal.y, ref _velocity.y, smoothTime));
            transform.position = new Vector3(ClampX(smoothed.x), ClampY(smoothed.y), position.z);
        }

        float ClampX(float x)
        {
            if (!useBounds) return x;
            float inset = insetByView ? ViewHalfExtents().x : 0f;
            float min = boundsMin.x + inset, max = boundsMax.x - inset;
            // Level narrower than the view: park on the rect centre so the
            // whole rect stays framed instead of snapping to an edge.
            return min <= max ? Mathf.Clamp(x, min, max) : (boundsMin.x + boundsMax.x) * 0.5f;
        }

        float ClampY(float y)
        {
            if (!useBounds) return y;
            float inset = insetByView ? ViewHalfExtents().y : 0f;
            float min = boundsMin.y + inset, max = boundsMax.y - inset;
            return min <= max ? Mathf.Clamp(y, min, max) : (boundsMin.y + boundsMax.y) * 0.5f;
        }

        Vector2 ViewHalfExtents()
        {
            var cam = GetComponent<Camera>();
            if (cam == null || !cam.orthographic) return Vector2.zero;
            float halfH = cam.orthographicSize;
            return new Vector2(halfH * cam.aspect, halfH);
        }
    }
}

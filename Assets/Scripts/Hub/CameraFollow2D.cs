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

        Vector2 _velocity;

        void LateUpdate()
        {
            if (target == null) return;
            var position = transform.position;
            var goal = (Vector2)target.position;
            var smoothed = new Vector2(
                Mathf.SmoothDamp(position.x, goal.x, ref _velocity.x, smoothTime),
                Mathf.SmoothDamp(position.y, goal.y, ref _velocity.y, smoothTime));
            transform.position = new Vector3(smoothed.x, smoothed.y, position.z);
        }
    }
}

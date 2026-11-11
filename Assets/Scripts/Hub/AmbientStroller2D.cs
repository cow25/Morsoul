using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Lightweight ambient patrol/stroll loop for non-interactable townsfolk.
    /// Walks smoothly between world waypoints, pauses briefly at stops, and
    /// drives HubCharacterAnimator + YSort2D without blocking player movement.
    /// </summary>
    public class AmbientStroller2D : MonoBehaviour
    {
        public Vector2[] waypoints;
        [Min(0.1f)] public float walkSpeed = 1.25f;
        [Min(0f)] public float pauseSeconds = 1.8f;
        public bool loop = true;

        int _index;
        int _dir = 1;
        float _pauseTimer;
        HubCharacterAnimator _animator;

        void Awake()
        {
            _animator = GetComponent<HubCharacterAnimator>();
            if (_animator != null)
                _animator.externalDrive = true;
        }

        void Start()
        {
            if (waypoints == null || waypoints.Length == 0) return;
            var p = waypoints[0];
            transform.position = new Vector3(p.x, p.y, transform.position.z);
            _index = waypoints.Length > 1 ? 1 : 0;
        }

        void Update()
        {
            if (waypoints == null || waypoints.Length < 2)
            {
                _animator?.Tick(Vector2.zero);
                return;
            }

            if (_pauseTimer > 0f)
            {
                _pauseTimer -= Time.deltaTime;
                _animator?.Tick(Vector2.zero);
                return;
            }

            Vector2 current = transform.position;
            Vector2 target = waypoints[_index];
            Vector2 delta = target - current;
            float dist = delta.magnitude;
            float step = walkSpeed * Time.deltaTime;

            if (dist <= step || dist < 0.02f)
            {
                transform.position = new Vector3(target.x, target.y, transform.position.z);
                _animator?.Tick(Vector2.zero);
                _pauseTimer = pauseSeconds;
                AdvanceIndex();
                return;
            }

            Vector2 moveDir = delta / dist;
            Vector2 next = current + moveDir * step;
            transform.position = new Vector3(next.x, next.y, transform.position.z);
            _animator?.Tick(moveDir);
        }

        void AdvanceIndex()
        {
            if (loop)
            {
                _index = (_index + 1) % waypoints.Length;
                return;
            }

            int next = _index + _dir;
            if (next < 0 || next >= waypoints.Length)
            {
                _dir = -_dir;
                next = Mathf.Clamp(_index + _dir, 0, waypoints.Length - 1);
            }
            _index = next;
        }
    }
}


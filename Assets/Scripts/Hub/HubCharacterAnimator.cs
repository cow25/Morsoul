using UnityEngine;

namespace Morsoul.Hub
{
    public class HubCharacterAnimator : MonoBehaviour
    {
        public string movingParam = "Moving";
        public string moveXParam = "MoveX";
        public string moveYParam = "MoveY";
        public string speedParam = "Speed";
        public bool flipByMoveX = true;
        [Tooltip("When true, another controller drives Tick(move) instead of Update polling HubPlayerController.")]
        public bool externalDrive;

        HubPlayerController _controller;
        EntityVisual _visual;
        Animator _animator;
        SpriteRenderer _renderer;
        bool _hasMoving;
        bool _hasMoveX;
        bool _hasMoveY;
        bool _hasSpeed;
        Vector2 _facing = Vector2.down;

        void Awake()
        {
            _controller = GetComponentInParent<HubPlayerController>();
            _visual = GetComponent<EntityVisual>();
            if (_visual != null) Bind(_visual.Animator, _visual.Renderer);
        }

        public void Bind(Animator animator, SpriteRenderer renderer)
        {
            _animator = animator;
            _renderer = renderer;
            _hasMoving = _hasMoveX = _hasMoveY = _hasSpeed = false;
            if (_animator == null) return;
            foreach (var p in _animator.parameters)
            {
                if (p.name == movingParam && p.type == AnimatorControllerParameterType.Bool)
                    _hasMoving = true;
                else if (p.name == moveXParam && p.type == AnimatorControllerParameterType.Float)
                    _hasMoveX = true;
                else if (p.name == moveYParam && p.type == AnimatorControllerParameterType.Float)
                    _hasMoveY = true;
                else if (p.name == speedParam && p.type == AnimatorControllerParameterType.Float)
                    _hasSpeed = true;
            }
        }

        void Update()
        {
            if (!externalDrive)
                Tick(_controller != null ? _controller.MoveInput : Vector2.zero);
        }

        public void Tick(Vector2 move)
        {
            if (_animator == null) return;
            bool moving = !move.Equals(Vector2.zero);
            if (moving) _facing = move.normalized;

            if (_hasMoving) _animator.SetBool(movingParam, moving);
            if (_hasSpeed) _animator.SetFloat(speedParam, move.magnitude);
            if (_hasMoveX) _animator.SetFloat(moveXParam, _facing.x);
            if (_hasMoveY) _animator.SetFloat(moveYParam, _facing.y);

            if (flipByMoveX && _renderer != null && moving && Mathf.Abs(move.x) > 0.01f)
            {
                _renderer.flipX = move.x < 0f;
                if (_visual != null) _visual.SyncFlip(_renderer.flipX);
            }
        }
    }
}

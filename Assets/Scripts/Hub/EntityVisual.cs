using UnityEngine;

namespace Morsoul.Hub
{
    public class EntityVisual : MonoBehaviour
    {
        public const string VisualChildName = "Visual";

        public VisualProfile profile;

        public SpriteRenderer Renderer { get; private set; }
        public Animator Animator { get; private set; }

        [SerializeField, HideInInspector] Material _defaultMaterial;

        void Awake()
        {
            Apply(profile);
        }

        public void Apply()
        {
            Apply(profile);
        }

        public void Apply(VisualProfile visualProfile)
        {
            profile = visualProfile;
            var rootRenderer = GetComponent<MeshRenderer>();

            if (profile == null || profile.sprite == null)
            {
                if (rootRenderer != null) rootRenderer.enabled = true;
                var existing = transform.Find(VisualChildName);
                if (existing != null) existing.gameObject.SetActive(false);
                Renderer = null;
                Animator = null;
                return;
            }

            var child = transform.Find(VisualChildName);
            GameObject childGo;
            if (child == null)
            {
                childGo = new GameObject(VisualChildName);
                childGo.transform.SetParent(transform, false);
            }
            else
            {
                childGo = child.gameObject;
            }
            childGo.SetActive(true);
            childGo.transform.localPosition = profile.visualOffset;
            childGo.transform.localRotation = Quaternion.identity;
            childGo.transform.localScale = new Vector3(profile.visualScale.x, profile.visualScale.y, 1f);

            Renderer = childGo.GetComponent<SpriteRenderer>();
            if (Renderer == null) Renderer = childGo.AddComponent<SpriteRenderer>();
            if (_defaultMaterial == null) _defaultMaterial = Renderer.sharedMaterial;
            Renderer.sprite = profile.sprite;
            Renderer.color = profile.tint;
            Renderer.sharedMaterial = profile.material != null ? profile.material : _defaultMaterial;
            Renderer.flipX = profile.flipX;
            Renderer.sortingOrder = profile.sortingOrder;

            Animator = childGo.GetComponent<Animator>();
            if (profile.animatorController != null)
            {
                if (Animator == null) Animator = childGo.AddComponent<Animator>();
                Animator.runtimeAnimatorController = profile.animatorController;

                var adapter = GetComponent<HubCharacterAnimator>();
                if (adapter == null) adapter = gameObject.AddComponent<HubCharacterAnimator>();
                adapter.Bind(Animator, Renderer);
            }
            else if (Animator != null)
            {
                Animator.runtimeAnimatorController = null;
            }

            if (rootRenderer != null) rootRenderer.enabled = false;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += ReapplyFromInspector;
        }

        void ReapplyFromInspector()
        {
            if (this == null) return;
            Apply(profile);
        }
#endif
    }
}

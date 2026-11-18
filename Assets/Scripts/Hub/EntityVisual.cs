using UnityEngine;

namespace Morsoul.Hub
{
    public class EntityVisual : MonoBehaviour
    {
        public const string VisualChildName = "Visual";

        public VisualProfile profile;

        const string AttachmentPrefix = "Attach_";

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
            var parentScale = transform.localScale;
            float sx = parentScale.x != 0 ? profile.visualScale.x / parentScale.x : profile.visualScale.x;
            float sy = parentScale.y != 0 ? profile.visualScale.y / parentScale.y : profile.visualScale.y;
            childGo.transform.localScale = new Vector3(sx, sy, 1f);

            Renderer = childGo.GetComponent<SpriteRenderer>();
            if (Renderer == null) Renderer = childGo.AddComponent<SpriteRenderer>();
            if (_defaultMaterial == null) _defaultMaterial = Renderer.sharedMaterial;
            Renderer.sprite = profile.sprite;
            Renderer.color = profile.tint;
            Renderer.sharedMaterial = profile.material != null ? profile.material : _defaultMaterial;
            Renderer.flipX = profile.flipX;
            Renderer.sortingOrder = profile.sortingOrder;

            ApplyAttachments(childGo.transform, profile);
            SyncFlip(Renderer.flipX);

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

        void ApplyAttachments(Transform visualRoot, VisualProfile visualProfile)
        {
            var attachments = visualProfile.attachments;
            int needed = attachments != null ? attachments.Length : 0;

            for (int i = 0; i < needed; i++)
            {
                var att = attachments[i];
                string childName = $"{AttachmentPrefix}{i}";
                var existing = visualRoot.Find(childName);
                GameObject go;
                if (existing == null)
                {
                    go = new GameObject(childName);
                    go.transform.SetParent(visualRoot, false);
                }
                else
                {
                    go = existing.gameObject;
                }

                if (att.sprite == null)
                {
                    go.SetActive(false);
                    continue;
                }

                go.SetActive(true);
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr == null) sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = att.sprite;
                sr.color = visualProfile.tint;
                sr.sharedMaterial = visualProfile.material != null ? visualProfile.material : _defaultMaterial;
                sr.sortingOrder = visualProfile.sortingOrder + att.sortingOrderOffset;
                Vector2 s = att.localScale == Vector2.zero ? Vector2.one : att.localScale;
                go.transform.localScale = new Vector3(s.x, s.y, 1f);
                if (i < _cachedAttachTransforms.Length)
                {
                    _cachedAttachTransforms[i] = go.transform;
                    _cachedAttachRenderers[i] = sr;
                }
            }

            // Disable any leftover Attach_* children from an earlier profile with more slots.
            for (int i = visualRoot.childCount - 1; i >= 0; i--)
            {
                var child = visualRoot.GetChild(i);
                if (!child.name.StartsWith(AttachmentPrefix, System.StringComparison.Ordinal)) continue;
                if (int.TryParse(child.name.Substring(AttachmentPrefix.Length), out int idx) && idx >= needed)
                    child.gameObject.SetActive(false);
            }
        }

        Transform[] _cachedAttachTransforms;
        SpriteRenderer[] _cachedAttachRenderers;

        public void SyncFlip(bool facingLeft)
        {
            AnimateAttachments(facingLeft, 0f, 0f);
        }

        public void AnimateAttachments(bool facingLeft, float stridePhase, float moveWeight)
        {
            if (profile == null || profile.attachments == null) return;
            if (_cachedAttachTransforms == null || _cachedAttachTransforms.Length != profile.attachments.Length)
            {
                var visualRoot = transform.Find(VisualChildName);
                if (visualRoot == null) return;
                _cachedAttachTransforms = new Transform[profile.attachments.Length];
                _cachedAttachRenderers = new SpriteRenderer[profile.attachments.Length];
                for (int i = 0; i < profile.attachments.Length; i++)
                {
                    var c = visualRoot.Find($"{AttachmentPrefix}{i}");
                    _cachedAttachTransforms[i] = c;
                    if (c != null) _cachedAttachRenderers[i] = c.GetComponent<SpriteRenderer>();
                }
            }

            float swingSin = moveWeight > 0.01f ? Mathf.Sin(stridePhase) * moveWeight : 0f;
            float bobCos = moveWeight > 0.01f ? Mathf.Abs(Mathf.Cos(stridePhase)) * 0.03f * moveWeight : 0f;

            for (int i = 0; i < profile.attachments.Length; i++)
            {
                var child = _cachedAttachTransforms[i];
                if (child == null || !child.gameObject.activeSelf) continue;

                var att = profile.attachments[i];
                float dirSign = i == 0 ? 1f : -1f;
                float flipSign = facingLeft ? -1f : 1f;
                float x = (facingLeft ? -att.localOffset.x : att.localOffset.x) + dirSign * swingSin * 0.07f * flipSign;
                float y = att.localOffset.y + bobCos;
                child.localPosition = new Vector3(x, y, att.localZ);
                float rot = (facingLeft ? -att.rotationZ : att.rotationZ) - dirSign * swingSin * 12f * flipSign;
                child.localRotation = Quaternion.Euler(0f, 0f, rot);
                var sr = _cachedAttachRenderers[i];
                if (sr != null)
                    sr.flipX = att.flipX ^ facingLeft;
            }
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

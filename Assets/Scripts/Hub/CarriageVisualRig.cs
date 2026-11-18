using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Renders and animates the Carrier's carriage assembly: the wagon body,
    /// the Carrier seated on the front bench managing the reins, and the
    /// 4-frame trotting Horse hitched ahead. Used both in 2D hub scenes and
    /// during <see cref="CarriageRideCutscene"/>.
    /// </summary>
    public class CarriageVisualRig : MonoBehaviour
    {
        public bool isMoving = false;
        public float trotFps = 8f;
        public int sortingOrder = 5;

        SpriteRenderer _wagonSr;
        SpriteRenderer _carrierSr;
        SpriteRenderer _horseSr;
        LineRenderer _reins;
        Sprite[] _horseFrames;
        readonly System.Collections.Generic.List<Object> _runtimeAssets = new System.Collections.Generic.List<Object>();
        float _animTime;
        Vector3 _wagonBaseLocal;
        Vector3 _carrierBaseLocal;
        Vector3 _horseBaseLocal;

        public void Initialize(bool moving, int order = 5)
        {
            isMoving = moving;
            sortingOrder = order;
            BuildParts();
        }

        void Start()
        {
            if (_horseSr == null) BuildParts();
        }

        void BuildParts()
        {
            var wagonTex = LoadTex("Assets/Art/Environments/Custom/Carrier_Wagon.png");
            var horseTex = LoadTex("Assets/Art/Environments/Custom/Horse_Trot_Sheet.png");
            var carrierTex = LoadTex("Assets/Art/Characters/NPCs/Named/Carrier/Idle/Idle_Side-Sheet.png");

            // Wagon
            if (_wagonSr == null)
            {
                var existingSr = GetComponent<SpriteRenderer>();
                if (existingSr != null && existingSr.sprite != null &&
                    existingSr.sprite.name.Contains("Carrier_Wagon"))
                {
                    _wagonSr = existingSr;
                }
                else
                {
                    var wGo = new GameObject("WagonBody");
                    wGo.transform.SetParent(transform, false);
                    _wagonSr = wGo.AddComponent<SpriteRenderer>();
                }
            }
            if (_wagonSr.sprite == null && wagonTex != null)
            {
                wagonTex.filterMode = FilterMode.Point;
                _wagonSr.sprite = Sprite.Create(wagonTex,
                    new Rect(0, 0, wagonTex.width, wagonTex.height),
                    new Vector2(0.5f, 0.25f), 24f);
                _runtimeAssets.Add(_wagonSr.sprite);
            }
            _wagonSr.sortingOrder = sortingOrder;
            _wagonBaseLocal = _wagonSr.transform.localPosition;

            // Carrier seated on the front bench of the wagon
            if (_carrierSr == null)
            {
                var cGo = new GameObject("CarrierDriver");
                cGo.transform.SetParent(transform, false);
                _carrierSr = cGo.AddComponent<SpriteRenderer>();
            }
            if (_carrierSr.sprite == null && carrierTex != null)
            {
                carrierTex.filterMode = FilterMode.Point;
                int frameW = Mathf.Min(64, carrierTex.width / 4);
                if (frameW <= 0) frameW = carrierTex.width;
                _carrierSr.sprite = Sprite.Create(carrierTex,
                    new Rect(0, 0, frameW, carrierTex.height),
                    new Vector2(0.5f, 0.2f), 32f);
                _runtimeAssets.Add(_carrierSr.sprite);
            }
            _carrierBaseLocal = new Vector3(1.15f, 0.45f, 0f);
            _carrierSr.transform.localPosition = _carrierBaseLocal;
            _carrierSr.sortingOrder = sortingOrder + 1;

            // Horse hitched in front of the wagon
            if (_horseSr == null)
            {
                var hGo = new GameObject("CarriageHorse");
                hGo.transform.SetParent(transform, false);
                _horseSr = hGo.AddComponent<SpriteRenderer>();
            }
            if (horseTex != null && (_horseFrames == null || _horseFrames.Length == 0))
            {
                horseTex.filterMode = FilterMode.Point;
                int fw = horseTex.width / 4;
                _horseFrames = new Sprite[4];
                for (int i = 0; i < 4; i++)
                {
                    _horseFrames[i] = Sprite.Create(horseTex,
                        new Rect(i * fw, 0, fw, horseTex.height),
                        new Vector2(0.5f, 0.15f), 16f);
                    _runtimeAssets.Add(_horseFrames[i]);
                }
                _horseSr.sprite = _horseFrames[0];
            }
            _horseBaseLocal = new Vector3(2.45f, -0.05f, 0f);
            _horseSr.transform.localPosition = _horseBaseLocal;
            _horseSr.sortingOrder = sortingOrder + 1;

            // Reins connecting Carrier hands to Horse bridle
            if (_reins == null)
            {
                var rGo = new GameObject("Reins");
                rGo.transform.SetParent(transform, false);
                _reins = rGo.AddComponent<LineRenderer>();
                _reins.useWorldSpace = false;
                _reins.positionCount = 2;
                _reins.startWidth = 0.04f;
                _reins.endWidth = 0.04f;
                _reins.sortingOrder = sortingOrder + 2;
                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = new Color(0.24f, 0.14f, 0.08f, 1f);
                _reins.material = mat;
                _runtimeAssets.Add(mat);
                _reins.startColor = new Color(0.24f, 0.14f, 0.08f, 1f);
                _reins.endColor = new Color(0.24f, 0.14f, 0.08f, 1f);
            }
            UpdateReins();
        }

        void Update()
        {
            if (_horseFrames == null || _horseFrames.Length == 0) return;

            if (isMoving)
            {
                _animTime += Time.unscaledDeltaTime;
                int frame = Mathf.FloorToInt(_animTime * trotFps) % _horseFrames.Length;
                _horseSr.sprite = _horseFrames[frame];

                float bob = Mathf.Abs(Mathf.Sin(_animTime * trotFps * 0.5f * Mathf.PI)) * 0.05f;
                float wagonRock = Mathf.Sin(_animTime * 11f) * 0.03f;
                _horseSr.transform.localPosition = _horseBaseLocal + new Vector3(0f, bob, 0f);
                if (_wagonSr != null && _wagonSr.transform != transform)
                    _wagonSr.transform.localPosition = _wagonBaseLocal + new Vector3(0f, wagonRock, 0f);
                if (_carrierSr != null)
                    _carrierSr.transform.localPosition = _carrierBaseLocal + new Vector3(0f, wagonRock * 1.15f, 0f);
            }
            else
            {
                // Subtle idle breathing / tail shift on parked carriage
                _animTime += Time.unscaledDeltaTime;
                int idleFrame = (Mathf.FloorToInt(_animTime * 1.5f) % 6 == 0) ? 1 : 0;
                _horseSr.sprite = _horseFrames[idleFrame];
                _horseSr.transform.localPosition = _horseBaseLocal;
                if (_carrierSr != null) _carrierSr.transform.localPosition = _carrierBaseLocal;
            }
            UpdateReins();
        }

        void UpdateReins()
        {
            if (_reins == null || _carrierSr == null || _horseSr == null) return;
            Vector3 handPos = _carrierSr.transform.localPosition + new Vector3(0.22f, 0.25f, 0f);
            Vector3 bridlePos = _horseSr.transform.localPosition + new Vector3(0.45f, 0.72f, 0f);
            _reins.SetPosition(0, handPos);
            _reins.SetPosition(1, bridlePos);
        }

        void OnDestroy()
        {
            for (int i = 0; i < _runtimeAssets.Count; i++)
            {
                if (_runtimeAssets[i] != null) Destroy(_runtimeAssets[i]);
            }
            _runtimeAssets.Clear();
        }

        static Texture2D LoadTex(string assetPath)
        {
            var cfg = Resources.Load<Morsoul.Core.PauseMenuAssets>("PauseMenuConfig");
            if (cfg != null)
            {
                if (assetPath.EndsWith("Carrier_Wagon.png") && cfg.carrierWagonTexture != null)
                    return cfg.carrierWagonTexture;
                if (assetPath.EndsWith("Horse_Trot_Sheet.png") && cfg.horseTrotSheetTexture != null)
                    return cfg.horseTrotSheetTexture;
                if (assetPath.EndsWith("Idle_Side-Sheet.png") && cfg.carrierIdleSheetTexture != null)
                    return cfg.carrierIdleSheetTexture;
            }
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
#else
            return null;
#endif
        }
    }
}


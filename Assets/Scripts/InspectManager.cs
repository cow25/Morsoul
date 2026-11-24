using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Cursor = UnityEngine.Cursor; 

public class InspectManager : MonoBehaviour
{
    public static InspectManager Instance { get; private set; }

    [Header("Camera Setup")]
    public Camera inspectCamera;
    public Transform inspectOrigin;
    public RenderTexture inspectRT;

    [Header("Settings")]
    public float rotateSpeed = 0.5f;
    public float zoomSpeed = 2f;
    public float minZoom = -1.1f;
    public float maxZoom = -3.5f;
    public float inspectFitExtent = 0.85f;
    public float defaultInspectDistance = -1.6f;

    private PanelRenderer panelRenderer;
    private VisualElement _inspectRoot;
    private Image _inspectImage;
    private Label _inspectDescription;
    private Button _closeButton;

    private GameObject _inspectPivot;
    private GameObject _currentClone;
    private float _currentZoom = -1.6f;
    private bool _isInspecting = false;
    private float _prevTimeScale = 1f;

    public bool IsInspecting => _isInspecting;
    public static bool IsAnyInspecting => Instance != null && Instance._isInspecting;

    private MonoBehaviour _playerController; 
    private ScanController _scanController;
    private Light _inspectLight;
    private System.Func<bool> _modalBlocker;
    private bool _returnToJournal;
    private System.Action _onReinspectClosed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RegisterJournalReinspectHook()
    {
        Morsoul.Journal.JournalController.ReinspectHandler = OpenReinspectFromJournal;
    }

    static void OpenReinspectFromJournal(Morsoul.Data.ClueData clueData, System.Action onClosed)
    {
        var mgr = EnsureRuntime();
        if (mgr == null || clueData == null)
        {
            onClosed?.Invoke();
            return;
        }
        mgr.OpenReinspect(clueData, onClosed);
    }

    public static InspectManager EnsureRuntime()
    {
        if (Instance != null) return Instance;
        var existing = FindAnyObjectByType<InspectManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        var config = Resources.Load<Morsoul.Core.PauseMenuAssets>("PauseMenuConfig");
        if (config == null || config.inspectUxml == null || config.inspectRT == null)
            return null;

        var root = new GameObject("InspectManagerRuntime");
        root.SetActive(false);

        var pr = root.AddComponent<PanelRenderer>();
        pr.panelSettings = config.panelSettings;
        pr.visualTreeAsset = config.inspectUxml;
        pr.sortingOrder = 90;

        var originGo = new GameObject("InspectOrigin");
        originGo.transform.SetParent(root.transform, false);
        originGo.transform.position = new Vector3(1.26f, -999.5f, -2.42f);

        var camGo = new GameObject("InspectCamera");
        camGo.transform.SetParent(root.transform, false);
        camGo.transform.position = originGo.transform.position + new Vector3(0f, 0f, -1.6f);
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0.784f);
        int inspectLayer = LayerMask.NameToLayer("InspectLayer");
        cam.cullingMask = inspectLayer >= 0 ? (1 << inspectLayer) : 0;
        cam.targetTexture = config.inspectRT;
        cam.depth = 1f;
        cam.enabled = false;

        var mgr = root.AddComponent<InspectManager>();
        mgr.inspectCamera = cam;
        mgr.inspectOrigin = originGo.transform;
        mgr.inspectRT = config.inspectRT;

        root.SetActive(true);
        return mgr;
    }

    private void Awake() 
    {
        Instance = this;
        _modalBlocker = () => _isInspecting;
        Morsoul.Core.PauseMenuController.RegisterExternalModal(_modalBlocker);

        if (inspectCamera != null)
        {
            inspectCamera.fieldOfView = 45f;
            inspectCamera.nearClipPlane = 0.02f;
            inspectCamera.farClipPlane = 40f;
            inspectCamera.enabled = false;

            GameObject lightObj = new GameObject("InspectLight");
            lightObj.transform.SetParent(inspectCamera.transform);
            lightObj.transform.localPosition = Vector3.zero;
            lightObj.transform.localRotation = Quaternion.identity;

            _inspectLight = lightObj.AddComponent<Light>();
            _inspectLight.type = LightType.Directional;
            _inspectLight.intensity = 0.7f;
            _inspectLight.cullingMask = LayerMask.GetMask("InspectLayer");
            _inspectLight.enabled = false;
        }
    }

    private void OnEnable()
    {
        panelRenderer = GetComponent<PanelRenderer>();
        if (panelRenderer != null)
        {
            if (panelRenderer.sortingOrder < 90) panelRenderer.sortingOrder = 90;
            panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }
    }

    private void OnDisable()
    {
        if (panelRenderer != null) panelRenderer.UnregisterUIReloadCallback(OnUIReload);
    }

    private void OnDestroy()
    {
        CleanupClone();
        if (_isInspecting)
        {
            Time.timeScale = _prevTimeScale > 0f ? _prevTimeScale : 1f;
            _isInspecting = false;
        }
        if (_modalBlocker != null)
        {
            Morsoul.Core.PauseMenuController.UnregisterExternalModal(_modalBlocker);
            _modalBlocker = null;
        }
        if (Instance == this) Instance = null;
    }

    private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
    {
        _inspectRoot = root.Q<VisualElement>("InspectRoot");
        _inspectImage = root.Q<Image>("InspectImage");
        _inspectDescription = root.Q<Label>("InspectDescription");
        _closeButton = root.Q<Button>("CloseInspectButton");
        if (_closeButton != null)
            _closeButton.clicked += CloseInspect;

        if (_inspectImage != null) _inspectImage.image = inspectRT;
        if (_inspectRoot != null) _inspectRoot.style.display = DisplayStyle.None;
    }

    public void OpenInspect(ClueNode clue)
    {
        if (clue == null || _isInspecting || Morsoul.Core.PauseMenuController.AnyModalOrPauseOpen) return;

        Morsoul.Data.ClueData clueData = null;
        var core = Morsoul.Core.CoreRuntime.Instance;
        if (core != null && core.Session != null && !string.IsNullOrEmpty(clue.id))
        {
            clueData = core.ResolveClue(clue.id);
            if (clueData != null)
            {
                var log = clueData.type == Morsoul.Data.ClueType.Latent
                    ? core.ResolveCase(clueData.caseId)?.latentLog
                    : null;
                if (!Morsoul.Core.ClueAcquisition.CanAcquire(clueData, core.Session, log, out _))
                    return;
            }
        }

        _isInspecting = true;

        bool newlyRegistered = DiaryManager.Instance != null
            ? !DiaryManager.Instance.HasClue(clue.id)
            : (core == null || !core.Session.HasClue(clue.id));
        if (newlyRegistered)
        {
            if (DiaryManager.Instance != null)
                DiaryManager.Instance.AddClue(clue);
            else if (core != null && clueData != null)
                core.Session.RegisterClue(clueData);
            clue.Collect();
        }

        string playerName = core != null ? core.Session.PlayerName : "";
        string bodyText = clueData != null && !string.IsNullOrEmpty(clueData.description)
            ? Morsoul.Core.ClueHighlightFormatter.FormatDescription(clueData, playerName, darkBackground: true)
            : Morsoul.Core.ClueHighlightFormatter.FormatRawText(clue.defaultDescription, null, playerName, darkBackground: true);

        string toast = newlyRegistered
            ? "<color=#74DFEE><b>ADDED TO JOURNAL [G]</b></color> — Close Inspect ([E] or [ESC]) before opening Journal.\n\n"
            : string.Empty;
        string fullText = $"{toast}{bodyText}\n\n[Drag] Rotate   |   [Scroll] Zoom   |   [E / ESC] Close";

        ShowInspectOverlay(fullText, ResolveSourceVisual(clue), returnToJournal: false, onClosed: null);
    }

    /// <summary>
    /// Reopens the 3D/2D inspection view for an already-discovered clue from
    /// the Journal without re-triggering discovery, registration, or dialogue.
    /// </summary>
    public void OpenReinspect(Morsoul.Data.ClueData clueData, System.Action onClosed = null)
    {
        if (clueData == null || _isInspecting)
        {
            onClosed?.Invoke();
            return;
        }
        _isInspecting = true;

        var core = Morsoul.Core.CoreRuntime.Instance;
        string playerName = core != null ? core.Session.PlayerName : "";
        string bodyText = !string.IsNullOrEmpty(clueData.description)
            ? Morsoul.Core.ClueHighlightFormatter.FormatDescription(clueData, playerName, darkBackground: true)
            : clueData.displayName ?? clueData.id ?? "";
        string fullText = $"{bodyText}\n\n[Drag] Rotate   |   [Scroll] Zoom   |   [E / ESC] Return to Journal";

        GameObject sourceVisual = null;
        foreach (var cn in FindObjectsByType<ClueNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cn != null && cn.id == clueData.id)
            {
                sourceVisual = ResolveSourceVisual(cn);
                if (sourceVisual != null) break;
            }
        }
        if (sourceVisual == null)
            sourceVisual = clueData.inspectPrefab;

        ShowInspectOverlay(fullText, sourceVisual, returnToJournal: true, onClosed: onClosed);
    }

    void ShowInspectOverlay(string fullDescription, GameObject sourceVisual, bool returnToJournal, System.Action onClosed)
    {
        _returnToJournal = returnToJournal;
        _onReinspectClosed = onClosed;

        if (_inspectDescription != null)
            _inspectDescription.text = fullDescription;
        if (_inspectImage != null)
            _inspectImage.style.display = sourceVisual != null ? DisplayStyle.Flex : DisplayStyle.None;
        if (_inspectRoot != null)
            _inspectRoot.style.display = DisplayStyle.Flex;

        if (!returnToJournal)
        {
            HUDManager.Instance?.SetHUDVisibility(false);
            _prevTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            Shader.SetGlobalFloat("_ScanProgress", 0f);

            if (_scanController == null) _scanController = FindAnyObjectByType<ScanController>();
            if (_scanController != null) _scanController.enabled = false;

            if (_playerController == null) _playerController = FindAnyObjectByType<PlayerController>();
            if (_playerController != null) _playerController.enabled = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (sourceVisual != null)
        {
            BuildCenteredInspectClone(sourceVisual);
            if (_inspectLight != null) _inspectLight.enabled = true;
            if (inspectCamera != null)
            {
                inspectCamera.fieldOfView = 45f;
                inspectCamera.nearClipPlane = 0.02f;
                inspectCamera.enabled = true;
                _currentZoom = Mathf.Clamp(defaultInspectDistance, EffectiveMaxZoom(), EffectiveMinZoom());
                if (inspectOrigin != null)
                {
                    inspectCamera.transform.position = inspectOrigin.position + new Vector3(0f, 0f, _currentZoom);
                    inspectCamera.transform.LookAt(inspectOrigin.position);
                }
                inspectCamera.Render();
            }
            _inspectImage?.MarkDirtyRepaint();
        }
        else
        {
            CleanupClone();
            if (_inspectLight != null) _inspectLight.enabled = false;
            if (inspectCamera != null) inspectCamera.enabled = false;
        }
    }

    public void CloseInspect()
    {
        if (!_isInspecting) return;
        _isInspecting = false;

        if (_inspectRoot != null) _inspectRoot.style.display = DisplayStyle.None;
        if (_inspectLight != null) _inspectLight.enabled = false;
        if (inspectCamera != null) inspectCamera.enabled = false;
        CleanupClone();

        bool wasJournalReturn = _returnToJournal;
        var cb = _onReinspectClosed;
        _returnToJournal = false;
        _onReinspectClosed = null;

        if (wasJournalReturn)
        {
            cb?.Invoke();
            return;
        }

        Time.timeScale = _prevTimeScale > 0f ? _prevTimeScale : 1f;

        HUDManager.Instance?.SetHUDVisibility(true);

        if (_playerController != null) _playerController.enabled = true;
        if (_scanController != null) _scanController.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Morsoul.Core.PauseMenuController.NotifyExternalModalClosed();
    }

    private void Update()
    {
        if (!_isInspecting) return;
        _inspectImage?.MarkDirtyRepaint();

        if (Keyboard.current != null &&
            (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame))
        {
            CloseInspect();
            return;
        }

        if (Mouse.current == null || _inspectPivot == null) return;

        if (Mouse.current.leftButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            _inspectPivot.transform.Rotate(Vector3.up, -delta.x * rotateSpeed, Space.World);
            _inspectPivot.transform.Rotate(Vector3.right, delta.y * rotateSpeed, Space.World);
        }

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll != 0f && inspectCamera != null && inspectOrigin != null)
        {
            _currentZoom += scroll * 0.01f * zoomSpeed;
            _currentZoom = Mathf.Clamp(_currentZoom, EffectiveMaxZoom(), EffectiveMinZoom());
            inspectCamera.transform.position = inspectOrigin.position + new Vector3(0f, 0f, _currentZoom);
        }
    }

    private float EffectiveMinZoom() => Mathf.Min(minZoom, -1.05f);
    private float EffectiveMaxZoom() => Mathf.Min(maxZoom, -3.5f);

    private void CleanupClone()
    {
        if (_inspectPivot != null)
        {
            Destroy(_inspectPivot);
            _inspectPivot = null;
            _currentClone = null;
        }
        else if (_currentClone != null)
        {
            Destroy(_currentClone);
            _currentClone = null;
        }
    }

    private static GameObject ResolveSourceVisual(ClueNode clue)
    {
        if (clue == null) return null;
        return clue.GetComponentInChildren<MeshRenderer>() != null
            ? clue.gameObject
            : (clue.inspectVisual != null ? clue.inspectVisual : FindNearestSiblingVisual(clue));
    }

    private void BuildCenteredInspectClone(GameObject source)
    {
        CleanupClone();
        if (inspectOrigin == null || source == null) return;

        _inspectPivot = new GameObject("InspectPivot");
        _inspectPivot.transform.position = inspectOrigin.position;
        _inspectPivot.transform.rotation = Quaternion.identity;

        _currentClone = Instantiate(source, _inspectPivot.transform);
        _currentClone.SetActive(true);
        _currentClone.transform.localPosition = Vector3.zero;

        foreach (var col in _currentClone.GetComponentsInChildren<Collider>(true)) DestroyImmediate(col);
        foreach (var cn in _currentClone.GetComponentsInChildren<ClueNode>(true)) DestroyImmediate(cn);
        foreach (var rb in _currentClone.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb);
        foreach (var ps in _currentClone.GetComponentsInChildren<ParticleSystem>(true)) DestroyImmediate(ps);
        foreach (var psr in _currentClone.GetComponentsInChildren<ParticleSystemRenderer>(true)) DestroyImmediate(psr);

        int inspectLayer = LayerMask.NameToLayer("InspectLayer");
        if (inspectLayer >= 0) SetLayerRecursively(_inspectPivot, inspectLayer);

        if (TryComputeRendererBounds(_currentClone, out Bounds b))
        {
            float maxAxis = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (maxAxis > 0.0001f)
            {
                float scaleFactor = Mathf.Clamp(inspectFitExtent / maxAxis, 0.15f, 5.0f);
                _currentClone.transform.localScale *= scaleFactor;
            }
            if (TryComputeRendererBounds(_currentClone, out Bounds scaledBounds))
                _currentClone.transform.position += inspectOrigin.position - scaledBounds.center;
        }

        _inspectPivot.transform.rotation = Quaternion.Euler(18f, -24f, 0f);
    }

    public static bool TryComputeRendererBounds(GameObject root, out Bounds bounds) =>
        Morsoul.Core.InspectBoundsUtility.TryComputeRendererBounds(root, out bounds);

    private static GameObject FindNearestSiblingVisual(ClueNode clue)
    {
        if (clue.transform.parent == null) return null;
        GameObject best = null;
        float bestDist = 3.0f;
        foreach (Transform sibling in clue.transform.parent)
        {
            if (sibling == clue.transform || sibling.GetComponent<ClueNode>() != null) continue;
            if (sibling.GetComponentInChildren<MeshRenderer>() == null) continue;
            float d = Vector3.Distance(sibling.position, clue.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = sibling.gameObject;
            }
        }
        return best;
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}
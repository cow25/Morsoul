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
        if (panelRenderer != null) panelRenderer.RegisterUIReloadCallback(OnUIReload);
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
        if (_inspectDescription != null)
            _inspectDescription.text = $"{toast}{bodyText}\n\n[Drag] Rotate   |   [Scroll] Zoom   |   [E / ESC] Close";
        if (_inspectRoot != null)
            _inspectRoot.style.display = DisplayStyle.Flex;

        HUDManager.Instance?.SetHUDVisibility(false);

        _prevTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        Shader.SetGlobalFloat("_ScanProgress", 0f);

        if (_scanController == null) _scanController = FindAnyObjectByType<ScanController>();
        if (_scanController != null) _scanController.enabled = false;

        if (_playerController == null) _playerController = FindAnyObjectByType<PlayerController>();
        if (_playerController != null) _playerController.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        BuildCenteredInspectClone(clue);

        if (_inspectLight != null) _inspectLight.enabled = true;
        if (inspectCamera != null)
        {
            inspectCamera.fieldOfView = 45f;
            inspectCamera.nearClipPlane = 0.02f;
            inspectCamera.enabled = true;
            _currentZoom = Mathf.Clamp(defaultInspectDistance, EffectiveMaxZoom(), EffectiveMinZoom());
            if (inspectOrigin != null)
                inspectCamera.transform.position = inspectOrigin.position + new Vector3(0f, 0f, _currentZoom);
        }
    }

    public void CloseInspect()
    {
        if (!_isInspecting) return;
        _isInspecting = false;

        if (_inspectRoot != null) _inspectRoot.style.display = DisplayStyle.None;
        Time.timeScale = _prevTimeScale > 0f ? _prevTimeScale : 1f;

        HUDManager.Instance?.SetHUDVisibility(true);

        if (_playerController != null) _playerController.enabled = true;
        if (_scanController != null) _scanController.enabled = true;

        if (_inspectLight != null) _inspectLight.enabled = false;
        if (inspectCamera != null) inspectCamera.enabled = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        CleanupClone();
        Morsoul.Core.PauseMenuController.NotifyExternalModalClosed();
    }

    private void Update()
    {
        if (!_isInspecting) return;

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

    private void BuildCenteredInspectClone(ClueNode clue)
    {
        CleanupClone();
        if (inspectOrigin == null) return;

        GameObject source = clue.GetComponentInChildren<MeshRenderer>() != null
            ? clue.gameObject
            : (clue.inspectVisual != null ? clue.inspectVisual : FindNearestSiblingVisual(clue));

        _inspectPivot = new GameObject("InspectPivot");
        _inspectPivot.transform.position = inspectOrigin.position;
        _inspectPivot.transform.rotation = Quaternion.identity;

        _currentClone = Instantiate(source != null ? source : clue.gameObject, _inspectPivot.transform);
        _currentClone.transform.localPosition = Vector3.zero;

        foreach (var col in _currentClone.GetComponentsInChildren<Collider>(true)) Destroy(col);
        foreach (var cn in _currentClone.GetComponentsInChildren<ClueNode>(true)) Destroy(cn);
        foreach (var rb in _currentClone.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
        foreach (var ps in _currentClone.GetComponentsInChildren<ParticleSystem>(true)) Destroy(ps);

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
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
    public float minZoom = -2f;
    public float maxZoom = -5f;

    private PanelRenderer panelRenderer;
    private VisualElement _inspectRoot;
    private Image _inspectImage;
    private Label _inspectDescription;

    private GameObject _currentClone;
    private float _currentZoom = -1f;
    private bool _isInspecting = false;
    
    private MonoBehaviour _playerController; 
    private ScanController _scanController;
    private Light _inspectLight; // NEW: Store the light reference
    private System.Func<bool> _modalBlocker;

    private void Awake() 
    {
        Instance = this;
        _modalBlocker = () => _isInspecting;
        Morsoul.Core.PauseMenuController.RegisterExternalModal(_modalBlocker);
        
        // Create the light once and attach it to the Inspect Camera
        GameObject lightObj = new GameObject("InspectLight");
        lightObj.transform.SetParent(inspectCamera.transform);
        lightObj.transform.localPosition = Vector3.zero;
        lightObj.transform.localRotation = Quaternion.identity;

        _inspectLight = lightObj.AddComponent<Light>();
        _inspectLight.type = LightType.Directional; // Directional light shines evenly
        _inspectLight.intensity = 0.5f;
        _inspectLight.cullingMask = LayerMask.GetMask("InspectLayer"); // Only light the inspect layer
        _inspectLight.enabled = false; // Turn it off by default
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

        _inspectImage.image = inspectRT;
        _inspectRoot.style.display = DisplayStyle.None;
    }

    public void OpenInspect(ClueNode clue)
    {
        if (clue == null || _isInspecting) return;

        if (Morsoul.Core.CoreRuntime.Instance is { } core && core.Session != null && !string.IsNullOrEmpty(clue.id))
        {
            var clueData = core.ResolveClue(clue.id);
            if (clueData != null)
            {
                var log = clueData.type == Morsoul.Data.ClueType.Latent
                    ? core.ResolveCase(clueData.caseId)?.latentLog
                    : null;
                if (!Morsoul.Core.ClueAcquisition.CanAcquire(clueData, core.Session, log, out _))
                {
                    return;
                }
            }
        }

        _isInspecting = true;

        bool newlyRegistered = !DiaryManager.Instance.HasClue(clue.id);
        if (newlyRegistered)
        {
            DiaryManager.Instance.AddClue(clue);
            clue.Collect();
        }
        string toast = newlyRegistered ? "Registered to Journal\n\n" : string.Empty;
        _inspectDescription.text = $"{toast}{clue.defaultDescription}\n\n[Drag] Rotate   |   [Scroll] Zoom   |   [ESC] Close";
        _inspectRoot.style.display = DisplayStyle.Flex;

        Time.timeScale = 0f; 
        Shader.SetGlobalFloat("_ScanProgress", 0f);

        if (_scanController == null) _scanController = FindAnyObjectByType<ScanController>();
        if (_scanController != null) _scanController.enabled = false;

        if (_playerController == null) _playerController = FindAnyObjectByType<PlayerController>();
        if (_playerController != null) _playerController.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (_currentClone != null) Destroy(_currentClone);
        GameObject source = clue.GetComponentInChildren<MeshRenderer>() != null
            ? clue.gameObject
            : (clue.inspectVisual != null ? clue.inspectVisual : FindNearestSiblingVisual(clue));
        _currentClone = Instantiate(source != null ? source : clue.gameObject, inspectOrigin.position, Quaternion.identity);
        
        foreach (var col in _currentClone.GetComponentsInChildren<Collider>(true)) Destroy(col);
        if (_currentClone.TryGetComponent(out ClueNode cn)) Destroy(cn);
        if (_currentClone.TryGetComponent(out Rigidbody rb)) Destroy(rb);

        SetLayerRecursively(_currentClone, LayerMask.NameToLayer("InspectLayer"));
        
        // Turn on the camera light
        if (_inspectLight != null) _inspectLight.enabled = true;

        inspectCamera.transform.position = inspectOrigin.position + new Vector3(0, 0, _currentZoom);
    }

    public void CloseInspect()
    {
        if (!_isInspecting) return;
        _isInspecting = false;

        _inspectRoot.style.display = DisplayStyle.None;
        Time.timeScale = 1f; 
        
        if (_playerController != null) _playerController.enabled = true;
        if (_scanController != null) _scanController.enabled = true;

        // Turn off the camera light
        if (_inspectLight != null) _inspectLight.enabled = false;

        // Lock and hide the cursor so the player can look around again
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (_currentClone != null) Destroy(_currentClone);
        Morsoul.Core.PauseMenuController.NotifyExternalModalClosed();
    }

    private void Update()
    {
        if (!_isInspecting) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseInspect();
            return;
        }

        if (Mouse.current == null || _currentClone == null) return;

        if (Mouse.current.leftButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            _currentClone.transform.Rotate(Vector3.up, -delta.x * rotateSpeed, Space.World);
            _currentClone.transform.Rotate(Vector3.right, delta.y * rotateSpeed, Space.World);
        }

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll != 0)
        {
            _currentZoom += scroll * 0.01f * zoomSpeed;
            _currentZoom = Mathf.Clamp(_currentZoom, maxZoom, minZoom);
            inspectCamera.transform.position = inspectOrigin.position + new Vector3(0, 0, _currentZoom);
        }
    }

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
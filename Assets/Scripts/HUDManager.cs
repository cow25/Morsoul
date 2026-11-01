using UnityEngine;
using UnityEngine.UIElements;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; private set; }
    private PanelRenderer panelRenderer; 

    private VisualElement _aetherSightRow;
    private VisualElement _interactionContainer;
    private Label _focusValueLabel;
    private VisualElement _focusBarFill;
    private Label _interactionText;
    private Label _itemDescriptionText;
    private Label _focusCostText;

    private int _cachedCurrentFocus = 120;
    private int _cachedMaxFocus = 120;
    private float _cachedFocusCost = 30f;
    private int _uiVersion = 0;

    private void Awake() => Instance = this;

    private void OnEnable()
    {
        panelRenderer = GetComponent<PanelRenderer>();
        if (panelRenderer != null) panelRenderer.RegisterUIReloadCallback(OnUIReload);
    }

    private void OnDisable()
    {
        if (panelRenderer != null) panelRenderer.UnregisterUIReloadCallback(OnUIReload);
    }

    private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
    {
        if (_uiVersion == version) return;
        _uiVersion = version;

        _aetherSightRow = root.Q<VisualElement>("AetherSightRow");
        _focusValueLabel = root.Q<Label>("FocusValueLabel");
        _focusBarFill = root.Q<VisualElement>("FocusBarFill");
        _focusCostText = root.Q<Label>("FocusCostText");
        // _focusCostText.text = "Focus Energy Cost: 15";
        if (_focusCostText != null) _focusCostText.text = $"Focus Energy Cost: {_cachedFocusCost}";
        
        _interactionContainer = root.Q<VisualElement>("InteractionContainer");
        _interactionText = root.Q<Label>("InteractionText");
        _itemDescriptionText = root.Q<Label>("ItemDescriptionText");

        _aetherSightRow.style.display = DisplayStyle.None;
        _interactionContainer.style.display = DisplayStyle.None;
        
        ApplyFocusToUI();
    }


    public void UpdateFocus(int currentFocus, int maxFocus)
    {
        _cachedCurrentFocus = currentFocus;
        _cachedMaxFocus = maxFocus;
        
        ApplyFocusToUI();
    }

    private void ApplyFocusToUI()
    {
        if (_focusValueLabel != null)
            _focusValueLabel.text = $"{_cachedCurrentFocus}/{_cachedMaxFocus}";
            
        if (_focusBarFill != null && _cachedMaxFocus > 0)
        {
            float percent = (float)_cachedCurrentFocus / _cachedMaxFocus * 100f;
            _focusBarFill.style.width = Length.Percent(percent);
        }
    }

    public void UnlockAetherHUD()
    {
        if (_aetherSightRow != null)
            _aetherSightRow.style.display = DisplayStyle.Flex;
    }

    public void UpdateFocusCost(float cost)
    {
        _cachedFocusCost = cost;
        if (_focusCostText != null)
            _focusCostText.text = $"Focus Energy Cost: {cost}";
    }

    public void ShowInteractionPrompt(string actionText, string description)
    {
        if (_interactionContainer != null)
        {
            _interactionText.text = actionText;
            _itemDescriptionText.text = description;
            _interactionContainer.style.display = DisplayStyle.Flex;
        }
    }

    public void HideInteractionPrompt()
    {
        if (_interactionContainer != null)
            _interactionContainer.style.display = DisplayStyle.None;
    }
}

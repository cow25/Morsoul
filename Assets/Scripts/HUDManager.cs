using System;
using System.Collections.Generic;
using System.Linq;
using Morsoul.Board;
using Morsoul.Core;
using Morsoul.Data;
using Morsoul.Hub;
using Morsoul.Journal;
using UnityEngine;
using UnityEngine.UIElements;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; private set; }
    public VisualElement HudRoot => _hudRoot;
    public event Action<VisualElement> HudLoaded;
    public bool IsJournalOpen => _journalController != null && _journalController.IsOpen;
    public bool IsBoardOpen => (_weightedBoard != null && _weightedBoard.IsOpen) || (_legacyBoard != null && _legacyBoard.IsOpen);
    private PanelRenderer panelRenderer; 

    private VisualElement _golemEyeRow;
    private VisualElement _aetherSightRow;
    private VisualElement _emotionEchoRow;
    private VisualElement _interactionContainer;
    private VisualElement _hudRoot;
    private VisualElement _objectivePanel;
    private Label _userNameLabel;
    private Label _focusValueLabel;
    private VisualElement _focusBarFill;
    private Label _interactionText;
    private Label _itemDescriptionText;
    private Label _focusCostText;
    private Label _activeGoalTitle;
    private Label _objectiveLabel;
    private Label _caseLabel;
    private Button _deskButton;
    private Button _boardButton;
    private Button _escButton;

    private JournalController _journalController;
    private HubJournal _hubJournal;
    private WeightedBoardController _weightedBoard;
    private BoardController _legacyBoard;
    private DiaryManager _diaryManager;
    private Func<bool> _modalBlocker;
    private int _syncedDiaryCount = -1;
    private int _syncedSessionClueCount = -1;
    private readonly List<ClueData> _runtimeClues = new List<ClueData>();

    private int _cachedCurrentFocus = 120;
    private int _cachedMaxFocus = 120;
    private float _cachedFocusCost = 30f;
    private string _cachedObjective = "Examine the ancient ruins and locate clues.";
    private string _cachedCaseTitle = "Prologue";
    private int _uiVersion = 0;

    private void Awake() => Instance = this;

    private void Start()
    {
        var core = CoreRuntime.Ensure();
        if (core != null && core.Session.CurrentSceneMode != SceneMode.Mode3D)
            core.Session.CurrentSceneMode = SceneMode.Mode3D;

        EnsureOverlayPanels();
        RegisterModalBlocker();
    }

    private void OnEnable()
    {
        panelRenderer = GetComponent<PanelRenderer>();
        if (panelRenderer != null)
        {
            panelRenderer.sortingOrder = 0;
            panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }
    }

    private void OnDisable()
    {
        if (panelRenderer != null) panelRenderer.UnregisterUIReloadCallback(OnUIReload);
    }

    private void OnDestroy()
    {
        if (_modalBlocker != null)
        {
            PauseMenuController.UnregisterExternalModal(_modalBlocker);
            _modalBlocker = null;
        }
        foreach (var c in _runtimeClues)
            if (c != null) Destroy(c);
        _runtimeClues.Clear();
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        SyncSessionState();
        SyncDiaryToJournal();
        UpdateKeyHighlights();
    }

    private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
    {
        if (_uiVersion == version) return;
        _uiVersion = version;

        _hudRoot = root.Q<VisualElement>("HUD-Root"); 

        _golemEyeRow = root.Q<VisualElement>("GolemEyeRow");
        _aetherSightRow = root.Q<VisualElement>("AetherSightRow");
        _emotionEchoRow = root.Q<VisualElement>("EmotionEchoRow");
        _userNameLabel = root.Q<Label>("UserNameLabel");
        _focusValueLabel = root.Q<Label>("FocusValueLabel");
        _focusBarFill = root.Q<VisualElement>("FocusBarFill");
        _focusCostText = root.Q<Label>("FocusCostText");
        if (_focusCostText != null) _focusCostText.text = $"Focus Energy Cost: {_cachedFocusCost}";

        _objectivePanel = root.Q<VisualElement>("ObjectivePanel");
        _activeGoalTitle = root.Q<Label>("ActiveGoalTitle");
        _objectiveLabel = root.Q<Label>("ObjectiveLabel");
        _caseLabel = root.Q<Label>("CaseLabel");
        _deskButton = root.Q<Button>("DeskKeyButton");
        if (_deskButton != null) _deskButton.clicked += ToggleJournal;
        _boardButton = root.Q<Button>("BoardKeyButton");
        if (_boardButton != null) _boardButton.clicked += ToggleBoard;
        _escButton = root.Q<Button>("EscKeyButton");
        if (_escButton != null) _escButton.clicked += PauseMenuController.TogglePause;
        
        _interactionContainer = root.Q<VisualElement>("InteractionContainer");
        _interactionText = root.Q<Label>("InteractionText");
        _itemDescriptionText = root.Q<Label>("ItemDescriptionText");

        if (_aetherSightRow != null) _aetherSightRow.style.display = DisplayStyle.None;
        if (_emotionEchoRow != null) _emotionEchoRow.style.display = DisplayStyle.None;
        if (_interactionContainer != null) _interactionContainer.style.display = DisplayStyle.None;
        
        ApplyFocusToUI();
        ApplyObjectiveToUI();
        SyncSessionState();
        HudLoaded?.Invoke(_hudRoot);
    }

    private void SyncSessionState()
    {
        var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
        if (_userNameLabel != null)
        {
            string displayName = session != null && !string.IsNullOrWhiteSpace(session.PlayerName)
                ? session.PlayerName.Trim() : "User";
            if (_userNameLabel.text != displayName) _userNameLabel.text = displayName;
        }
        if (session != null)
        {
            if (session.Tools.Contains(Tool.AetherSight)) UnlockAetherHUD();
            if (session.Tools.Contains(Tool.EmotionEcho)) UnlockEmotionEchoHUD();
            int caseNum = session.InvestigationCaseId >= 0 ? session.InvestigationCaseId : session.CurrentCase;
            string expectedCase = caseNum <= 0 ? "Prologue" : $"Case {caseNum}";
            if (_caseLabel != null && _caseLabel.text != expectedCase)
            {
                _cachedCaseTitle = expectedCase;
                _caseLabel.text = expectedCase;
            }
        }
    }

    private void UpdateKeyHighlights()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return;

        if (_escButton != null)
            _escButton.style.opacity = keyboard.escapeKey.isPressed ? 1f : 0.2f;
        if (_golemEyeRow != null)
            _golemEyeRow.style.opacity = keyboard.tabKey.isPressed ? 1f : 0.2f;
        if (_deskButton != null)
            _deskButton.style.opacity = keyboard.gKey.isPressed ? 1f : 0.2f;
        if (_boardButton != null)
            _boardButton.style.opacity = keyboard.hKey.isPressed ? 1f : 0.2f;
    }

    public void SetObjective(string objective, string caseTitle = null)
    {
        _cachedObjective = objective ?? "";
        if (caseTitle != null) _cachedCaseTitle = caseTitle;
        ApplyObjectiveToUI();
    }

    private void ApplyObjectiveToUI()
    {
        bool hasObj = !string.IsNullOrEmpty(_cachedObjective);
        if (_objectivePanel != null)
            _objectivePanel.style.display = hasObj ? DisplayStyle.Flex : DisplayStyle.None;
        if (_activeGoalTitle != null)
            _activeGoalTitle.style.display = hasObj ? DisplayStyle.Flex : DisplayStyle.None;
        if (_objectiveLabel != null)
            _objectiveLabel.text = _cachedObjective;
        if (_caseLabel != null)
        {
            _caseLabel.text = _cachedCaseTitle ?? "";
            _caseLabel.style.display = string.IsNullOrEmpty(_cachedCaseTitle)
                ? DisplayStyle.None : DisplayStyle.Flex;
        }
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

    public void UnlockEmotionEchoHUD()
    {
        if (_emotionEchoRow != null)
            _emotionEchoRow.style.display = DisplayStyle.Flex;
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

    public void SetHUDVisibility(bool isVisible)
{
    if (_hudRoot != null)
    {
        _hudRoot.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
    private void EnsureOverlayPanels()
    {
        var core = CoreRuntime.Ensure();
        if (core != null && core.Session.CurrentSceneMode != SceneMode.Mode3D)
            core.Session.CurrentSceneMode = SceneMode.Mode3D;

        var config = Resources.Load<PauseMenuAssets>("PauseMenuConfig");
        var panelSettings = panelRenderer != null ? panelRenderer.panelSettings
            : (config != null ? config.panelSettings : null);
        if (panelSettings == null && config != null) panelSettings = config.panelSettings;

        _journalController = FindAnyObjectByType<JournalController>(FindObjectsInactive.Include);
        if (_journalController == null && config != null && config.journalUxml != null)
        {
            var journalGo = new GameObject("JournalPanel");
            journalGo.SetActive(false);
            var hubJournal = journalGo.AddComponent<HubJournal>();
            var pr = journalGo.AddComponent<PanelRenderer>();
            pr.panelSettings = panelSettings;
            pr.visualTreeAsset = config.journalUxml;
            var jc = journalGo.AddComponent<JournalController>();
            jc.journal = hubJournal;
            jc.manageCursor = true;
            journalGo.SetActive(true);
            _journalController = jc;
            _hubJournal = hubJournal;
        }
        else if (_journalController != null)
        {
            _hubJournal = _journalController.journal ?? _journalController.GetComponent<HubJournal>()
                ?? FindAnyObjectByType<HubJournal>(FindObjectsInactive.Include);
            if (_hubJournal != null && _journalController.journal == null)
                _journalController.SetJournal(_hubJournal);
        }

        if (_journalController != null)
        {
            var prevCanOpen = _journalController.CanOpen;
            _journalController.CanOpen = () =>
                (prevCanOpen == null || prevCanOpen()) &&
                !PauseMenuController.IsPaused &&
                !(_weightedBoard != null && _weightedBoard.IsOpen) &&
                !(_legacyBoard != null && _legacyBoard.IsOpen);
            SyncSessionCluesToHubJournal();
        }

        _weightedBoard = FindAnyObjectByType<WeightedBoardController>(FindObjectsInactive.Include);
        _legacyBoard = FindAnyObjectByType<BoardController>(FindObjectsInactive.Include);

        if (_weightedBoard == null && _legacyBoard == null && config != null && config.weightedBoardUxml != null)
        {
            var boardGo = new GameObject("WeightedBoardPanel");
            boardGo.SetActive(false);
            var pr = boardGo.AddComponent<PanelRenderer>();
            pr.panelSettings = panelSettings;
            pr.visualTreeAsset = config.weightedBoardUxml;
            var wb = boardGo.AddComponent<WeightedBoardController>();
            wb.manageCursor = true;
            var hotkey = boardGo.AddComponent<BoardHotkey>();
            hotkey.board = wb;
            boardGo.SetActive(true);
            _weightedBoard = wb;
        }

        if (_weightedBoard != null)
        {
            var existingHotkey = _weightedBoard.GetComponent<BoardHotkey>();
            if (existingHotkey == null)
            {
                existingHotkey = _weightedBoard.gameObject.AddComponent<BoardHotkey>();
                existingHotkey.board = _weightedBoard;
            }
            WrapWeightedBoardHooks();
        }
        RegisterModalBlocker();
    }

    private void WrapWeightedBoardHooks()
    {
        if (_weightedBoard == null) return;
        var baseCanUse = _weightedBoard.CanUse;
        _weightedBoard.CanUse = () =>
            (baseCanUse == null || baseCanUse()) &&
            !PauseMenuController.IsPaused &&
            !(_journalController != null && _journalController.IsOpen);

        var baseEnsure = _weightedBoard.EnsureSession;
        if (baseEnsure != null)
        {
            _weightedBoard.EnsureSession = () =>
            {
                bool ok = baseEnsure();
                _weightedBoard.CanUse = () =>
                    CoreRuntime.Instance != null &&
                    CoreRuntime.Instance.Session.CurrentSceneMode == SceneMode.Mode3D &&
                    !PauseMenuController.IsPaused &&
                    !(_journalController != null && _journalController.IsOpen);
                return ok;
            };
        }
    }

    private void RegisterModalBlocker()
    {
        if (_modalBlocker != null) return;
        _modalBlocker = () =>
            (_weightedBoard != null && (_weightedBoard.IsOpen || _weightedBoard.LastCloseFrame == Time.frameCount)) ||
            (_legacyBoard != null && (_legacyBoard.IsOpen || _legacyBoard.LastCloseFrame == Time.frameCount));
        PauseMenuController.RegisterExternalModal(_modalBlocker);
    }

    private void SyncSessionCluesToHubJournal()
    {
        if (_hubJournal == null || CoreRuntime.Instance == null) return;
        var core = CoreRuntime.Instance;
        _syncedSessionClueCount = core.Session.ClueIds.Count;
        foreach (string id in core.Session.ClueIds)
        {
            var clue = core.ResolveClue(id);
            if (clue != null) _hubJournal.Register(clue);
        }
    }

    private void SyncDiaryToJournal()
    {
        if (_hubJournal == null) return;
        var core = CoreRuntime.Instance;
        if (core != null && core.Session.ClueIds.Count != _syncedSessionClueCount)
            SyncSessionCluesToHubJournal();

        if (_diaryManager == null) _diaryManager = FindAnyObjectByType<DiaryManager>();
        if (_diaryManager == null) return;
        int count = _diaryManager.collectedClues.Count;
        if (count == _syncedDiaryCount) return;
        _syncedDiaryCount = count;

        foreach (var node in _diaryManager.collectedClues)
        {
            if (node == null || string.IsNullOrEmpty(node.id)) continue;
            var resolved = core != null ? core.ResolveClue(node.id) : null;
            if (resolved == null)
            {
                resolved = ScriptableObject.CreateInstance<ClueData>();
                resolved.hideFlags = HideFlags.DontSave;
                resolved.id = node.id;
                resolved.displayName = node.id;
                resolved.description = node.defaultDescription;
                resolved.type = node.kind == ClueNode.Kind.Dialogue
                    ? ClueType.Testimony : ClueType.Physical;
                _runtimeClues.Add(resolved);
            }
            core?.Session.RegisterClue(resolved);
            _hubJournal.Register(resolved);
        }
    }

    public void ToggleJournal()
    {
        if (_journalController == null) EnsureOverlayPanels();
        if (_journalController == null) return;
        if (_journalController.IsOpen)
        {
            _journalController.Close();
            return;
        }
        if (PauseMenuController.IsPaused) return;
        if (_weightedBoard != null && _weightedBoard.IsOpen)
            _weightedBoard.Close();
        if (_legacyBoard != null && _legacyBoard.IsOpen)
            _legacyBoard.Close();
        if (_journalController.OpenAllowed())
            _journalController.Open();
    }

    public void ToggleBoard()
    {
        if (_weightedBoard == null && _legacyBoard == null) EnsureOverlayPanels();
        if (PauseMenuController.IsPaused) return;
        if (_journalController != null && _journalController.IsOpen)
            _journalController.Close();

        if (_weightedBoard != null)
        {
            if (_weightedBoard.IsOpen) _weightedBoard.Close();
            else _weightedBoard.Open();
            return;
        }
        if (_legacyBoard != null)
        {
            if (_legacyBoard.IsOpen) _legacyBoard.Close();
            else _legacyBoard.Open();
        }
    }
}

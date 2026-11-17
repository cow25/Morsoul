using Morsoul.Board;
using Morsoul.Core;
using Morsoul.Data;
using Morsoul.Hub;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

public class PrologueTutorialController : MonoBehaviour
{
    public const string DiagnosticClueId = "T1_00";
    public const string DeskLogsClueId = "C1_00";
    public const string CapacitorClueId = "C2_00";
    public const string KeyClueId = "C3_00";

    private enum Phase
    {
        AwakeningDialogue,
        FreeExploreBeforeScan,
        ScanWindup,
        ScanTutorialModal,
        Investigation,
        PowerDownBlackout
    }

    public float scanTriggerExploreDelay = 3.5f;
    public float accidentalScanRadius = 4.5f;
    public float awakeningCameraOffsetY = -0.65f;

    private static readonly string[] AwakeningLines =
    {
        ">> CORE RE-INITIALIZATION: OK. CHASSIS INTEGRITY: 42%. MEMORY SECTORS 01–89: UNREADABLE.",
        "Where... am I? This body feels heavy—stone and brass, yet completely unfamiliar. I remember nothing.",
        "The stone gate ahead is sealed. I should look around these ruins and see what I can find."
    };

    private static readonly (string title, string body, bool isWarning)[] ScanPages =
    {
        ("GOLEM EYE", "Your optical analyzer just triggered on its own.\nIt highlights hidden or unreadable clues so you can [L-Click] to inspect them.", false),
        ("SCAN CONTROLS", "Hold [Tab] to scan your surroundings.\nRelease [Tab] to turn it off.", false),
        ("FOCUS ENERGY WARNING", "Scanning drains Focus Energy (top-left bar). Inspecting clues costs 0 extra Focus.\nIf Focus reaches 0, you will power down and reboot at your last checkpoint.", true)
    };

    private ScanController _scanController;
    private PlayerController _playerController;
    private CharacterController _characterController;
    private WeightedBoardController _boardController;
    private Transform _playerTransform;
    private Transform _cradleTransform;
    private Transform _cameraTransform;
    private float _cameraBaseLocalY;
    private float _currentCameraOffsetY;
    private bool _cameraOffsetInitialized;
    private bool _awakeningOffsetActive;
    private ClueNode _deskLogsNode;

    private Phase _phase = Phase.AwakeningDialogue;
    private int _dialogueIndex;
    private int _scanPageIndex;
    private float _exploreTimer;
    private float _windupTimer;
    private float _blackoutTimer;
    private int _lastClueCount;
    private bool _focusRefilledOnSolve;
    private string _lastObjective = "";

    private Vector3 _checkpointPos;
    private Quaternion _checkpointRot;

    private VisualElement _dialogueBox;
    private Label _dialogueTextLabel;

    private VisualElement _scanModal;
    private Label _scanModalTitle;
    private Label _scanModalBody;
    private Button _scanModalBtn;

    private VisualElement _blackoutOverlay;
    private Label _blackoutLabel;

    private VisualElement _boardStrip;
    private Label _boardStripLabel;

    private void Start()
    {
        var core = CoreRuntime.Ensure();
        if (core != null)
        {
            core.Session.CurrentCase = 0;
            core.Session.InvestigationCaseId = 0;
            core.Session.CurrentSceneMode = SceneMode.Mode3D;

            var t1 = core.ResolveClue(DiagnosticClueId);
            if (t1 != null) core.Session.RegisterClue(t1);
            if (core.AutoCompleteCasesEnabled)
                Morsoul.Flow.InvestigationCompletion.ForceCompleteCase(core, 0);
            _lastClueCount = core.Session.ClueIds.Count;
        }

        _scanController = FindAnyObjectByType<ScanController>();
        _playerController = FindAnyObjectByType<PlayerController>();
        if (_playerController != null)
        {
            _playerTransform = _playerController.transform;
            _characterController = _playerController.GetComponent<CharacterController>();
            _checkpointPos = _playerTransform.position;
            _checkpointRot = _playerTransform.rotation;
        }

        EnsureCradleAnchor();
        EnsureTravelerRobeProp();
        EnsureExitGateSeam();

        foreach (var node in FindObjectsByType<ClueNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (node != null && node.id == DeskLogsClueId)
            {
                _deskLogsNode = node;
                break;
            }
        }

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.HudLoaded += OnHudLoaded;
            if (HUDManager.Instance.HudRoot != null)
                BuildOverlays(HUDManager.Instance.HudRoot);
        }

        EnterAwakeningDialogue();
    }

    private void OnDestroy()
    {
        ResetAwakeningCameraOffset();
        if (HUDManager.Instance != null)
            HUDManager.Instance.HudLoaded -= OnHudLoaded;
        if (_boardController != null)
            _boardController.BoardLoaded -= OnBoardLoaded;
    }

    private void OnHudLoaded(VisualElement hudRoot)
    {
        BuildOverlays(hudRoot);
        RefreshOverlays();
    }

    private void OnBoardLoaded(VisualElement boardRoot)
    {
        BuildBoardStrip(boardRoot);
        RefreshBoardStrip();
    }

    private void BuildOverlays(VisualElement hudRoot)
    {
        if (hudRoot == null) return;
        hudRoot.Q<VisualElement>("PrologueAwakeningDialogue")?.RemoveFromHierarchy();
        hudRoot.Q<VisualElement>("PrologueScanModal")?.RemoveFromHierarchy();
        hudRoot.Q<VisualElement>("PrologueBlackoutOverlay")?.RemoveFromHierarchy();

        var cyan = new Color(0.28f, 0.82f, 0.92f, 0.9f);

        // 1. Awakening Dialogue Box
        _dialogueBox = new VisualElement { name = "PrologueAwakeningDialogue" };
        _dialogueBox.style.position = Position.Absolute;
        _dialogueBox.style.left = Length.Percent(18);
        _dialogueBox.style.right = Length.Percent(18);
        _dialogueBox.style.bottom = 42;
        _dialogueBox.style.paddingLeft = 20;
        _dialogueBox.style.paddingRight = 20;
        _dialogueBox.style.paddingTop = 14;
        _dialogueBox.style.paddingBottom = 14;
        _dialogueBox.style.backgroundColor = new Color(0.04f, 0.06f, 0.09f, 0.92f);
        SetBorder(_dialogueBox, 2, cyan, 6);

        var speaker = new Label("INTERNAL DIAGNOSTIC");
        speaker.style.fontSize = 13;
        speaker.style.unityFontStyleAndWeight = FontStyle.Bold;
        speaker.style.color = new Color(0.45f, 0.92f, 1f, 1f);
        speaker.style.marginBottom = 6;
        _dialogueBox.Add(speaker);

        _dialogueTextLabel = new Label();
        _dialogueTextLabel.style.fontSize = 15;
        _dialogueTextLabel.style.color = new Color(0.94f, 0.95f, 0.97f, 1f);
        _dialogueTextLabel.style.whiteSpace = WhiteSpace.Normal;
        _dialogueTextLabel.style.marginBottom = 8;
        _dialogueBox.Add(_dialogueTextLabel);

        var hint = new Label("[Space / E / Click] Continue");
        hint.style.fontSize = 12;
        hint.style.color = new Color(0.65f, 0.78f, 0.84f, 1f);
        hint.style.unityTextAlign = TextAnchor.MiddleRight;
        _dialogueBox.Add(hint);
        _dialogueBox.RegisterCallback<ClickEvent>(_ => AdvanceDialogue());
        hudRoot.Add(_dialogueBox);

        // 2. Paged Scan Tutorial Modal (one piece of info per page)
        _scanModal = new VisualElement { name = "PrologueScanModal" };
        _scanModal.style.position = Position.Absolute;
        _scanModal.style.left = Length.Percent(28);
        _scanModal.style.right = Length.Percent(28);
        _scanModal.style.top = Length.Percent(30);
        _scanModal.style.paddingLeft = 24;
        _scanModal.style.paddingRight = 24;
        _scanModal.style.paddingTop = 20;
        _scanModal.style.paddingBottom = 18;
        _scanModal.style.backgroundColor = new Color(0.04f, 0.07f, 0.11f, 0.96f);
        SetBorder(_scanModal, 2, cyan, 6);

        _scanModalTitle = new Label();
        _scanModalTitle.style.fontSize = 18;
        _scanModalTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        _scanModalTitle.style.marginBottom = 12;
        _scanModal.Add(_scanModalTitle);

        _scanModalBody = new Label();
        _scanModalBody.style.fontSize = 15;
        _scanModalBody.style.whiteSpace = WhiteSpace.Normal;
        _scanModalBody.style.marginBottom = 18;
        _scanModal.Add(_scanModalBody);

        _scanModalBtn = new Button(AdvanceScanModal);
        _scanModalBtn.style.alignSelf = Align.FlexEnd;
        _scanModalBtn.style.paddingLeft = 18;
        _scanModalBtn.style.paddingRight = 18;
        _scanModalBtn.style.paddingTop = 8;
        _scanModalBtn.style.paddingBottom = 8;
        _scanModalBtn.style.fontSize = 14;
        _scanModalBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
        _scanModalBtn.style.backgroundColor = new Color(0.12f, 0.36f, 0.44f, 1f);
        _scanModalBtn.style.color = Color.white;
        SetBorder(_scanModalBtn, 1, cyan, 4);
        _scanModal.Add(_scanModalBtn);

        hudRoot.Add(_scanModal);

        // 3. Anti-hardstuck Power-Down Blackout Overlay
        _blackoutOverlay = new VisualElement { name = "PrologueBlackoutOverlay" };
        _blackoutOverlay.pickingMode = PickingMode.Ignore;
        _blackoutOverlay.style.position = Position.Absolute;
        _blackoutOverlay.style.left = 0;
        _blackoutOverlay.style.right = 0;
        _blackoutOverlay.style.top = 0;
        _blackoutOverlay.style.bottom = 0;
        _blackoutOverlay.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        _blackoutOverlay.style.justifyContent = Justify.Center;
        _blackoutOverlay.style.alignItems = Align.Center;

        _blackoutLabel = new Label();
        _blackoutLabel.style.fontSize = 18;
        _blackoutLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _blackoutLabel.style.color = new Color(0.95f, 0.65f, 0.22f, 1f);
        _blackoutLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _blackoutLabel.style.whiteSpace = WhiteSpace.Normal;
        _blackoutOverlay.Add(_blackoutLabel);
        hudRoot.Add(_blackoutOverlay);

        RefreshOverlays();
    }

    private void BuildBoardStrip(VisualElement boardRoot)
    {
        if (boardRoot == null) return;
        boardRoot.Q<VisualElement>("PrologueBoardTutorialStrip")?.RemoveFromHierarchy();

        _boardStrip = new VisualElement { name = "PrologueBoardTutorialStrip" };
        _boardStrip.pickingMode = PickingMode.Ignore;
        _boardStrip.style.position = Position.Absolute;
        _boardStrip.style.left = 24;
        _boardStrip.style.bottom = 18;
        _boardStrip.style.paddingLeft = 12;
        _boardStrip.style.paddingRight = 12;
        _boardStrip.style.paddingTop = 6;
        _boardStrip.style.paddingBottom = 6;
        _boardStrip.style.backgroundColor = new Color(0.05f, 0.08f, 0.11f, 0.90f);
        SetBorder(_boardStrip, 1, new Color(0.28f, 0.82f, 0.92f, 0.85f), 4);

        _boardStripLabel = new Label();
        _boardStripLabel.pickingMode = PickingMode.Ignore;
        _boardStripLabel.style.fontSize = 12;
        _boardStripLabel.style.color = new Color(0.88f, 0.94f, 0.97f, 1f);
        _boardStrip.Add(_boardStripLabel);
        boardRoot.Add(_boardStrip);
    }

    private void Update()
    {
        EnsureBoardGuardrails();
        UpdateCheckpointOnClueGain();
        UpdateAwakeningCameraOffset();

        switch (_phase)
        {
            case Phase.AwakeningDialogue:
                HandleDialogueInput();
                break;
            case Phase.FreeExploreBeforeScan:
                UpdateBeforeScan();
                break;
            case Phase.ScanWindup:
                UpdateScanWindup();
                break;
            case Phase.ScanTutorialModal:
                HandleScanModalInput();
                break;
            case Phase.Investigation:
                UpdateInvestigation();
                break;
            case Phase.PowerDownBlackout:
                UpdateBlackout();
                break;
        }

        RefreshBoardStrip();
    }

    private void EnterAwakeningDialogue()
    {
        _phase = Phase.AwakeningDialogue;
        _dialogueIndex = 0;
        _awakeningOffsetActive = true;
        _currentCameraOffsetY = awakeningCameraOffsetY;
        SetGameplayControl(false, unlockCursor: true);
        SetObjective("Look around the ruins and examine your surroundings.");
        RefreshOverlays();
    }

    private void HandleDialogueInput()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if ((kb != null && (kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) ||
            (mouse != null && mouse.leftButton.wasPressedThisFrame))
        {
            AdvanceDialogue();
        }
    }

    private void AdvanceDialogue()
    {
        if (_phase != Phase.AwakeningDialogue) return;
        _dialogueIndex++;
        if (_dialogueIndex >= AwakeningLines.Length)
        {
            if (CoreRuntime.Instance != null && CoreRuntime.Instance.AutoCompleteCasesEnabled)
            {
                Morsoul.Flow.InvestigationCompletion.ForceCompleteCase(CoreRuntime.Instance, 0);
                _phase = Phase.Investigation;
                ResetAwakeningCameraOffset();
                SetGameplayControl(true, unlockCursor: false);
                RefreshOverlays();
                return;
            }
            _phase = Phase.FreeExploreBeforeScan;
            _exploreTimer = 0f;
            SetGameplayControl(true, unlockCursor: false);
        }
        RefreshOverlays();
    }

    private void UpdateBeforeScan()
    {
        if (PauseMenuController.AnyModalOrPauseOpen || Time.timeScale == 0f) return;
        if (CoreRuntime.Instance != null && CoreRuntime.Instance.AutoCompleteCasesEnabled)
        {
            Morsoul.Flow.InvestigationCompletion.ForceCompleteCase(CoreRuntime.Instance, 0);
            _phase = Phase.Investigation;
            ResetAwakeningCameraOffset();
            SetGameplayControl(true, unlockCursor: false);
            RefreshOverlays();
            return;
        }

        _exploreTimer += Time.deltaTime;
        bool nearDesk = _deskLogsNode != null && _playerTransform != null &&
                        Vector3.Distance(_playerTransform.position, _deskLogsNode.transform.position) <= accidentalScanRadius;
        bool timerFallback = _deskLogsNode == null && _exploreTimer >= scanTriggerExploreDelay;
        bool pressedTab = Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;

        if (nearDesk || timerFallback || pressedTab)
        {
            _phase = Phase.ScanWindup;
            _windupTimer = 0f;
            if (_deskLogsNode != null) _deskLogsNode.state = ClueNode.State.Revealed;
            if (_scanController != null) _scanController.TriggerAccidentalScan(2.4f);
        }
    }

    private void UpdateScanWindup()
    {
        _windupTimer += Time.unscaledDeltaTime;
        if (_windupTimer >= 0.75f)
        {
            _phase = Phase.ScanTutorialModal;
            _scanPageIndex = 0;
            Time.timeScale = 0f;
            SetGameplayControl(false, unlockCursor: true);
            RefreshOverlays();
        }
    }

    private void HandleScanModalInput()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            AdvanceScanModal();
    }

    private void AdvanceScanModal()
    {
        if (_phase != Phase.ScanTutorialModal) return;
        _scanPageIndex++;
        if (_scanPageIndex >= ScanPages.Length)
        {
            _phase = Phase.Investigation;
            Time.timeScale = 1f;
            SetGameplayControl(true, unlockCursor: false);
            SetObjective("Hold [Tab] to scan and [L-Click] to inspect clues in the ruins.");
        }
        RefreshOverlays();
    }

    private void UpdateInvestigation()
    {
        var core = CoreRuntime.Instance;
        var session = core != null ? core.Session : null;

        bool hasC1 = session != null && session.HasClue(DeskLogsClueId);
        bool hasC2 = session != null && session.HasClue(CapacitorClueId);
        bool hasC3 = session != null && session.HasClue(KeyClueId);

        bool boardSolved = false;
        if (session != null)
        {
            var board = session.GetOrCreateBoardState(0);
            boardSolved = board != null && board.BoardSolved;
        }

        if (boardSolved)
        {
            if (!_focusRefilledOnSolve)
            {
                _focusRefilledOnSolve = true;
                _scanController?.RefillFocusToMax();
            }
            SetObjective("Manual override calibrated. Unlock the Sealed Stone Gate [L-Click] to exit.");
        }
        else if (hasC1 && hasC2 && hasC3)
        {
            SetObjective("All clues gathered. Press [H] to open your Deduction Board.");
        }
        else if (hasC1 || hasC2 || hasC3)
        {
            SetObjective("Search the ruins for more clues. (Press [G] anytime to check your Journal.)");
        }

        // Anti-hardstuck fallback: if Focus Energy hits 0, power down and reboot at last checkpoint.
        if (_scanController != null && _scanController.currentFocus <= 0.01f && Time.timeScale > 0f)
        {
            _phase = Phase.PowerDownBlackout;
            _blackoutTimer = 0f;
            Shader.SetGlobalFloat("_ScanProgress", 0f);
            SetGameplayControl(false, unlockCursor: false);
            RefreshOverlays();
        }
    }

    private void UpdateBlackout()
    {
        _blackoutTimer += Time.unscaledDeltaTime;
        if (_blackoutOverlay == null) return;

        if (_blackoutTimer < 0.8f)
        {
            float t = Mathf.Clamp01(_blackoutTimer / 0.8f);
            _blackoutOverlay.style.backgroundColor = new Color(0f, 0f, 0f, t);
            _blackoutLabel.text = "FOCUS ENERGY DEPLETED\nEMERGENCY POWER-DOWN INITIATED...";
        }
        else if (_blackoutTimer < 1.9f)
        {
            _blackoutOverlay.style.backgroundColor = new Color(0f, 0f, 0f, 1f);
            _blackoutLabel.text = "RESERVE CAPACITORS RECOVERED.\nREBOOTING AT LAST SAFE CHECKPOINT...";
        }
        else if (_blackoutTimer < 2.0f)
        {
            RespawnAtCheckpoint();
            _scanController?.RefillFocusToMax();
        }
        else if (_blackoutTimer < 2.6f)
        {
            float t = 1f - Mathf.Clamp01((_blackoutTimer - 2.0f) / 0.6f);
            _blackoutOverlay.style.backgroundColor = new Color(0f, 0f, 0f, t);
            _blackoutLabel.text = "";
        }
        else
        {
            _phase = Phase.Investigation;
            SetGameplayControl(true, unlockCursor: false);
            RefreshOverlays();
        }
    }

    private void RespawnAtCheckpoint()
    {
        if (_playerTransform == null) return;
        if (_characterController != null) _characterController.enabled = false;
        _playerTransform.position = _checkpointPos;
        _playerTransform.rotation = _checkpointRot;
        if (_characterController != null) _characterController.enabled = true;
    }

    private void UpdateCheckpointOnClueGain()
    {
        var session = CoreRuntime.Instance?.Session;
        if (session == null || _playerTransform == null) return;
        if (session.ClueIds.Count > _lastClueCount && Time.timeScale > 0f)
        {
            _lastClueCount = session.ClueIds.Count;
            _checkpointPos = _playerTransform.position;
            _checkpointRot = _playerTransform.rotation;
        }
    }

    private void EnsureBoardGuardrails()
    {
        if (_boardController != null) return;
        _boardController = FindAnyObjectByType<WeightedBoardController>(FindObjectsInactive.Include);
        if (_boardController == null) return;

        // Spec §6: Eliza is absent from the ruins; all Case 0 prompts are Internal Diagnostic.
        _boardController.hintPortrait = null;
        _boardController.hintSpeaker = "Internal Diagnostic";
        _boardController.diagnosticSpeaker = "Internal Diagnostic";
        _boardController.BoardLoaded += OnBoardLoaded;
        if (_boardController.Root != null)
            BuildBoardStrip(_boardController.Root);
    }

    private void RefreshOverlays()
    {
        if (_dialogueBox != null)
        {
            bool showDlg = _phase == Phase.AwakeningDialogue;
            _dialogueBox.style.display = showDlg ? DisplayStyle.Flex : DisplayStyle.None;
            if (showDlg && _dialogueIndex < AwakeningLines.Length)
                _dialogueTextLabel.text = AwakeningLines[_dialogueIndex];
        }

        if (_scanModal != null)
        {
            bool showModal = _phase == Phase.ScanTutorialModal && _scanPageIndex < ScanPages.Length;
            _scanModal.style.display = showModal ? DisplayStyle.Flex : DisplayStyle.None;
            if (showModal)
            {
                var page = ScanPages[_scanPageIndex];
                var cyan = new Color(0.42f, 0.92f, 1f, 1f);
                var amber = new Color(0.95f, 0.65f, 0.22f, 1f);
                _scanModalTitle.text = page.title;
                _scanModalTitle.style.color = page.isWarning ? amber : cyan;
                _scanModalBody.text = page.body;
                _scanModalBody.style.color = page.isWarning ? amber : new Color(0.92f, 0.94f, 0.96f, 1f);
                _scanModalBtn.text = _scanPageIndex < ScanPages.Length - 1
                    ? $"Next ({_scanPageIndex + 1}/{ScanPages.Length}) [Space]"
                    : $"Continue ({_scanPageIndex + 1}/{ScanPages.Length}) [Space]";
            }
        }

        if (_blackoutOverlay != null)
            _blackoutOverlay.style.display = _phase == Phase.PowerDownBlackout ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void RefreshBoardStrip()
    {
        if (_boardStrip == null || _boardController == null) return;
        var session = _boardController.Session;
        bool show = _boardController.IsOpen && session != null && session.State.CaseId == 0;
        _boardStrip.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        if (!show) return;

        string completeId = _boardController.caseData != null && _boardController.caseData.evidenceGraph != null
            ? _boardController.caseData.evidenceGraph.completeConclusionId : "CASE0_MANUAL_OVERRIDE";

        if (session.State.BoardSolved)
            _boardStripLabel.text = "Manual Override calibrated. Press [H] or [ESC] to close.";
        else if (session.IsConclusionUnlocked(completeId))
            _boardStripLabel.text = "Select a conclusion on the right, then press [Space] to submit.";
        else if (session.State.Links.Count == 0)
            _boardStripLabel.text = "Click one clue card, then click another to draw a link.";
        else
            _boardStripLabel.text = "Keep linking related clues. (Right-click a link row on the right to sever it.)";
    }

    private void SetGameplayControl(bool enabled, bool unlockCursor)
    {
        if (_playerController != null) _playerController.enabled = enabled;
        if (_scanController != null) _scanController.enabled = enabled;
        Cursor.lockState = unlockCursor ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = unlockCursor;
    }

    private void SetObjective(string text)
    {
        if (text == _lastObjective || HUDManager.Instance == null) return;
        _lastObjective = text;
        HUDManager.Instance.SetObjective(text, "Prologue");
    }

    private void EnsureCradleAnchor()
    {
        var existing = GameObject.Find("case00_cradle");
        if (existing != null)
        {
            _cradleTransform = existing.transform;
            return;
        }

        var cradle = new GameObject("case00_cradle");
        if (_playerTransform != null)
            cradle.transform.SetPositionAndRotation(_playerTransform.position, _playerTransform.rotation);
        _cradleTransform = cradle.transform;
    }

    private void UpdateAwakeningCameraOffset()
    {
        if (!_awakeningOffsetActive) return;

        if (!_cameraOffsetInitialized)
        {
            var cam = _playerController != null
                ? _playerController.GetComponentInChildren<Camera>()
                : Camera.main;
            if (cam == null) return;
            _cameraTransform = cam.transform;
            _cameraBaseLocalY = _cameraTransform.localPosition.y;
            _cameraOffsetInitialized = true;
        }

        if (_cameraTransform == null) return;

        float targetOffset = _phase == Phase.AwakeningDialogue
            ? Mathf.Lerp(awakeningCameraOffsetY, 0f, (float)_dialogueIndex / Mathf.Max(1, AwakeningLines.Length))
            : 0f;

        float speed = _phase == Phase.AwakeningDialogue ? 1.8f : 2.5f;
        _currentCameraOffsetY = Mathf.MoveTowards(_currentCameraOffsetY, targetOffset, Time.unscaledDeltaTime * speed);

        var pos = _cameraTransform.localPosition;
        pos.y = _cameraBaseLocalY + _currentCameraOffsetY;
        _cameraTransform.localPosition = pos;

        if (_phase != Phase.AwakeningDialogue && Mathf.Approximately(_currentCameraOffsetY, 0f))
            ResetAwakeningCameraOffset();
    }

    private void ResetAwakeningCameraOffset()
    {
        _awakeningOffsetActive = false;
        _currentCameraOffsetY = 0f;
        if (_cameraOffsetInitialized && _cameraTransform != null)
        {
            var pos = _cameraTransform.localPosition;
            pos.y = _cameraBaseLocalY;
            _cameraTransform.localPosition = pos;
        }
    }

    private static void EnsureTravelerRobeProp()
    {
        if (GameObject.Find("TravelerRobeProp") != null) return;

        var robe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        robe.name = "TravelerRobeProp";
        robe.transform.position = new Vector3(-1f, 0.05f, 9f);
        robe.transform.rotation = Quaternion.Euler(0f, 22f, 0f);
        robe.transform.localScale = new Vector3(0.75f, 0.12f, 0.55f);

        var col = robe.GetComponent<Collider>();
        if (col != null) Destroy(col);

        var renderer = robe.GetComponent<Renderer>();
        if (renderer != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = new Color(0.22f, 0.18f, 0.16f, 1f);
                renderer.sharedMaterial = mat;
            }
        }
    }

    private static void EnsureExitGateSeam()
    {
        var lockables = FindObjectsByType<LockableInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var lockable in lockables)
        {
            if (lockable == null) continue;
            bool isPrologueExit = lockable.name == "PrologueExitPortal" ||
                                  (lockable.boardCaseId == 0 && lockable.requireBoardSolved);
            if (!isPrologueExit) continue;

            var seam = lockable.GetComponent<PrologueGateSeam>() ?? lockable.gameObject.AddComponent<PrologueGateSeam>();
            seam.boardCaseId = lockable.boardCaseId;
            seam.triggerOnPlayerEnter = false;

            // Replace persistent SceneLoader listeners so exit always routes through PrologueGateSeam.
            lockable.onInteractSuccess = new UnityEvent();
            lockable.onInteractSuccess.AddListener(() =>
            {
                seam.MarkGateUnlocked();
                seam.TryCrossGate();
            });
        }
    }

    private static void SetBorder(VisualElement el, float width, Color color, float radius)
    {
        el.style.borderTopWidth = width;
        el.style.borderBottomWidth = width;
        el.style.borderLeftWidth = width;
        el.style.borderRightWidth = width;
        el.style.borderTopColor = color;
        el.style.borderBottomColor = color;
        el.style.borderLeftColor = color;
        el.style.borderRightColor = color;
        el.style.borderTopLeftRadius = radius;
        el.style.borderTopRightRadius = radius;
        el.style.borderBottomLeftRadius = radius;
        el.style.borderBottomRightRadius = radius;
    }
}

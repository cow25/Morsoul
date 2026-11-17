using System.Collections.Generic;
using Morsoul.Board;
using Morsoul.Core;
using Morsoul.Data;
using Morsoul.Engine;
using Morsoul.Flow;
using Morsoul.Hub;
using Morsoul.Interrogation;
using Morsoul.Journal;
using Morsoul.Resolution;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Morsoul.Dev
{
    /// <summary>
    /// F1-toggled IMGUI dev menu. Auto-spawns on play in the editor and
    /// development builds only; stripped from release builds by the
    /// conditional below. Tabs: Scenes / Session / Flags / Clues / Focus /
    /// Hub / Scenarios / Sequences.
    /// </summary>
    public class DevMenuOverlay : MonoBehaviour
    {
        static DevMenuOverlay _instance;
        bool _visible;
        public static bool IsOpen => _instance != null && _instance._visible;
        public static bool DevHintsEnabled { get; set; }
        Vector2 _scroll;
        int _tab;
        Rect _window = new Rect(20, 20, 660, 560);

        // input fields
        string _sceneInput = "";
        string _flagInput = "";
        string _clueInput = "";
        string _playerName = "Dev";
        float _focusSpend = 25f;
        float _focusMaxBonus = 20f;
        Vector3 _teleport = Vector3.zero;

        // sequences
        DevSmokeSequences.DevReport _report;
        System.Collections.IEnumerator _running;

        // UI preview state
        int _previewCaseId = 1;
        bool _previewAutoHide = true;
        bool _previewEmotionEcho = true;
        bool _previewUnease;
        string _previewStatus = "";
        readonly List<ScriptableObject> _previewTempAssets = new();
        readonly List<System.Func<bool>> _previewModalPredicates = new();

        // panels we suspended while the menu is open (restored on close)
        readonly List<System.ValueTuple<UnityEngine.UIElements.VisualElement,
            UnityEngine.UIElements.PickingMode>> _suspended = new();

        static readonly string[] Tabs =
            { "Scenes", "Session", "Flags", "Clues", "Focus", "Hub", "UI Preview", "Scenarios", "Sequences" };

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap() => Ensure();
#endif

        public static DevMenuOverlay Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject(nameof(DevMenuOverlay));
            _instance = go.AddComponent<DevMenuOverlay>();
            DontDestroyOnLoad(go);
            return _instance;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.f1Key.wasPressedThisFrame)
                    SetMenuVisible(!_visible);
                if (keyboard.f2Key.wasPressedThisFrame)
                    DevHintsEnabled = !DevHintsEnabled;
                if (keyboard.f3Key.wasPressedThisFrame)
                {
                    var ensured = CoreRuntime.Ensure();
                    ensured.AutoCompleteCasesEnabled = !ensured.AutoCompleteCasesEnabled;
                }
            }

            var core = CoreRuntime.Instance;
            if (core != null && core.AutoCompleteCasesEnabled &&
                core.Session.CurrentSceneMode == SceneMode.Mode3D)
            {
                int activeCase = core.Session.InvestigationCaseId >= 0
                    ? core.Session.InvestigationCaseId
                    : core.Session.CurrentCase;
                if (!core.Session.GetOrCreateBoardState(activeCase).BoardSolved)
                    InvestigationCompletion.ForceCompleteCase(core, activeCase);
            }
        }

        void SetMenuVisible(bool visible)
        {
            if (_visible == visible) return;
            _visible = visible;
            if (_visible)
            {
                _prevLock = UnityEngine.Cursor.lockState;
                _prevCursorVisible = UnityEngine.Cursor.visible;
                SuspendPanelInput();
            }
            else
            {
                RestorePanelInput();
                if (!PauseMenuController.AnyModalOrPauseOpen)
                {
                    UnityEngine.Cursor.lockState = _prevLock;
                    UnityEngine.Cursor.visible = _prevCursorVisible;
                }
            }
        }

        void OnDestroy()
        {
            foreach (var pred in _previewModalPredicates)
                PauseMenuController.UnregisterExternalModal(pred);
            _previewModalPredicates.Clear();
            foreach (var asset in _previewTempAssets)
                if (asset != null) Destroy(asset);
            _previewTempAssets.Clear();
            if (_instance == this) _instance = null;
        }

        CursorLockMode _prevLock;
        bool _prevCursorVisible;

        // Hub panels re-lock the cursor when they close; a locked cursor
        // sends every click to screen centre, so keep it free while open.
        void LateUpdate()
        {
            if (!_visible) return;
            if (UnityEngine.Cursor.lockState != CursorLockMode.None)
                UnityEngine.Cursor.lockState = CursorLockMode.None;
            if (!UnityEngine.Cursor.visible)
                UnityEngine.Cursor.visible = true;
        }

        /// <summary>
        /// UI Toolkit fullscreen roots swallow pointer events before IMGUI
        /// sees them — while the menu is open, every live panel root is made
        /// click-through; originals restored on close.
        /// </summary>
        void SuspendPanelInput()
        {
            _suspended.Clear();
            SuspendAll<UnityEngine.UIElements.PanelRenderer>();
            SuspendAll<UnityEngine.UIElements.UIDocument>();
        }

        void SuspendAll<T>() where T : Component
        {
            foreach (var panel in Object.FindObjectsByType<T>())
            {
                var root = RootOf(panel);
                if (root == null || root.pickingMode ==
                        UnityEngine.UIElements.PickingMode.Ignore)
                    continue;
                _suspended.Add((root, root.pickingMode));
                root.pickingMode = UnityEngine.UIElements.PickingMode.Ignore;
            }
        }

        /// <summary>The root element is exposed as rootVisualElement on
        /// UIDocument and uiRootVisualElement on newer PanelRenderer builds —
        /// reflect so either works.</summary>
        static UnityEngine.UIElements.VisualElement RootOf(Component panel)
        {
            foreach (var name in new[] { "rootVisualElement", "uiRootVisualElement" })
            {
                var prop = panel.GetType().GetProperty(name);
                if (prop?.GetValue(panel) is UnityEngine.UIElements.VisualElement v)
                    return v;
            }
            return null;
        }

        void RestorePanelInput()
        {
            foreach (var (root, mode) in _suspended)
                if (root != null) root.pickingMode = mode;
            _suspended.Clear();
        }

        void OnGUI()
        {
            if (DevHintsEnabled) DrawDevHintsOverlay();
            if (CoreRuntime.Instance != null && CoreRuntime.Instance.AutoCompleteCasesEnabled)
                DrawAutoCompleteBanner();
            if (!_visible) return;
            _window = GUILayout.Window(987654, _window, DrawWindow,
                "Morsoul Dev Menu  [F1]");
        }

        static void DrawAutoCompleteBanner()
        {
            var prev = GUI.color;
            GUI.color = Color.green;
            GUI.Box(new Rect(Screen.width * 0.5f - 135f, 8f, 270f, 24f),
                "CASE COMPLETE MODE: ON  [F3]");
            GUI.color = prev;
        }

        void DrawWindow(int id)
        {
            var core = CoreRuntime.Instance;
            GUILayout.BeginHorizontal();
            GUILayout.Label(core == null
                ? "CoreRuntime: ABSENT (limited tools)"
                : $"Scene: {SceneManager.GetActiveScene().name}   " +
                  $"Case: {core.Session.CurrentCase}   " +
                  $"Focus: {core.Focus.Percent:0}/{core.Focus.Maximum:0}");
            if (core != null)
                core.AutoCompleteCasesEnabled = GUILayout.Toggle(
                    core.AutoCompleteCasesEnabled, "Case Complete [F3]", GUILayout.Width(145));
            DevHintsEnabled = GUILayout.Toggle(DevHintsEnabled, "Dev Hints [F2]", GUILayout.Width(120));
            GUILayout.EndHorizontal();

            _tab = GUILayout.Toolbar(_tab, Tabs);
            _scroll = GUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case 0: DrawScenes(); break;
                case 1: DrawSession(core); break;
                case 2: DrawFlags(core); break;
                case 3: DrawClues(core); break;
                case 4: DrawFocus(core); break;
                case 5: DrawHub(); break;
                case 6: DrawUiPreview(core); break;
                case 7: DrawScenarios(); break;
                case 8: DrawSequences(); break;
            }
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, _window.width, 20));
        }

        // --- Scenes ------------------------------------------------------------

        void DrawScenes()
        {
            var gsm = GameStateManager.Instance;
            Section("Route table");
            if (gsm == null || gsm.routes.routes.Count == 0)
                GUILayout.Label("(no routes registered)");
            else
                foreach (var route in gsm.routes.routes)
                {
                    bool loadable = Application.CanStreamedLevelBeLoaded(route.sceneName);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{route.key} → {route.sceneName}" +
                                    (loadable ? "" : "  (not in build)"),
                        GUILayout.Width(430));
                    GUI.enabled = loadable;
                    if (GUILayout.Button("Go", GUILayout.Width(60)))
                        DevTravel.Go(route.sceneName);
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }

            Section("Direct scene name");
            GUILayout.BeginHorizontal();
            _sceneInput = GUILayout.TextField(_sceneInput, GUILayout.Width(430));
            if (GUILayout.Button("Load", GUILayout.Width(60)))
                DevTravel.Go(_sceneInput);
            GUILayout.EndHorizontal();
        }

        // --- Session -----------------------------------------------------------

        void DrawSession(CoreRuntime core)
        {
            if (core == null) { NeedCore(); return; }
            var s = core.Session;
            var gsm = GameStateManager.Instance;

            Section("Fast Narrative / Case Complete Mode");
            core.AutoCompleteCasesEnabled = GUILayout.Toggle(
                core.AutoCompleteCasesEnabled,
                "Auto-complete clues & board on entering case areas + instant interrogation win [F3]");
            int targetCase = s.InvestigationCaseId >= 0 ? s.InvestigationCaseId : s.CurrentCase;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Complete Case {targetCase} Clues & Board Now", GUILayout.Width(265)))
            {
                bool ok = InvestigationCompletion.ForceCompleteCase(core, targetCase);
                Debug.Log(ok
                    ? $"[DevMenu] Completed clues & solved board for Case {targetCase}."
                    : $"[DevMenu] Could not complete Case {targetCase}.");
            }
            GUI.enabled = s.CurrentCase >= 1 && s.CurrentCase <= 4;
            if (GUILayout.Button($"Finish Interrogation Now (Case {Mathf.Clamp(s.CurrentCase, 1, 4)})", GUILayout.Width(270)))
            {
                SetMenuVisible(false);
                InvestigationCompletion.CompleteInterrogationAndTriggerCutscene(core, s.CurrentCase);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            Section("Investigation development fallback");
            core.DevelopmentInvestigationFallbackEnabled = GUILayout.Toggle(
                core.DevelopmentInvestigationFallbackEnabled,
                "Simulate explicitly unintegrated investigation areas (development only)");
            GUILayout.Label(core.InvestigationStatusMessage);
            for (int caseId = 0; caseId <= 4; caseId++)
                if (s.WasInvestigationSimulated(caseId))
                    GUILayout.Label($"Case {caseId}: simulation used; deduction source {s.GetInvestigationSource(caseId)}");

            Section("Current case");
            GUILayout.BeginHorizontal();
            for (int i = 0; i <= 5; i++)
            {
                GUI.enabled = s.CurrentCase != i;
                if (GUILayout.Button(i.ToString(), GUILayout.Width(40)))
                    s.CurrentCase = i;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            Section("Campaign");
            GUILayout.BeginHorizontal();
            _playerName = GUILayout.TextField(_playerName, GUILayout.Width(200));
            GUI.enabled = gsm != null;
            if (GUILayout.Button("New Game", GUILayout.Width(100)))
                gsm.NewGame(_playerName);
            if (GUILayout.Button("Continue", GUILayout.Width(100)))
                gsm.ContinueGame();
            if (GUILayout.Button("Checkpoint", GUILayout.Width(100)))
                gsm.WriteCheckpoint("DevMenu", SceneManager.GetActiveScene().name);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            Section("Danger zone");
            if (GUILayout.Button("Reset SessionState (flags/clues/case/logs)",
                    GUILayout.Width(320)))
                s.Reset();

            Section("Prologue / Tutorial state");
            s.PrologueGateUnlocked = GUILayout.Toggle(s.PrologueGateUnlocked, "PrologueGateUnlocked");
            s.PrologueRobeCollected = GUILayout.Toggle(s.PrologueRobeCollected, "PrologueRobeCollected");
            s.PrologueExitLoaded = GUILayout.Toggle(s.PrologueExitLoaded, "PrologueExitLoaded");
            s.PrologueCompleted = GUILayout.Toggle(s.PrologueCompleted, "PrologueCompleted");
            s.ElizaIntroSeen = GUILayout.Toggle(s.ElizaIntroSeen, "ElizaIntroSeen");
            s.EasternRouteUnlocked = GUILayout.Toggle(s.EasternRouteUnlocked, "EasternRouteUnlocked");

            Section("State dump");
            GUILayout.Label($"Flags: {s.Flags.Count}  Clues: {s.ClueIds.Count}  " +
                            $"Runic logs: {s.RunicLogCount}  Board edges: {s.BoardEdges.Count}");
            foreach (var kv in s.Grades)
                GUILayout.Label($"  Grade case {kv.Key}: {kv.Value}");
        }

        // --- Flags -------------------------------------------------------------

        void DrawFlags(CoreRuntime core)
        {
            if (core == null) { NeedCore(); return; }
            var s = core.Session;

            Section("Add / toggle");
            GUILayout.BeginHorizontal();
            _flagInput = GUILayout.TextField(_flagInput, GUILayout.Width(340));
            if (GUILayout.Button("Set", GUILayout.Width(60))) s.SetFlag(_flagInput);
            if (GUILayout.Button("Clear", GUILayout.Width(60))) s.SetFlag(_flagInput, false);
            GUILayout.EndHorizontal();

            Section("Known progression flags");
            DrawFlagToggle(s, DevScenarios.WarrantFlag);
            DrawFlagToggle(s, DevScenarios.BelfryKeyFlag);
            DrawFlagToggle(s, DevScenarios.PerimeterSealFlag);
            DrawFlagToggle(s, DevScenarios.OverrideClearanceFlag);
            foreach (var f in DevScenarios.InterludeFlags) DrawFlagToggle(s, f);

            Section($"All session flags ({s.Flags.Count})");
            foreach (var kv in s.Flags) DrawFlagToggle(s, kv.Key);
        }

        static void DrawFlagToggle(SessionState s, string flag)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(flag, GUILayout.Width(400));
            bool has = s.HasFlag(flag);
            var prev = GUI.color;
            GUI.color = has ? Color.green : Color.gray;
            if (GUILayout.Button(has ? "SET" : "unset", GUILayout.Width(70)))
                s.SetFlag(flag, !has);
            GUI.color = prev;
            GUILayout.EndHorizontal();
        }

        // --- Clues -------------------------------------------------------------

        void DrawClues(CoreRuntime core)
        {
            if (core == null) { NeedCore(); return; }
            var s = core.Session;

            Section("Register all clues by case (0 = Prologue)");
            GUILayout.BeginHorizontal();
            for (int c = 0; c <= 5; c++)
            {
                if (GUILayout.Button($"All Case {c}", GUILayout.Width(82)))
                    RegisterCaseClues(core, c);
            }
            GUILayout.EndHorizontal();

            var currentData = core.ResolveCase(s.CurrentCase);
            if (currentData != null && currentData.clues != null && currentData.clues.Length > 0)
            {
                Section($"Case {s.CurrentCase} clues ({currentData.clues.Length})");
                foreach (var clue in currentData.clues)
                {
                    if (clue == null || string.IsNullOrEmpty(clue.id)) continue;
                    bool has = s.HasClue(clue.id);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{clue.id}  ({clue.displayName})", GUILayout.Width(340));
                    GUI.enabled = !has;
                    if (GUILayout.Button(has ? "✓ Owned" : "Register", GUILayout.Width(90)))
                    {
                        if (clue.type == ClueType.Latent) s.RegisterLatentClue(clue);
                        else s.RegisterClue(clue);
                    }
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
            }

            Section("Register clue by ID");
            GUILayout.BeginHorizontal();
            _clueInput = GUILayout.TextField(_clueInput, GUILayout.Width(340));
            if (GUILayout.Button("Register", GUILayout.Width(80)))
            {
                var clue = core.ResolveClue(_clueInput);
                if (clue == null) Debug.LogWarning($"DevMenu: no clue '{_clueInput}'.");
                else if (clue.type == ClueType.Latent) s.RegisterLatentClue(clue);
                else s.RegisterClue(clue);
            }
            GUILayout.EndHorizontal();

            Section("Testimony / red-herring shortcuts");
            for (int c = 1; c <= 5; c++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Case {c}", GUILayout.Width(60));
                foreach (var id in new[] { $"T1_0{c}", $"T2_0{c}", $"RH1_0{c}" })
                {
                    bool has = s.HasClue(id);
                    GUI.enabled = !has;
                    if (GUILayout.Button((has ? "✓ " : "") + id, GUILayout.Width(90)))
                    {
                        var clue = core.ResolveClue(id);
                        if (clue != null) s.RegisterClue(clue);
                    }
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
            }

            Section($"Registered clues ({s.ClueIds.Count})");
            foreach (var id in s.ClueIds) GUILayout.Label("  " + id);
        }

        // --- Focus -------------------------------------------------------------

        void DrawFocus(CoreRuntime core)
        {
            if (core == null) { NeedCore(); return; }
            var f = core.Focus;
            GUILayout.Label($"Focus: {f.Percent:0.0} / {f.Maximum:0.0} " +
                            $"({f.Normalized:P0})  StrainedScanUsed={f.StrainedScanUsed}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refill", GUILayout.Width(100))) f.Refill();
            _focusSpend = GUILayout.HorizontalSlider(_focusSpend, 5f, 100f, GUILayout.Width(200));
            if (GUILayout.Button($"Spend {_focusSpend:0}", GUILayout.Width(100)))
                f.Spend(_focusSpend);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            _focusMaxBonus = GUILayout.HorizontalSlider(_focusMaxBonus, 5f, 60f, GUILayout.Width(200));
            if (GUILayout.Button($"Max +{_focusMaxBonus:0}", GUILayout.Width(100)))
                f.IncreaseMaximum(_focusMaxBonus);
            if (GUILayout.Button("Mark closed now", GUILayout.Width(140)))
                f.MarkClosed(System.DateTime.UtcNow);
            GUILayout.EndHorizontal();
            core.ZeroScanFocusCost = GUILayout.Toggle(core.ZeroScanFocusCost, "Zero Scan Energy Cost (Dev)", GUILayout.Width(220));
        }

        // --- Hub ----------------------------------------------------------------

        void DrawHub()
        {
            var ctx = Object.FindAnyObjectByType<HubContext>();
            if (ctx == null)
            {
                GUILayout.Label("No HubContext in this scene (not a hub?).");
                return;
            }

            Section("Panels");
            GUILayout.Label($"dialogue: {(ctx.dialogue ? "wired" : "MISSING")}   " +
                            $"journal: {(ctx.journal ? "wired" : "MISSING")}   " +
                            $"flags: {(ctx.flags ? "wired" : "MISSING")}   " +
                            $"travelMenu: {(ctx.travelMenu ? "wired" : "MISSING")}   " +
                            $"UiBlocked: {ctx.UiBlocked}");

            if (ctx.travelMenu != null &&
                GUILayout.Button("Open carrier travel menu now", GUILayout.Width(260)))
            {
                var dests = ctx.travelMenu
                    .CollectDestinations(SceneManager.GetActiveScene().name);
                ctx.travelMenu.Show(dests);
            }

            Section("Interactables");
            var player = Object.FindAnyObjectByType<HubPlayerController>();
            var interactables =
                Object.FindObjectsByType<Interactable2D>();
            foreach (var it in interactables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{it.name}  ({it.transform.position.x:0.0}," +
                                $"{it.transform.position.y:0.0})",
                    GUILayout.Width(360));
                GUI.enabled = player != null;
                if (GUILayout.Button("Teleport player", GUILayout.Width(130)))
                    player.transform.position =
                        it.transform.position + Vector3.left;
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            Section("Manual teleport");
            GUILayout.BeginHorizontal();
            _teleport.x = Field("x", _teleport.x);
            _teleport.y = Field("y", _teleport.y);
            GUI.enabled = player != null;
            if (GUILayout.Button("Go", GUILayout.Width(50)))
                player.transform.position = _teleport;
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        // --- UI Preview ---------------------------------------------------------

        void DrawUiPreview(CoreRuntime core)
        {
            if (core == null) core = CoreRuntime.Ensure();
            Section("Preview case data");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Case ID:", GUILayout.Width(64));
            for (int i = 0; i <= 5; i++)
            {
                GUI.enabled = _previewCaseId != i;
                if (GUILayout.Button(i.ToString(), GUILayout.Width(38)))
                    _previewCaseId = i;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            var selectedCase = core != null ? core.ResolveCase(_previewCaseId) : null;
            GUILayout.Label(selectedCase != null
                ? $"Selected: Case {selectedCase.caseId} — {selectedCase.title}"
                : $"Case {_previewCaseId} not found in CaseDatabase.");

            _previewAutoHide = GUILayout.Toggle(_previewAutoHide,
                "Auto-hide Dev Menu when opening a panel (press F1 to reopen)");
            _previewEmotionEcho = GUILayout.Toggle(_previewEmotionEcho,
                "Unlock Emotion Echo for Interrogation preview");
            _previewUnease = GUILayout.Toggle(_previewUnease,
                "Show Lingering Unease banner on Resolution preview");

            Section("Open UI panels");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Weighted Board", GUILayout.Width(190)))
                PreviewWeightedBoard(core, selectedCase);
            if (GUILayout.Button("Interrogation", GUILayout.Width(190)))
                PreviewInterrogation(core, selectedCase);
            if (GUILayout.Button("Journal (Desk)", GUILayout.Width(190)))
                PreviewJournal(core, selectedCase);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Legacy Board (Slots)", GUILayout.Width(190)))
                PreviewLegacyBoard(core, selectedCase);
            if (GUILayout.Button("Resolution (Clean)", GUILayout.Width(190)))
                PreviewResolution(selectedCase, Grade.Clean);
            if (GUILayout.Button("Resolution (Flawed)", GUILayout.Width(190)))
                PreviewResolution(selectedCase, Grade.Flawed);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Pause Menu", GUILayout.Width(190)))
            {
                CloseAllPreviewOverlays();
                PauseMenuController.TogglePause();
                FinishPreviewLaunch("Opened Pause Menu.");
            }
            if (GUILayout.Button("Close All UI Panels", GUILayout.Width(190)))
            {
                CloseAllPreviewOverlays();
                _previewStatus = "Closed all UI panels.";
            }
            GUILayout.EndHorizontal();

            Section("CaseLoop shortcuts");
            if (GUILayout.Button("Enter CaseLoop ready for Interrogation (Case " +
                                 Mathf.Clamp(_previewCaseId, 1, 4) + ")", GUILayout.Width(390)))
            {
                LaunchCaseLoopInterrogation(core, Mathf.Clamp(_previewCaseId, 1, 4));
            }

            if (!string.IsNullOrEmpty(_previewStatus))
            {
                Section("Status");
                GUILayout.Label(_previewStatus);
            }
        }

        T GetOrCreatePreviewController<T>(string name, VisualTreeAsset uxml, int sortingOrder)
            where T : Component
        {
            var existing = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            var config = Resources.Load<PauseMenuAssets>("PauseMenuConfig");
            if (config == null || config.panelSettings == null || uxml == null)
            {
                _previewStatus = $"Missing PauseMenuConfig UXML/PanelSettings for {name}.";
                return null;
            }

            var go = new GameObject($"[DevUIPreview] {name}");
            go.SetActive(false);
            var pr = go.AddComponent<PanelRenderer>();
            pr.panelSettings = config.panelSettings;
            pr.visualTreeAsset = uxml;
            pr.sortingOrder = sortingOrder;
            var ctrl = go.AddComponent<T>();
            go.SetActive(true);
            return ctrl;
        }

        void RegisterPreviewModal(System.Func<bool> predicate)
        {
            if (predicate == null || _previewModalPredicates.Contains(predicate)) return;
            _previewModalPredicates.Add(predicate);
            PauseMenuController.RegisterExternalModal(predicate);
        }

        static bool HasFirstPersonLookController()
        {
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
                if (mb != null && mb.GetType().Name == "PlayerController")
                    return true;
            return false;
        }

        void CloseAllPreviewOverlays()
        {
            if (PauseMenuController.IsPaused && PauseMenuController.Instance != null)
                PauseMenuController.Instance.Close();
            foreach (var b in Object.FindObjectsByType<WeightedBoardController>(FindObjectsInactive.Include))
                if (b != null && b.IsOpen) b.Close();
            foreach (var b in Object.FindObjectsByType<BoardController>(FindObjectsInactive.Include))
                if (b != null && b.IsOpen) b.Close();
            foreach (var i in Object.FindObjectsByType<InterrogationController>(FindObjectsInactive.Include))
                if (i != null && i.IsOpen) i.Close();
            foreach (var j in Object.FindObjectsByType<JournalController>(FindObjectsInactive.Include))
                if (j != null && j.IsOpen) j.Close();
            foreach (var r in Object.FindObjectsByType<ResolutionController>(FindObjectsInactive.Include))
                if (r != null && r.IsVisible) r.Hide();
        }

        void FinishPreviewLaunch(string status)
        {
            _previewStatus = status;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            if (_previewAutoHide) SetMenuVisible(false);
        }

        static CaseData ResolveCaseWithFallback(CoreRuntime core, int preferredId,
            System.Func<CaseData, bool> predicate)
        {
            if (core == null) return null;
            var direct = core.ResolveCase(preferredId);
            if (direct != null && predicate(direct)) return direct;
            for (int id = 0; id <= 5; id++)
            {
                var c = core.ResolveCase(id);
                if (c != null && predicate(c)) return c;
            }
            return null;
        }

        static List<ClueData> GatherCaseClues(CaseData data)
        {
            var clues = new List<ClueData>();
            if (data == null) return clues;
            if (data.preloadedClues != null && data.preloadedClues.Length > 0)
            {
                foreach (var c in data.preloadedClues)
                    if (c != null) clues.Add(c);
            }
            else if (data.clues != null)
            {
                foreach (var c in data.clues)
                    if (c != null) clues.Add(c);
            }
            return clues;
        }

        void PreviewWeightedBoard(CoreRuntime core, CaseData preferred)
        {
            var data = preferred != null && preferred.evidenceGraph != null
                ? preferred
                : ResolveCaseWithFallback(core, _previewCaseId, c => c.evidenceGraph != null);
            if (data == null)
            {
                _previewStatus = "No CaseData with an EvidenceGraph found.";
                return;
            }

            CloseAllPreviewOverlays();
            var config = Resources.Load<PauseMenuAssets>("PauseMenuConfig");
            var wb = GetOrCreatePreviewController<WeightedBoardController>(
                "WeightedBoard", config != null ? config.weightedBoardUxml : null, 50);
            if (wb == null) return;

            wb.manageCursor = HasFirstPersonLookController();
            wb.CanUse = () => true;
            var prevEnsure = wb.EnsureSession;
            wb.EnsureSession = null;
            if (!wb.SetCase(data))
            {
                wb.EnsureSession = prevEnsure;
                _previewStatus = $"WeightedBoard failed SetCase for '{data.title}'.";
                return;
            }
            wb.SetClues(GatherCaseClues(data));
            RegisterPreviewModal(() => wb != null && (wb.IsOpen || wb.LastCloseFrame == Time.frameCount));
            wb.Open();
            wb.EnsureSession = prevEnsure;
            FinishPreviewLaunch($"Opened Weighted Board for Case {data.caseId} ({data.title}).");
        }

        void PreviewInterrogation(CoreRuntime core, CaseData preferred)
        {
            var data = preferred != null && preferred.interrogation != null
                ? preferred
                : ResolveCaseWithFallback(core, _previewCaseId, c => c.interrogation != null);
            if (data == null || data.interrogation == null)
            {
                _previewStatus = "No CaseData with InterrogationData found.";
                return;
            }

            CloseAllPreviewOverlays();
            var config = Resources.Load<PauseMenuAssets>("PauseMenuConfig");
            var ic = GetOrCreatePreviewController<InterrogationController>(
                "Interrogation", config != null ? config.interrogationUxml : null, 70);
            if (ic == null) return;

            ic.manageCursor = HasFirstPersonLookController();
            ic.SetInterrogation(data.interrogation, GatherCaseClues(data),
                data.caseId == 1, _previewEmotionEcho);
            RegisterPreviewModal(() => ic != null && (ic.IsOpen || ic.LastEndFrame == Time.frameCount));
            ic.Open();
            FinishPreviewLaunch($"Opened Interrogation for Case {data.caseId} ({data.title}).");
        }

        void PreviewJournal(CoreRuntime core, CaseData preferred)
        {
            var data = preferred ?? ResolveCaseWithFallback(core, _previewCaseId, c => c.clues != null && c.clues.Length > 0);
            CloseAllPreviewOverlays();
            var config = Resources.Load<PauseMenuAssets>("PauseMenuConfig");
            var jc = GetOrCreatePreviewController<JournalController>(
                "Journal", config != null ? config.journalUxml : null, 40);
            if (jc == null) return;

            jc.manageCursor = HasFirstPersonLookController();
            var prevCanOpen = jc.CanOpen;
            jc.CanOpen = () => true;
            if (jc.journal == null)
            {
                var hubJournal = jc.GetComponent<HubJournal>() ?? Object.FindAnyObjectByType<HubJournal>(FindObjectsInactive.Include)
                    ?? jc.gameObject.AddComponent<HubJournal>();
                jc.SetJournal(hubJournal);
            }
            if (jc.journal != null && data != null)
            {
                foreach (var c in GatherCaseClues(data))
                    if (c != null) jc.journal.Register(c);
            }
            if (!jc.IsOpen) jc.Open();
            jc.CanOpen = prevCanOpen;
            FinishPreviewLaunch($"Opened Journal{(data != null ? $" (seeded Case {data.caseId} clues)" : "")}.");
        }

        void PreviewLegacyBoard(CoreRuntime core, CaseData preferred)
        {
            var data = preferred != null && preferred.inquiryNodes != null && preferred.inquiryNodes.Length == 2
                ? preferred
                : BuildSampleLegacyCase(preferred);

            CloseAllPreviewOverlays();
            var config = Resources.Load<PauseMenuAssets>("PauseMenuConfig");
            var bc = GetOrCreatePreviewController<BoardController>(
                "Board", config != null ? config.boardUxml : null, 50);
            if (bc == null) return;

            bc.manageCursor = HasFirstPersonLookController();
            bc.SetCase(data);
            bc.SetClues(GatherCaseClues(data));
            RegisterPreviewModal(() => bc != null && (bc.IsOpen || bc.LastCloseFrame == Time.frameCount));
            bc.Open();
            FinishPreviewLaunch($"Opened Legacy Board ({data.title}).");
        }

        void PreviewResolution(CaseData preferred, Grade grade)
        {
            CloseAllPreviewOverlays();
            var config = Resources.Load<PauseMenuAssets>("PauseMenuConfig");
            var rc = GetOrCreatePreviewController<ResolutionController>(
                "Resolution", config != null ? config.resolutionUxml : null, 80);
            if (rc == null) return;

            rc.manageCursor = HasFirstPersonLookController();
            string title = preferred != null && !string.IsNullOrEmpty(preferred.title)
                ? preferred.title : "Sample Case Resolution";
            RegisterPreviewModal(() => rc != null && rc.IsVisible);
            rc.Show(title, grade, _previewUnease, "Lower Vault Alcove",
                "Inspect behind the cracked keystone with Emotion Echo.");
            FinishPreviewLaunch($"Opened Resolution screen ({title} — {grade}).");
        }

        void LaunchCaseLoopInterrogation(CoreRuntime core, int caseId)
        {
            if (core == null) core = CoreRuntime.Ensure();
            var data = core.ResolveCase(caseId);
            core.Session.CurrentCase = caseId;
            core.Session.SetGrade(caseId, Grade.Ungraded);
            if (data != null && data.clues != null)
                foreach (var c in data.clues)
                    if (c != null) core.Session.RegisterClue(c);
            var boardState = core.Session.GetOrCreateBoardState(caseId);
            boardState.BoardSolved = true;
            FinishPreviewLaunch($"Prepared Case {caseId} Interrogation; loading CaseLoop...");
            DevTravel.Go("CaseLoop");
        }

        CaseData BuildSampleLegacyCase(CaseData seed)
        {
            T Track<T>() where T : ScriptableObject
            {
                var inst = ScriptableObject.CreateInstance<T>();
                inst.hideFlags = HideFlags.DontSave;
                _previewTempAssets.Add(inst);
                return inst;
            }

            var sample = Track<CaseData>();
            sample.caseId = 99;
            sample.hasInvestigationScene = true;
            sample.title = seed != null ? $"{seed.title} (Slot Preview)" : "Sample Deduction Board";

            var c1 = Track<ClueData>();
            c1.id = "PREVIEW_C1"; c1.caseId = 99; c1.displayName = "Shattered Seal";
            c1.type = ClueType.Physical; c1.description = "Broken wax bearing the guildmaster's signet.";
            var c2 = Track<ClueData>();
            c2.id = "PREVIEW_C2"; c2.caseId = 99; c2.displayName = "Ledger Entry";
            c2.type = ClueType.Testimony; c2.description = "Mentions a midnight vault transfer.";
            var c3 = Track<ClueData>();
            c3.id = "PREVIEW_C3"; c3.caseId = 99; c3.displayName = "Trace Dust";
            c3.type = ClueType.Physical; c3.description = "Fine silver shavings beside the lock.";
            var c4 = Track<ClueData>();
            c4.id = "PREVIEW_C4"; c4.caseId = 99; c4.displayName = "Guard Shift Log";
            c4.type = ClueType.Testimony; c4.description = "Confirms no patrol passed after midnight.";

            var p1 = Track<PremiseData>();
            p1.id = "PREVIEW_P1"; p1.title = "Unscheduled Access"; p1.proposition = "The vault was opened using a duplicate seal.";
            var p2 = Track<PremiseData>();
            p2.id = "PREVIEW_P2"; p2.title = "Unmonitored Window"; p2.proposition = "No guard witnessed the midnight transfer.";

            var n1 = Track<InquiryNodeData>();
            n1.id = "PREVIEW_N1"; n1.slotA = c1; n1.slotB = c2; n1.producesPremise = p1;
            n1.prompt = new CausalPromptData
            {
                question = "Why was the guild seal broken alongside the ledger entry?",
                options = new[] { "A duplicate key forced the seal.", "Wear from normal daily use.", "Humidity warped the wax." },
                correctIndex = 0,
                hintText = "Match the signet seal to the midnight entry."
            };
            var n2 = Track<InquiryNodeData>();
            n2.id = "PREVIEW_N2"; n2.slotA = c3; n2.slotB = c4; n2.producesPremise = p2;
            n2.prompt = new CausalPromptData
            {
                question = "How did the intruder file the lock unnoticed?",
                options = new[] { "During the unguarded midnight window.", "At noon during market rush.", "Before the vault existed." },
                correctIndex = 0,
                hintText = "Check when the patrols paused."
            };

            var hyp = Track<HypothesisData>();
            hyp.id = "PREVIEW_HYP"; hyp.premise1 = p1; hyp.premise2 = p2;
            hyp.verdictProposition = "An insider used a filed key during the unguarded midnight hour.";

            sample.clues = new[] { c1, c2, c3, c4 };
            sample.inquiryNodes = new[] { n1, n2 };
            sample.hypothesis = hyp;
            return sample;
        }

        // --- Scenarios ----------------------------------------------------------

        void DrawScenarios()
        {
            foreach (var s in DevScenarios.All)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(s.name, GUILayout.Width(240)))
                    Debug.Log($"[DevMenu] {DevScenarios.Apply(s)}");
                GUILayout.Label(s.description);
                GUILayout.EndHorizontal();
            }
        }

        // --- Sequences ----------------------------------------------------------

        void DrawSequences()
        {
            if (_running != null)
            {
                GUILayout.Label("Sequence running…");
                if (GUILayout.Button("Abort", GUILayout.Width(100)))
                {
                    StopAllCoroutines();
                    _running = null;
                    _report?.Note("aborted by user");
                    if (_report != null) _report.done = true;
                }
            }
            else
            {
                foreach (var seq in DevSmokeSequences.All)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(seq.name, GUILayout.Width(220)))
                    {
                        _report = new DevSmokeSequences.DevReport();
                        _running = seq.run(_report);
                        StartCoroutine(RunSeq(_running));
                    }
                    GUILayout.Label(seq.description);
                    GUILayout.EndHorizontal();
                }
            }

            if (_report == null) return;
            Section($"Result — {_report.passCount} pass / {_report.failCount} fail" +
                    (_report.done ? "" : "  (running)"));
            var prev = GUI.color;
            foreach (var line in _report.lines)
            {
                GUI.color = line.StartsWith("FAIL") ? Color.red
                    : line.StartsWith("PASS") ? Color.green
                    : Color.white;
                GUILayout.Label(line);
            }
            GUI.color = prev;
        }

        System.Collections.IEnumerator RunSeq(System.Collections.IEnumerator seq)
        {
            while (seq.MoveNext()) yield return seq.Current;
            _report.done = true;
            _running = null;
            Debug.Log($"[DevMenu] sequence finished: " +
                      $"{_report.passCount} pass / {_report.failCount} fail");
        }

        // --- Dev Hints Overlay -------------------------------------------------

        void DrawDevHintsOverlay()
        {
            WeightedBoardController wb = null;
            foreach (var c in Object.FindObjectsByType<WeightedBoardController>())
                if (c != null && c.IsOpen && c.Session != null) { wb = c; break; }

            BoardController bc = null;
            if (wb == null)
                foreach (var c in Object.FindObjectsByType<BoardController>())
                    if (c != null && c.IsOpen && c.caseData != null) { bc = c; break; }

            InterrogationController ic = null;
            foreach (var c in Object.FindObjectsByType<InterrogationController>())
                if (c != null && c.IsOpen && c.Engine != null) { ic = c; break; }

            if (wb == null && bc == null && ic == null) return;

            var rect = new Rect(Mathf.Max(16f, Screen.width - 436f), 16f, 420f, 360f);
            GUILayout.BeginArea(rect, "DEV HINTS  [F2 to hide]", GUI.skin.window);

            if (wb != null) DrawWeightedBoardHints(wb);
            else if (bc != null) DrawLegacyBoardHints(bc);

            if (ic != null)
            {
                if (wb != null || bc != null) GUILayout.Space(8);
                DrawInterrogationHints(ic.Engine);
            }

            GUILayout.EndArea();
        }

        static void DrawWeightedBoardHints(WeightedBoardController wb)
        {
            var graph = wb.Session.Graph;
            var state = wb.Session.State;
            Section("Deduction Board — Required Links (S*)");

            var drawn = new HashSet<DirectedEdge>(state.Links);
            var collected = new HashSet<string>(state.CollectedClueIds, System.StringComparer.Ordinal);
            var edges = new List<DirectedEdge>(graph.SolutionEdges);
            edges.Sort((a, b) =>
            {
                int cmp = string.CompareOrdinal(a.sourceId, b.sourceId);
                return cmp != 0 ? cmp : string.CompareOrdinal(a.targetId, b.targetId);
            });

            var prev = GUI.color;
            foreach (var edge in edges)
            {
                bool isDrawn = drawn.Contains(edge);
                bool hasBoth = collected.Contains(edge.sourceId) && collected.Contains(edge.targetId);
                GUI.color = isDrawn ? Color.green : hasBoth ? Color.yellow : Color.gray;
                string tag = isDrawn ? "[✓]" : hasBoth ? "[ ]" : "[missing clue]";
                string src = wb.GetClueDisplayName(edge.sourceId);
                string dst = wb.GetClueDisplayName(edge.targetId);
                GUILayout.Label($"{tag} {src}  →  {dst}");
            }
            GUI.color = prev;

            var complete = graph.GetConclusion(graph.CompleteConclusionId);
            if (complete != null)
            {
                bool ready = graph.HasAllSolutionLinks(state.Links);
                GUILayout.Space(4);
                GUI.color = ready ? Color.green : Color.white;
                GUILayout.Label($"Target Conclusion {(ready ? "(READY)" : "(complete links first)")}: {complete.text}");
                GUI.color = prev;
            }
        }

        static void DrawLegacyBoardHints(BoardController bc)
        {
            Section("Deduction Board — Inquiry Pairs");
            if (bc.caseData.inquiryNodes == null) return;
            foreach (var node in bc.caseData.inquiryNodes)
            {
                if (node == null) continue;
                string a = node.slotA != null ? node.slotA.displayName : "?";
                string b = node.slotB != null ? node.slotB.displayName : "?";
                string ans = node.prompt != null && node.prompt.options != null &&
                             node.prompt.correctIndex >= 0 &&
                             node.prompt.correctIndex < node.prompt.options.Length
                    ? node.prompt.options[node.prompt.correctIndex]
                    : "?";
                GUILayout.Label($"• {a}  +  {b}");
                GUILayout.Label($"   → {ans}");
            }
        }

        static void DrawInterrogationHints(InterrogationEngine engine)
        {
            if (engine.Status != InterrogationStatus.InProgress || engine.CurrentPhase == null)
            {
                GUILayout.Label($"Interrogation: {engine.Status}");
                return;
            }

            var phase = engine.CurrentPhase;
            Section($"Interrogation — Phase {engine.PhaseIndex + 1}/{engine.Data.phases.Length}");

            var prev = GUI.color;
            if (!engine.IsCoreContradictUnlocked)
            {
                GUI.color = Color.yellow;
                string kws = phase.keywords != null && phase.keywords.Length > 0
                    ? string.Join(", ", phase.keywords)
                    : "any keyword";
                GUILayout.Label($"1) PRESS [1] keyword first: {kws}");
                GUI.color = prev;
            }

            if (engine.ShieldActive)
                GUILayout.Label("(Falsehood Shield active on this phase)");

            bool anyMove = false;
            if (phase.validMoves != null)
            {
                foreach (var move in phase.validMoves)
                {
                    if (move == null || !move.breaksComposureSegment) continue;
                    anyMove = true;
                    string verb = move.verb == Verb.Contradict ? "Contradict [2]" : "Present Evidence [3]";
                    string ev = FormatEvidenceName(move.evidence);
                    string coreNote = move.isCoreContradiction && !engine.IsCoreContradictUnlocked
                        ? " (unlock via Press)" : "";
                    GUI.color = Color.green;
                    GUILayout.Label($"→ {verb}: {ev}{coreNote}");
                }
            }
            GUI.color = prev;
            if (!anyMove) GUILayout.Label("(No composure-breaking moves authored in this phase)");
        }

        static string FormatEvidenceName(EvidenceData ev)
        {
            if (ev == null) return "(none)";
            if (ev is ClueData c && !string.IsNullOrEmpty(c.displayName)) return c.displayName;
            if (ev is PremiseData p && !string.IsNullOrEmpty(p.title)) return p.title;
            return string.IsNullOrEmpty(ev.id) ? ev.name : ev.id;
        }

        static void RegisterCaseClues(CoreRuntime core, int caseId)
        {
            var data = core.ResolveCase(caseId);
            if (data == null || data.clues == null) return;
            foreach (var clue in data.clues)
            {
                if (clue == null || clue.isBacktrackingOnly) continue;
                if (clue.type == ClueType.Latent) core.Session.RegisterLatentClue(clue);
                else core.Session.RegisterClue(clue);
            }
        }

        // --- helpers ------------------------------------------------------------

        static void Section(string title) =>
            GUILayout.Label($"<b>{title}</b>", new GUIStyle(GUI.skin.label)
                { richText = true, margin = new RectOffset(0, 0, 8, 0) });

        static void NeedCore() =>
            GUILayout.Label("Requires CoreRuntime — enter Play mode via Title " +
                            "(or any scene; CoreRuntime.Ensure boots).");

        static float Field(string label, float value)
        {
            GUILayout.Label(label, GUILayout.Width(14));
            return float.TryParse(
                GUILayout.TextField(value.ToString("0.0"), GUILayout.Width(60)),
                out var v) ? v : value;
        }
    }
}

using System.Collections.Generic;
using Morsoul.Core;
using Morsoul.Hub;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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

        // panels we suspended while the menu is open (restored on close)
        readonly List<System.ValueTuple<UnityEngine.UIElements.VisualElement,
            UnityEngine.UIElements.PickingMode>> _suspended = new();

        static readonly string[] Tabs =
            { "Scenes", "Session", "Flags", "Clues", "Focus", "Hub", "Scenarios", "Sequences" };

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
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
            {
                _visible = !_visible;
                if (_visible)
                {
                    _prevLock = Cursor.lockState;
                    _prevCursorVisible = Cursor.visible;
                    SuspendPanelInput();
                }
                else
                {
                    RestorePanelInput();
                    Cursor.lockState = _prevLock;
                    Cursor.visible = _prevCursorVisible;
                }
            }
        }

        CursorLockMode _prevLock;
        bool _prevCursorVisible;

        // Hub panels re-lock the cursor when they close; a locked cursor
        // sends every click to screen centre, so keep it free while open.
        void LateUpdate()
        {
            if (!_visible) return;
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible) Cursor.visible = true;
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
            if (!_visible) return;
            _window = GUILayout.Window(987654, _window, DrawWindow,
                "Morsoul Dev Menu  [F1]");
        }

        void DrawWindow(int id)
        {
            var core = CoreRuntime.Instance;
            GUILayout.Label(core == null
                ? "CoreRuntime: ABSENT (limited tools)"
                : $"Scene: {SceneManager.GetActiveScene().name}   " +
                  $"Case: {core.Session.CurrentCase}   " +
                  $"Focus: {core.Focus.Percent:0}/{core.Focus.Maximum:0}");

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
                case 6: DrawScenarios(); break;
                case 7: DrawSequences(); break;
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

            Section("Register clue by ID");
            GUILayout.BeginHorizontal();
            _clueInput = GUILayout.TextField(_clueInput, GUILayout.Width(340));
            if (GUILayout.Button("Register", GUILayout.Width(80)))
            {
                var clue = core.ResolveClue(_clueInput);
                if (clue == null) Debug.LogWarning($"DevMenu: no clue '{_clueInput}'.");
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

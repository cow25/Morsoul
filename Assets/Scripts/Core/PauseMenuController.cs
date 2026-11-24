using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Morsoul.Core
{
    /// <summary>
    /// Shared Escape / Pause Menu overlay matching Figma's dual ornate runic cards:
    /// upper Detective ID dossier card and lower Actions / Settings card.
    /// Auto-bootstraps in PlayMode on every non-Title gameplay scene so both 2D
    /// hubs and 3D scenes share identical pause behavior.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class PauseMenuController : MonoBehaviour
    {
        const string ConfigResourcePath = "PauseMenuConfig";
        const string TitleSceneName = "Title";

        static PauseMenuController _instance;
        static readonly List<Func<bool>> ExternalModals = new List<Func<bool>>();
        static int _lastExternalModalCloseFrame = -1;

        public static PauseMenuController Instance => _instance;
        public static bool IsPaused => _instance != null && _instance.IsOpen;
        public static bool AnyModalOrPauseOpen => IsPaused || IsAnyExternalModalOpen();

        public static void RegisterExternalModal(Func<bool> isOpenPredicate)
        {
            if (isOpenPredicate != null && !ExternalModals.Contains(isOpenPredicate))
                ExternalModals.Add(isOpenPredicate);
        }

        public static void UnregisterExternalModal(Func<bool> isOpenPredicate)
        {
            if (isOpenPredicate != null)
                ExternalModals.Remove(isOpenPredicate);
        }

        /// <summary>
        /// Signals that an in-game modal (Journal, Board, TravelMenu, Interrogation)
        /// closed this frame so Escape does not also open the Pause Menu on the same keypress.
        /// </summary>
        public static void NotifyExternalModalClosed()
        {
            _lastExternalModalCloseFrame = Time.frameCount;
        }

        public static void TogglePause()
        {
            var ctrl = EnsureRuntime();
            if (ctrl == null) return;
            if (ctrl.IsOpen) ctrl.Close();
            else ctrl.Open();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInstallOnSceneLoad()
        {
            SceneManager.sceneLoaded -= OnAnySceneLoaded;
            SceneManager.sceneLoaded += OnAnySceneLoaded;
            TryInstallForScene(SceneManager.GetActiveScene().name);
        }

        static void OnAnySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryInstallForScene(scene.name);
        }

        static void TryInstallForScene(string sceneName)
        {
            if (!Application.isPlaying) return;
            if (string.Equals(sceneName, TitleSceneName, StringComparison.OrdinalIgnoreCase))
            {
                if (_instance != null && _instance.IsOpen)
                    _instance.Close();
                return;
            }
            EnsureRuntime();
        }

        /// <summary>
        /// Ensures a persistent PauseMenuController exists on CoreRuntime with its
        /// high-priority PanelRenderer. Safe to call in PlayMode or EditMode tests.
        /// </summary>
        public static PauseMenuController EnsureRuntime()
        {
            if (_instance != null) return _instance;

            var existing = FindAnyObjectByType<PauseMenuController>(FindObjectsInactive.Include);
            if (existing != null)
            {
                _instance = existing;
                return _instance;
            }

            var core = CoreRuntime.Ensure();
            var child = new GameObject("PauseMenuOverlay");
            child.transform.SetParent(core.transform, false);

            var config = Resources.Load<PauseMenuAssets>(ConfigResourcePath);
            var pr = child.AddComponent<PanelRenderer>();
            pr.sortingOrder = 120;
            if (config != null)
            {
                if (config.panelSettings != null) pr.panelSettings = config.panelSettings;
                if (config.pauseMenuUxml != null) pr.visualTreeAsset = config.pauseMenuUxml;
            }

            _instance = child.AddComponent<PauseMenuController>();
            return _instance;
        }

        PanelRenderer _panelRenderer;
        VisualElement _root;
        VisualElement _actionsPage;
        VisualElement _settingsPage;
        VisualElement _tutorialsPage;
        VisualElement _quitConfirmRow;

        // Dossier labels
        Label _nameLabel;
        Label _speciesLabel;
        Label _activeCaseLabel;
        Label _casesSolvedLabel;
        Label _focusPointsLabel;
        VisualElement _focusBarFill;
        Label _cluesLoggedLabel;
        Label _runicLogsLabel;
        Label _titleLabel;
        Label _statusBanner;

        // Actions buttons
        Button _saveButton;
        Button _settingsButton;
        Button _tutorialLogsButton;
        Button _quitButton;
        Button _resumeButton;
        Button _returnTitleButton;
        Button _exitAppButton;
        Button _quitCancelButton;

        // Tutorials subpage
        VisualElement _tutFilterRow;
        ScrollView _tutListScroll;
        Label _tutDetailTitle;
        Label _tutDetailKeys;
        Label _tutDetailBody;
        Button _autoTutorialsToggle;
        Button _tutorialsBackButton;
        TutorialCategory? _selectedTutCategory;
        string _selectedTutId;

        // Settings elements
        VisualElement _masterFill;
        VisualElement _musicFill;
        VisualElement _sfxFill;
        Label _masterValueLabel;
        Label _musicValueLabel;
        Label _sfxValueLabel;
        Button _fullscreenButton;
        Button _settingsBackButton;

        bool _isOpen;
        bool _wasExternalModalOpenPrevFrame;
        int _uiVersion;
        float _prevTimeScale = 1f;
        CursorLockMode _prevCursorLock = CursorLockMode.None;
        bool _prevCursorVisible = true;

        public bool IsOpen => _isOpen;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        void OnDestroy()
        {
            if (_isOpen) Time.timeScale = _prevTimeScale;
            if (_instance == this) _instance = null;
        }

        void OnEnable()
        {
            _panelRenderer = GetComponent<PanelRenderer>();
            if (_panelRenderer != null)
            {
                _panelRenderer.sortingOrder = 120;
                _panelRenderer.RegisterUIReloadCallback(OnUIReload);
            }
        }

        void OnDisable()
        {
            if (_panelRenderer != null)
                _panelRenderer.UnregisterUIReloadCallback(OnUIReload);
            if (_isOpen) Close();
        }

        string _cachedSceneName;
        bool _isTitleScene;

        void LateUpdate()
        {
            var activeName = SceneManager.GetActiveScene().name;
            if (_cachedSceneName != activeName)
            {
                _cachedSceneName = activeName;
                _isTitleScene = string.Equals(activeName, TitleSceneName, StringComparison.OrdinalIgnoreCase);
            }
            if (_isTitleScene)
            {
                if (_isOpen) Close();
                _wasExternalModalOpenPrevFrame = false;
                return;
            }

            bool anyExternal = IsAnyExternalModalOpen();
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (_isOpen)
                {
                    if ((_settingsPage != null && _settingsPage.style.display == DisplayStyle.Flex) ||
                        (_tutorialsPage != null && _tutorialsPage.style.display == DisplayStyle.Flex))
                        ShowActionsPage();
                    else
                        Close();
                }
                else if (!anyExternal &&
                         !_wasExternalModalOpenPrevFrame &&
                         _lastExternalModalCloseFrame != Time.frameCount &&
                         !IsCutsceneActive())
                {
                    Open();
                }
            }
            _wasExternalModalOpenPrevFrame = anyExternal;

            if (_isOpen && Screen.fullScreen != _lastObservedFullscreen)
            {
                _lastObservedFullscreen = Screen.fullScreen;
                UpdateFullscreenLabel(Screen.fullScreen);
            }
        }

        bool _lastObservedFullscreen;

        static bool IsCutsceneActive()
        {
            return CoreRuntime.Instance != null &&
                   !string.IsNullOrEmpty(CoreRuntime.Instance.Session.ActiveCutsceneId);
        }

        static bool IsAnyExternalModalOpen()
        {
            for (int i = ExternalModals.Count - 1; i >= 0; i--)
            {
                var pred = ExternalModals[i];
                if (pred == null)
                {
                    ExternalModals.RemoveAt(i);
                    continue;
                }
                if (pred()) return true;
            }
            return false;
        }

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;
            _prevTimeScale = Time.timeScale;
            _prevCursorLock = UnityEngine.Cursor.lockState;
            _prevCursorVisible = UnityEngine.Cursor.visible;

            Time.timeScale = 0f;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            RefreshDossier();
            RefreshSettingsUi();
            ShowActionsPage();
            ClearStatus();

            if (_root != null) _root.style.display = DisplayStyle.Flex;
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            Time.timeScale = _prevTimeScale;
            UnityEngine.Cursor.lockState = _prevCursorLock;
            UnityEngine.Cursor.visible = _prevCursorVisible;
            if (_root != null) _root.style.display = DisplayStyle.None;
        }

        public void ShowSettingsPage()
        {
            if (_actionsPage != null) _actionsPage.style.display = DisplayStyle.None;
            if (_tutorialsPage != null) _tutorialsPage.style.display = DisplayStyle.None;
            if (_settingsPage != null) _settingsPage.style.display = DisplayStyle.Flex;
            if (_quitConfirmRow != null) _quitConfirmRow.style.display = DisplayStyle.None;
            RefreshSettingsUi();
        }

        public void ShowTutorialsPage()
        {
            if (_actionsPage != null) _actionsPage.style.display = DisplayStyle.None;
            if (_settingsPage != null) _settingsPage.style.display = DisplayStyle.None;
            if (_tutorialsPage != null) _tutorialsPage.style.display = DisplayStyle.Flex;
            if (_quitConfirmRow != null) _quitConfirmRow.style.display = DisplayStyle.None;
            RefreshTutorialsUi();
        }

        public void ShowActionsPage()
        {
            if (_settingsPage != null) _settingsPage.style.display = DisplayStyle.None;
            if (_tutorialsPage != null) _tutorialsPage.style.display = DisplayStyle.None;
            if (_actionsPage != null) _actionsPage.style.display = DisplayStyle.Flex;
            if (_quitConfirmRow != null) _quitConfirmRow.style.display = DisplayStyle.None;
        }

        public void SaveManualCheckpoint()
        {
            var gsm = GameStateManager.Ensure();
            string sceneName = SceneManager.GetActiveScene().name;
            gsm.WriteCheckpoint("ManualSave", sceneName, preserveRestoredPose: false);
            SetStatus("Game state saved.");
        }

        public void ReturnToTitle()
        {
            Close();
            Time.timeScale = 1f;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            SceneManager.LoadScene(TitleSceneName);
        }

        public void QuitApplication()
        {
            Close();
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void RefreshDossier()
        {
            var core = CoreRuntime.Instance;
            var session = core != null ? core.Session : null;
            var focus = core != null ? core.Focus : null;

            string playerName = session != null && !string.IsNullOrWhiteSpace(session.PlayerName)
                ? session.PlayerName.Trim()
                : "Golem";

            int solved = 0;
            if (session != null)
            {
                foreach (var kv in session.Grades)
                {
                    if (kv.Value != Morsoul.Data.Grade.Ungraded) solved++;
                }
            }

            string activeCase = "Prologue";
            if (session != null && session.CurrentCase >= 1)
            {
                var caseData = core.ResolveCase(session.CurrentCase);
                activeCase = caseData != null && !string.IsNullOrEmpty(caseData.title)
                    ? $"Case {session.CurrentCase}: {caseData.title}"
                    : $"Case {session.CurrentCase}";
            }

            float focusPct = focus != null ? focus.Percent : FocusTuning.FocusMax;
            float focusMax = focus != null ? focus.Maximum : FocusTuning.FocusMax;
            float focusRatio = focusMax > 0f ? Mathf.Clamp01(focusPct / focusMax) : 1f;
            int cluesCount = session != null ? session.ClueIds.Count : 0;
            int logsCount = session != null ? session.RunicLogCount : 0;

            string rankTitle = solved >= 3 ? "Master Detective"
                : solved >= 2 ? "Senior Investigator"
                : solved >= 1 ? "Case Investigator"
                : "Initiated Construct";

            if (_nameLabel != null) _nameLabel.text = playerName;
            if (_speciesLabel != null) _speciesLabel.text = "Golem";
            if (_activeCaseLabel != null) _activeCaseLabel.text = activeCase;
            if (_casesSolvedLabel != null) _casesSolvedLabel.text = solved.ToString();
            if (_focusPointsLabel != null) _focusPointsLabel.text = $"{Mathf.RoundToInt(focusPct)}/{Mathf.RoundToInt(focusMax)}";
            if (_focusBarFill != null) _focusBarFill.style.width = Length.Percent(focusRatio * 100f);
            if (_cluesLoggedLabel != null) _cluesLoggedLabel.text = cluesCount.ToString();
            if (_runicLogsLabel != null) _runicLogsLabel.text = $"{logsCount}/4";
            if (_titleLabel != null) _titleLabel.text = rankTitle;
        }

        void RefreshSettingsUi()
        {
            var audio = CoreRuntime.Instance != null ? CoreRuntime.Instance.Audio : null;
            float master = audio != null ? audio.MasterVolume : 1f;
            float music = audio != null ? audio.MusicVolume : 1f;
            float sfx = audio != null ? audio.SfxVolume : 1f;

            UpdateVolumeBar(_masterFill, _masterValueLabel, master);
            UpdateVolumeBar(_musicFill, _musicValueLabel, music);
            UpdateVolumeBar(_sfxFill, _sfxValueLabel, sfx);

            _lastObservedFullscreen = Screen.fullScreen;
            UpdateFullscreenLabel(Screen.fullScreen);
        }

        void UpdateFullscreenLabel(bool isFullscreen)
        {
            if (_fullscreenButton != null)
                _fullscreenButton.text = isFullscreen ? "Fullscreen: ON" : "Fullscreen: OFF";
        }

        static void UpdateVolumeBar(VisualElement fill, Label label, float val)
        {
            int pct = Mathf.RoundToInt(Mathf.Clamp01(val) * 100f);
            if (fill != null) fill.style.width = Length.Percent(pct);
            if (label != null) label.text = $"{pct}%";
        }

        void AdjustVolume(string channel, float delta)
        {
            var audio = CoreRuntime.Ensure().Audio;
            switch (channel)
            {
                case "master": audio.SetMasterVolume(audio.MasterVolume + delta); break;
                case "music": audio.SetMusicVolume(audio.MusicVolume + delta); break;
                case "sfx": audio.SetSfxVolume(audio.SfxVolume + delta); break;
            }
            RefreshSettingsUi();
        }

        void ToggleFullscreen()
        {
            _lastObservedFullscreen = Screen.fullScreen;
            bool next = ToggleDisplayFullscreen();
            UpdateFullscreenLabel(next);
        }

        /// <summary>
        /// Switches between FullScreenWindow (native) and Windowed (1280x720 scaled)
        /// and returns the target fullscreen state immediately.
        /// </summary>
        public static bool ToggleDisplayFullscreen()
        {
            bool next = !Screen.fullScreen;
            if (next)
            {
                int w = Display.main.systemWidth > 0 ? Display.main.systemWidth : Screen.currentResolution.width;
                int h = Display.main.systemHeight > 0 ? Display.main.systemHeight : Screen.currentResolution.height;
                Screen.SetResolution(w, h, FullScreenMode.FullScreenWindow);
            }
            else
            {
                int sysW = Display.main.systemWidth > 0 ? Display.main.systemWidth : 1920;
                int sysH = Display.main.systemHeight > 0 ? Display.main.systemHeight : 1080;
                int w = Mathf.Min(1280, Mathf.Max(640, sysW - 160));
                int h = Mathf.RoundToInt(w * 9f / 16f);
                if (h > sysH - 120)
                {
                    h = Mathf.Max(360, sysH - 120);
                    w = Mathf.RoundToInt(h * 16f / 9f);
                }
                Screen.SetResolution(w, h, FullScreenMode.Windowed);
            }
            return next;
        }

        void SetStatus(string msg)
        {
            if (_statusBanner == null) return;
            _statusBanner.text = msg ?? "";
            _statusBanner.style.display = string.IsNullOrEmpty(msg) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        void ClearStatus() => SetStatus("");

        void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            if (_uiVersion == version) return;
            _uiVersion = version;

            _root = root.Q<VisualElement>("PauseMenu-Root");
            _actionsPage = root.Q<VisualElement>("ActionsPage");
            _settingsPage = root.Q<VisualElement>("SettingsPage");
            _tutorialsPage = root.Q<VisualElement>("TutorialsPage");
            _quitConfirmRow = root.Q<VisualElement>("QuitConfirmRow");

            _nameLabel = root.Q<Label>("DossierName");
            _speciesLabel = root.Q<Label>("DossierSpecies");
            _activeCaseLabel = root.Q<Label>("DossierActiveCase");
            _casesSolvedLabel = root.Q<Label>("DossierCasesSolved");
            _focusPointsLabel = root.Q<Label>("FocusPointsLabel");
            _focusBarFill = root.Q<VisualElement>("FocusBarFill");
            _cluesLoggedLabel = root.Q<Label>("DossierCluesLogged");
            _runicLogsLabel = root.Q<Label>("DossierRunicLogs");
            _titleLabel = root.Q<Label>("DossierTitle");
            _statusBanner = root.Q<Label>("PauseStatusBanner");

            _saveButton = root.Q<Button>("SaveStateButton");
            _settingsButton = root.Q<Button>("SettingsButton");
            _tutorialLogsButton = root.Q<Button>("TutorialLogsButton");
            _quitButton = root.Q<Button>("QuitGameButton");
            _resumeButton = root.Q<Button>("ResumeButton");
            _returnTitleButton = root.Q<Button>("ReturnTitleButton");
            _exitAppButton = root.Q<Button>("ExitAppButton");
            _quitCancelButton = root.Q<Button>("QuitCancelButton");

            _masterFill = root.Q<VisualElement>("MasterFill");
            _musicFill = root.Q<VisualElement>("MusicFill");
            _sfxFill = root.Q<VisualElement>("SfxFill");
            _masterValueLabel = root.Q<Label>("MasterValueLabel");
            _musicValueLabel = root.Q<Label>("MusicValueLabel");
            _sfxValueLabel = root.Q<Label>("SfxValueLabel");
            _fullscreenButton = root.Q<Button>("FullscreenToggleButton");
            _settingsBackButton = root.Q<Button>("SettingsBackButton");

            _tutFilterRow = root.Q<VisualElement>("TutorialFilterRow");
            _tutListScroll = root.Q<ScrollView>("TutorialListScroll");
            _tutDetailTitle = root.Q<Label>("TutorialDetailTitle");
            _tutDetailKeys = root.Q<Label>("TutorialDetailKeys");
            _tutDetailBody = root.Q<Label>("TutorialDetailBody");
            _autoTutorialsToggle = root.Q<Button>("AutoTutorialsToggleButton");
            _tutorialsBackButton = root.Q<Button>("TutorialsBackButton");

            if (_saveButton != null) _saveButton.clicked += SaveManualCheckpoint;
            if (_settingsButton != null) _settingsButton.clicked += ShowSettingsPage;
            if (_tutorialLogsButton != null) _tutorialLogsButton.clicked += ShowTutorialsPage;
            if (_resumeButton != null) _resumeButton.clicked += Close;
            if (_quitButton != null) _quitButton.clicked += () =>
            {
                if (_quitConfirmRow != null)
                    _quitConfirmRow.style.display = _quitConfirmRow.style.display == DisplayStyle.Flex
                        ? DisplayStyle.None : DisplayStyle.Flex;
            };
            if (_returnTitleButton != null) _returnTitleButton.clicked += ReturnToTitle;
            if (_exitAppButton != null) _exitAppButton.clicked += QuitApplication;
            if (_quitCancelButton != null) _quitCancelButton.clicked += () =>
            {
                if (_quitConfirmRow != null) _quitConfirmRow.style.display = DisplayStyle.None;
            };
            if (_fullscreenButton != null) _fullscreenButton.clicked += ToggleFullscreen;
            if (_settingsBackButton != null) _settingsBackButton.clicked += ShowActionsPage;
            if (_autoTutorialsToggle != null) _autoTutorialsToggle.clicked += ToggleAutoTutorials;
            if (_tutorialsBackButton != null) _tutorialsBackButton.clicked += ShowActionsPage;

            WireVolumeButtons(root, "Master", "master");
            WireVolumeButtons(root, "Music", "music");
            WireVolumeButtons(root, "Sfx", "sfx");
            BuildTutorialFilters();

            if (_root != null)
                _root.style.display = _isOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (_isOpen)
            {
                RefreshDossier();
                RefreshSettingsUi();
                RefreshTutorialsUi();
            }
        }

        void BuildTutorialFilters()
        {
            if (_tutFilterRow == null) return;
            _tutFilterRow.Clear();
            AddTutFilter("All", null);
            AddTutFilter("Controls", TutorialCategory.MovementAndControls);
            AddTutFilter("Investigate", TutorialCategory.InvestigationAndJournal);
            AddTutFilter("Board", TutorialCategory.DeductionBoard);
            AddTutFilter("Focus/Rest", TutorialCategory.FocusAndRest);
            AddTutFilter("Abilities", TutorialCategory.Abilities);
        }

        void AddTutFilter(string label, TutorialCategory? category)
        {
            var btn = new Button(() =>
            {
                _selectedTutCategory = category;
                _selectedTutId = null;
                RefreshTutorialsUi();
            })
            {
                text = label
            };
            btn.AddToClassList("tut-log-filter-btn");
            if (_selectedTutCategory == category)
                btn.AddToClassList("tut-log-filter-btn--active");
            _tutFilterRow.Add(btn);
        }

        void ToggleAutoTutorials()
        {
            TutorialCatalog.AutoTutorialsSuppressed = !TutorialCatalog.AutoTutorialsSuppressed;
            RefreshTutorialsUi();
        }

        void RefreshTutorialsUi()
        {
            if (_autoTutorialsToggle != null)
                _autoTutorialsToggle.text = TutorialCatalog.AutoTutorialsSuppressed
                    ? "Auto-Tips: OFF"
                    : "Auto-Tips: ON";

            BuildTutorialFilters();
            if (_tutListScroll == null) return;
            _tutListScroll.Clear();

            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            TutorialEntry firstVisible = null;
            TutorialEntry selected = null;

            foreach (var entry in TutorialCatalog.All)
            {
                if (_selectedTutCategory.HasValue && entry.Category != _selectedTutCategory.Value)
                    continue;
                if (firstVisible == null) firstVisible = entry;

                bool unlocked = TutorialCatalog.IsUnlockedInLogs(session, entry);
                bool seen = TutorialCatalog.IsSeen(session, entry.Id);
                string prefix = !unlocked ? "[Locked] " : (seen ? "✓ " : "• ");
                string title = unlocked ? entry.Title : $"{entry.Title} (Discover in-game)";

                var captured = entry;
                var itemBtn = new Button(() =>
                {
                    _selectedTutId = captured.Id;
                    RefreshTutorialsUi();
                })
                {
                    text = prefix + title
                };
                itemBtn.AddToClassList("tut-log-item");
                if (!unlocked) itemBtn.AddToClassList("tut-log-item--locked");
                if (_selectedTutId == entry.Id)
                {
                    itemBtn.AddToClassList("tut-log-item--selected");
                    selected = entry;
                }
                _tutListScroll.Add(itemBtn);
            }

            if (selected == null) selected = firstVisible;
            if (selected != null) _selectedTutId = selected.Id;
            RenderTutorialDetail(selected, session);
        }

        void RenderTutorialDetail(TutorialEntry entry, SessionState session)
        {
            if (_tutDetailTitle == null || _tutDetailKeys == null || _tutDetailBody == null) return;
            if (entry == null)
            {
                _tutDetailTitle.text = "No tutorials in this category";
                _tutDetailKeys.text = "";
                _tutDetailBody.text = "";
                return;
            }

            bool unlocked = TutorialCatalog.IsUnlockedInLogs(session, entry);
            _tutDetailTitle.text = unlocked ? entry.Title : $"{entry.Title}  [Not Yet Encountered]";
            _tutDetailKeys.text = unlocked ? entry.Keybinds : "Encounter this mechanic in gameplay to unlock full details.";
            if (!unlocked)
            {
                _tutDetailBody.text = entry.Summary;
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(entry.Summary);
            if (entry.Steps != null)
            {
                foreach (var s in entry.Steps)
                    sb.AppendLine("• " + s);
            }
            _tutDetailBody.text = sb.ToString().TrimEnd();
        }

        void WireVolumeButtons(VisualElement root, string prefix, string channel)
        {
            var minus = root.Q<Button>($"{prefix}MinusButton");
            var plus = root.Q<Button>($"{prefix}PlusButton");
            if (minus != null) minus.clicked += () => AdjustVolume(channel, -0.1f);
            if (plus != null) plus.clicked += () => AdjustVolume(channel, +0.1f);
        }
    }
}

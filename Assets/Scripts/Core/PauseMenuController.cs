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
        Button _quitButton;
        Button _resumeButton;
        Button _returnTitleButton;
        Button _exitAppButton;
        Button _quitCancelButton;

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

        void LateUpdate()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (string.Equals(sceneName, TitleSceneName, StringComparison.OrdinalIgnoreCase))
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
                    if (_settingsPage != null && _settingsPage.style.display == DisplayStyle.Flex)
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
        }

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
            if (_settingsPage != null) _settingsPage.style.display = DisplayStyle.Flex;
            if (_quitConfirmRow != null) _quitConfirmRow.style.display = DisplayStyle.None;
            RefreshSettingsUi();
        }

        public void ShowActionsPage()
        {
            if (_settingsPage != null) _settingsPage.style.display = DisplayStyle.None;
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
                    ? $"Case {session.CurrentCase}"
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

            if (_fullscreenButton != null)
                _fullscreenButton.text = Screen.fullScreen ? "Fullscreen: ON" : "Fullscreen: OFF";
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
            Screen.fullScreen = !Screen.fullScreen;
            if (_fullscreenButton != null)
                _fullscreenButton.text = Screen.fullScreen ? "Fullscreen: ON" : "Fullscreen: OFF";
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

            if (_saveButton != null) _saveButton.clicked += SaveManualCheckpoint;
            if (_settingsButton != null) _settingsButton.clicked += ShowSettingsPage;
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

            WireVolumeButtons(root, "Master", "master");
            WireVolumeButtons(root, "Music", "music");
            WireVolumeButtons(root, "Sfx", "sfx");

            if (_root != null)
                _root.style.display = _isOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (_isOpen)
            {
                RefreshDossier();
                RefreshSettingsUi();
            }
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

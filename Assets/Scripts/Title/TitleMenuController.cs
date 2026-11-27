using System;
using System.Collections.Generic;
using Morsoul.Core;
using Morsoul.Cutscene;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;
using Image = UnityEngine.UI.Image;
using UguiButton = UnityEngine.UI.Button;
using UitButton = UnityEngine.UIElements.Button;
using UitLabel = UnityEngine.UIElements.Label;

namespace Morsoul.Title
{
    /// <summary>
    /// Controller for the Morsoul title screen matching the Figma candlelit study layout,
    /// Save Slot tarot modal (Load / New Game + Overwrite prompt), and Settings modal.
    /// Drives the UI Toolkit panel when attached to a PanelRenderer and preserves the
    /// legacy uGUI button contract for EditMode compatibility tests.
    /// </summary>
    public class TitleMenuController : MonoBehaviour
    {
        [Header("Legacy uGUI Refs (EditMode tests & fallback)")]
        public UguiButton startGameButton;
        public UguiButton loadGameButton;
        public UguiButton newGameButton;
        public UguiButton continueButton;
        public UguiButton settingsButton;
        public UguiButton quitButton;
        public bool saveExists;

        [Header("Opening Cutscene")]
        public CutscenePlayerController openingCutscenePlayer;
        public CutsceneData openingCutscene;

        [Header("Selection Indicator (Legacy uGUI)")]
        public RectTransform selectionDiamond;
        public RectTransform selectionUnderline;

        [Header("Save Slot Modal (Legacy uGUI)")]
        public GameObject saveSlotModal;
        public TMP_Text saveSlotHeader;
        public UguiButton slot1Button;
        public UguiButton slot2Button;
        public UguiButton slot3Button;
        public Image slot1CardImage;
        public GameObject slot1PortraitRoot;
        public TMP_Text slot1TitleLabel;
        public TMP_Text slot1SubtitleLabel;
        public TMP_Text saveStatusLabel;
        public UguiButton saveModalBackButton;

        [Header("Overwrite Prompt (Legacy uGUI)")]
        public GameObject overwritePrompt;
        public UguiButton overwriteYesButton;
        public UguiButton overwriteNoButton;

        [Header("Settings Modal (Legacy uGUI)")]
        public GameObject settingsModal;
        public TMP_Text masterValueLabel;
        public TMP_Text musicValueLabel;
        public TMP_Text sfxValueLabel;
        public UguiButton masterDownButton;
        public UguiButton masterUpButton;
        public UguiButton musicDownButton;
        public UguiButton musicUpButton;
        public UguiButton sfxDownButton;
        public UguiButton sfxUpButton;
        public UguiButton fullscreenButton;
        public UguiButton settingsBackButton;

        public event Action NewGameRequested;
        public event Action ContinueRequested;
        public event Action QuitRequested;

        bool _saveModalIsNewGameMode;
        int _pendingOverwriteSlot = 1;

        // UI Toolkit state
        PanelRenderer _panelRenderer;
        int _uiVersion;
        VisualElement _uitRoot;
        readonly List<(VisualElement row, UitButton btn, Action onActivate)> _menuEntries = new();
        int _selectedMenuIndex;

        UitButton _uitStartBtn;
        UitButton _uitLoadBtn;
        UitButton _uitNewBtn;
        UitButton _uitSettingsBtn;
        UitButton _uitExitBtn;

        VisualElement _uitSaveModal;
        UitLabel _uitSaveHeader;
        UitButton _uitSlot1Btn;
        UitButton _uitSlot2Btn;
        UitButton _uitSlot3Btn;
        UitLabel _uitSlot1Title;
        VisualElement _uitSlot1Portrait;
        UitLabel _uitSlot1Subtitle;
        UitLabel _uitSlot2Title;
        VisualElement _uitSlot2Portrait;
        UitLabel _uitSlot2Subtitle;
        UitLabel _uitSlot3Title;
        VisualElement _uitSlot3Portrait;
        UitLabel _uitSlot3Subtitle;
        UitLabel _uitSaveStatus;
        UitButton _uitSaveBackBtn;

        VisualElement _uitOverwritePrompt;
        UitButton _uitOverwriteYesBtn;
        UitButton _uitOverwriteNoBtn;

        VisualElement _uitSettingsModal;
        VisualElement _uitMasterFill;
        VisualElement _uitMusicFill;
        VisualElement _uitSfxFill;
        UitLabel _uitMasterVal;
        UitLabel _uitMusicVal;
        UitLabel _uitSfxVal;
        UitButton _uitFullscreenBtn;
        UitButton _uitSettingsBackBtn;

        public bool IsUiToolkitBound => _uitRoot != null;
        public bool IsSaveModalOpen => (_uitSaveModal != null && _uitSaveModal.style.display == DisplayStyle.Flex)
                                       || (saveSlotModal != null && saveSlotModal.activeSelf);
        public bool IsSettingsModalOpen => (_uitSettingsModal != null && _uitSettingsModal.style.display == DisplayStyle.Flex)
                                           || (settingsModal != null && settingsModal.activeSelf);

        void Awake()
        {
            if (Application.isPlaying) CoreRuntime.Ensure();
            EnsureMenuCursorReady();
            BindUguiButtons();
            RefreshUguiMenuIndicators();
        }

        void OnEnable()
        {
            EnsureMenuCursorReady();
            _panelRenderer = GetComponent<PanelRenderer>();
            if (_panelRenderer != null)
                _panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }

        void OnDisable()
        {
            if (_panelRenderer != null)
                _panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        }

        void Start()
        {
            EnsureMenuCursorReady();
            RefreshSaveState();
            if (startGameButton != null || newGameButton != null)
                StartCoroutine(PositionInitialCursorNextFrame());
        }

        static void EnsureMenuCursorReady()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        System.Collections.IEnumerator PositionInitialCursorNextFrame()
        {
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (startGameButton != null)
                MoveSelectionIndicator(startGameButton.GetComponent<RectTransform>());
            else if (newGameButton != null)
                MoveSelectionIndicator(newGameButton.GetComponent<RectTransform>());
        }

        bool _lastObservedFullscreen;

        void Update()
        {
            if (_startingNewGameCutscene) return;
            if (IsSettingsModalOpen && Screen.fullScreen != _lastObservedFullscreen)
            {
                _lastObservedFullscreen = Screen.fullScreen;
                RefreshSettingsLabels(Screen.fullScreen);
            }
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (HandleEscapePressed()) return;
            }

            if (_uitRoot == null || IsSaveModalOpen || IsSettingsModalOpen || _menuEntries.Count == 0)
                return;

            int dir = 0;
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) dir = 1;
            else if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) dir = -1;

            if (dir != 0)
            {
                MoveKeyboardMenuSelection(dir);
            }
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            {
                if (_selectedMenuIndex >= 0 && _selectedMenuIndex < _menuEntries.Count)
                {
                    var entry = _menuEntries[_selectedMenuIndex];
                    if (IsMenuEntrySelectable(entry))
                    {
                        PlayUiClick();
                        entry.onActivate?.Invoke();
                    }
                }
            }
        }

        bool HandleEscapePressed()
        {
            if (_uitOverwritePrompt != null && _uitOverwritePrompt.style.display == DisplayStyle.Flex)
            {
                _uitOverwritePrompt.style.display = DisplayStyle.None;
                return true;
            }
            if (_uitSaveModal != null && _uitSaveModal.style.display == DisplayStyle.Flex)
            {
                CloseSaveSlotModal();
                return true;
            }
            if (_uitSettingsModal != null && _uitSettingsModal.style.display == DisplayStyle.Flex)
            {
                CloseSettingsModal();
                return true;
            }
            if (overwritePrompt != null && overwritePrompt.activeSelf)
            {
                overwritePrompt.SetActive(false);
                return true;
            }
            if (saveSlotModal != null && saveSlotModal.activeSelf)
            {
                CloseSaveSlotModal();
                return true;
            }
            if (settingsModal != null && settingsModal.activeSelf)
            {
                CloseSettingsModal();
                return true;
            }
            return false;
        }

        static bool IsMenuEntrySelectable((VisualElement row, UitButton btn, Action onActivate) entry)
        {
            if (entry.row != null && entry.row.style.display == DisplayStyle.None)
                return false;
            return entry.btn != null && entry.btn.enabledSelf;
        }

        void MoveKeyboardMenuSelection(int delta)
        {
            if (_menuEntries.Count == 0) return;
            int idx = _selectedMenuIndex;
            for (int step = 0; step < _menuEntries.Count; step++)
            {
                idx = (idx + delta + _menuEntries.Count) % _menuEntries.Count;
                if (IsMenuEntrySelectable(_menuEntries[idx]))
                {
                    SetActiveMenuIndex(idx);
                    _menuEntries[idx].btn.Focus();
                    return;
                }
            }
        }

        void SetActiveMenuIndex(int index)
        {
            _selectedMenuIndex = Mathf.Clamp(index, 0, Mathf.Max(0, _menuEntries.Count - 1));
            for (int i = 0; i < _menuEntries.Count; i++)
            {
                var row = _menuEntries[i].row;
                if (row == null) continue;
                if (i == _selectedMenuIndex) row.AddToClassList("title-menu-row--active");
                else row.RemoveFromClassList("title-menu-row--active");
            }
        }

        void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            if (_uiVersion == version && _uitRoot != null) return;
            _uiVersion = version;
            _uitRoot = root.Q<VisualElement>("Title-Root") ?? root;

            _menuEntries.Clear();
            _uitStartBtn = BindUitMenuRow("StartGameRow", "StartGameButton", 0, OnStartGameClicked);
            _uitLoadBtn = BindUitMenuRow("LoadGameRow", "LoadGameButton", 1, () => OpenSaveSlotModal(isNewGame: false));
            _uitNewBtn = BindUitMenuRow("NewGameRow", "NewGameButton", 2, OnNewGameClicked);
            _uitSettingsBtn = BindUitMenuRow("SettingsRow", "SettingsButton", 3, OpenSettingsModal);
            _uitExitBtn = BindUitMenuRow("ExitRow", "ExitButton", 4, RequestQuit);

            _uitSaveModal = root.Q<VisualElement>("SaveSlotModal");
            _uitSaveHeader = root.Q<UitLabel>("SaveSlotHeader");
            _uitSlot1Btn = root.Q<UitButton>("Slot1Card");
            _uitSlot2Btn = root.Q<UitButton>("Slot2Card");
            _uitSlot3Btn = root.Q<UitButton>("Slot3Card");
            _uitSlot1Title = root.Q<UitLabel>("Slot1Title");
            _uitSlot1Portrait = root.Q<VisualElement>("Slot1Portrait");
            _uitSlot1Subtitle = root.Q<UitLabel>("Slot1Subtitle");
            _uitSlot2Title = root.Q<UitLabel>("Slot2Title");
            _uitSlot2Portrait = root.Q<VisualElement>("Slot2Portrait");
            _uitSlot2Subtitle = root.Q<UitLabel>("Slot2Subtitle");
            _uitSlot3Title = root.Q<UitLabel>("Slot3Title");
            _uitSlot3Portrait = root.Q<VisualElement>("Slot3Portrait");
            _uitSlot3Subtitle = root.Q<UitLabel>("Slot3Subtitle");
            _uitSaveStatus = root.Q<UitLabel>("SaveStatusLabel");
            _uitSaveBackBtn = root.Q<UitButton>("SaveModalBackButton");

            if (_uitSlot1Btn != null) _uitSlot1Btn.clicked += () => OnSlotClicked(1);
            if (_uitSlot2Btn != null) _uitSlot2Btn.clicked += () => OnSlotClicked(2);
            if (_uitSlot3Btn != null) _uitSlot3Btn.clicked += () => OnSlotClicked(3);
            if (_uitSaveBackBtn != null) _uitSaveBackBtn.clicked += CloseSaveSlotModal;

            _uitOverwritePrompt = root.Q<VisualElement>("OverwritePrompt");
            _uitOverwriteYesBtn = root.Q<UitButton>("OverwriteYes");
            _uitOverwriteNoBtn = root.Q<UitButton>("OverwriteNo");

            if (_uitOverwriteYesBtn != null)
            {
                _uitOverwriteYesBtn.clicked += () =>
                {
                    if (_uitOverwritePrompt != null) _uitOverwritePrompt.style.display = DisplayStyle.None;
                    SaveService.ForSlot(_pendingOverwriteSlot).DeleteSave();
                    SaveService.ActiveSlot = _pendingOverwriteSlot;
                    CloseSaveSlotModal();
                    RequestNewGame();
                };
            }
            if (_uitOverwriteNoBtn != null)
            {
                _uitOverwriteNoBtn.clicked += () =>
                {
                    if (_uitOverwritePrompt != null) _uitOverwritePrompt.style.display = DisplayStyle.None;
                };
            }

            _uitSettingsModal = root.Q<VisualElement>("SettingsModal");
            _uitMasterFill = root.Q<VisualElement>("MasterFill");
            _uitMusicFill = root.Q<VisualElement>("MusicFill");
            _uitSfxFill = root.Q<VisualElement>("SfxFill");
            _uitMasterVal = root.Q<UitLabel>("MasterValueLabel");
            _uitMusicVal = root.Q<UitLabel>("MusicValueLabel");
            _uitSfxVal = root.Q<UitLabel>("SfxValueLabel");
            _uitFullscreenBtn = root.Q<UitButton>("FullscreenButton");
            _uitSettingsBackBtn = root.Q<UitButton>("SettingsBackButton");

            WireUitVolumeButtons(root, "Master", "master");
            WireUitVolumeButtons(root, "Music", "music");
            WireUitVolumeButtons(root, "Sfx", "sfx");
            if (_uitFullscreenBtn != null) _uitFullscreenBtn.clicked += ToggleFullscreen;
            if (_uitSettingsBackBtn != null) _uitSettingsBackBtn.clicked += CloseSettingsModal;

            SetActiveMenuIndex(0);
            RefreshSaveState();
            RefreshSettingsLabels();
            root.Query<UitButton>().ForEach(b => b.clicked += PlayUiClick);
        }

        static void PlayUiClick() => AudioCoordinator.PlayBankSfx(b => b.sfxUiClick, 0.7f);

        UitButton BindUitMenuRow(string rowName, string btnName, int index, Action onClick)
        {
            var row = _uitRoot.Q<VisualElement>(rowName);
            var btn = _uitRoot.Q<UitButton>(btnName);
            _menuEntries.Add((row, btn, onClick));
            if (btn != null)
            {
                btn.clicked += onClick;
                btn.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    if (btn.enabledSelf) SetActiveMenuIndex(index);
                });
                btn.RegisterCallback<FocusInEvent>(_ =>
                {
                    if (btn.enabledSelf) SetActiveMenuIndex(index);
                });
            }
            return btn;
        }

        void WireUitVolumeButtons(VisualElement root, string prefix, string channel)
        {
            var minus = root.Q<UitButton>($"{prefix}MinusButton");
            var plus = root.Q<UitButton>($"{prefix}PlusButton");
            if (minus != null) minus.clicked += () => AdjustVolume(channel, -0.1f);
            if (plus != null) plus.clicked += () => AdjustVolume(channel, 0.1f);
        }

        public void SetSaveExists(bool exists)
        {
            saveExists = exists;
            if (continueButton != null)
                continueButton.interactable = exists;
            if (loadGameButton != null)
                loadGameButton.interactable = exists;
            if (startGameButton != null)
            {
                startGameButton.interactable = exists;
                startGameButton.gameObject.SetActive(exists);
            }
            if (_uitStartBtn != null)
                _uitStartBtn.SetEnabled(exists);
            if (_menuEntries.Count > 0 && _menuEntries[0].row != null)
                _menuEntries[0].row.style.display = exists ? DisplayStyle.Flex : DisplayStyle.None;
            if (_uitLoadBtn != null)
                _uitLoadBtn.SetEnabled(exists);
            if (_menuEntries.Count > 0 && !IsMenuEntrySelectable(_menuEntries[_selectedMenuIndex]))
                MoveKeyboardMenuSelection(1);
            RefreshSaveSlotCardPreview();
        }

        void RefreshSaveState()
        {
            bool any = SaveService.ForSlot(1).HasSave
                       || SaveService.ForSlot(2).HasSave
                       || SaveService.ForSlot(3).HasSave;
            SetSaveExists(any);
        }

        void BindUguiButtons()
        {
            if (startGameButton != null)
            {
                startGameButton.onClick.AddListener(OnStartGameClicked);
                RegisterHoverMove(startGameButton);
            }
            if (loadGameButton != null)
            {
                loadGameButton.onClick.AddListener(() => OpenSaveSlotModal(isNewGame: false));
                RegisterHoverMove(loadGameButton);
            }
            if (newGameButton != null)
            {
                newGameButton.onClick.AddListener(OnNewGameClicked);
                RegisterHoverMove(newGameButton);
            }
            if (continueButton != null && continueButton != loadGameButton && continueButton != startGameButton)
            {
                continueButton.onClick.AddListener(RequestContinue);
                RegisterHoverMove(continueButton);
            }
            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(OpenSettingsModal);
                RegisterHoverMove(settingsButton);
            }
            if (quitButton != null)
            {
                quitButton.onClick.AddListener(RequestQuit);
                RegisterHoverMove(quitButton);
            }

            if (slot1Button != null) slot1Button.onClick.AddListener(() => OnSlotClicked(1));
            if (slot2Button != null) slot2Button.onClick.AddListener(() => OnSlotClicked(2));
            if (slot3Button != null) slot3Button.onClick.AddListener(() => OnSlotClicked(3));
            if (saveModalBackButton != null) saveModalBackButton.onClick.AddListener(CloseSaveSlotModal);

            if (overwriteYesButton != null)
            {
                overwriteYesButton.onClick.AddListener(() =>
                {
                    if (overwritePrompt != null) overwritePrompt.SetActive(false);
                    SaveService.ForSlot(_pendingOverwriteSlot).DeleteSave();
                    SaveService.ActiveSlot = _pendingOverwriteSlot;
                    CloseSaveSlotModal();
                    RequestNewGame();
                });
            }
            if (overwriteNoButton != null)
            {
                overwriteNoButton.onClick.AddListener(() =>
                {
                    if (overwritePrompt != null) overwritePrompt.SetActive(false);
                });
            }

            if (masterDownButton != null) masterDownButton.onClick.AddListener(() => AdjustVolume("master", -0.1f));
            if (masterUpButton != null) masterUpButton.onClick.AddListener(() => AdjustVolume("master", 0.1f));
            if (musicDownButton != null) musicDownButton.onClick.AddListener(() => AdjustVolume("music", -0.1f));
            if (musicUpButton != null) musicUpButton.onClick.AddListener(() => AdjustVolume("music", 0.1f));
            if (sfxDownButton != null) sfxDownButton.onClick.AddListener(() => AdjustVolume("sfx", -0.1f));
            if (sfxUpButton != null) sfxUpButton.onClick.AddListener(() => AdjustVolume("sfx", 0.1f));
            if (fullscreenButton != null) fullscreenButton.onClick.AddListener(ToggleFullscreen);
            if (settingsBackButton != null) settingsBackButton.onClick.AddListener(CloseSettingsModal);
        }

        public void OnStartGameClicked()
        {
            RefreshSaveState();
            if (!saveExists) return;
            if (!SaveService.Persistent.HasSave)
            {
                for (int s = 1; s <= 3; s++)
                    if (SaveService.ForSlot(s).HasSave) { SaveService.ActiveSlot = s; break; }
            }
            RequestContinue();
        }

        public void OnNewGameClicked()
        {
            RefreshSaveState();
            if (saveExists && (_uitSaveModal != null || saveSlotModal != null))
                OpenSaveSlotModal(isNewGame: true);
            else
                RequestNewGame();
        }

        public void OpenSaveSlotModal(bool isNewGame)
        {
            _saveModalIsNewGameMode = isNewGame;
            RefreshSaveState();
            string header = isNewGame ? "Create New Game Save" : "Load Existing Game Save";

            if (_uitSaveHeader != null) _uitSaveHeader.text = header;
            if (_uitSaveStatus != null) _uitSaveStatus.text = "";
            if (_uitOverwritePrompt != null) _uitOverwritePrompt.style.display = DisplayStyle.None;
            if (_uitSettingsModal != null) _uitSettingsModal.style.display = DisplayStyle.None;
            if (_uitSaveModal != null) _uitSaveModal.style.display = DisplayStyle.Flex;

            if (saveSlotHeader != null) saveSlotHeader.text = header;
            if (saveStatusLabel != null) saveStatusLabel.text = "";
            if (overwritePrompt != null) overwritePrompt.SetActive(false);
            if (settingsModal != null) settingsModal.SetActive(false);
            if (saveSlotModal != null) saveSlotModal.SetActive(true);
        }

        public void CloseSaveSlotModal()
        {
            if (_uitOverwritePrompt != null) _uitOverwritePrompt.style.display = DisplayStyle.None;
            if (_uitSaveModal != null) _uitSaveModal.style.display = DisplayStyle.None;
            if (overwritePrompt != null) overwritePrompt.SetActive(false);
            if (saveSlotModal != null) saveSlotModal.SetActive(false);
        }

        public void OnSlot1Clicked() => OnSlotClicked(1);

        public void OnSlotClicked(int slotNumber)
        {
            int slot = Mathf.Clamp(slotNumber, 1, 3);
            var service = SaveService.ForSlot(slot);
            bool slotHasSave = service.HasSave;
            RefreshSaveState();

            if (_saveModalIsNewGameMode)
            {
                if (slotHasSave && (_uitOverwritePrompt != null || overwritePrompt != null))
                {
                    _pendingOverwriteSlot = slot;
                    if (_uitOverwritePrompt != null) _uitOverwritePrompt.style.display = DisplayStyle.Flex;
                    if (overwritePrompt != null) overwritePrompt.SetActive(true);
                }
                else
                {
                    SaveService.ActiveSlot = slot;
                    CloseSaveSlotModal();
                    RequestNewGame();
                }
            }
            else
            {
                if (slotHasSave)
                {
                    SaveService.ActiveSlot = slot;
                    CloseSaveSlotModal();
                    RequestContinue();
                }
                else
                {
                    string msg = $"No save file found in Slot {slot}.";
                    if (_uitSaveStatus != null) _uitSaveStatus.text = msg;
                    if (saveStatusLabel != null) saveStatusLabel.text = msg;
                }
            }
        }

        void RefreshSaveSlotCardPreview()
        {
            RefreshUitSlotCard(1, _uitSlot1Btn, _uitSlot1Title, _uitSlot1Portrait, _uitSlot1Subtitle);
            RefreshUitSlotCard(2, _uitSlot2Btn, _uitSlot2Title, _uitSlot2Portrait, _uitSlot2Subtitle);
            RefreshUitSlotCard(3, _uitSlot3Btn, _uitSlot3Title, _uitSlot3Portrait, _uitSlot3Subtitle);

            // Legacy uGUI (Slot 1)
            var slot1 = SaveService.ForSlot(1);
            bool has1 = slot1.HasSave;
            string title1 = has1 ? "Save 1" : "Empty Slot";
            string sub1 = ReadSlotSubtitle(slot1, has1, 1);
            if (slot1CardImage != null)
                slot1CardImage.color = has1 ? new Color(0.76f, 0.64f, 0.24f, 1f) : new Color(0.08f, 0.08f, 0.09f, 0.96f);
            if (slot1PortraitRoot != null)
                slot1PortraitRoot.SetActive(has1);
            if (slot1TitleLabel != null)
                slot1TitleLabel.text = title1;
            if (slot1SubtitleLabel != null)
                slot1SubtitleLabel.text = sub1;
        }

        static void RefreshUitSlotCard(int slot, UitButton btn, UitLabel titleLabel, VisualElement portrait, UitLabel subtitleLabel)
        {
            var service = SaveService.ForSlot(slot);
            bool hasSave = service.HasSave;
            string title = hasSave ? $"Save {slot}" : "Empty Slot";
            string sub = ReadSlotSubtitle(service, hasSave, slot);

            if (btn != null)
            {
                if (hasSave) btn.AddToClassList("tarot-card--populated");
                else btn.RemoveFromClassList("tarot-card--populated");
            }
            if (portrait != null)
                portrait.style.display = hasSave ? DisplayStyle.Flex : DisplayStyle.None;
            if (titleLabel != null) titleLabel.text = title;
            if (subtitleLabel != null)
            {
                subtitleLabel.text = sub;
                subtitleLabel.style.opacity = hasSave ? 1f : 0.45f;
            }
        }

        static string ReadSlotSubtitle(SaveService service, bool hasSave, int slot)
        {
            if (!hasSave) return $"Slot {slot}";
            if (service.TryLoad(out SaveState saved, out _) && saved != null)
            {
                string name = string.IsNullOrWhiteSpace(saved.playerName) ? "Detective" : saved.playerName.Trim();
                string caseName = saved.currentCase <= 0 ? "Prologue" : $"Case {saved.currentCase}";
                return $"{caseName}\n{name}";
            }
            return "Saved Run";
        }

        public void OpenSettingsModal()
        {
            if (_uitSaveModal != null) _uitSaveModal.style.display = DisplayStyle.None;
            if (_uitSettingsModal != null) _uitSettingsModal.style.display = DisplayStyle.Flex;
            if (saveSlotModal != null) saveSlotModal.SetActive(false);
            if (settingsModal != null) settingsModal.SetActive(true);
            RefreshSettingsLabels();
        }

        public void CloseSettingsModal()
        {
            if (_uitSettingsModal != null) _uitSettingsModal.style.display = DisplayStyle.None;
            if (settingsModal != null) settingsModal.SetActive(false);
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
            RefreshSettingsLabels();
        }

        void ToggleFullscreen()
        {
            bool next = PauseMenuController.ToggleDisplayFullscreen();
            RefreshSettingsLabels(next);
        }

        void RefreshSettingsLabels() => RefreshSettingsLabels(Screen.fullScreen);

        void RefreshSettingsLabels(bool fullscreenState)
        {
            _lastObservedFullscreen = Screen.fullScreen;
            var audio = CoreRuntime.Ensure().Audio;
            int masterPct = Mathf.RoundToInt(audio.MasterVolume * 100f);
            int musicPct = Mathf.RoundToInt(audio.MusicVolume * 100f);
            int sfxPct = Mathf.RoundToInt(audio.SfxVolume * 100f);
            string fsText = fullscreenState ? "Fullscreen: ON" : "Fullscreen: OFF";

            if (_uitMasterFill != null) _uitMasterFill.style.width = Length.Percent(masterPct);
            if (_uitMusicFill != null) _uitMusicFill.style.width = Length.Percent(musicPct);
            if (_uitSfxFill != null) _uitSfxFill.style.width = Length.Percent(sfxPct);
            if (_uitMasterVal != null) _uitMasterVal.text = $"{masterPct}%";
            if (_uitMusicVal != null) _uitMusicVal.text = $"{musicPct}%";
            if (_uitSfxVal != null) _uitSfxVal.text = $"{sfxPct}%";
            if (_uitFullscreenBtn != null) _uitFullscreenBtn.text = fsText;

            if (masterValueLabel != null) masterValueLabel.text = $"{masterPct}%";
            if (musicValueLabel != null) musicValueLabel.text = $"{musicPct}%";
            if (sfxValueLabel != null) sfxValueLabel.text = $"{sfxPct}%";
            if (fullscreenButton != null)
            {
                var lbl = fullscreenButton.GetComponentInChildren<TMP_Text>();
                if (lbl != null) lbl.text = fsText;
            }
        }

        void RegisterHoverMove(UguiButton btn)
        {
            if (btn == null) return;
            var trigger = btn.gameObject.GetComponent<EventTrigger>() ?? btn.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            var targetRect = btn.GetComponent<RectTransform>();
            entry.callback.AddListener(_ =>
            {
                if (btn.interactable) MoveSelectionIndicator(targetRect);
            });
            trigger.triggers.Add(entry);
        }

        void RefreshUguiMenuIndicators()
        {
            if (selectionDiamond != null) selectionDiamond.gameObject.SetActive(false);
            if (selectionUnderline != null) selectionUnderline.gameObject.SetActive(false);
        }

        void MoveSelectionIndicator(RectTransform target)
        {
            if (target == null) return;
            var tmp = target.GetComponentInChildren<TMP_Text>();
            float textWidth = tmp != null ? tmp.preferredWidth : 180f;
            float leftEdge = target.position.x - target.rect.width * target.pivot.x * target.lossyScale.x;
            float bottomEdge = target.position.y - target.rect.height * target.pivot.y * target.lossyScale.y;
            float scaleX = target.lossyScale.x;

            if (selectionDiamond != null)
            {
                selectionDiamond.gameObject.SetActive(true);
                selectionDiamond.position = new Vector3(
                    leftEdge + (textWidth + 22f) * scaleX,
                    target.position.y,
                    target.position.z);
            }
            if (selectionUnderline != null)
            {
                selectionUnderline.gameObject.SetActive(true);
                selectionUnderline.sizeDelta = new Vector2(Mathf.Max(140f, textWidth + 32f), 2f);
                selectionUnderline.position = new Vector3(
                    leftEdge + (textWidth + 16f) * 0.5f * scaleX,
                    bottomEdge + 2f * scaleX,
                    target.position.z);
            }
        }

        bool _startingNewGameCutscene;

        public void RequestNewGame()
        {
            if (_startingNewGameCutscene) return;
            Debug.Log("TitleMenuController: New Game requested.", this);
            var player = Application.isPlaying
                ? (openingCutscenePlayer != null ? openingCutscenePlayer : FindAnyObjectByType<CutscenePlayerController>())
                : null;
            if (player != null)
            {
                _startingNewGameCutscene = true;
                CutsceneData tempData = null;
                Sprite tempBackdrop = null;
                void OnDone()
                {
                    player.CutsceneEnded -= OnDone;
                    if (tempData != null) Destroy(tempData);
                    if (tempBackdrop != null) Destroy(tempBackdrop);
                    _startingNewGameCutscene = false;
                    NewGameRequested?.Invoke();
                    if (CoreRuntime.Instance != null)
                        CoreRuntime.Instance.Bus.Publish(new NewGameEvent());
                }
                player.CutsceneEnded += OnDone;
                if (_uitRoot != null) _uitRoot.style.display = DisplayStyle.None;
                CutsceneData toPlay = openingCutscene != null
                    ? openingCutscene
                    : Resources.Load<CutsceneData>("CS_Opening");
                if (toPlay == null)
                {
                    tempData = BuildStandstillOpeningCutscene(out tempBackdrop);
                    toPlay = tempData;
                }
                player.Play(toPlay);
                return;
            }
            NewGameRequested?.Invoke();
            if (CoreRuntime.Instance != null)
                CoreRuntime.Instance.Bus.Publish(new NewGameEvent());
        }

        static CutsceneData BuildStandstillOpeningCutscene(out Sprite backdrop)
        {
            backdrop = Sprite.Create(Texture2D.blackTexture,
                new Rect(0f, 0f, Texture2D.blackTexture.width, Texture2D.blackTexture.height),
                new Vector2(0.5f, 0.5f));
            backdrop.hideFlags = HideFlags.DontSave;

            var data = ScriptableObject.CreateInstance<CutsceneData>();
            data.hideFlags = HideFlags.DontSave;
            data.id = "CS_Opening_Standstill";
            data.skipToScene = false;
            data.shots = new[]
            {
                new CutsceneShotData
                {
                    image = backdrop,
                    waitForInput = true,
                    captionSpeaker = "",
                    caption = "In the lightless expanse of the Mana Abyss, a lone consciousness drifts across an endless sea of silence\u2014stripped of name, stripped of flesh, stripped of time."
                },
                new CutsceneShotData
                {
                    image = backdrop,
                    waitForInput = true,
                    captionSpeaker = "",
                    caption = "From the dark, scattered incandescent fragments\u2014shards of instinct, logic, and an unyielding vow\u2014draw together around the drifting core."
                },
                new CutsceneShotData
                {
                    image = backdrop,
                    waitForInput = true,
                    captionSpeaker = "???",
                    caption = "(A distant, unplaced warmth echoes through the void\u2014a phantom hand bracing against yours before the dark closes in.)"
                },
                new CutsceneShotData
                {
                    image = backdrop,
                    waitForInput = true,
                    captionSpeaker = "",
                    caption = "Pulled by an unseen gravity, the converged soul plummets from the astral tide into cold, ancient stone."
                },
                new CutsceneShotData
                {
                    image = backdrop,
                    waitForInput = true,
                    captionSpeaker = "System",
                    caption = ">> CORE RE-INITIALIZATION: OK.\n" +
                              ">> PRIMARY DIRECTIVE: UNKNOWN.\n" +
                              ">> WARNING: MEMORY SECTORS 01 THROUGH 99 CORRUPTED OR MISSING.\n" +
                              ">> BIOLOGICAL SIGNATURE: NULL. CHASSIS INTEGRITY: 84%.\n" +
                              ">> OPTICAL SENSORS: ONLINE."
                }
            };
            return data;
        }

        public void RequestContinue()
        {
            Debug.Log("TitleMenuController: Continue requested.", this);
            ContinueRequested?.Invoke();
            if (CoreRuntime.Instance != null)
                CoreRuntime.Instance.Bus.Publish(new ContinueGameEvent());
        }

        public void RequestQuit()
        {
            Debug.Log("TitleMenuController: Quit requested.", this);
            QuitRequested?.Invoke();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Morsoul.Core
{
    /// <summary>
    /// Top-most contextual tutorial overlay (sortingOrder 140 — renders above HUD,
    /// Journal, Deduction Board, Inspect, and Pause Menu). Driven by TutorialCatalog.
    /// </summary>
    [DefaultExecutionOrder(250)]
    public class TutorialOverlayController : MonoBehaviour
    {
        const string ConfigResourcePath = "PauseMenuConfig";
        const string TitleSceneName = "Title";

        static TutorialOverlayController _instance;
        public static TutorialOverlayController Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInstall()
        {
            TryInstallForScene(SceneManager.GetActiveScene().name);
            SceneManager.sceneLoaded -= OnSceneLoadedStatic;
            SceneManager.sceneLoaded += OnSceneLoadedStatic;
        }

        static void OnSceneLoadedStatic(Scene scene, LoadSceneMode _) => TryInstallForScene(scene.name);

        static void TryInstallForScene(string sceneName)
        {
            if (!Application.isPlaying) return;
            if (string.Equals(sceneName, TitleSceneName, StringComparison.OrdinalIgnoreCase))
            {
                if (_instance != null) _instance.ClearAll();
                return;
            }
            EnsureRuntime();
        }

        public static TutorialOverlayController EnsureRuntime()
        {
            if (_instance != null) return _instance;

            var existing = FindAnyObjectByType<TutorialOverlayController>(FindObjectsInactive.Include);
            if (existing != null)
            {
                _instance = existing;
                return _instance;
            }

            var core = CoreRuntime.Ensure();
            var child = new GameObject("TutorialOverlay");
            child.transform.SetParent(core.transform, false);

            var config = Resources.Load<PauseMenuAssets>(ConfigResourcePath);
            var pr = child.AddComponent<PanelRenderer>();
            pr.sortingOrder = 140;
            if (config != null)
            {
                if (config.panelSettings != null) pr.panelSettings = config.panelSettings;
                if (config.tutorialOverlayUxml != null) pr.visualTreeAsset = config.tutorialOverlayUxml;
            }

            _instance = child.AddComponent<TutorialOverlayController>();
            return _instance;
        }

        public static bool TryShow(string tutorialId, bool modal = false, float autoDismissSeconds = 18f)
        {
            if (!Application.isPlaying || TutorialCatalog.AutoTutorialsSuppressed) return false;
            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            if (TutorialCatalog.IsSeen(session, tutorialId)) return false;

            var ctrl = EnsureRuntime();
            return ctrl != null && ctrl.Enqueue(tutorialId, modal, markSeen: true, autoDismissSeconds: autoDismissSeconds);
        }

        public static bool ForceShow(string tutorialId, bool modal = false)
        {
            var ctrl = EnsureRuntime();
            return ctrl != null && ctrl.Enqueue(tutorialId, modal, markSeen: true, autoDismissSeconds: 0f);
        }

        PanelRenderer _panelRenderer;
        VisualElement _root;
        VisualElement _backdrop;
        Label _categoryLabel;
        Label _badgeLabel;
        Label _titleLabel;
        Label _keysLabel;
        Label _summaryLabel;
        VisualElement _stepsList;
        Button _muteBtn;
        Button _dismissBtn;

        readonly Queue<(TutorialEntry entry, bool modal, float autoDismiss)> _queue =
            new Queue<(TutorialEntry, bool, float)>();

        TutorialEntry _current;
        bool _isOpen;
        bool _isModal;
        float _autoTimer;
        int _uiVersion;
        float _prevTimeScale = 1f;
        CursorLockMode _prevCursorLock = CursorLockMode.None;
        bool _prevCursorVisible = true;
        Func<bool> _modalBlocker;

        public bool IsOpen => _isOpen;
        public bool IsModalOpen => _isOpen && _isModal;
        public string CurrentTutorialId => _current != null ? _current.Id : null;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            _modalBlocker = () => _isOpen && _isModal;
            PauseMenuController.RegisterExternalModal(_modalBlocker);
        }

        void OnDestroy()
        {
            RestoreModalState();
            if (_modalBlocker != null)
            {
                PauseMenuController.UnregisterExternalModal(_modalBlocker);
                _modalBlocker = null;
            }
            if (_instance == this) _instance = null;
        }

        void OnEnable()
        {
            _panelRenderer = GetComponent<PanelRenderer>();
            if (_panelRenderer != null)
            {
                _panelRenderer.sortingOrder = 140;
                _panelRenderer.RegisterUIReloadCallback(OnUIReload);
            }
        }

        void OnDisable()
        {
            if (_panelRenderer != null)
                _panelRenderer.UnregisterUIReloadCallback(OnUIReload);
            ClearAll();
        }

        public bool Enqueue(string tutorialId, bool modal, bool markSeen = true, float autoDismissSeconds = 18f)
        {
            var entry = TutorialCatalog.Find(tutorialId);
            if (entry == null) return false;

            if (_current != null && _current.Id == entry.Id) return false;
            foreach (var item in _queue)
                if (item.entry.Id == entry.Id) return false;

            if (markSeen)
            {
                var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
                TutorialCatalog.MarkSeen(session, entry.Id);
            }

            _queue.Enqueue((entry, modal, autoDismissSeconds));
            if (!_isOpen) ShowNext();
            return true;
        }

        public void Dismiss()
        {
            if (!_isOpen) return;
            if (_isModal)
            {
                RestoreModalState();
                PauseMenuController.NotifyExternalModalClosed();
            }
            _isOpen = false;
            _isModal = false;
            _current = null;
            if (_root != null) _root.style.display = DisplayStyle.None;
            ShowNext();
        }

        public void ClearAll()
        {
            _queue.Clear();
            if (_isOpen)
            {
                if (_isModal) RestoreModalState();
                _isOpen = false;
                _isModal = false;
                _current = null;
                if (_root != null) _root.style.display = DisplayStyle.None;
            }
        }

        void ShowNext()
        {
            if (_queue.Count == 0) return;
            var next = _queue.Dequeue();
            _current = next.entry;
            _isModal = next.modal;
            _autoTimer = _isModal ? 0f : Mathf.Max(0f, next.autoDismiss);
            _isOpen = true;

            if (_isModal)
            {
                _prevTimeScale = Time.timeScale;
                _prevCursorLock = UnityEngine.Cursor.lockState;
                _prevCursorVisible = UnityEngine.Cursor.visible;
                Time.timeScale = 0f;
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }

            ApplyUi();
        }

        void RestoreModalState()
        {
            if (!_isModal) return;
            Time.timeScale = _prevTimeScale;
            UnityEngine.Cursor.lockState = _prevCursorLock;
            UnityEngine.Cursor.visible = _prevCursorVisible;
            _isModal = false;
        }

        void Update()
        {
            if (!_isOpen) return;

            if (!_isModal && _autoTimer > 0f)
            {
                if (!PauseMenuController.IsPaused)
                    _autoTimer -= Time.unscaledDeltaTime;
                if (_autoTimer <= 0f)
                {
                    Dismiss();
                    return;
                }
            }

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.tKey.wasPressedThisFrame)
            {
                Dismiss();
                return;
            }

            if (_isModal && (kb.eKey.wasPressedThisFrame ||
                             kb.spaceKey.wasPressedThisFrame ||
                             kb.enterKey.wasPressedThisFrame ||
                             kb.escapeKey.wasPressedThisFrame))
            {
                Dismiss();
            }
        }

        void MuteAutoTutorials()
        {
            TutorialCatalog.AutoTutorialsSuppressed = true;
            _queue.Clear();
            Dismiss();
        }

        void ApplyUi()
        {
            if (_root == null || _current == null) return;

            _root.EnableInClassList("tut-root--modal", _isModal);
            _root.pickingMode = _isModal ? PickingMode.Position : PickingMode.Ignore;
            if (_backdrop != null)
                _backdrop.style.display = _isModal ? DisplayStyle.Flex : DisplayStyle.None;

            if (_categoryLabel != null)
                _categoryLabel.text = TutorialCatalog.CategoryDisplayName(_current.Category).ToUpperInvariant();
            if (_badgeLabel != null)
                _badgeLabel.text = _isModal ? "ESSENTIAL" : "TIP";
            if (_titleLabel != null) _titleLabel.text = _current.Title;
            if (_keysLabel != null) _keysLabel.text = _current.Keybinds;
            if (_summaryLabel != null) _summaryLabel.text = _current.Summary;

            if (_stepsList != null)
            {
                _stepsList.Clear();
                if (_current.Steps != null)
                {
                    foreach (var step in _current.Steps)
                    {
                        var lbl = new Label("•  " + step) { pickingMode = PickingMode.Ignore };
                        lbl.AddToClassList("tut-step");
                        _stepsList.Add(lbl);
                    }
                }
            }

            if (_dismissBtn != null)
                _dismissBtn.text = _isModal ? "Continue  [E / Space]" : "Got it  [T]";
            if (_muteBtn != null)
                _muteBtn.style.display = TutorialCatalog.AutoTutorialsSuppressed ? DisplayStyle.None : DisplayStyle.Flex;

            _root.style.display = DisplayStyle.Flex;
        }

        void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            if (_uiVersion == version) return;
            _uiVersion = version;

            _root = root.Q<VisualElement>("TutorialOverlay-Root");
            _backdrop = root.Q<VisualElement>("TutorialBackdrop");
            _categoryLabel = root.Q<Label>("TutorialCategoryLabel");
            _badgeLabel = root.Q<Label>("TutorialBadgeLabel");
            _titleLabel = root.Q<Label>("TutorialTitleLabel");
            _keysLabel = root.Q<Label>("TutorialKeysLabel");
            _summaryLabel = root.Q<Label>("TutorialSummaryLabel");
            _stepsList = root.Q<VisualElement>("TutorialStepsList");
            _muteBtn = root.Q<Button>("TutorialMuteButton");
            _dismissBtn = root.Q<Button>("TutorialDismissButton");

            if (_dismissBtn != null) _dismissBtn.clicked += Dismiss;
            if (_muteBtn != null) _muteBtn.clicked += MuteAutoTutorials;

            if (_root != null)
                _root.style.display = _isOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (_isOpen) ApplyUi();
        }
    }
}


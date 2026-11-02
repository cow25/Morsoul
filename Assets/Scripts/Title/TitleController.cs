using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Morsoul.Title
{
    /// <summary>
    /// Title screen controller. Sits on the same GameObject as a PanelRenderer
    /// (same pattern as DialogueController / BoardController). The controller
    /// only renders the title and re-emits menu choices as events; scene
    /// loading and save handling are the Bootstrap's job.
    /// </summary>
    public class TitleController : MonoBehaviour
    {
        /// <summary>Controls the enabled look of the Continue button.</summary>
        public bool saveExists;
        public bool showOnStart = true;
        public bool manageCursor = true;

        /// <summary>Raised with the (possibly empty) detective name when New Game is clicked.</summary>
        public event Action<string> NewGameRequested;
        /// <summary>Raised when Continue is clicked with a save present.</summary>
        public event Action ContinueRequested;
        /// <summary>Raised before the application quits.</summary>
        public event Action QuitRequested;

        const string ButtonDisabledClass = "title-button--disabled";

        PanelRenderer _panelRenderer;

        // Cached elements (re-queried on every UI reload).
        VisualElement _root;
        TextField _nameField;
        Button _newGameButton;
        Button _continueButton;
        Button _quitButton;

        bool _isVisible;
        int _uiVersion;

        public bool IsVisible => _isVisible;

        void OnEnable()
        {
            _panelRenderer = GetComponent<PanelRenderer>();
            if (_panelRenderer != null) _panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }

        void OnDisable()
        {
            if (_panelRenderer != null) _panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        }

        void Start()
        {
            if (showOnStart) Show();
        }

        public void Show()
        {
            _isVisible = true;
            if (_root != null) _root.style.display = DisplayStyle.Flex;
            if (manageCursor)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
        }

        public void Hide()
        {
            _isVisible = false;
            if (_root != null) _root.style.display = DisplayStyle.None;
            if (manageCursor)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
            }
        }

        /// <summary>Updates the save flag and the Continue button's enabled look.</summary>
        public void SetSaveExists(bool exists)
        {
            saveExists = exists;
            ApplyContinueState();
        }

        void OnNewGameClicked()
        {
            string detectiveName = _nameField != null && _nameField.value != null
                ? _nameField.value.Trim()
                : "";
            NewGameRequested?.Invoke(detectiveName);
        }

        void OnContinueClicked()
        {
            if (!saveExists) return;
            ContinueRequested?.Invoke();
        }

        void OnQuitClicked()
        {
            QuitRequested?.Invoke();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void ApplyContinueState()
        {
            if (_continueButton == null) return;
            _continueButton.EnableInClassList(ButtonDisabledClass, !saveExists);
            _continueButton.SetEnabled(saveExists);
        }

        void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            if (_uiVersion == version) return;
            _uiVersion = version;

            // The UXML clone can arrive after this callback (empty container at
            // version 1, no further reload event). Poll until Title-Root exists.
            TryBind(root);
            if (_root == null)
            {
                root.schedule.Execute(() => TryBind(root))
                    .Every(50).Until(() => _root != null);
            }
        }

        void TryBind(VisualElement root)
        {
            if (_root != null || root == null) return;
            // root may BE the document root; Q() only searches descendants.
            _root = root.name == "Title-Root" ? root : root.Q<VisualElement>("Title-Root");
            if (_root == null) return;
            _nameField = root.Q<TextField>("NameField");

            _newGameButton = root.Q<Button>("NewGameButton");
            if (_newGameButton != null)
                _newGameButton.RegisterCallback<ClickEvent>(_ => OnNewGameClicked());
            _continueButton = root.Q<Button>("ContinueButton");
            if (_continueButton != null)
                _continueButton.RegisterCallback<ClickEvent>(_ => OnContinueClicked());
            _quitButton = root.Q<Button>("QuitButton");
            if (_quitButton != null)
                _quitButton.RegisterCallback<ClickEvent>(_ => OnQuitClicked());

            ApplyContinueState();
            if (_root != null)
                _root.style.display = _isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}

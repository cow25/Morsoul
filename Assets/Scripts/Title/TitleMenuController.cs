using System;
using Morsoul.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Morsoul.Title
{
    /// <summary>
    /// uGUI title menu: name entry plus New Game / Continue / Quit buttons.
    /// Lives on the Canvas in the dedicated Title scene; events carry the
    /// decisions so a flow controller can load scenes once Bootstrap exists.
    /// </summary>
    public class TitleMenuController : MonoBehaviour
    {
        public TMP_InputField nameField;
        public Button newGameButton;
        public Button continueButton;
        public Button quitButton;

        /// <summary>Whether a save exists; gates the Continue button.</summary>
        public bool saveExists;

        /// <summary>Raised with the (possibly empty) detective name when New Game is clicked.</summary>
        public event Action<string> NewGameRequested;
        /// <summary>Raised when Continue is clicked with a save present.</summary>
        public event Action ContinueRequested;
        /// <summary>Raised when Quit is clicked.</summary>
        public event Action QuitRequested;

        void Awake()
        {
            if (newGameButton != null)
                newGameButton.onClick.AddListener(OnNewGameClicked);
            if (continueButton != null)
                continueButton.onClick.AddListener(OnContinueClicked);
            if (quitButton != null)
                quitButton.onClick.AddListener(OnQuitClicked);
        }

        void Start()
        {
            // Inspector flag stays as a debug override; a real checkpoint on
            // disk also enables Continue.
            SetSaveExists(saveExists || SaveService.Persistent.HasSave);
        }

        public void SetSaveExists(bool exists)
        {
            saveExists = exists;
            if (continueButton != null) continueButton.interactable = exists;
        }

        void OnNewGameClicked()
        {
            string playerName = nameField != null ? nameField.text.Trim() : "";
            Debug.Log($"TitleMenuController: New Game requested (name='{playerName}').", this);
            NewGameRequested?.Invoke(playerName);
            CoreRuntime.Ensure().Bus.Publish(new NewGameEvent { PlayerName = playerName });
        }

        void OnContinueClicked()
        {
            Debug.Log("TitleMenuController: Continue requested.", this);
            ContinueRequested?.Invoke();
            CoreRuntime.Ensure().Bus.Publish(new ContinueGameEvent());
        }

        void OnQuitClicked()
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

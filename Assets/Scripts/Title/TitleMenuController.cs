using System;
using Morsoul.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Morsoul.Title
{
    /// <summary>
    /// uGUI title menu: New Game / Continue / Quit buttons. Lives on the Canvas
    /// in the dedicated Title scene; events carry the decisions so a flow
    /// controller can load scenes once Bootstrap exists. The detective's name
    /// is collected later, inside the first-contact dialogue with Eliza — the
    /// title screen intentionally has no name field.
    /// </summary>
    public class TitleMenuController : MonoBehaviour
    {
        public Button newGameButton;
        public Button continueButton;
        public Button quitButton;

        /// <summary>Whether a save exists; gates the Continue button.</summary>
        public bool saveExists;

        /// <summary>Raised when New Game is clicked (name is collected in-game, so always empty).</summary>
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
            Debug.Log("TitleMenuController: New Game requested.", this);
            NewGameRequested?.Invoke("");
            CoreRuntime.Ensure().Bus.Publish(new NewGameEvent { PlayerName = "" });
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

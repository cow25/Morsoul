using System;
using System.Collections;
using System.Collections.Generic;
using Morsoul.Data;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Morsoul.Dialogue
{
    /// <summary>
    /// UI overlay controller for the bottom dialogue box. Sits on the same
    /// GameObject as a PanelRenderer (same pattern as BoardController /
    /// InterrogationController). All traversal decisions are made by
    /// <see cref="DialogueRunner"/>; this class only renders nodes, forwards
    /// input and re-emits events for external systems.
    /// </summary>
    public class DialogueController : MonoBehaviour
    {
        public DialogueData dialogueData;
        public string playerName = "Detective";
        public bool manageCursor = true;
        /// <summary>Calls Play() in Start; a testing convenience like the interrogation's openOnStart.</summary>
        public bool playOnStart;

        /// <summary>Raised once when the runner reaches an end (panel closes).</summary>
        public event Action DialogueEnded;
        /// <summary>Re-emitted when a shown node declares registersClue.</summary>
        public event Action<ClueData> ClueRegistered;
        /// <summary>Re-emitted when a shown node declares setsFlag.</summary>
        public event Action<string> FlagSet;
        /// <summary>Raised when the player confirms a name in the name prompt.</summary>
        public event Action<string> NameSubmitted;

        PanelRenderer _panelRenderer;
        DialogueRunner _runner;
        ISet<string> _flags = new HashSet<string>(StringComparer.Ordinal);

        // Cached elements (re-queried on every UI reload).
        VisualElement _root;
        VisualElement _portrait;
        VisualElement _optionsContainer;
        Label _speakerLabel;
        Label _dialogueText;
        Label _continueHint;
        VisualElement _nameInputContainer;
        TextField _nameInputField;
        Button _nameInputConfirm;
        VisualElement _fadeOverlay;
        Label _fadeText;
        readonly Button[] _optionButtons = new Button[3];

        // Interaction state.
        bool _isPlaying;
        int _uiVersion;
        float _fadeOpacity;

        public bool IsPlaying => _isPlaying;
        /// <summary>True while the name-entry prompt is visible.</summary>
        public bool NamePromptActive { get; private set; }
        /// <summary>True while the full-screen fade overlay is opaque.</summary>
        public bool FadeActive { get; private set; }
        /// <summary>Frame the last dialogue ended/stopped on; -1 = never. Lets
        /// interact systems ignore the same keypress that closed the panel.</summary>
        public int LastEndFrame { get; private set; } = -1;
        public DialogueRunner Runner => _runner;
        /// <summary>Flag set shared with the runner; replace via SetFlags.</summary>
        public ISet<string> Flags => _flags;

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
            if (playOnStart) Play();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (NamePromptActive)
            {
                if (keyboard.enterKey.wasPressedThisFrame
                    || keyboard.numpadEnterKey.wasPressedThisFrame)
                    SubmitName();
                return;
            }
            if (!_isPlaying || _runner == null) return;

            if (_runner.HasOptions)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) TryChoose(0);
                else if (keyboard.digit2Key.wasPressedThisFrame) TryChoose(1);
                else if (keyboard.digit3Key.wasPressedThisFrame) TryChoose(2);
            }
            else if (keyboard.eKey.wasPressedThisFrame)
            {
                _runner.Advance();
            }
        }

        /// <summary>Replaces the flag set used for gating by future Play() calls.</summary>
        public void SetFlags(ISet<string> flags)
        {
            _flags = flags ?? new HashSet<string>(StringComparer.Ordinal);
        }

        /// <summary>Replaces the dialogue without auto-playing; call Play() to start.</summary>
        public void SetDialogue(DialogueData data)
        {
            dialogueData = data;
        }

        /// <summary>Builds a runner for the assigned dialogue and shows the panel.</summary>
        public void Play()
        {
            Stop();
            if (dialogueData == null)
            {
                Debug.LogWarning("DialogueController: dialogueData is null; panel stays hidden.", this);
                return;
            }
            try
            {
                _runner = new DialogueRunner(dialogueData, _flags) { PlayerName = playerName };
            }
            catch (Exception e)
            {
                _runner = null;
                Debug.LogError($"DialogueController: failed to build DialogueRunner - {e.Message}", this);
                return;
            }
            _runner.NodeShown += OnNodeShown;
            _runner.ClueRegistered += OnRunnerClueRegistered;
            _runner.FlagSet += OnRunnerFlagSet;
            _runner.DialogueEnded += OnRunnerEnded;

            _isPlaying = true;
            if (_root != null) _root.style.display = DisplayStyle.Flex;
            if (manageCursor)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }

            // The start node is shown during runner construction, before events
            // can be observed; re-emit its side effects and render it.
            var start = _runner.Current;
            if (start != null)
            {
                if (start.registersClue != null) ClueRegistered?.Invoke(start.registersClue);
                if (!string.IsNullOrEmpty(start.setsFlag)) FlagSet?.Invoke(start.setsFlag);
                RenderNode(start);
            }
            if (_runner.IsEnded) OnRunnerEnded();
        }

        /// <summary>Stops playback and hides the panel without firing DialogueEnded.</summary>
        public void Stop()
        {
            _isPlaying = false;
            LastEndFrame = Time.frameCount;
            HideNamePrompt();
            StopRunner();
            if (_root != null) _root.style.display = FadeActive ? DisplayStyle.Flex : DisplayStyle.None;
            if (manageCursor)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
            }
        }

        /// <summary>
        /// Shows one scripted line without a DialogueRunner. Timed in-world
        /// sequences use this so the normal dialogue UI still blocks input but
        /// no player advance is required. Hide it with <see cref="Stop"/>.
        /// </summary>
        public void ShowStandaloneLine(string speakerName, string text)
        {
            StopRunner();
            _isPlaying = true;
            if (_root != null) _root.style.display = DisplayStyle.Flex;
            if (manageCursor)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
            RenderNode(new DialogueNodeData
            {
                speakerName = speakerName,
                text = text,
            });
        }

        void StopRunner()
        {
            if (_runner == null) return;
            _runner.NodeShown -= OnNodeShown;
            _runner.ClueRegistered -= OnRunnerClueRegistered;
            _runner.FlagSet -= OnRunnerFlagSet;
            _runner.DialogueEnded -= OnRunnerEnded;
            _runner = null;
        }

        // --- Name prompt ---------------------------------------------------------

        /// <summary>
        /// Shows the name-entry row inside the dialogue box (scripted sequences
        /// show a prompt line first via <see cref="ShowStandaloneLine"/>).
        /// Enter or the Confirm button submits; an empty entry falls back to
        /// "Detective". The submitted value also updates <see cref="playerName"/>
        /// so later {PlayerName} substitutions resolve. Returns false when the
        /// panel lacks the name-input elements.
        /// </summary>
        public bool ShowNamePrompt()
        {
            if (_nameInputContainer == null || _nameInputField == null) return false;
            NamePromptActive = true;
            _isPlaying = true;
            if (_root != null) _root.style.display = DisplayStyle.Flex;
            if (manageCursor)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
            _nameInputField.value = "";
            _nameInputContainer.style.display = DisplayStyle.Flex;
            if (_continueHint != null) _continueHint.style.display = DisplayStyle.None;
            _nameInputField.Focus();
            return true;
        }

        /// <summary>Hides the name-entry row without submitting.</summary>
        public void HideNamePrompt()
        {
            NamePromptActive = false;
            if (_nameInputContainer != null)
                _nameInputContainer.style.display = DisplayStyle.None;
        }

        void SubmitName()
        {
            string entered = _nameInputField != null ? (_nameInputField.value ?? "").Trim() : "";
            if (entered.Length == 0) entered = "Detective";
            playerName = entered;
            HideNamePrompt();
            NameSubmitted?.Invoke(entered);
        }

        // --- Screen fade -----------------------------------------------------------

        /// <summary>Sets the caption shown on top of the fade overlay.</summary>
        public void SetFadeText(string text)
        {
            if (_fadeText != null) _fadeText.text = text ?? "";
        }

        /// <summary>Lerps the full-screen fade overlay toward target opacity (1 = black).
        /// The overlay lives inside the dialogue root, so fading in forces it visible.</summary>
        public IEnumerator FadeTo(float targetOpacity, float seconds)
        {
            if (_fadeOverlay == null) yield break;
            targetOpacity = Mathf.Clamp01(targetOpacity);
            if (targetOpacity > 0f)
            {
                if (_root != null) _root.style.display = DisplayStyle.Flex;
                _fadeOverlay.style.display = DisplayStyle.Flex;
            }
            float start = _fadeOpacity;
            float elapsed = 0f;
            seconds = Mathf.Max(0.01f, seconds);
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                _fadeOpacity = Mathf.Lerp(start, targetOpacity, Mathf.Clamp01(elapsed / seconds));
                _fadeOverlay.style.opacity = _fadeOpacity;
                yield return null;
            }
            _fadeOpacity = targetOpacity;
            _fadeOverlay.style.opacity = targetOpacity;
            if (targetOpacity <= 0f)
            {
                _fadeOverlay.style.display = DisplayStyle.None;
                if (_root != null) _root.style.display = _isPlaying ? DisplayStyle.Flex : DisplayStyle.None;
            }
            FadeActive = targetOpacity > 0f;
        }

        void TryChoose(int optionIndex)
        {
            if (_runner == null || optionIndex >= _runner.CurrentOptions.Count) return;
            _runner.Choose(optionIndex);
        }

        // --- Runner callbacks ----------------------------------------------------

        void OnNodeShown(DialogueNodeData node) => RenderNode(node);

        void OnRunnerClueRegistered(ClueData clue) => ClueRegistered?.Invoke(clue);

        void OnRunnerFlagSet(string flag) => FlagSet?.Invoke(flag);

        void OnRunnerEnded()
        {
            _isPlaying = false;
            LastEndFrame = Time.frameCount;
            if (_root != null) _root.style.display = FadeActive ? DisplayStyle.Flex : DisplayStyle.None;
            if (manageCursor)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
            }
            DialogueEnded?.Invoke();
        }

        // --- Rendering -----------------------------------------------------------

        void RenderNode(DialogueNodeData node)
        {
            if (node == null) return;
            if (_speakerLabel != null) _speakerLabel.text = node.speakerName ?? "";
            if (_dialogueText != null)
            {
                _dialogueText.text = _runner != null
                    ? _runner.SubstituteName(node.text)
                    : (node.text ?? "").Replace(DialogueRunner.PlayerNameToken, playerName ?? "");
            }
            RenderPortrait(node);

            var options = _runner != null ? _runner.CurrentOptions : null;
            int count = options != null ? options.Count : 0;
            for (int i = 0; i < _optionButtons.Length; i++)
            {
                var button = _optionButtons[i];
                if (button == null) continue;
                bool hasOption = i < count;
                button.text = hasOption ? $"[{i + 1}] {options[i].label}" : "";
                button.style.display = hasOption ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (_optionsContainer != null)
                _optionsContainer.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (_continueHint != null)
                _continueHint.style.display = _runner != null && count == 0 && !NamePromptActive
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
        }

        void RenderPortrait(DialogueNodeData node)
        {
            if (_portrait == null) return;
            var sprite = node.speakerPortrait != null
                ? node.speakerPortrait
                : FindSpeakerPortrait(node.speakerName);
            _portrait.style.display = sprite != null ? DisplayStyle.Flex : DisplayStyle.None;
            _portrait.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : default;
        }

        Sprite FindSpeakerPortrait(string speakerName)
        {
            if (string.IsNullOrEmpty(speakerName) || dialogueData == null
                || dialogueData.portraits == null) return null;
            foreach (var entry in dialogueData.portraits)
            {
                if (entry != null && entry.speakerName == speakerName && entry.portrait != null)
                    return entry.portrait;
            }
            return null;
        }

        // --- UI reload ------------------------------------------------------------

        void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            if (_uiVersion == version) return;
            _uiVersion = version;

            _root = root.Q<VisualElement>("Dialogue-Root");
            _portrait = root.Q<VisualElement>("SpeakerPortrait");
            _optionsContainer = root.Q<VisualElement>("OptionsContainer");
            _speakerLabel = root.Q<Label>("SpeakerLabel");
            _dialogueText = root.Q<Label>("DialogueText");
            _continueHint = root.Q<Label>("ContinueHint");
            _nameInputContainer = root.Q<VisualElement>("NameInputContainer");
            _nameInputField = root.Q<TextField>("NameInputField");
            _nameInputConfirm = root.Q<Button>("NameInputConfirm");
            _fadeOverlay = root.Q<VisualElement>("FadeOverlay");
            _fadeText = root.Q<Label>("FadeText");
            if (_nameInputConfirm != null)
                _nameInputConfirm.RegisterCallback<ClickEvent>(_ => SubmitName());
            if (_fadeOverlay != null) _fadeOverlay.style.opacity = _fadeOpacity;
            for (int i = 0; i < _optionButtons.Length; i++)
            {
                _optionButtons[i] = root.Q<Button>($"OptionButton{i}");
                int captured = i;
                if (_optionButtons[i] != null)
                    _optionButtons[i].RegisterCallback<ClickEvent>(_ => TryChoose(captured));
            }

            // Restore UI from cached runner state.
            if (_root != null)
                _root.style.display = _isPlaying ? DisplayStyle.Flex : DisplayStyle.None;
            if (_isPlaying && _runner != null && _runner.Current != null)
                RenderNode(_runner.Current);
        }
    }
}

using System;
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
        readonly Button[] _optionButtons = new Button[3];

        // Interaction state.
        bool _isPlaying;
        int _uiVersion;

        public bool IsPlaying => _isPlaying;
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
            if (keyboard == null || !_isPlaying || _runner == null) return;

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
            StopRunner();
            if (_root != null) _root.style.display = DisplayStyle.None;
            if (manageCursor)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
            }
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
            if (_root != null) _root.style.display = DisplayStyle.None;
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
            if (node == null || _runner == null) return;
            if (_speakerLabel != null) _speakerLabel.text = node.speakerName ?? "";
            if (_dialogueText != null) _dialogueText.text = _runner.SubstituteName(node.text);
            RenderPortrait(node);

            var options = _runner.CurrentOptions;
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
                _continueHint.style.display = count > 0 ? DisplayStyle.None : DisplayStyle.Flex;
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

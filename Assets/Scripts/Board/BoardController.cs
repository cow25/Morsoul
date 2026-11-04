using System;
using System.Collections.Generic;
using System.Linq;
using Morsoul.Data;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Morsoul.Board
{
    /// <summary>
    /// UI overlay controller for the deduction board. Sits on the same GameObject
    /// as a PanelRenderer (same pattern as HUDManager). All rule decisions are
    /// made by <see cref="DeductionBoard"/>; this class only renders results,
    /// forwards input and re-emits costs/events for external systems.
    /// </summary>
    public class BoardController : MonoBehaviour
    {
        public CaseData caseData;
        public bool preloadAllCaseClues = true;
        public bool manageCursor = true;
        public bool openOnStart = false;
        /// <summary>Portrait shown in the hint popup (Eliza by default).</summary>
        public Sprite hintPortrait;
        /// <summary>Speaker name for hint popups outside the prologue case.</summary>
        public string hintSpeaker = "Eliza";
        /// <summary>Speaker name used for hint popups during case 0 (internal diagnostic voice).</summary>
        public string diagnosticSpeaker = "Internal Diagnostic";

        /// <summary>Raised whenever the engine reports a Focus cost (invalid pairing, wrong answer).</summary>
        public event Action<float> FocusCosted;
        /// <summary>Raised whenever the engine reports a strike cost.</summary>
        public event Action<int> StrikeCosted;
        /// <summary>Raised when the master hypothesis unlocks an interrogation.</summary>
        public event Action<InterrogationData> InterrogationUnlocked;
        /// <summary>Raised when the master hypothesis routes to an ending.</summary>
        public event Action HypothesisRoutesToEnding;
        /// <summary>Raised after every causal prompt answer (correct or wrong).</summary>
        public event Action<AnswerResult> AnswerResolved;

        const string BannerErrorClass = "banner--error";
        const string BannerInfoClass = "banner--info";
        const string CardSelectedClass = "clue-card--selected";
        const string SlotFilledClass = "slot-button--filled";
        const string SubmitDisabledClass = "submit-button--disabled";

        PanelRenderer _panelRenderer;
        DeductionBoard _board;
        List<ClueData> _providedClues;
        List<ClueData> _rackClues = new List<ClueData>();
        readonly Dictionary<ClueData, Button> _clueCards = new Dictionary<ClueData, Button>();

        // Cached elements (re-queried on every UI reload).
        VisualElement _root;
        VisualElement _premiseRow;
        VisualElement _feedbackBanner;
        VisualElement _promptModal;
        VisualElement _hintPopup;
        VisualElement _hintPortrait;
        ScrollView _clueRack;
        Label _caseTitleLabel;
        Label _feedbackLabel;
        Label _hintLabel;
        Label _promptQuestion;
        Label _hintSpeaker;
        Label _hintText;
        Button _slotA;
        Button _slotB;
        Button _linkButton;
        Button _submitButton;
        Button _cancelPromptButton;
        Button _hintClose;
        readonly Button[] _optionButtons = new Button[3];

        // Interaction state.
        bool _isOpen;
        bool _promptOpen;
        ClueData _slotAClue;
        ClueData _slotBClue;
        int _slotAStamp;
        int _slotBStamp;
        int _fillCounter;

        // Cached display state so UI reloads restore correctly (HUDManager pattern).
        string _bannerText;
        string _bannerHint;
        string _bannerClass;
        string _hintPopupText;
        int _uiVersion;

        public bool IsOpen => _isOpen;
        /// <summary>Premises verified so far (null until a board is built). Exposed for GameFlow evidence wiring.</summary>
        public IReadOnlyList<PremiseData> Premises => _board?.Premises;

        /// <summary>Validated board node ids (null until a board is built). Exposed for save/checkpoint wiring.</summary>
        public IEnumerable<string> ValidatedNodeIds => _board?.ValidatedNodeIds;

        /// <summary>
        /// Restores validated nodes from a saved id list (e.g. SessionState.BoardEdges
        /// after a Continue). Ids belonging to other cases are ignored, and the
        /// produced premises rejoin <see cref="Premises"/> so the Master
        /// Hypothesis stays submittable. No-op until a board is built.
        /// </summary>
        public void RestoreValidatedNodes(IEnumerable<string> nodeIds)
        {
            if (_board == null || caseData == null || nodeIds == null) return;
            var validIds = new HashSet<string>(
                caseData.inquiryNodes.Where(n => n != null).Select(n => n.id));
            _board.Restore(nodeIds.Where(validIds.Contains));
        }

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
            BuildBoard();
            if (openOnStart && _board != null) Open();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.hKey.wasPressedThisFrame)
            {
                if (_isOpen) Close();
                else Open();
                return;
            }

            if (!_isOpen) return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (_promptOpen) CancelPrompt();
                else Close();
            }
            else if (_hintPopupShowing
                     && (keyboard.eKey.wasPressedThisFrame
                         || keyboard.enterKey.wasPressedThisFrame
                         || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                HideHintPopup();
            }
            else if (_promptOpen)
            {
                // (A/B/C) or (1/2/3) answer the open causal prompt.
                if (keyboard.aKey.wasPressedThisFrame || keyboard.digit1Key.wasPressedThisFrame)
                    OnOptionClicked(0);
                else if (keyboard.bKey.wasPressedThisFrame || keyboard.digit2Key.wasPressedThisFrame)
                    OnOptionClicked(1);
                else if (keyboard.cKey.wasPressedThisFrame || keyboard.digit3Key.wasPressedThisFrame)
                    OnOptionClicked(2);
            }
            else if (keyboard.spaceKey.wasPressedThisFrame)
            {
                SubmitHypothesis();
            }
        }

        /// <summary>Replaces the case, rebuilds the engine and the UI.</summary>
        public void SetCase(CaseData data)
        {
            caseData = data;
            ClearSlots();
            _promptOpen = false;
            HideBanner();
            HideHintPopup();
            BuildBoard();
            if (_isOpen) RefreshAll();
        }

        /// <summary>Provides the clue list shown in the rack (latent clues are filtered out).</summary>
        public void SetClues(IEnumerable<ClueData> clues)
        {
            _providedClues = clues?.ToList();
            RecomputeClueList();
            RebuildClueRack();
        }

        public void Open()
        {
            if (_board == null)
            {
                Debug.LogWarning("BoardController: no valid CaseData assigned; board stays closed.", this);
                return;
            }
            _isOpen = true;
            _board.CancelPrompt();
            _promptOpen = false;
            ClearSlots();
            HideBanner();
            HideHintPopup();
            RefreshAll();
            if (_root != null) _root.style.display = DisplayStyle.Flex;
            if (manageCursor)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
        }

        public void Close()
        {
            _isOpen = false;
            if (_root != null) _root.style.display = DisplayStyle.None;
            if (manageCursor)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
            }
        }

        void BuildBoard()
        {
            if (caseData == null)
            {
                _board = null;
                Debug.LogWarning("BoardController: caseData is null; board stays closed.", this);
                return;
            }
            try
            {
                _board = new DeductionBoard(caseData);
            }
            catch (Exception e)
            {
                _board = null;
                Debug.LogError($"BoardController: failed to build DeductionBoard — {e.Message}", this);
                return;
            }
            RecomputeClueList();
        }

        void RecomputeClueList()
        {
            _rackClues.Clear();
            IEnumerable<ClueData> source = null;
            if (_providedClues != null)
            {
                source = _providedClues;
            }
            else if (caseData != null)
            {
                var preloaded = caseData.preloadedClues;
                if (preloaded != null && preloaded.Length > 0)
                    source = preloaded;
                else if (preloadAllCaseClues)
                    source = caseData.clues;
            }
            if (source == null) return;
            foreach (var clue in source)
            {
                if (clue == null) continue;
                if (clue.isBacktrackingOnly || clue.type == ClueType.Latent) continue;
                _rackClues.Add(clue);
            }
        }

        void RefreshAll()
        {
            if (_caseTitleLabel != null)
                _caseTitleLabel.text = caseData != null ? caseData.title : "";
            RebuildClueRack();
            RefreshSlotButtons();
            RebuildPremises();
            ApplyBanner();
            ApplyHintPopup();
            ApplyPromptState();
        }

        // --- Clue rack -------------------------------------------------------

        void RebuildClueRack()
        {
            _clueCards.Clear();
            if (_clueRack == null) return;
            _clueRack.Clear();
            foreach (var clue in _rackClues)
            {
                var card = new Button();
                card.AddToClassList("clue-card");
                if (_slotAClue == clue || _slotBClue == clue)
                    card.AddToClassList(CardSelectedClass);

                var nameLabel = new Label(clue.displayName);
                nameLabel.AddToClassList("clue-card-name");
                card.Add(nameLabel);

                var typeLabel = new Label(clue.type.ToString());
                typeLabel.AddToClassList("clue-card-type");
                card.Add(typeLabel);

                var captured = clue;
                card.RegisterCallback<ClickEvent>(_ => OnClueCardClicked(captured));
                _clueCards[clue] = card;
                _clueRack.Add(card);
            }
        }

        void OnClueCardClicked(ClueData clue)
        {
            if (_board == null || clue == null || _promptOpen) return;

            // Clicking a slotted card deselects it (also prevents pairing a clue with itself).
            if (_slotAClue == clue) { ClearSlotA(); return; }
            if (_slotBClue == clue) { ClearSlotB(); return; }

            if (_slotAClue == null) FillSlotA(clue);
            else if (_slotBClue == null) FillSlotB(clue);
            else if (_slotAStamp <= _slotBStamp) { ClearSlotA(); FillSlotA(clue); }
            else { ClearSlotB(); FillSlotB(clue); }

            TryPairIfReady();
        }

        // --- Slots -----------------------------------------------------------

        void FillSlotA(ClueData clue)
        {
            _slotAClue = clue;
            _slotAStamp = ++_fillCounter;
            RefreshSlotButtons();
            SetCardSelected(clue, true);
        }

        void FillSlotB(ClueData clue)
        {
            _slotBClue = clue;
            _slotBStamp = ++_fillCounter;
            RefreshSlotButtons();
            SetCardSelected(clue, true);
        }

        void ClearSlotA()
        {
            var clue = _slotAClue;
            _slotAClue = null;
            _slotAStamp = 0;
            RefreshSlotButtons();
            if (clue != null) SetCardSelected(clue, false);
        }

        void ClearSlotB()
        {
            var clue = _slotBClue;
            _slotBClue = null;
            _slotBStamp = 0;
            RefreshSlotButtons();
            if (clue != null) SetCardSelected(clue, false);
        }

        void ClearSlots()
        {
            ClearSlotA();
            ClearSlotB();
        }

        void SetCardSelected(ClueData clue, bool selected)
        {
            if (clue != null && _clueCards.TryGetValue(clue, out var card) && card != null)
                card.EnableInClassList(CardSelectedClass, selected);
        }

        void RefreshSlotButtons()
        {
            if (_slotA != null)
            {
                _slotA.text = _slotAClue != null ? _slotAClue.displayName : "Slot A — empty";
                _slotA.EnableInClassList(SlotFilledClass, _slotAClue != null);
            }
            if (_slotB != null)
            {
                _slotB.text = _slotBClue != null ? _slotBClue.displayName : "Slot B — empty";
                _slotB.EnableInClassList(SlotFilledClass, _slotBClue != null);
            }
        }

        void OnLinkClicked()
        {
            if (_board == null || _promptOpen) return;
            if (_slotAClue == null || _slotBClue == null)
            {
                ShowBanner(BannerInfoClass, "Select two clues to link.");
                return;
            }
            TryPairIfReady();
        }

        // --- Pairing ---------------------------------------------------------

        void TryPairIfReady()
        {
            if (_board == null || _promptOpen || _slotAClue == null || _slotBClue == null) return;

            var result = _board.TryPair(_slotAClue, _slotBClue);
            switch (result.Outcome)
            {
                case PairingOutcome.OpensPrompt:
                    ShowPrompt(result.Node);
                    break;
                case PairingOutcome.AlreadyValidated:
                    ShowBanner(BannerInfoClass, "This inference has already been established.");
                    ClearSlots();
                    break;
                case PairingOutcome.Rejected:
                case PairingOutcome.Invalid:
                    string message = string.IsNullOrEmpty(result.Message)
                        ? DeductionBoard.InvalidPairingMessage
                        : result.Message;
                    ShowBanner(BannerErrorClass, message + CostTag(result.FocusCost, result.StrikeCost));
                    InvokeCosts(result.FocusCost, result.StrikeCost);
                    ClearSlots();
                    break;
            }
        }

        // --- Causal prompt ---------------------------------------------------

        void ShowPrompt(InquiryNodeData node)
        {
            _promptOpen = node != null;
            ApplyPromptState();
        }

        void ApplyPromptState()
        {
            var node = _board != null ? _board.PendingNode : null;
            if (_promptOpen && node == null) _promptOpen = false;

            if (_promptModal == null) return;
            if (!_promptOpen || node == null || node.prompt == null)
            {
                _promptModal.style.display = DisplayStyle.None;
                return;
            }

            if (_promptQuestion != null)
                _promptQuestion.text = node.prompt.question;
            var options = node.prompt.options;
            for (int i = 0; i < _optionButtons.Length; i++)
            {
                var button = _optionButtons[i];
                if (button == null) continue;
                bool hasOption = options != null && i < options.Length;
                button.text = hasOption ? options[i] : "";
                button.style.display = hasOption ? DisplayStyle.Flex : DisplayStyle.None;
            }
            _promptModal.style.display = DisplayStyle.Flex;
        }

        void OnOptionClicked(int optionIndex)
        {
            if (_board == null || !_promptOpen || _board.PendingNode == null) return;
            HideHintPopup();

            var result = _board.AnswerPrompt(optionIndex);
            if (result.Outcome == AnswerOutcome.Correct)
            {
                string title = result.Premise != null ? result.Premise.title : "premise";
                ShowBanner(BannerInfoClass, $"Premise established: {title}");
            }
            else
            {
                // Costs stay in the banner; the hint goes to the non-blocking
                // Eliza/diagnostic popup (GDD §4.5.2).
                ShowBanner(BannerErrorClass,
                    "Connection rejected." + CostTag(result.FocusCost, result.StrikeCost));
                if (!string.IsNullOrEmpty(result.Hint))
                    ShowHintPopup(result.Hint);
            }
            InvokeCosts(result.FocusCost, result.StrikeCost);
            AnswerResolved?.Invoke(result);

            _promptOpen = false;
            ApplyPromptState();
            ClearSlots();
            RebuildPremises();
        }

        void CancelPrompt()
        {
            _board?.CancelPrompt();
            _promptOpen = false;
            ApplyPromptState();
        }

        // --- Premises & hypothesis -------------------------------------------

        void RebuildPremises()
        {
            if (_premiseRow != null)
            {
                _premiseRow.Clear();
                if (_board != null)
                {
                    foreach (var premise in _board.Premises)
                    {
                        var card = new VisualElement();
                        card.AddToClassList("premise-card");
                        var title = new Label(premise != null ? premise.title : "?");
                        title.AddToClassList("premise-card-title");
                        card.Add(title);
                        if (premise != null) card.tooltip = premise.proposition;
                        _premiseRow.Add(card);
                    }
                }
            }
            RefreshSubmitState();
        }

        void RefreshSubmitState()
        {
            bool ready = _board != null && caseData != null && caseData.hypothesis != null
                         && _board.Premises.Contains(caseData.hypothesis.premise1)
                         && _board.Premises.Contains(caseData.hypothesis.premise2);
            if (_submitButton != null)
                _submitButton.EnableInClassList(SubmitDisabledClass, !ready);
        }

        void SubmitHypothesis()
        {
            if (_board == null || _promptOpen) return;

            var result = _board.SubmitHypothesis();
            switch (result.Outcome)
            {
                case HypothesisOutcome.NotReady:
                    ShowBanner(BannerInfoClass,
                        "Two verified premises are required before the Master Hypothesis.");
                    break;
                case HypothesisOutcome.UnlocksInterrogation:
                    ShowBanner(BannerInfoClass,
                        "Master Hypothesis confirmed. Interrogation unlocked.");
                    InterrogationUnlocked?.Invoke(result.Interrogation);
                    break;
                case HypothesisOutcome.RoutesToEnding:
                    ShowBanner(BannerInfoClass, "Master Hypothesis confirmed.");
                    HypothesisRoutesToEnding?.Invoke();
                    break;
            }
        }

        // --- Feedback banner --------------------------------------------------

        void ShowBanner(string bannerClass, string message, string hint = null)
        {
            _bannerClass = bannerClass;
            _bannerText = message;
            _bannerHint = hint;
            ApplyBanner();
        }

        void HideBanner()
        {
            _bannerClass = null;
            _bannerText = null;
            _bannerHint = null;
            ApplyBanner();
        }

        void ApplyBanner()
        {
            if (_feedbackBanner == null) return;
            _feedbackBanner.RemoveFromClassList(BannerErrorClass);
            _feedbackBanner.RemoveFromClassList(BannerInfoClass);
            if (!string.IsNullOrEmpty(_bannerClass))
                _feedbackBanner.AddToClassList(_bannerClass);
            if (_feedbackLabel != null)
                _feedbackLabel.text = _bannerText ?? "";
            if (_hintLabel != null)
            {
                bool hasHint = !string.IsNullOrEmpty(_bannerHint);
                _hintLabel.text = _bannerHint ?? "";
                _hintLabel.style.display = hasHint ? DisplayStyle.Flex : DisplayStyle.None;
            }
            _feedbackBanner.style.display = string.IsNullOrEmpty(_bannerText)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        // --- Hint popup (Eliza / internal diagnostic) ---------------------------

        bool _hintPopupShowing => !string.IsNullOrEmpty(_hintPopupText);

        void ShowHintPopup(string hint)
        {
            _hintPopupText = hint;
            ApplyHintPopup();
        }

        void HideHintPopup()
        {
            _hintPopupText = null;
            ApplyHintPopup();
        }

        void ApplyHintPopup()
        {
            if (_hintPopup == null) return;
            if (_hintPopupShowing)
            {
                if (_hintPortrait != null)
                    _hintPortrait.style.backgroundImage = hintPortrait != null
                        ? new StyleBackground(hintPortrait)
                        : new StyleBackground(StyleKeyword.Null);
                if (_hintSpeaker != null)
                    _hintSpeaker.text = caseData != null && caseData.caseId == 0
                        ? diagnosticSpeaker
                        : hintSpeaker;
                if (_hintText != null)
                    _hintText.text = _hintPopupText;
            }
            _hintPopup.style.display = _hintPopupShowing ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // --- Helpers ----------------------------------------------------------

        static string CostTag(float focusCost, int strikeCost)
        {
            var tag = "";
            if (focusCost > 0f) tag += $" (-{focusCost:0}% Focus)";
            if (strikeCost > 0) tag += $" (-{strikeCost} Strike{(strikeCost == 1 ? "" : "s")})";
            return tag;
        }

        void InvokeCosts(float focusCost, int strikeCost)
        {
            if (focusCost > 0f) FocusCosted?.Invoke(focusCost);
            if (strikeCost > 0) StrikeCosted?.Invoke(strikeCost);
        }

        // --- UI reload --------------------------------------------------------

        void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            if (_uiVersion == version) return;
            _uiVersion = version;

            _root = root.Q<VisualElement>("Board-Root");
            _clueRack = root.Q<ScrollView>("ClueRack");
            _premiseRow = root.Q<VisualElement>("PremiseRow");
            _feedbackBanner = root.Q<VisualElement>("FeedbackBanner");
            _promptModal = root.Q<VisualElement>("PromptModal");
            _hintPopup = root.Q<VisualElement>("HintPopup");
            _hintPortrait = root.Q<VisualElement>("HintPortrait");
            _caseTitleLabel = root.Q<Label>("CaseTitleLabel");
            _feedbackLabel = root.Q<Label>("FeedbackLabel");
            _hintLabel = root.Q<Label>("HintLabel");
            _promptQuestion = root.Q<Label>("PromptQuestion");
            _hintSpeaker = root.Q<Label>("HintSpeaker");
            _hintText = root.Q<Label>("HintText");

            _slotA = root.Q<Button>("SlotA");
            if (_slotA != null) _slotA.RegisterCallback<ClickEvent>(_ => ClearSlotA());
            _slotB = root.Q<Button>("SlotB");
            if (_slotB != null) _slotB.RegisterCallback<ClickEvent>(_ => ClearSlotB());
            _linkButton = root.Q<Button>("LinkButton");
            if (_linkButton != null) _linkButton.RegisterCallback<ClickEvent>(_ => OnLinkClicked());
            _submitButton = root.Q<Button>("SubmitButton");
            if (_submitButton != null)
                _submitButton.RegisterCallback<ClickEvent>(_ => SubmitHypothesis());
            _cancelPromptButton = root.Q<Button>("CancelPromptButton");
            if (_cancelPromptButton != null)
                _cancelPromptButton.RegisterCallback<ClickEvent>(_ => CancelPrompt());
            _hintClose = root.Q<Button>("HintClose");
            if (_hintClose != null)
                _hintClose.RegisterCallback<ClickEvent>(_ => HideHintPopup());
            for (int i = 0; i < _optionButtons.Length; i++)
            {
                _optionButtons[i] = root.Q<Button>($"OptionButton{i}");
                int captured = i;
                if (_optionButtons[i] != null)
                    _optionButtons[i].RegisterCallback<ClickEvent>(_ => OnOptionClicked(captured));
            }

            // Restore UI from cached engine/display state.
            if (_root != null)
                _root.style.display = _isOpen ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshAll();
        }
    }
}

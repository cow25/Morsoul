using System;
using System.Collections.Generic;
using System.Linq;
using Morsoul.Core;
using Morsoul.Data;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Morsoul.Interrogation
{
    /// <summary>
    /// UI overlay controller for the turn-based interrogation panel. Sits on the
    /// same GameObject as a PanelRenderer (same pattern as BoardController /
    /// HUDManager). All rule decisions are made by <see cref="InterrogationEngine"/>;
    /// this class only renders results, forwards input and re-emits events for
    /// external systems.
    /// </summary>
    public class InterrogationController : MonoBehaviour
    {
        public InterrogationData interrogationData;
        public Sprite suspectPortrait;
        public bool guidedFirstPhase;
        public bool emotionEchoUnlocked;
        public bool manageCursor = true;
        public bool openOnStart;

        /// <summary>Raised once when the interrogation reaches Confessed or Mistrial.</summary>
        public event Action<InterrogationStatus> InterrogationEnded;
        /// <summary>Raised when the engine reports a mistrial (all strikes lost).</summary>
        public event Action MistrialRequested;
        /// <summary>
        /// Raised when the player requests a retry from the mistrial modal
        /// (RetryButton click or the R key). The actual checkpoint reload is
        /// GameFlow/backend wiring — this controller only emits the event.
        /// </summary>
        public event Action RetryRequested;
        /// <summary>Raised whenever a strike is counted; parameter is total strikes lost.</summary>
        public event Action<int> StrikesChanged;
        /// <summary>Raised when the player forfeits with ESC.</summary>
        public event Action Forfeited;
        /// <summary>
        /// Raised whenever engine progress changes (phase advance, composure
        /// segment break, strike, keyword press or restore). GameFlow subscribes
        /// to keep the session encounter snapshot current.
        /// </summary>
        public event Action StateChanged;

        /// <summary>Optional external gate checked in Update (e.g. while Journal is open over Interrogation).</summary>
        public Func<bool> InputBlocked;

        const string BannerErrorClass = "banner--error";
        const string BannerInfoClass = "banner--info";
        const string SegmentBrokenClass = "segment--broken";
        const string SegmentShieldedClass = "segment--shielded";
        const string SealLostClass = "strike-seal--lost";
        const string KeywordPressedClass = "keyword--pressed";
        const string StanceSelectedClass = "stance--selected";

        enum Stance { Press, Contradict, PresentEvidence }

        PanelRenderer _panelRenderer;
        InterrogationEngine _engine;
        List<EvidenceData> _providedEvidence;

        // Cached elements (re-queried on every UI reload).
        VisualElement _root;
        VisualElement _portrait;
        VisualElement _keywordRow;
        VisualElement _evidenceTray;
        VisualElement _evidenceGrid;
        VisualElement _endOverlay;
        VisualElement _mistrialModal;
        Label _suspectNameLabel;
        Label _claimLabel;
        Label _responseLabel;
        Label _banner;
        Label _guidedHint;
        Label _endLabel;
        Button _pressButton;
        Button _contradictButton;
        Button _presentButton;
        Button _retryButton;
        readonly VisualElement[] _segments = new VisualElement[3];
        readonly VisualElement[] _seals = new VisualElement[3];

        // Interaction state.
        bool _isOpen;
        Stance _stance = Stance.Press;

        // Cached display state so UI reloads restore correctly (HUDManager pattern).
        string _bannerText;
        string _bannerClass;
        string _responseText;
        bool _trayOpen;
        bool _endShown;
        bool _endDismissed;
        string _endText;
        int _uiVersion;

        public bool IsOpen => _isOpen;
        public InterrogationEngine Engine => _engine;
        /// <summary>Last frame the panel closed; lets callers suppress same-frame input leaks.</summary>
        public int LastEndFrame { get; private set; }
        /// <summary>Live engine state for snapshotting; safe defaults when idle.</summary>
        public int EnginePhaseIndex => _engine != null ? _engine.PhaseIndex : 0;
        public int EngineSegmentsRemaining => _engine != null ? _engine.SegmentsRemaining : 0;
        public int EngineStrikesLost => _engine != null ? _engine.StrikesLost : 0;

        void OnEnable()
        {
            _panelRenderer = GetComponent<PanelRenderer>();
            if (_panelRenderer != null)
            {
                _panelRenderer.sortingOrder = 70;
                _panelRenderer.RegisterUIReloadCallback(OnUIReload);
            }
        }

        void OnDisable()
        {
            if (_panelRenderer != null) _panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        }

        void Start()
        {
            BuildEngine();
            if (openOnStart && _engine != null) Open();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !_isOpen) return;
            if (InputBlocked != null && InputBlocked()) return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Forfeited?.Invoke();
                Close();
            }
            else if (keyboard.digit1Key.wasPressedThisFrame)
            {
                SelectStance(Stance.Press);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame)
            {
                SelectStance(Stance.Contradict);
            }
            else if (keyboard.digit3Key.wasPressedThisFrame)
            {
                SelectStance(Stance.PresentEvidence);
            }
            else if (keyboard.rKey.wasPressedThisFrame && AwaitingMistrialRetry)
            {
                RequestRetry();
            }
            else if (keyboard.eKey.wasPressedThisFrame)
            {
                Advance();
            }
        }

        /// <summary>Replaces the interrogation, rebuilds the engine and the UI.</summary>
        public void SetInterrogation(InterrogationData data, IEnumerable<EvidenceData> evidence,
            bool guided, bool echo)
        {
            interrogationData = data;
            guidedFirstPhase = guided;
            emotionEchoUnlocked = echo;
            _providedEvidence = evidence?.Where(e => e != null).ToList();
            ResetDisplayState();
            BuildEngine();
            if (_isOpen) RefreshAll();
        }

        /// <summary>Rebuilds the evidence tray (call when the journal changes).</summary>
        public void SetEvidence(IEnumerable<EvidenceData> evidence)
        {
            _providedEvidence = evidence?.Where(e => e != null).ToList();
            if (_engine != null && _providedEvidence != null)
                foreach (var e in _providedEvidence) _engine.AddEvidence(e);
            RebuildEvidenceTray();
        }

        public void Open()
        {
            if (_engine == null)
            {
                Debug.LogWarning("InterrogationController: no valid InterrogationData assigned; " +
                                 "panel stays closed.", this);
                return;
            }
            _isOpen = true;
            _endShown = _engine.Status != InterrogationStatus.InProgress;
            if (_engine.Status == InterrogationStatus.InProgress) _endText = null;
            _stance = Stance.Press;
            _trayOpen = false;
            HideBanner();
            _responseText = null;
            ApplyResponse();
            RefreshAll();
            ApplyGuidedHint();
            if (_engine.EmotionEchoUnlocked)
                ShowBanner(BannerInfoClass, "Emotion Echo Active — Press [1] on a keyword to read their emotional aura.");
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
            LastEndFrame = Time.frameCount;
            if (_root != null) _root.style.display = DisplayStyle.None;
            if (manageCursor)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
            }
        }

        /// <summary>
        /// Resets the engine to phase 0 (mistrial retry) and refreshes the UI.
        /// No-op until an interrogation has been set via <see cref="SetInterrogation"/>.
        /// </summary>
        public void Reset()
        {
            if (_engine == null) return;
            _engine.Reset();
            ResetDisplayState();
            if (_isOpen) RefreshAll();
            StateChanged?.Invoke();
        }

        /// <summary>
        /// Restores a persisted mid-encounter snapshot (phase, composure
        /// segments, strikes) into the engine and repaints. Pressed keywords
        /// resume unpressed inside the restored phase. Safe defaults when the
        /// snapshot is null.
        /// </summary>
        public void RestoreState(int phaseIndex, int segmentsRemaining, int strikesLost)
        {
            if (_engine == null) return;
            _engine.RestoreState(phaseIndex, segmentsRemaining, strikesLost);
            ResetDisplayState();
            if (_isOpen) RefreshAll();
            StateChanged?.Invoke();
        }

        void BuildEngine()
        {
            if (interrogationData == null)
            {
                _engine = null;
                Debug.LogWarning("InterrogationController: interrogationData is null; " +
                                 "panel stays closed.", this);
                return;
            }
            try
            {
                _engine = new InterrogationEngine(interrogationData,
                    (IEnumerable<EvidenceData>)_providedEvidence ?? Array.Empty<EvidenceData>(),
                    guidedFirstPhase, emotionEchoUnlocked);
            }
            catch (Exception e)
            {
                _engine = null;
                Debug.LogError($"InterrogationController: failed to build InterrogationEngine — {e.Message}", this);
            }
        }

        void ResetDisplayState()
        {
            _stance = Stance.Press;
            _bannerText = null;
            _bannerClass = null;
            _responseText = null;
            _trayOpen = false;
            _endShown = false;
            _endDismissed = false;
            _endText = null;
        }

        void RefreshAll()
        {
            if (_suspectNameLabel != null)
                _suspectNameLabel.text = interrogationData != null
                    ? interrogationData.suspectName : "";
            ApplyPortrait();
            RefreshClaim();
            RebuildKeywords();
            RefreshSegments();
            RefreshSeals();
            ApplyStanceClasses();
            RebuildEvidenceTray();
            ApplyTrayState();
            ApplyBanner();
            ApplyResponse();
            ApplyGuidedHint();
            ApplyEndOverlay();
        }

        // --- Input -----------------------------------------------------------

        void SelectStance(Stance stance)
        {
            _stance = stance;
            _trayOpen = stance != Stance.Press;
            ApplyStanceClasses();
            ApplyTrayState();
            if (stance == Stance.Press)
                PressNextKeyword();
        }

        void PressNextKeyword()
        {
            if (_engine == null || !_isOpen) return;
            var keywords = _engine.CurrentPhase?.keywords;
            if (keywords == null || keywords.Length == 0)
            {
                ShowBanner(BannerInfoClass, "No highlighted claim to press in this phase.");
                return;
            }
            string target = null;
            foreach (var kw in keywords)
            {
                if (!string.IsNullOrEmpty(kw) && !_engine.IsKeywordPressed(kw))
                {
                    target = kw;
                    break;
                }
            }
            target ??= keywords.FirstOrDefault(k => !string.IsNullOrEmpty(k));
            if (!string.IsNullOrEmpty(target))
                OnKeywordClicked(target);
        }

        void Advance()
        {
            HideBanner();
            _responseText = null;
            ApplyResponse();
        }

        void OnKeywordClicked(string keyword)
        {
            if (_engine == null || !_isOpen) return;

            MoveResult result;
            try
            {
                result = _engine.Press(keyword);
            }
            catch (Exception e)
            {
                Debug.LogError($"InterrogationController: Press failed — {e.Message}", this);
                return;
            }

            SetResponse(!string.IsNullOrEmpty(result.Response)
                ? result.Response
                : "The suspect refuses to elaborate further.");
            RefreshKeywordStates();
            ApplyPortrait();
            ApplyGuidedHint();
            AudioCoordinator.PlayBankSfx(b => b.sfxDialogueBlip, 0.35f);
            if (_engine.EmotionEchoUnlocked)
            {
                var aura = _engine.InferEffectiveAura();
                if (aura == Aura.Amber)
                    ShowBanner(BannerInfoClass, "Emotion Echo [AMBER]: Mana tremors betray a lie — use [2] CONTRADICT.");
                else if (aura == Aura.Violet)
                    ShowBanner(BannerInfoClass, "Emotion Echo [VIOLET]: Guarded distress — probe with [3] PRESENT EVIDENCE.");
                else
                    ShowBanner(BannerInfoClass, $"Pressed \"{keyword}\" — read their response above.");
            }
            else
            {
                ShowBanner(BannerInfoClass, $"Pressed \"{keyword}\" — read their response above.");
            }
            ResolveStatus(result.Status);
            StateChanged?.Invoke();
        }

        void OnEvidenceCardClicked(EvidenceData evidence)
        {
            if (_engine == null || !_isOpen || evidence == null) return;
            if (_stance == Stance.Press)
            {
                ShowBanner(BannerInfoClass, "Choose CONTRADICT or PRESENT EVIDENCE first.");
                return;
            }

            MoveResult result;
            try
            {
                result = _stance == Stance.Contradict
                    ? _engine.Contradict(evidence)
                    : _engine.PresentEvidence(evidence);
            }
            catch (Exception e)
            {
                Debug.LogError($"InterrogationController: move failed — {e.Message}", this);
                return;
            }

            ApplyMoveResult(result);
        }

        // --- Move results ------------------------------------------------------

        void ApplyMoveResult(MoveResult result)
        {
            switch (result.Outcome)
            {
                case MoveOutcome.Correct:
                    SetResponse(result.Response);
                    if (result.ShieldShattered)
                        ShowBanner(BannerInfoClass, "The falsehood shield shatters.");
                    else
                        HideBanner();
                    break;
                case MoveOutcome.Wrong:
                    SetResponse(!string.IsNullOrEmpty(result.Response)
                        ? result.Response
                        : "The objection does not land.");
                    if (result.StrikeCounted)
                        StrikesChanged?.Invoke(_engine.StrikesLost);
                    break;
                case MoveOutcome.Locked:
                    SetResponse(result.Response);
                    ShowBanner(BannerErrorClass, "Press the underlined keyword first.");
                    break;
            }

            RefreshSegments();
            RefreshSeals();
            RefreshClaim();
            RebuildKeywords();
            ApplyPortrait();
            ApplyGuidedHint();
            ResolveStatus(result.Status);
            StateChanged?.Invoke();
        }

        void ResolveStatus(InterrogationStatus status)
        {
            if (status == InterrogationStatus.InProgress || _endShown) return;
            _endShown = true;
            _trayOpen = false;
            ApplyTrayState();
            if (status == InterrogationStatus.Confessed)
            {
                SetEndOverlay("CONFESSION EXTRACTED");
                InterrogationEnded?.Invoke(InterrogationStatus.Confessed);
            }
            else if (status == InterrogationStatus.Mistrial)
            {
                ApplyEndOverlay();
                MistrialRequested?.Invoke();
                InterrogationEnded?.Invoke(InterrogationStatus.Mistrial);
            }
        }

        // --- Rendering ---------------------------------------------------------

        void RefreshClaim()
        {
            if (_claimLabel == null) return;
            var phase = _engine?.CurrentPhase;
            _claimLabel.text = phase?.suspectClaim ?? "";
        }

        void RebuildKeywords()
        {
            if (_keywordRow == null) return;
            _keywordRow.Clear();
            var keywords = _engine?.CurrentPhase?.keywords;
            if (keywords == null) return;
            foreach (var keyword in keywords)
            {
                if (string.IsNullOrEmpty(keyword)) continue;
                var button = new Button { text = keyword };
                button.AddToClassList("keyword-button");
                string captured = keyword;
                button.RegisterCallback<ClickEvent>(_ => OnKeywordClicked(captured));
                ApplyKeywordState(button, captured);
                _keywordRow.Add(button);
            }
        }

        void RefreshKeywordStates()
        {
            if (_keywordRow == null) return;
            foreach (var child in _keywordRow.Children())
                if (child is Button button)
                    ApplyKeywordState(button, button.text);
        }

        void ApplyKeywordState(Button button, string keyword)
        {
            bool pressed = _engine != null && _engine.IsKeywordPressed(keyword);
            button.EnableInClassList(KeywordPressedClass, pressed);
            var aura = _engine == null ? Aura.None
                : (pressed ? _engine.InferEffectiveAura() : _engine.GetKeywordAura(keyword));
            button.EnableInClassList("keyword--amber", aura == Aura.Amber);
            button.EnableInClassList("keyword--violet", aura == Aura.Violet);
        }

        void RefreshSegments()
        {
            int total = 0;
            if (_engine != null && _engine.Data != null && _engine.Data.phases != null)
                total = _engine.Data.phases.Length;
            int remaining = _engine != null ? _engine.SegmentsRemaining : 0;
            int broken = Mathf.Max(0, total - remaining);
            for (int i = 0; i < _segments.Length; i++)
            {
                var segment = _segments[i];
                if (segment == null) continue;
                segment.style.display = i < total ? DisplayStyle.Flex : DisplayStyle.None;
                segment.EnableInClassList(SegmentBrokenClass, i < broken);
            }
            if (_segments[0] != null)
                _segments[0].EnableInClassList(SegmentShieldedClass,
                    _engine != null && _engine.ShieldActive);
        }

        void RefreshSeals()
        {
            int lost = _engine != null ? _engine.StrikesLost : 0;
            for (int i = 0; i < _seals.Length; i++)
            {
                if (_seals[i] != null)
                    _seals[i].EnableInClassList(SealLostClass, i < lost);
            }
        }

        void ApplyPortrait()
        {
            if (_portrait == null) return;
            if (suspectPortrait != null)
            {
                _portrait.style.backgroundImage = new StyleBackground(suspectPortrait);
            }
            else
            {
                var tex = LoadSuspectPortrait(_engine?.Data?.suspectName);
                _portrait.style.backgroundImage = tex != null
                    ? new StyleBackground(tex)
                    : new StyleBackground(StyleKeyword.Null);
            }
            var aura = _engine != null ? _engine.PortraitAura : Aura.None;
            _portrait.EnableInClassList("portrait--amber", aura == Aura.Amber);
            _portrait.EnableInClassList("portrait--violet", aura == Aura.Violet);
            _portrait.EnableInClassList("portrait--crimson", aura == Aura.Crimson);
        }

        static Texture2D LoadSuspectPortrait(string suspectName)
        {
            if (string.IsNullOrEmpty(suspectName)) return null;
            string key = null;
            if (suspectName.IndexOf("Ignatius", StringComparison.OrdinalIgnoreCase) >= 0) key = "ArtIgnatius";
            else if (suspectName.IndexOf("Accord", StringComparison.OrdinalIgnoreCase) >= 0) key = "ArtAccord";
            else if (suspectName.IndexOf("Vane", StringComparison.OrdinalIgnoreCase) >= 0) key = "ArtVane";
            else if (suspectName.IndexOf("Harlan", StringComparison.OrdinalIgnoreCase) >= 0) key = "ArtHarlan";
            else if (suspectName.IndexOf("Overseer", StringComparison.OrdinalIgnoreCase) >= 0) key = "ArtGrandOverseer";
            else if (suspectName.IndexOf("Eliza", StringComparison.OrdinalIgnoreCase) >= 0) key = "ArtEliza";
            else if (suspectName.IndexOf("Ovan", StringComparison.OrdinalIgnoreCase) >= 0) key = "ArtOvan";
            return key != null ? Resources.Load<Texture2D>($"Portraits/{key}") : null;
        }

        void ApplyStanceClasses()
        {
            if (_pressButton != null)
                _pressButton.EnableInClassList(StanceSelectedClass, _stance == Stance.Press);
            if (_contradictButton != null)
                _contradictButton.EnableInClassList(StanceSelectedClass, _stance == Stance.Contradict);
            if (_presentButton != null)
                _presentButton.EnableInClassList(StanceSelectedClass, _stance == Stance.PresentEvidence);
        }

        // --- Evidence tray ------------------------------------------------------

        void RebuildEvidenceTray()
        {
            if (_evidenceGrid == null) return;
            _evidenceGrid.Clear();
            if (_engine == null) return;
            var ordered = _engine.AvailableEvidence
                .Where(e => e != null)
                .OrderBy(EvidenceTitle, StringComparer.Ordinal);
            foreach (var evidence in ordered)
            {
                var card = new Button();
                card.AddToClassList("evidence-card");

                var nameLabel = new Label(EvidenceTitle(evidence));
                nameLabel.AddToClassList("evidence-card-name");
                card.Add(nameLabel);

                var typeLabel = new Label(EvidenceTypeName(evidence));
                typeLabel.AddToClassList("evidence-card-type");
                card.Add(typeLabel);

                var captured = evidence;
                card.RegisterCallback<ClickEvent>(_ => OnEvidenceCardClicked(captured));
                _evidenceGrid.Add(card);
            }
        }

        void ApplyTrayState()
        {
            if (_evidenceTray != null)
                _evidenceTray.style.display = _trayOpen ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static string EvidenceTitle(EvidenceData evidence)
        {
            if (evidence == null) return "";
            if (evidence is ClueData clue)
            {
                if (!string.IsNullOrEmpty(clue.displayName)) return clue.displayName;
                return !string.IsNullOrEmpty(clue.id) ? clue.id : clue.name;
            }
            if (evidence is PremiseData premise)
            {
                if (!string.IsNullOrEmpty(premise.title)) return premise.title;
                return !string.IsNullOrEmpty(premise.id) ? premise.id : premise.name;
            }
            return !string.IsNullOrEmpty(evidence.id) ? evidence.id : evidence.name;
        }

        static string EvidenceTypeName(EvidenceData evidence)
        {
            if (evidence is ClueData clue) return clue.type.ToString();
            if (evidence is PremiseData) return "Premise";
            return evidence != null ? evidence.GetType().Name : "";
        }

        // --- Banner / response / hints -------------------------------------------

        void ShowBanner(string bannerClass, string message)
        {
            _bannerClass = bannerClass;
            _bannerText = message;
            ApplyBanner();
        }

        void HideBanner()
        {
            _bannerClass = null;
            _bannerText = null;
            ApplyBanner();
        }

        void ApplyBanner()
        {
            if (_banner == null) return;
            _banner.RemoveFromClassList(BannerErrorClass);
            _banner.RemoveFromClassList(BannerInfoClass);
            if (!string.IsNullOrEmpty(_bannerClass))
                _banner.AddToClassList(_bannerClass);
            _banner.text = _bannerText ?? "";
            _banner.style.display = string.IsNullOrEmpty(_bannerText)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        void SetResponse(string text)
        {
            _responseText = text;
            ApplyResponse();
        }

        void ApplyResponse()
        {
            if (_responseLabel != null)
                _responseLabel.text = _responseText ?? "";
        }

        void ApplyGuidedHint()
        {
            if (_guidedHint == null) return;
            bool show = _engine != null
                        && _engine.GuidedFirstPhase
                        && _engine.Status == InterrogationStatus.InProgress;
            _guidedHint.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            if (_engine.PhaseIndex == 0)
            {
                bool anyPressed = _engine.CurrentPhase?.keywords?.Any(k => _engine.IsKeywordPressed(k)) == true;
                _guidedHint.text = anyPressed
                    ? "TUTORIAL (2/2): Select [2] CONTRADICT or [3] PRESENT EVIDENCE, then pick matching evidence below. (Wrong picks cost a Seal.)"
                    : "TUTORIAL (1/2): Click an underlined keyword (or press [1] PRESS) to probe the suspect's claim for weak spots.";
            }
            else
            {
                _guidedHint.text = "TUTORIAL: Probe keywords with [1] PRESS, then break claims with [2] CONTRADICT or [3] PRESENT EVIDENCE. [E] clears response, [G] opens Journal.";
            }
        }

        void SetEndOverlay(string text)
        {
            _endText = text;
            ApplyEndOverlay();
        }

        /// <summary>True while the mistrial retry modal is (or should be) showing.</summary>
        bool AwaitingMistrialRetry => _engine != null
                                      && _engine.Status == InterrogationStatus.Mistrial
                                      && !_endDismissed;

        /// <summary>
        /// Hides the mistrial retry modal and raises <see cref="RetryRequested"/>.
        /// The checkpoint reload itself is handled by GameFlow/backend wiring.
        /// </summary>
        void RequestRetry()
        {
            if (!AwaitingMistrialRetry) return;
            _endDismissed = true;
            ApplyEndOverlay();
            RetryRequested?.Invoke();
        }

        void ApplyEndOverlay()
        {
            if (_endOverlay == null) return;
            bool mistrial = _engine != null && _engine.Status == InterrogationStatus.Mistrial;
            if (_endLabel != null)
            {
                if (!string.IsNullOrEmpty(_endText))
                    _endLabel.text = _endText;
                else if (_engine != null && _engine.Status == InterrogationStatus.Confessed)
                    _endLabel.text = "CONFESSION EXTRACTED";
                else if (mistrial)
                    _endLabel.text = "MISTRIAL DECLARED";
                else
                    _endLabel.text = "";
                // Confessed keeps the plain overlay label; Mistrial shows the modal.
                _endLabel.style.display = mistrial ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (_mistrialModal != null)
            {
                bool showModal = AwaitingMistrialRetry
                                 && (_endShown || mistrial);
                _mistrialModal.style.display = showModal ? DisplayStyle.Flex : DisplayStyle.None;
            }
            bool show = _endShown
                        || (_engine != null && _engine.Status != InterrogationStatus.InProgress);
            _endOverlay.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // --- UI reload -----------------------------------------------------------

        void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            if (_uiVersion == version) return;
            _uiVersion = version;

            _root = root.Q<VisualElement>("Interrogation-Root");
            _portrait = root.Q<VisualElement>("PortraitImage");
            _keywordRow = root.Q<VisualElement>("KeywordRow");
            _evidenceTray = root.Q<VisualElement>("EvidenceTray");
            _evidenceGrid = root.Q<VisualElement>("EvidenceGrid");
            _endOverlay = root.Q<VisualElement>("EndOverlay");
            _mistrialModal = root.Q<VisualElement>("MistrialModal");
            _suspectNameLabel = root.Q<Label>("SuspectNameLabel");
            _claimLabel = root.Q<Label>("ClaimText");
            _responseLabel = root.Q<Label>("ResponseText");
            _banner = root.Q<Label>("Banner");
            _guidedHint = root.Q<Label>("GuidedHint");
            _endLabel = root.Q<Label>("EndLabel");
            for (int i = 0; i < _segments.Length; i++)
                _segments[i] = root.Q<VisualElement>($"Segment{i}");
            for (int i = 0; i < _seals.Length; i++)
                _seals[i] = root.Q<VisualElement>($"Seal{i}");

            _pressButton = root.Q<Button>("PressButton");
            if (_pressButton != null)
                _pressButton.RegisterCallback<ClickEvent>(_ => SelectStance(Stance.Press));
            _contradictButton = root.Q<Button>("ContradictButton");
            if (_contradictButton != null)
                _contradictButton.RegisterCallback<ClickEvent>(_ => SelectStance(Stance.Contradict));
            _presentButton = root.Q<Button>("PresentEvidenceButton");
            if (_presentButton != null)
                _presentButton.RegisterCallback<ClickEvent>(_ => SelectStance(Stance.PresentEvidence));
            _retryButton = root.Q<Button>("RetryButton");
            if (_retryButton != null)
                _retryButton.RegisterCallback<ClickEvent>(_ => RequestRetry());

            // Restore UI from cached engine/display state.
            if (_root != null)
                _root.style.display = _isOpen ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshAll();
        }
    }
}

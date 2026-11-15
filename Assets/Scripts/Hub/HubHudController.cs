using System.Linq;
using Morsoul.Core;
using Morsoul.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace Morsoul.Hub
{
    /// <summary>
    /// 2D hub HUD (GDD §4.2): top-center active objective banner, top-right
    /// Focus gauge bound to CoreRuntime.Focus, bottom keybinding footer.
    /// Sits on the same GameObject as a PanelRenderer; installed in every hub
    /// scene by the hub factories.
    /// </summary>
    public class HubHudController : MonoBehaviour
    {
        [TextArea]
        [Tooltip("Objective banner text for this hub (e.g. 'Interview Guildmaster Ovan for storage clearance').")]
        public string objectiveText;
        [Tooltip("Objective shown once the bound case's investigation is accepted " +
                 "and the suspect interrogation is unlocked. Generic fallback " +
                 "applies when left empty.")]
        public string postInvestigationObjective;
        [Tooltip("Case id whose investigation completion swaps in postInvestigationObjective " +
                 "(0 = derive from Session.CurrentCase).")]
        public int objectiveCaseId;
        [Tooltip("Optional case label shown above the objective (e.g. 'Case 1: The Poisoned Vault').")]
        public string caseText;
        [Tooltip("Footer keybinding legend.")]
        public string footerText =
            "[WASD] Move | [Shift] Brisk Walk | [E] Interact | [G] Desk/Journal";

        const string DefaultPostInvestigationObjective =
            "The deduction is accepted — confront the suspect.";

        PanelRenderer _panelRenderer;
        VisualElement _root;
        Label _caseLabel;
        Label _objectiveLabel;
        Label _activeGoalTitle;
        Label _userNameLabel;
        Label _focusText;
        Label _footerLabel;
        VisualElement _focusFill;
        VisualElement _golemEyeRow;
        VisualElement _aetherSightRow;
        VisualElement _emotionEchoRow;
        Button _escButton;
        Button _deskButton;
        Button _boardButton;
        int _uiVersion;
        float _lastFocus = -1f;
        float _lastFocusMax = -1f;
        IEventBus _bus;

        void OnEnable()
        {
            _panelRenderer = GetComponent<PanelRenderer>();
            if (_panelRenderer != null)
            {
                _panelRenderer.sortingOrder = 0;
                _panelRenderer.RegisterUIReloadCallback(OnUIReload);
            }
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            RefreshObjective();
            if (CoreRuntime.Instance != null)
            {
                _bus = CoreRuntime.Instance.Bus;
                _bus.Subscribe<InvestigationStateChangedEvent>(OnInvestigationChanged);
            }
        }

        void OnDisable()
        {
            if (_panelRenderer != null) _panelRenderer.UnregisterUIReloadCallback(OnUIReload);
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_bus != null)
            {
                _bus.Unsubscribe<InvestigationStateChangedEvent>(OnInvestigationChanged);
                _bus = null;
            }
        }

        void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            AppliedCaseText = null;
            AppliedObjective = null;
            RefreshObjective();
        }

        void OnInvestigationChanged(InvestigationStateChangedEvent _) => RefreshObjective();

        /// <summary>The case text currently shown.</summary>
        public string AppliedCaseText { get; private set; }

        /// <summary>The objective text currently shown (post-investigation aware).</summary>
        public string AppliedObjective { get; private set; }

        /// <summary>
        /// Recomputes the objective from session truth: gates Case 1 behind
        /// the Lower Market arrival and Guild first-visit sequence, and swaps in
        /// the post-investigation objective once interrogation is unlocked.
        /// </summary>
        public void RefreshObjective()
        {
            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            int caseId = session != null ? session.CurrentCase : objectiveCaseId;

            bool sceneMatchesSessionCase = session == null || objectiveCaseId <= 0 || objectiveCaseId == caseId;
            string effectiveCaseText = sceneMatchesSessionCase ? (caseText ?? "") : "";
            string effectiveObjective = sceneMatchesSessionCase ? (objectiveText ?? "") : "";

            // A. Prologue progression (or dev-warped to a hub before Prologue finishes)
            if (sceneName.Contains("Prologue") || (session != null && caseId == 0))
            {
                effectiveCaseText = "Prologue";
                if (!sceneName.Contains("Prologue"))
                {
                    effectiveObjective = "Complete the prologue and follow Eliza toward Asterveil.";
                }
                else if (session != null && (session.ElizaIntroSeen || session.HasFlag("Prologue_Completed")))
                {
                    effectiveObjective = "Follow Eliza Astra toward Asterveil Lower Market.";
                }
                else
                {
                    effectiveObjective = "Investigate the wilderness path toward the settlement.";
                }
            }
            // B. Guild Hall pre-Case-1 intro gating
            else if (sceneName.Contains("Guild") && caseId <= 1 && (session == null || !GuildIntroRules.IsCaseOneActive(session)))
            {
                effectiveCaseText = "";
                effectiveObjective = (session != null && session.HasFlag(GuildIntroRules.IntroSeenFlag))
                    ? "Speak with Guildmaster Ovan upstairs in the Guild hall."
                    : "Follow Eliza to meet Guildmaster Ovan.";
            }
            // C. Case 1 or Lower Market / Guild exploration
            else if (caseId == 1 && session != null && (session.PrologueCompleted || sceneName.Contains("LowerMarket")))
            {
                if (!session.HasFlag(GuildIntroRules.ArrivedFlag) && session.PrologueCompleted)
                {
                    effectiveCaseText = "";
                    effectiveObjective = "Follow Eliza to the Investigator's Guild.";
                }
                else if (!session.HasFlag(GuildIntroRules.IntroSeenFlag) && session.PrologueCompleted)
                {
                    effectiveCaseText = "";
                    effectiveObjective = "Follow Eliza to meet Guildmaster Ovan.";
                }
                else if (!GuildIntroRules.IsCaseOneActive(session))
                {
                    effectiveCaseText = "";
                    effectiveObjective = "Speak with Guildmaster Ovan upstairs in the Guild hall.";
                }
                else
                {
                    effectiveCaseText = !string.IsNullOrEmpty(effectiveCaseText) ? effectiveCaseText : "Case 1: The Poisoned Vault";
                    effectiveObjective = GetDynamicCaseObjective(session, caseId, effectiveObjective,
                        sceneMatchesSessionCase ? postInvestigationObjective : null, sceneName);
                }
            }
            // D. Dynamic goals for Cases 2-5, cross-hub visits, or standalone tests
            else
            {
                if (string.IsNullOrEmpty(effectiveCaseText) && caseId > 0)
                    effectiveCaseText = GetCaseFallbackTitle(caseId);
                effectiveObjective = GetDynamicCaseObjective(session, caseId, effectiveObjective,
                    sceneMatchesSessionCase ? postInvestigationObjective : null, sceneName);
            }

            ApplyObjective(effectiveCaseText, effectiveObjective);
        }

        static string GetDynamicCaseObjective(SessionState session, int caseId,
            string baseObjective, string postObjective, string sceneName = "")
        {
            if (GuildIntroRules.IsBriefingPending(session, caseId))
                return "Report to Guildmaster Ovan at the Investigator's Guild for your next assignment.";

            if (session != null && session.HasFlag($"Guild.CheckBoard.{caseId}"))
                return "Check the Quest Board downstairs in the Investigator's Guild for potential cases.";

            if (session != null && session.HasFlag(ElizaCarrierGuideController.PendingFlag(caseId)))
                return "Head out to Lower Market — Eliza will guide you to the Carrier.";

            if (session != null && session.CanStartInterrogation(caseId))
            {
                if (!string.IsNullOrEmpty(postObjective)) return postObjective;
                switch (caseId)
                {
                    case 1: return "The deduction is accepted - confront Master Alchemist Ignatius.";
                    case 2: return sceneName.Contains("CathedralCourtyard")
                        ? "The deduction is accepted - confront Accord inside the Cathedral."
                        : "The deduction is accepted - confront Accord the Bell-Ringer.";
                    case 3: return "The deduction is accepted - confront Vane the Veiled Circle Operative.";
                    case 4: return "The deduction is accepted - confront Harlan, the Veiled Circle Infiltrator.";
                    case 5: return "The deduction is accepted - confront the Sanctum Core.";
                    default: return DefaultPostInvestigationObjective;
                }
            }

            if (session != null)
            {
                switch (caseId)
                {
                    case 1:
                        if (session.HasFlag(GuildIntroRules.WarrantFlag))
                            return "Investigate the poisoned vault via the cellar grate.";
                        break;
                    case 2:
                        if (session.HasFlag("C2_BelfryKeyGranted"))
                            return "Investigate the Cathedral Belfry.";
                        break;
                    case 3:
                        if (session.HasFlag("C3_PerimeterSealGranted"))
                            return "Investigate Whisperwood Grove.";
                        break;
                    case 4:
                        if (session.HasFlag("C4_OverrideClearance"))
                            return "Investigate Iron Hollow Drift 09.";
                        break;
                }
            }

            if (!string.IsNullOrEmpty(baseObjective))
                return baseObjective;

            if (caseId >= 2 && caseId <= 4 && (sceneName.Contains("Guild") || sceneName.Contains("LowerMarket")))
            {
                return caseId switch
                {
                    2 => "Take the Carrier wagon in Lower Market to the Cathedral Courtyard.",
                    3 => "Take the Carrier wagon in Lower Market to Whisperwood Grove.",
                    4 => "Take the Carrier wagon in Lower Market to Iron Hollow.",
                    _ => GetCaseFallbackObjective(caseId, sceneName)
                };
            }

            return GetCaseFallbackObjective(caseId, sceneName);
        }

        static string GetCaseFallbackTitle(int caseId)
        {
            switch (caseId)
            {
                case 1: return "Case 1: The Poisoned Vault";
                case 2: return "Case 2: The Muted Belfry";
                case 3: return "Case 3: The Dying Grove";
                case 4: return "Case 4: The Shattered Seam";
                case 5: return "Case 5: The Sanctum Core";
                default: return "";
            }
        }

        static string GetCaseFallbackObjective(int caseId, string sceneName = "")
        {
            switch (caseId)
            {
                case 1: return "Interview Merchant Guildmaster Ovan for storage clearance.";
                case 2: return sceneName.Contains("CathedralCourtyard")
                    ? "Enter the Cathedral of Saint Althea."
                    : "Examine Mother Lyra and secure the Belfry Key.";
                case 3: return "Receive Kaelen's briefing and acquire the Elder Grove Perimeter Seal.";
                case 4: return "Present Guild authority to Overseer Thrun and reach Drift 09.";
                case 5: return "Speak with Guildmaster Ovan when you're ready to proceed to Case 5.";
                default: return "Explore the area.";
            }
        }

        void Update()
        {
            RefreshObjective();
            SyncSessionHud();

            var focus = CoreRuntime.Instance != null ? CoreRuntime.Instance.Focus : null;
            if (focus == null) return;
            if (!Mathf.Approximately(focus.Percent, _lastFocus)
                || !Mathf.Approximately(focus.Maximum, _lastFocusMax))
            {
                _lastFocus = focus.Percent;
                _lastFocusMax = focus.Maximum;
                ApplyFocus(focus.Percent, focus.Maximum, focus.Normalized);
            }
        }

        void SyncSessionHud()
        {
            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            if (_userNameLabel != null)
            {
                string display = session != null && !string.IsNullOrWhiteSpace(session.PlayerName)
                    ? session.PlayerName.Trim() : "User";
                if (_userNameLabel.text != display) _userNameLabel.text = display;
            }
            if (session != null)
            {
                if (_golemEyeRow != null)
                    _golemEyeRow.style.display = session.Tools.Contains(Tool.GolemEye)
                        ? DisplayStyle.Flex : DisplayStyle.None;
                if (_aetherSightRow != null)
                    _aetherSightRow.style.display = session.Tools.Contains(Tool.AetherSight)
                        ? DisplayStyle.Flex : DisplayStyle.None;
                if (_emotionEchoRow != null)
                    _emotionEchoRow.style.display = session.Tools.Contains(Tool.EmotionEcho)
                        ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>Updates the entry objective banner at runtime (e.g. after a quest beat).</summary>
        public void SetObjective(string text)
        {
            objectiveText = text;
            RefreshObjective();
        }

        void ApplyObjective(string text) => ApplyObjective(caseText, text);

        void ApplyObjective(string currentCaseText, string text)
        {
            currentCaseText = currentCaseText ?? "";
            text = text ?? "";

            bool sameText = AppliedCaseText == currentCaseText && AppliedObjective == text;
            bool labelsBoundAndMatching = _caseLabel != null && _objectiveLabel != null &&
                _caseLabel.text == currentCaseText && _objectiveLabel.text == text;

            if (sameText && labelsBoundAndMatching)
                return;

            AppliedCaseText = currentCaseText;
            AppliedObjective = text;

            var panel = _root != null ? _root.Q<VisualElement>("ObjectivePanel") : null;
            bool hasObjective = !string.IsNullOrEmpty(text);
            if (panel != null)
            {
                panel.style.display = hasObjective ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (_activeGoalTitle != null)
            {
                _activeGoalTitle.style.display = hasObjective ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_caseLabel != null)
            {
                _caseLabel.text = currentCaseText;
                _caseLabel.style.display = string.IsNullOrEmpty(currentCaseText)
                    ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (_objectiveLabel != null)
            {
                _objectiveLabel.text = text;
                _objectiveLabel.style.display = string.IsNullOrEmpty(text)
                    ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        void ApplyFocus(float energy, float maximum, float normalized)
        {
            if (_focusText != null)
                _focusText.text = $"Focus Energy: {Mathf.RoundToInt(energy)}/{Mathf.RoundToInt(maximum)}";
            if (_focusFill != null)
                _focusFill.style.width = Length.Percent(Mathf.Clamp01(normalized) * 100f);
        }

        void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            if (_uiVersion == version) return;
            _uiVersion = version;

            _root = root.Q<VisualElement>("HubHud-Root");
            _caseLabel = root.Q<Label>("CaseLabel");
            _objectiveLabel = root.Q<Label>("ObjectiveLabel");
            _activeGoalTitle = root.Q<Label>("ActiveGoalTitle");
            _userNameLabel = root.Q<Label>("UserNameLabel");
            _focusText = root.Q<Label>("FocusText");
            _focusFill = root.Q<VisualElement>("FocusBarFill");
            _footerLabel = root.Q<Label>("FooterLabel");
            _golemEyeRow = root.Q<VisualElement>("GolemEyeRow");
            _aetherSightRow = root.Q<VisualElement>("AetherSightRow");
            _emotionEchoRow = root.Q<VisualElement>("EmotionEchoRow");
            _escButton = root.Q<Button>("EscKeyButton");
            _deskButton = root.Q<Button>("DeskKeyButton");
            _boardButton = root.Q<Button>("BoardKeyButton");

            if (_escButton != null) _escButton.clicked += PauseMenuController.TogglePause;
            if (_deskButton != null) _deskButton.clicked += TriggerJournalShortcut;
            if (_boardButton != null) _boardButton.clicked += TriggerBoardShortcut;

            AppliedCaseText = null;
            AppliedObjective = null;
            RefreshObjective();
            SyncSessionHud();
            if (_footerLabel != null) _footerLabel.text = footerText ?? "";

            // Force a refresh on the next Update.
            _lastFocus = -1f;
            _lastFocusMax = -1f;
        }

        static void TriggerJournalShortcut()
        {
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (mb == null) continue;
                var type = mb.GetType();
                if (type.Name == "JournalController")
                {
                    var isOpenProp = type.GetProperty("IsOpen");
                    bool isOpen = isOpenProp != null && (bool)isOpenProp.GetValue(mb);
                    type.GetMethod(isOpen ? "Close" : "Open")?.Invoke(mb, null);
                    return;
                }
            }
        }

        static void TriggerBoardShortcut()
        {
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (mb == null) continue;
                var type = mb.GetType();
                if (type.Name == "WeightedBoardController")
                {
                    var canUseField = type.GetField("CanUse");
                    if (canUseField != null && canUseField.GetValue(mb) is System.Func<bool> canUse && !canUse())
                        continue;
                    var isOpenProp = type.GetProperty("IsOpen");
                    bool isOpen = isOpenProp != null && (bool)isOpenProp.GetValue(mb);
                    type.GetMethod(isOpen ? "Close" : "Open")?.Invoke(mb, null);
                    return;
                }
            }
        }
    }
}

using Morsoul.Core;
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
        Label _focusText;
        Label _footerLabel;
        VisualElement _focusFill;
        int _uiVersion;
        float _lastFocus = -1f;
        float _lastFocusMax = -1f;
        IEventBus _bus;

        void OnEnable()
        {
            _panelRenderer = GetComponent<PanelRenderer>();
            if (_panelRenderer != null) _panelRenderer.RegisterUIReloadCallback(OnUIReload);
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
            if (_bus != null)
            {
                _bus.Unsubscribe<InvestigationStateChangedEvent>(OnInvestigationChanged);
                _bus = null;
            }
        }

        void OnInvestigationChanged(InvestigationStateChangedEvent _) => RefreshObjective();

        /// <summary>The objective text currently shown (post-investigation aware).</summary>
        public string AppliedObjective { get; private set; }

        /// <summary>
        /// Recomputes the objective from session truth: once the bound case's
        /// interrogation is unlocked the post-investigation text replaces the
        /// entry objective. Called on enable and after committed investigation
        /// changes (including the development fallback).
        /// </summary>
        public void RefreshObjective()
        {
            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            int caseId = objectiveCaseId > 0 ? objectiveCaseId
                : session != null ? session.CurrentCase : 0;
            bool unlocked = session != null && session.CanStartInterrogation(caseId);
            ApplyObjective(unlocked
                ? (string.IsNullOrEmpty(postInvestigationObjective)
                    ? DefaultPostInvestigationObjective : postInvestigationObjective)
                : objectiveText);
        }

        void Update()
        {
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

        /// <summary>Updates the entry objective banner at runtime (e.g. after a quest beat).</summary>
        public void SetObjective(string text)
        {
            objectiveText = text;
            RefreshObjective();
        }

        void ApplyObjective(string text)
        {
            AppliedObjective = text;
            if (_caseLabel != null)
            {
                _caseLabel.text = caseText ?? "";
                _caseLabel.style.display = string.IsNullOrEmpty(caseText)
                    ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (_objectiveLabel != null)
            {
                _objectiveLabel.text = text ?? "";
                _objectiveLabel.style.display = string.IsNullOrEmpty(text)
                    ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        void ApplyFocus(float energy, float maximum, float normalized)
        {
            if (_focusText != null)
                _focusText.text = $"Focus: {Mathf.RoundToInt(energy)} / {Mathf.RoundToInt(maximum)}";
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
            _focusText = root.Q<Label>("FocusText");
            _focusFill = root.Q<VisualElement>("FocusBarFill");
            _footerLabel = root.Q<Label>("FooterLabel");

            RefreshObjective();
            if (_footerLabel != null) _footerLabel.text = footerText ?? "";

            // Force a refresh on the next Update.
            _lastFocus = -1f;
            _lastFocusMax = -1f;
        }
    }
}

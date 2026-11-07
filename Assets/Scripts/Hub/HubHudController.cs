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
        [Tooltip("Optional case label shown above the objective (e.g. 'Case 1: The Poisoned Vault').")]
        public string caseText;
        [Tooltip("Footer keybinding legend.")]
        public string footerText =
            "[WASD] Move | [Shift] Brisk Walk | [E] Interact | [G] Desk/Journal | [H] Board | [ESC] Menu";

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

        void OnEnable()
        {
            _panelRenderer = GetComponent<PanelRenderer>();
            if (_panelRenderer != null) _panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }

        void OnDisable()
        {
            if (_panelRenderer != null) _panelRenderer.UnregisterUIReloadCallback(OnUIReload);
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

        /// <summary>Updates the objective banner at runtime (e.g. after a quest beat).</summary>
        public void SetObjective(string text)
        {
            objectiveText = text;
            ApplyObjective();
        }

        void ApplyObjective()
        {
            if (_caseLabel != null)
            {
                _caseLabel.text = caseText ?? "";
                _caseLabel.style.display = string.IsNullOrEmpty(caseText)
                    ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (_objectiveLabel != null)
            {
                _objectiveLabel.text = objectiveText ?? "";
                _objectiveLabel.style.display = string.IsNullOrEmpty(objectiveText)
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

            ApplyObjective();
            if (_footerLabel != null) _footerLabel.text = footerText ?? "";

            // Force a refresh on the next Update.
            _lastFocus = -1f;
            _lastFocusMax = -1f;
        }
    }
}

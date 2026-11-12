using System;
using Morsoul.Data;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Morsoul.Resolution
{
    /// <summary>
    /// UI overlay controller for the end-of-case resolution screen. Sits on the
    /// same GameObject as a PanelRenderer (same pattern as DialogueController /
    /// BoardController). Shows the resolved case title, the earned grade, and —
    /// when the case left unease behind — a violet pulsing banner that
    /// auto-dismisses after <see cref="uneaseBannerSeconds"/> or on E. Pressing
    /// E once the banner is gone dismisses the screen via <see cref="Dismissed"/>.
    /// </summary>
    public class ResolutionController : MonoBehaviour
    {
        public bool manageCursor = true;
        public float uneaseBannerSeconds = 5f;
        public float uneasePulseSeconds = 0.9f;

        [Header("Debug preview")]
        /// <summary>Calls Show() in Start with the debug fields below; GameFlow drives Show() in real play.</summary>
        public bool showOnStart;
        public string debugCaseTitle = "Test Case";
        public Grade debugGrade = Grade.Clean;
        public bool debugUnease;
        public string debugUneaseLocation = "Vault alcove";
        public string debugLocationHint = "";

        /// <summary>Raised when the player dismisses the screen with E.</summary>
        public event Action Dismissed;

        const string GradeCleanClass = "grade--clean";
        const string GradeStandardClass = "grade--standard";
        const string GradeFlawedClass = "grade--flawed";
        const string GradeUngradedClass = "grade--ungraded";
        const string UneasePulseClass = "unease-banner--pulse";

        const string UneaseBannerText =
            "A lingering dissonance remains in your core — a feeling of unease washes over you.";

        PanelRenderer _panelRenderer;

        // Cached elements (re-queried on every UI reload).
        VisualElement _root;
        Label _headerLabel;
        Label _gradeLabel;
        VisualElement _uneaseBanner;
        Label _uneaseText;
        Label _uneaseLocation;
        Label _uneaseHint;
        Label _dismissHint;

        // Display state (re-applied on every UI reload).
        bool _isVisible;
        bool _uneaseVisible;
        bool _pulseOn;
        string _caseTitle;
        Grade _grade;
        string _uneaseLocationName;
        string _locationHint;
        int _uiVersion;

        IVisualElementScheduledItem _bannerTimer;
        IVisualElementScheduledItem _pulseTimer;

        public bool IsVisible => _isVisible;
        public bool UneaseVisible => _uneaseVisible;

        void OnEnable()
        {
            _panelRenderer = GetComponent<PanelRenderer>();
            if (_panelRenderer != null)
            {
                _panelRenderer.sortingOrder = 80;
                _panelRenderer.RegisterUIReloadCallback(OnUIReload);
            }
        }

        void OnDisable()
        {
            if (_panelRenderer != null) _panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        }

        void Start()
        {
            if (showOnStart)
                Show(debugCaseTitle, debugGrade, debugUnease,
                    debugUneaseLocation, debugLocationHint);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !_isVisible) return;
            if (!keyboard.eKey.wasPressedThisFrame) return;

            if (_uneaseVisible) DismissUneaseBanner();
            else Dismiss();
        }

        /// <summary>Shows the resolution screen for a finished case.</summary>
        /// <param name="caseTitle">Case display name, rendered as "CASE RESOLVED — {caseTitle}".</param>
        /// <param name="grade">Earned grade; Clean = gold, Standard = white, Flawed = grey.</param>
        /// <param name="unease">Whether a lingering unease banner should appear.</param>
        /// <param name="uneaseLocation">Location named by "A trace remains at {location}."</param>
        /// <param name="locationHintText">Optional extra hint line under the location line.</param>
        public void Show(string caseTitle, Grade grade, bool unease,
            string uneaseLocation, string locationHintText)
        {
            _caseTitle = caseTitle;
            _grade = grade;
            _uneaseLocationName = uneaseLocation;
            _locationHint = locationHintText;

            _isVisible = true;
            _uneaseVisible = unease;
            _pulseOn = false;

            ApplyAll();
            RestartBannerTimers();
            if (_root != null) _root.style.display = DisplayStyle.Flex;
            if (manageCursor)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
        }

        /// <summary>Hides the screen without firing <see cref="Dismissed"/>.</summary>
        public void Hide()
        {
            _isVisible = false;
            _uneaseVisible = false;
            StopBannerTimers();
            if (_root != null) _root.style.display = DisplayStyle.None;
            if (manageCursor)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
            }
        }

        void Dismiss()
        {
            Hide();
            Dismissed?.Invoke();
        }

        void DismissUneaseBanner()
        {
            _uneaseVisible = false;
            StopBannerTimers();
            ApplyUneaseBanner();
        }

        // --- Unease banner timers ----------------------------------------------

        void RestartBannerTimers()
        {
            StopBannerTimers();
            if (!_uneaseVisible || _root == null) return;
            _bannerTimer = _root.schedule
                .Execute(DismissUneaseBanner)
                .StartingIn((long)(Mathf.Max(0.1f, uneaseBannerSeconds) * 1000f));
            _pulseTimer = _root.schedule
                .Execute(TogglePulse)
                .Every((long)(Mathf.Max(0.1f, uneasePulseSeconds) * 1000f));
        }

        void StopBannerTimers()
        {
            _bannerTimer?.Pause();
            _bannerTimer = null;
            _pulseTimer?.Pause();
            _pulseTimer = null;
            _pulseOn = false;
        }

        void TogglePulse()
        {
            _pulseOn = !_pulseOn;
            if (_uneaseBanner != null)
                _uneaseBanner.EnableInClassList(UneasePulseClass, _pulseOn);
        }

        // --- Rendering ---------------------------------------------------------

        void ApplyAll()
        {
            if (_headerLabel != null)
                _headerLabel.text = string.IsNullOrEmpty(_caseTitle)
                    ? "CASE RESOLVED"
                    : $"CASE RESOLVED — {_caseTitle}";
            ApplyGrade();
            ApplyUneaseBanner();
            if (_dismissHint != null)
                _dismissHint.text = _uneaseVisible ? "(E) dismiss" : "(E) continue";
        }

        void ApplyGrade()
        {
            if (_gradeLabel == null) return;
            _gradeLabel.text = _grade == Grade.Ungraded ? "" : $"Grade: {_grade}";
            _gradeLabel.style.display = _grade == Grade.Ungraded ? DisplayStyle.None : DisplayStyle.Flex;
            _gradeLabel.RemoveFromClassList(GradeCleanClass);
            _gradeLabel.RemoveFromClassList(GradeStandardClass);
            _gradeLabel.RemoveFromClassList(GradeFlawedClass);
            _gradeLabel.RemoveFromClassList(GradeUngradedClass);
            switch (_grade)
            {
                case Grade.Clean: _gradeLabel.AddToClassList(GradeCleanClass); break;
                case Grade.Standard: _gradeLabel.AddToClassList(GradeStandardClass); break;
                case Grade.Flawed: _gradeLabel.AddToClassList(GradeFlawedClass); break;
                default: _gradeLabel.AddToClassList(GradeUngradedClass); break;
            }
        }

        void ApplyUneaseBanner()
        {
            if (_uneaseBanner != null)
            {
                _uneaseBanner.style.display = _uneaseVisible ? DisplayStyle.Flex : DisplayStyle.None;
                _uneaseBanner.EnableInClassList(UneasePulseClass, _pulseOn && _uneaseVisible);
            }
            if (!_uneaseVisible) return;
            if (_uneaseText != null) _uneaseText.text = UneaseBannerText;
            if (_uneaseLocation != null)
            {
                bool hasLocation = !string.IsNullOrEmpty(_uneaseLocationName);
                _uneaseLocation.text = hasLocation ? $"A trace remains at {_uneaseLocationName}." : "";
                _uneaseLocation.style.display = hasLocation ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (_uneaseHint != null)
            {
                bool hasHint = !string.IsNullOrEmpty(_locationHint);
                _uneaseHint.text = hasHint ? _locationHint : "";
                _uneaseHint.style.display = hasHint ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (_dismissHint != null) _dismissHint.text = "(E) dismiss";
        }

        // --- UI reload ----------------------------------------------------------

        void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            if (_uiVersion == version) return;
            _uiVersion = version;

            StopBannerTimers();
            _root = root.Q<VisualElement>("Resolution-Root");
            _headerLabel = root.Q<Label>("HeaderLabel");
            _gradeLabel = root.Q<Label>("GradeLabel");
            _uneaseBanner = root.Q<VisualElement>("UneaseBanner");
            _uneaseText = root.Q<Label>("UneaseText");
            _uneaseLocation = root.Q<Label>("UneaseLocation");
            _uneaseHint = root.Q<Label>("UneaseHint");
            _dismissHint = root.Q<Label>("DismissHint");

            if (_root != null)
                _root.style.display = _isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            ApplyAll();
            RestartBannerTimers();
        }
    }
}

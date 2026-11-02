using System;
using System.Collections.Generic;
using Morsoul.Data;
using Morsoul.Hub;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Morsoul.Journal
{
    /// <summary>
    /// UI overlay controller for the Clue Journal (GDD wireframe §4.4). Sits on
    /// the same GameObject as a PanelRenderer — same pattern as BoardController.
    /// Reads collected clues from <see cref="HubJournal"/>, runic logs from its
    /// own serialized lists, and dossier entries gated by hub flags
    /// (<see cref="SetFlags"/>). All filtering/masking rules live in
    /// <see cref="JournalFilter"/> so they stay EditMode-testable; this class
    /// only renders, forwards input and re-emits events.
    /// </summary>
    public class JournalController : MonoBehaviour
    {
        /// <summary>A suspect dossier entry, hidden until its flag is set.</summary>
        [Serializable]
        public class DossierEntry
        {
            public string suspectName;
            public string role;
            [TextArea] public string notes;
            public string requiresFlag;
        }

        [Header("Data sources")]
        /// <summary>Hub clue store; the Physical/Testimony tabs render its Collected list.</summary>
        public HubJournal journal;
        /// <summary>Every runic log that can appear in the scene (uncollected ones render locked).</summary>
        public RunicLogData[] allLogs;
        /// <summary>Suspect dossier entries; gated by <see cref="DossierEntry.requiresFlag"/>.</summary>
        public DossierEntry[] dossiers;
        /// <summary>Runic logs collected so far, in registration order.</summary>
        public List<RunicLogData> collectedLogs = new List<RunicLogData>();

        [Header("Behaviour")]
        public bool manageCursor = true;

        [Header("Lingering Dissonance")]
        /// <summary>Hub flag that reveals the dissonance banner on the Testimony tab.</summary>
        public string dissonanceFlag;
        /// <summary>Location hint shown on the dissonance banner card.</summary>
        [TextArea] public string dissonanceLocationText;

        /// <summary>Raised when the player marks a clue for the deduction board.</summary>
        public event Action<EvidenceData> MarkedForBoard;

        const string TabActiveClass = "journal-tab--active";
        const string LogLockedClass = "log--locked";
        const string DissonanceLine =
            "A lingering dissonance remains in your core — a feeling of unease washes over you.";

        static readonly string[] TabButtonNames =
            { "TabPhysical", "TabTestimony", "TabLogs", "TabDossier" };

        PanelRenderer _panelRenderer;
        readonly HashSet<string> _flags = new HashSet<string>();

        // Cached elements (re-queried on every UI reload).
        VisualElement _root;
        ScrollView _tabBody;
        Label _detailTitle;
        Label _detailType;
        Label _detailDesc;
        Button _markButton;
        readonly Button[] _tabButtons = new Button[4];

        // Interaction state.
        bool _isOpen;
        JournalTab _activeTab = JournalTab.Physical;
        ClueData _detailClue;
        float _previousTimeScale = 1f;
        int _uiVersion;

        public bool IsOpen => _isOpen;
        public JournalTab ActiveTab => _activeTab;

        void OnEnable()
        {
            _panelRenderer = GetComponent<PanelRenderer>();
            if (_panelRenderer != null) _panelRenderer.RegisterUIReloadCallback(OnUIReload);
            if (journal != null) journal.ClueRegistered += OnClueRegistered;
        }

        void OnDisable()
        {
            if (_panelRenderer != null) _panelRenderer.UnregisterUIReloadCallback(OnUIReload);
            if (journal != null) journal.ClueRegistered -= OnClueRegistered;
            if (_isOpen)
            {
                Time.timeScale = _previousTimeScale;
                _isOpen = false;
            }
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.gKey.wasPressedThisFrame)
            {
                if (_isOpen) Close();
                else Open();
                return;
            }

            if (!_isOpen) return;

            if (keyboard.escapeKey.wasPressedThisFrame) Close();
            else if (keyboard.digit1Key.wasPressedThisFrame) SelectTab(JournalTab.Physical);
            else if (keyboard.digit2Key.wasPressedThisFrame) SelectTab(JournalTab.Testimony);
            else if (keyboard.digit3Key.wasPressedThisFrame) SelectTab(JournalTab.Logs);
            else if (keyboard.digit4Key.wasPressedThisFrame) SelectTab(JournalTab.Dossier);
        }

        /// <summary>Swaps the clue store (e.g. when entering a new hub scene).</summary>
        public void SetJournal(HubJournal store)
        {
            if (journal == store) return;
            if (journal != null && isActiveAndEnabled)
                journal.ClueRegistered -= OnClueRegistered;
            journal = store;
            if (journal != null && isActiveAndEnabled)
                journal.ClueRegistered += OnClueRegistered;
            if (_isOpen) RebuildBody();
        }

        /// <summary>Provides the hub flag set used for dossier gating and the dissonance banner.</summary>
        public void SetFlags(ISet<string> flags)
        {
            _flags.Clear();
            if (flags != null) _flags.UnionWith(flags);
            if (_isOpen) RebuildBody();
        }

        /// <summary>Registers a runic log as collected. Idempotent; returns true when added.</summary>
        public bool RegisterLog(RunicLogData log)
        {
            if (log == null || collectedLogs.Contains(log)) return false;
            collectedLogs.Add(log);
            if (_isOpen && _activeTab == JournalTab.Logs) RebuildBody();
            return true;
        }

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f; // journal pauses the sim per GDD
            RefreshTabButtons();
            RebuildBody();
            ClearDetail();
            if (_root != null) _root.style.display = DisplayStyle.Flex;
            if (manageCursor)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            Time.timeScale = _previousTimeScale;
            if (_root != null) _root.style.display = DisplayStyle.None;
            if (manageCursor)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
            }
        }

        /// <summary>Switches the active tab (also bound to keys 1-4 and the header buttons).</summary>
        public void SelectTab(JournalTab tab)
        {
            if (_activeTab == tab) return;
            _activeTab = tab;
            RefreshTabButtons();
            RebuildBody();
            ClearDetail();
        }

        void OnClueRegistered(ClueData clue)
        {
            if (!_isOpen) return;
            if (_activeTab == JournalTab.Physical || _activeTab == JournalTab.Testimony)
                RebuildBody();
        }

        // --- Tab body ---------------------------------------------------------

        void RebuildBody()
        {
            if (_tabBody == null) return;
            _tabBody.Clear();
            switch (_activeTab)
            {
                case JournalTab.Physical:
                case JournalTab.Testimony:
                    BuildClueList();
                    break;
                case JournalTab.Logs:
                    BuildLogList();
                    break;
                case JournalTab.Dossier:
                    BuildDossierList();
                    break;
            }
        }

        void BuildClueList()
        {
            if (_activeTab == JournalTab.Testimony
                && JournalFilter.HasFlag(_flags, dissonanceFlag))
            {
                _tabBody.Add(BuildDissonanceBanner());
            }

            var clues = JournalFilter.CluesForTab(journal != null ? journal.Collected : null, _activeTab);
            if (clues.Count == 0)
            {
                AddEmptyLabel("No entries yet.");
                return;
            }
            foreach (var clue in clues)
            {
                var card = new Button();
                card.AddToClassList("journal-card");

                var name = new Label(clue.displayName);
                name.AddToClassList("journal-card-name");
                card.Add(name);

                var preview = new Label(JournalFilter.Preview(clue.description));
                preview.AddToClassList("journal-card-preview");
                card.Add(preview);

                var captured = clue;
                card.RegisterCallback<ClickEvent>(_ => ShowClueDetail(captured));
                _tabBody.Add(card);
            }
        }

        VisualElement BuildDissonanceBanner()
        {
            var banner = new VisualElement();
            banner.AddToClassList("dissonance-banner");

            var title = new Label("Lingering Dissonance");
            title.AddToClassList("dissonance-title");
            banner.Add(title);

            var line = new Label(DissonanceLine);
            line.AddToClassList("dissonance-line");
            banner.Add(line);

            if (!string.IsNullOrEmpty(dissonanceLocationText))
            {
                var location = new Label(dissonanceLocationText);
                location.AddToClassList("dissonance-location");
                banner.Add(location);
            }
            return banner;
        }

        void BuildLogList()
        {
            if (allLogs == null || allLogs.Length == 0)
            {
                AddEmptyLabel("No runic logs recorded.");
                return;
            }
            foreach (var log in allLogs)
            {
                if (log == null) continue;
                bool collected = JournalFilter.IsLogCollected(log, collectedLogs);
                string title = JournalFilter.LogTitle(log);
                string location = string.IsNullOrEmpty(log.location) ? "Unknown site" : log.location;
                string body = collected ? log.contents : JournalFilter.Mask(log.contents);

                var card = new Button();
                card.AddToClassList("journal-card");
                card.AddToClassList("log-card");
                if (!collected) card.AddToClassList(LogLockedClass);

                var name = new Label(title);
                name.AddToClassList("journal-card-name");
                card.Add(name);

                var meta = new Label(location);
                meta.AddToClassList("journal-card-meta");
                card.Add(meta);

                var preview = new Label(collected ? JournalFilter.Preview(body, 140) : body);
                preview.AddToClassList("journal-card-preview");
                card.Add(preview);

                var capturedTitle = title;
                var capturedLocation = location;
                var capturedBody = body;
                card.RegisterCallback<ClickEvent>(_ =>
                    ShowTextDetail(capturedTitle, capturedLocation, capturedBody));
                _tabBody.Add(card);
            }
        }

        void BuildDossierList()
        {
            if (dossiers == null || dossiers.Length == 0)
            {
                AddEmptyLabel("No suspects on record.");
                return;
            }
            int shown = 0;
            foreach (var entry in dossiers)
            {
                if (entry == null) continue;
                if (!JournalFilter.IsDossierVisible(entry.requiresFlag, _flags)) continue;
                shown++;

                var card = new Button();
                card.AddToClassList("journal-card");
                card.AddToClassList("dossier-card");

                var name = new Label(entry.suspectName);
                name.AddToClassList("journal-card-name");
                card.Add(name);

                if (!string.IsNullOrEmpty(entry.role))
                {
                    var role = new Label(entry.role);
                    role.AddToClassList("journal-card-meta");
                    card.Add(role);
                }

                var notes = new Label(JournalFilter.Preview(entry.notes));
                notes.AddToClassList("journal-card-preview");
                card.Add(notes);

                var captured = entry;
                card.RegisterCallback<ClickEvent>(_ =>
                    ShowTextDetail(captured.suspectName, captured.role, captured.notes));
                _tabBody.Add(card);
            }
            if (shown == 0) AddEmptyLabel("No suspects on record.");
        }

        void AddEmptyLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("empty-label");
            _tabBody.Add(label);
        }

        // --- Detail pane ------------------------------------------------------

        void ShowClueDetail(ClueData clue)
        {
            _detailClue = clue;
            if (_detailTitle != null) _detailTitle.text = clue.displayName;
            if (_detailType != null)
                _detailType.text = string.IsNullOrEmpty(clue.id)
                    ? clue.type.ToString()
                    : $"{clue.type} — {clue.id}";
            if (_detailDesc != null) _detailDesc.text = clue.description;
            if (_markButton != null) _markButton.style.display = DisplayStyle.Flex;
        }

        void ShowTextDetail(string title, string subtitle, string body)
        {
            _detailClue = null; // only clue entries can be marked for the board
            if (_detailTitle != null) _detailTitle.text = title ?? "";
            if (_detailType != null) _detailType.text = subtitle ?? "";
            if (_detailDesc != null) _detailDesc.text = body ?? "";
            if (_markButton != null) _markButton.style.display = DisplayStyle.None;
        }

        void ClearDetail()
        {
            _detailClue = null;
            if (_detailTitle != null) _detailTitle.text = "Clue Journal";
            if (_detailType != null) _detailType.text = "";
            if (_detailDesc != null) _detailDesc.text = "Select an entry.";
            if (_markButton != null) _markButton.style.display = DisplayStyle.None;
        }

        void OnMarkClicked()
        {
            if (_detailClue != null) MarkedForBoard?.Invoke(_detailClue);
        }

        // --- Tabs ---------------------------------------------------------------

        void RefreshTabButtons()
        {
            for (int i = 0; i < _tabButtons.Length; i++)
                if (_tabButtons[i] != null)
                    _tabButtons[i].EnableInClassList(TabActiveClass, i == (int)_activeTab);
        }

        // --- UI reload --------------------------------------------------------

        void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            if (_uiVersion == version) return;
            _uiVersion = version;

            _root = root.Q<VisualElement>("Journal-Root");
            _tabBody = root.Q<ScrollView>("TabBody");
            _detailTitle = root.Q<Label>("JournalTitle");
            _detailType = root.Q<Label>("JournalType");
            _detailDesc = root.Q<Label>("JournalDesc");
            _markButton = root.Q<Button>("MarkForBoardButton");
            if (_markButton != null)
                _markButton.RegisterCallback<ClickEvent>(_ => OnMarkClicked());

            for (int i = 0; i < _tabButtons.Length; i++)
            {
                _tabButtons[i] = root.Q<Button>(TabButtonNames[i]);
                int captured = i;
                if (_tabButtons[i] != null)
                    _tabButtons[i].RegisterCallback<ClickEvent>(_ => SelectTab((JournalTab)captured));
            }

            // Restore UI from cached state.
            if (_root != null)
                _root.style.display = _isOpen ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshTabButtons();
            RebuildBody();
            ClearDetail();
        }
    }
}

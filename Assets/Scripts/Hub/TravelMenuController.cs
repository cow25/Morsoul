using System;
using System.Collections.Generic;
using Morsoul.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Morsoul.Hub
{
    /// <summary>
    /// Carrier fast-travel menu (GDD §3 Transit Node). Sits on the same
    /// GameObject as a PanelRenderer, like DialogueController. Given a list of
    /// <see cref="TravelDestination"/> entries it shows a navigable list:
    /// W/S moves the selection, E confirms, Escape cancels; entries are also
    /// clickable. Player movement is suppressed through
    /// <see cref="HubContext.UiBlocked"/> while the menu is open.
    /// </summary>
    public class TravelMenuController : MonoBehaviour
    {
        /// <summary>Display labels per hub case id (0 = Lower Market, etc.).</summary>
        [Serializable]
        public class HubLabel
        {
            public int caseId;
            public string label;
        }

        /// <summary>Hub case ids offered by carriers, in menu order.</summary>
        public int[] hubCaseIds = { 0, 1, 2, 3, 4 };
        /// <summary>Per-hub display names; unlisted hubs fall back to the scene name.</summary>
        public HubLabel[] hubLabels =
        {
            new HubLabel { caseId = 0, label = "Asterveil — Lower Market" },
            new HubLabel { caseId = 1, label = "Asterveil — Lower Market" },
            new HubLabel { caseId = 2, label = "Asterveil — Cathedral Grounds" },
            new HubLabel { caseId = 3, label = "Whisperwood — Boundary Trail" },
            new HubLabel { caseId = 4, label = "Iron Hollow — Railhead" },
        };

        /// <summary>Fired when the player confirms a destination.</summary>
        public event Action<TravelDestination> DestinationSelected;
        /// <summary>Fired when the menu closes without a selection.</summary>
        public event Action Cancelled;

        const string SelectedClass = "travel-destination--selected";

        PanelRenderer _panelRenderer;
        VisualElement _root;
        VisualElement _list;
        readonly List<TravelDestination> _destinations = new List<TravelDestination>();
        readonly List<Button> _buttons = new List<Button>();
        int _selected;
        bool _isOpen;
        int _uiVersion;
        int _openFrame = -1;

        /// <summary>True while the menu is displayed (blocks hub movement).</summary>
        public bool IsOpen => _isOpen;
        /// <summary>Frame the menu last closed on; -1 = never.</summary>
        public int LastCloseFrame { get; private set; } = -1;
        /// <summary>Currently listed destinations (empty while closed).</summary>
        public IReadOnlyList<TravelDestination> Destinations => _destinations;
        /// <summary>Index of the highlighted destination.</summary>
        public int SelectedIndex => _selected;

        void OnEnable()
        {
            _panelRenderer = GetComponent<PanelRenderer>();
            if (_panelRenderer != null)
            {
                _panelRenderer.sortingOrder = 60;
                _panelRenderer.RegisterUIReloadCallback(OnUIReload);
            }
        }

        void OnDisable()
        {
            if (_panelRenderer != null) _panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        }

        void Update()
        {
            if (!_isOpen || Time.frameCount == _openFrame) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame) { Cancel(); return; }
            if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
                MoveSelection(-1);
            else if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
                MoveSelection(+1);
            else if (keyboard.eKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
                Confirm(_selected);
        }

        /// <summary>
        /// Collects the carrier's unlocked destinations from the GSM route
        /// table. Returns the list (also handed to <see cref="Show(List{TravelDestination})"/>
        /// by the carrier itself); empty when no Core/session or nothing is
        /// reachable.
        /// </summary>
        public List<TravelDestination> CollectDestinations(string currentScene)
        {
            var gsm = GameStateManager.Ensure();
            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            if (gsm == null || session == null) return new List<TravelDestination>();
            return TravelRules.CollectDestinations(
                gsm.routes, hubCaseIds, LabelMap(), session.CurrentCase, currentScene,
                Application.CanStreamedLevelBeLoaded,
                caseId => !GuildIntroRules.IsBriefingPending(session, caseId));
        }

        /// <summary>Opens the menu with the given destinations (no-op when empty).</summary>
        public void Show(List<TravelDestination> destinations)
        {
            if (destinations == null || destinations.Count == 0) return;
            _destinations.Clear();
            _destinations.AddRange(destinations);
            _selected = 0;
            _isOpen = true;
            _openFrame = Time.frameCount;
            if (_root != null) _root.style.display = DisplayStyle.Flex;
            RebuildList();
        }

        /// <summary>Confirms a destination, closes the menu and fires DestinationSelected.</summary>
        public void Confirm(int index)
        {
            if (index < 0 || index >= _destinations.Count) return;
            var destination = _destinations[index];
            Close();
            DestinationSelected?.Invoke(destination);
        }

        /// <summary>Closes without travelling; fires Cancelled.</summary>
        public void Cancel()
        {
            Close();
            Cancelled?.Invoke();
        }

        void Close()
        {
            _isOpen = false;
            LastCloseFrame = Time.frameCount;
            PauseMenuController.NotifyExternalModalClosed();
            _destinations.Clear();
            if (_root != null) _root.style.display = DisplayStyle.None;
        }

        void MoveSelection(int delta)
        {
            if (_destinations.Count == 0) return;
            _selected = ((_selected + delta) % _destinations.Count + _destinations.Count)
                        % _destinations.Count;
            ApplySelection();
        }

        Dictionary<int, string> LabelMap()
        {
            var map = new Dictionary<int, string>();
            if (hubLabels == null) return map;
            foreach (var entry in hubLabels)
                if (entry != null) map[entry.caseId] = entry.label;
            return map;
        }

        void RebuildList()
        {
            if (_list == null) return;
            _list.Clear();
            _buttons.Clear();
            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            for (int i = 0; i < _destinations.Count; i++)
            {
                int captured = i;
                var dest = _destinations[i];
                bool uneasy = session != null && dest.caseId >= 1 &&
                              session.HasFlag($"Case{dest.caseId}_UneaseActive");
                string label = uneasy ? $"{dest.label}  [Uneasy Feeling]" : dest.label;
                var button = new Button(() => Confirm(captured))
                {
                    text = label,
                };
                button.AddToClassList("travel-destination");
                _list.Add(button);
                _buttons.Add(button);
            }
            ApplySelection();
        }

        void ApplySelection()
        {
            for (int i = 0; i < _buttons.Count; i++)
                _buttons[i].EnableInClassList(SelectedClass, i == _selected);
        }

        void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            if (_uiVersion == version) return;
            _uiVersion = version;
            _root = root.Q<VisualElement>("TravelMenu-Root");
            _list = root.Q<VisualElement>("DestinationList");
            if (_root != null)
                _root.style.display = _isOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (_isOpen) RebuildList();
        }
    }
}

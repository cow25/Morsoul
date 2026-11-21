using System;
using System.Collections.Generic;
using Morsoul.Core;
using Morsoul.Data;
using Morsoul.Engine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Morsoul.Board
{
    /// <summary>
    /// UI overlay controller for the weighted deduction board
    /// (Algorithm.md active model). Renders collected clues as draggable
    /// nodes on a canvas; click-click draws a directed link, right-click on
    /// a link row severs it, the lamp shows the selected conclusion's band,
    /// and Space submits the selected unlocked conclusion. All rules live
    /// in <see cref="WeightedBoardSession"/> + Morsoul.Engine; this class
    /// only renders and forwards input.
    /// </summary>
    public class WeightedBoardController : MonoBehaviour
    {
        public CaseData caseData;
        public bool manageCursor = true;
        public bool openOnStart = false;
        public Func<bool> CanUse;

        bool Authorized => CanUse == null || CanUse();
        /// <summary>Portrait shown in the hint popup (Eliza by default).</summary>
        public Sprite hintPortrait;
        public string hintSpeaker = "Eliza";
        public string diagnosticSpeaker = "Internal Diagnostic";

        /// <summary>Raised after a DrawLink call with the progression events it produced.</summary>
        public event Action<IReadOnlyList<ProgressionEvent>> LinkResolved;
        /// <summary>Raised when an unaffordable error on Case 5 requests the Ending Resolver.</summary>
        public event Action EndingResolveRequested;
        /// <summary>Raised when a supported complete submission solves the board.</summary>
        public event Action<DeductionCertificate> BoardSolved;
        /// <summary>Raised whenever the session state changed (links, selection, focus).</summary>
        public event Action StateChanged;

        const string BannerErrorClass = "banner--error";
        const string BannerInfoClass = "banner--info";
        const string NodeSelectedClass = "wboard-node--selected";
        const string NodeHintClass = "wboard-node--hint";

        PanelRenderer _panelRenderer;
        WeightedBoardSession _session;
        EvidenceGraph _graph;
        readonly Dictionary<string, VisualElement> _nodes = new Dictionary<string, VisualElement>();
        readonly Dictionary<string, string> _clueNames = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly Dictionary<string, ClueData> _clueData = new Dictionary<string, ClueData>(StringComparer.Ordinal);

        VisualElement _root;
        VisualElement _canvas;
        VisualElement _linkList;
        VisualElement _conclusionList;
        VisualElement _feedbackBanner;
        VisualElement _hintPopup;
        VisualElement _hintPortrait;
        Label _caseTitleLabel;
        Label _errorCountLabel;
        Label _focusLabel;
        Label _lampLabel;
        Label _feedbackLabel;
        Label _hintLabel;
        Label _hintSpeaker;
        Label _hintText;
        Label _selectedClueTitle;
        Label _selectedClueMeta;
        Label _selectedClueDesc;
        Button _submitButton;
        Button _hintClose;

        bool _isOpen;
        CursorLockMode _prevCursorLock = CursorLockMode.None;
        bool _prevCursorVisible = true;
        string _inspectedClueId;
        string _pendingSource;
        string _dragLinkSource;
        Vector2 _dragLinkPointer;
        VisualElement _linkLayer;
        VisualElement _pinLayer;
        readonly Dictionary<string, VisualElement> _pins = new Dictionary<string, VisualElement>();
        string _hintTargetClue;
        string _bannerText;
        string _bannerHint;
        string _bannerClass;
        string _hintPopupText;
        int _uiVersion;

        public bool IsOpen => _isOpen;
        public VisualElement Root => _root;
        public event Action<VisualElement> BoardLoaded;
        /// <summary>Last frame the panel closed; lets callers avoid same-frame ESC overlap.</summary>
        public int LastCloseFrame { get; private set; } = -1;
        /// <summary>The headless session (null until SetCase builds one). Exposed for GameFlow/tests.</summary>
        public WeightedBoardSession Session => _session;
        public string GetClueDisplayName(string id) =>
            id != null && _clueNames.TryGetValue(id, out var name) ? name : id;

        void OnEnable()
        {
            _panelRenderer = GetComponent<PanelRenderer>();
            if (_panelRenderer != null)
            {
                _panelRenderer.sortingOrder = 50;
                _panelRenderer.RegisterUIReloadCallback(OnUIReload);
            }
        }

        void OnDisable()
        {
            if (_panelRenderer != null) _panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        }

        void Start()
        {
            if (caseData != null && _session == null) SetCase(caseData);
            if (openOnStart && _session != null) Open();
        }

        void Update()
        {
            if (!Authorized)
            {
                if (_isOpen) Close();
                return;
            }
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
                if (_hintPopupShowing) HideHintPopup();
                else if (_pendingSource != null) { _pendingSource = null; RefreshNodeStates(); }
                else Close();
            }
            else if (_hintPopupShowing
                     && (keyboard.eKey.wasPressedThisFrame
                         || keyboard.enterKey.wasPressedThisFrame
                         || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                HideHintPopup();
            }
            else if (keyboard.spaceKey.wasPressedThisFrame)
            {
                SubmitConclusion();
            }
        }

        /// <summary>
        /// Optional focus bridge. When wired (by GameFlow/Core), the source
        /// mirrors persistent Focus into the board state before every rule
        /// application and the sink writes it back afterwards. Unwired, the
        /// board state owns its own Focus fields (standalone/testing).
        /// </summary>
        public Func<(float energy, float max)> FocusSource;
        public Action<float, float> FocusSink;
        /// <summary>
        /// Lazy session factory: invoked by Open() when no session exists
        /// (e.g. the board panel in a hub scene, opened via H). Assign a
        /// delegate that builds the case/session — GameFlow wires it for
        /// CaseLoop; BoardHotkey wires it elsewhere. Null = no lazy init.
        /// </summary>
        public Func<bool> EnsureSession;

        /// <summary>
        /// (Re)builds the session for the case. Weighted cases need an
        /// EvidenceGraph; legacy slot/premise cases return false and the
        /// caller keeps using DeductionBoard. <paramref name="state"/> is the
        /// persisted per-case runtime state (SessionState.GetOrCreateBoardState);
        /// a fresh one is created for standalone/test use.
        /// </summary>
        public bool SetCase(CaseData data, BoardRuntimeState state = null)
        {
            caseData = data;
            _pendingSource = null;
            _hintTargetClue = null;
            HideBanner();
            HideHintPopup();

            if (data == null || data.evidenceGraph == null)
            {
                _session = null;
                _graph = null;
                return false;
            }

            var graph = data.evidenceGraph.BuildGraph();
            if (graph == null)
            {
                _session = null;
                _graph = null;
                var errors = data.evidenceGraph.Validate();
                Debug.LogError($"WeightedBoardController: evidence graph for '{data.title}' " +
                               $"failed validation — {(errors != null ? string.Join(" | ", errors) : "unknown error")}", this);
                return false;
            }

            _graph = graph;
            _session = new WeightedBoardSession(graph,
                state ?? new BoardRuntimeState { CaseId = data.caseId });
            SyncFocusIn();
            if (_isOpen) RefreshAll();
            return true;
        }

        void SyncFocusIn()
        {
            if (FocusSource == null || _session == null) return;
            var (energy, max) = FocusSource();
            _session.SyncFocus(energy, max);
        }

        void SyncFocusOut()
        {
            if (FocusSink == null || _session == null) return;
            FocusSink(_session.State.Focus, _session.State.MaxFocus);
        }

        /// <summary>
        /// Pushes the clues the player may use: id set + display-name lookup.
        /// Ids not in the case graph are ignored.
        /// </summary>
        public void SetClues(IEnumerable<ClueData> clues)
        {
            _clueNames.Clear();
            _clueData.Clear();
            var ids = new List<string>();
            if (clues != null)
                foreach (var clue in clues)
                {
                    if (clue == null || string.IsNullOrEmpty(clue.id)) continue;
                    _clueNames[clue.id] = string.IsNullOrEmpty(clue.displayName)
                        ? clue.id : clue.displayName;
                    _clueData[clue.id] = clue;
                    ids.Add(clue.id);
                }
            if (_session != null)
            {
                _session.SetVisibleClues(ids);
                foreach (var id in ids) _session.CollectClue(id);
            }
            if (_isOpen) RefreshAll();
        }

        public void Open()
        {
            if (!Authorized || PauseMenuController.AnyModalOrPauseOpen) return;
            // Re-run the lazy hook on every open so freshly collected clues
            // and focus state sync into a live session, not just the first.
            if (EnsureSession != null && !EnsureSession())
            {
                if (caseData != null) Debug.LogWarning(
                    $"WeightedBoardController: no usable evidence graph on '{caseData.title}'; board stays closed.", this);
                return;
            }
            if (_session == null)
            {
                if (caseData != null) Debug.LogWarning(
                    $"WeightedBoardController: no evidence graph on '{caseData.title}'; board stays closed.", this);
                return;
            }
            SyncFocusIn();
            _isOpen = true;
            _pendingSource = null;
            _session.Solve();
            RefreshAll();
            if (_root != null) _root.style.display = DisplayStyle.Flex;
            if (manageCursor)
            {
                _prevCursorLock = UnityEngine.Cursor.lockState;
                _prevCursorVisible = UnityEngine.Cursor.visible;
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
            TutorialOverlayController.TryShow("TUT_BOARD_BASICS");
            if (caseData != null && caseData.caseId >= 1)
                TutorialOverlayController.TryShow("TUT_BOARD_LOGIC");
        }

        public void Close()
        {
            if (_isOpen) LastCloseFrame = Time.frameCount;
            _isOpen = false;
            _pendingSource = null;
            if (_root != null) _root.style.display = DisplayStyle.None;
            if (manageCursor)
            {
                UnityEngine.Cursor.lockState = _prevCursorLock;
                UnityEngine.Cursor.visible = _prevCursorVisible;
            }
            PauseMenuController.NotifyExternalModalClosed();
        }

        // --- Interactions (public for tests) ---------------------------------

        /// <summary>
        /// Draws a directed link and applies the progression events:
        /// focus charge on error, board clear/lockout on unaffordable
        /// errors, hint tiers, and the Case-5 Ending Resolver request.
        /// </summary>
        public IReadOnlyList<ProgressionEvent> DrawLink(string sourceId, string targetId)
        {
            if (_session == null || !Authorized)
                return new List<ProgressionEvent>
                {
                    new ProgressionEvent { Kind = ProgressionEventKind.InvalidInput },
                };
            SyncFocusIn();
            var link = new DirectedEdge(sourceId, targetId);
            var events = _session.DrawLink(link);
            ApplyLinkEvents(events, link);
            return events;
        }

        /// <summary>Severs a drawn link; free and non-destructive.</summary>
        public bool SeverLink(string sourceId, string targetId)
        {
            if (_session == null || !Authorized) return false;
            bool removed = _session.SeverLink(new DirectedEdge(sourceId, targetId));
            if (removed)
            {
                _session.Solve();
                RefreshAll();
                StateChanged?.Invoke();
            }
            return removed;
        }

        public bool SelectConclusion(string conclusionId)
        {
            if (_session == null || !Authorized || !_session.SelectConclusion(conclusionId)) return false;
            RefreshConclusions();
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>Submits the selected conclusion (Space / submit button).</summary>
        public IReadOnlyList<ProgressionEvent> SubmitConclusion()
        {
            if (_session == null || !Authorized) return new List<ProgressionEvent>();
            SyncFocusIn();
            var events = _session.Submit(out var cert);
            SyncFocusOut();
            bool solved = false;
            foreach (var e in events)
            {
                switch (e.Kind)
                {
                    case ProgressionEventKind.CaseComplete:
                        ShowBanner(BannerInfoClass, AuthoredSubmissionFeedback(
                            e.ConclusionId, "Conclusion supported — the case clicks into place."));
                        solved = true;
                        break;
                    case ProgressionEventKind.BoardAlreadySolved:
                        ShowBanner(BannerInfoClass, "Conclusion already confirmed.");
                        break;
                    case ProgressionEventKind.ConclusionLocked:
                        ShowBanner(BannerInfoClass, AuthoredLockedFeedback(e.ConclusionId));
                        break;
                    case ProgressionEventKind.WrongConclusionRejected:
                        ShowBanner(BannerInfoClass, AuthoredSubmissionFeedback(
                            e.ConclusionId,
                            "The board doesn't support that conclusion. Keep linking."));
                        break;
                    case ProgressionEventKind.PartialConclusionFeedback:
                        ShowBanner(BannerInfoClass, AuthoredSubmissionFeedback(
                            e.ConclusionId,
                            "That explains part of the evidence, but not the whole case."));
                        break;
                    case ProgressionEventKind.InvalidInput:
                        ShowBanner(BannerErrorClass, "That submission can't be evaluated.");
                        break;
                }
            }
            RefreshAll();
            StateChanged?.Invoke();
            if (solved) BoardSolved?.Invoke(cert);
            return events;
        }

        // --- Progression-event application --------------------------------------

        string AuthoredSubmissionFeedback(string conclusionId, string fallback)
        {
            string authored = caseData != null && caseData.evidenceGraph != null
                ? caseData.evidenceGraph.GetConclusionSubmissionFeedback(conclusionId)
                : null;
            return string.IsNullOrEmpty(authored) ? fallback : authored;
        }

        string AuthoredLockedFeedback(string conclusionId)
        {
            var graphData = caseData != null ? caseData.evidenceGraph : null;
            if (graphData != null &&
                graphData.caseId == 0 &&
                conclusionId == graphData.completeConclusionId &&
                !string.IsNullOrEmpty(graphData.tutorialLockedConclusionFeedback))
                return graphData.tutorialLockedConclusionFeedback;
            return "That conclusion is still locked — the evidence doesn't reach it yet.";
        }

        string AuthoredTutorialLinkFeedback(DirectedEdge attemptedLink)
        {
            var graphData = caseData != null ? caseData.evidenceGraph : null;
            string authored = graphData != null
                ? graphData.GetTutorialLinkFeedback(attemptedLink)
                : null;
            return string.IsNullOrEmpty(authored)
                ? "That connection isn't part of the manual override chain."
                : authored;
        }

        void ApplyLinkEvents(IReadOnlyList<ProgressionEvent> events, DirectedEdge attemptedLink)
        {
            bool resolveEnding = false;
            foreach (var e in events)
            {
                switch (e.Kind)
                {
                    case ProgressionEventKind.InvalidInput:
                        ShowBanner(BannerErrorClass, "That link can't be drawn here.");
                        break;
                    case ProgressionEventKind.TutorialLinkRejected:
                        ShowBanner(BannerInfoClass, AuthoredTutorialLinkFeedback(attemptedLink));
                        break;
                    case ProgressionEventKind.Blocked:
                        ShowBanner(BannerErrorClass,
                            $"Linking locked — Focus below {ProgressionConstants.LinkingUnlockFocus:0}. Rest at a bench or recover Focus before continuing.");
                        break;
                    case ProgressionEventKind.DuplicateLink:
                        ShowBanner(BannerInfoClass, "Link already on the board.");
                        break;
                    case ProgressionEventKind.LinkOk:
                        ShowBanner(BannerInfoClass, "Connection recorded on the board.");
                        break;
                    case ProgressionEventKind.LinkWrong:
                        ShowBanner(BannerErrorClass,
                            $"The connection doesn't hold. (-{ProgressionConstants.ErrorFocusCost:0} Focus)");
                        break;
                    case ProgressionEventKind.LinksCleared:
                        ShowBanner(BannerErrorClass,
                            $"Focus can't absorb the error — the board is wiped and linking is locked until Focus reaches {ProgressionConstants.LinkingUnlockFocus:0}.");
                        break;
                    case ProgressionEventKind.ResolveEnding:
                        resolveEnding = true;
                        break;
                    case ProgressionEventKind.HintTierFired:
                        ApplyHintTier(e);
                        break;
                }
            }
            if (_session != null) _session.Solve();
            SyncFocusOut();
            RefreshAll();
            LinkResolved?.Invoke(events);
            StateChanged?.Invoke();
            if (resolveEnding) EndingResolveRequested?.Invoke();
        }

        void ApplyHintTier(ProgressionEvent e)
        {
            switch (e.Tier)
            {
                case 1:
                    ShowBanner(BannerInfoClass,
                        "Pause and re-read your Clue Notes — check tools, mechanisms, and witness timelines.");
                    break;
                case 2:
                case 3:
                    if (e.HintClueCollected && !string.IsNullOrEmpty(e.HintClueId))
                    {
                        _hintTargetClue = e.HintClueId;
                        ShowBanner(BannerInfoClass, "A clue lights up on the board — follow it.");
                    }
                    else
                    {
                        ShowHintPopup("Something's missing — there may still be evidence " +
                                      "you haven't collected for this case.");
                    }
                    break;
            }
        }

        // --- Rendering -------------------------------------------------------

        void RefreshAll()
        {
            if (_caseTitleLabel != null)
                _caseTitleLabel.text = caseData != null ? caseData.title : "";
            RefreshStatusRow();
            RebuildNodes();
            RebuildLinkList();
            RefreshConclusions();
            RefreshLamp();
            RefreshClueInspector();
            ApplyBanner();
            ApplyHintPopup();
        }

        void RefreshStatusRow()
        {
            if (_session == null) return;
            if (_errorCountLabel != null)
                _errorCountLabel.text = $"Errors: {_session.State.CaseErrorCount}" +
                                        (_session.State.LinkingLockedOut ? "  [LINKING LOCKED]" : "");
            if (_focusLabel != null)
                _focusLabel.text = $"Focus {_session.State.Focus:0}/{_session.State.MaxFocus:0}";
        }

        void RebuildNodes()
        {
            if (_canvas == null) return;
            _canvas.Clear();
            _nodes.Clear();
            _pins.Clear();
            _linkLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            _linkLayer.style.position = Position.Absolute;
            _linkLayer.style.left = 0;
            _linkLayer.style.top = 0;
            _linkLayer.style.right = 0;
            _linkLayer.style.bottom = 0;
            _linkLayer.generateVisualContent += OnGenerateLinkVisuals;
            _pinLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            _pinLayer.style.position = Position.Absolute;
            _pinLayer.style.left = 0;
            _pinLayer.style.top = 0;
            _pinLayer.style.right = 0;
            _pinLayer.style.bottom = 0;
            if (_session == null)
            {
                _canvas.Add(_linkLayer);
                _canvas.Add(_pinLayer);
                return;
            }

            // Build a shuffled pool of jittered grid slots for any unpositioned clues
            // so cards start scattered and players organize them manually.
            var unpositioned = new List<string>();
            foreach (string id in _session.State.CollectedClueIds)
                if (!_session.TryGetNodePosition(id, out _)) unpositioned.Add(id);

            var slots = new List<Vector2>();
            if (unpositioned.Count > 0)
            {
                int slotCount = Mathf.Max(9, unpositioned.Count);
                for (int s = 0; s < slotCount; s++)
                    slots.Add(new Vector2(18 + (s % 3) * 185, 20 + (s / 3) * 92));
                for (int s = slots.Count - 1; s > 0; s--)
                {
                    int j = UnityEngine.Random.Range(0, s + 1);
                    (slots[s], slots[j]) = (slots[j], slots[s]);
                }
            }

            int slotIdx = 0;
            foreach (string clueId in _session.State.CollectedClueIds)
            {
                var node = new VisualElement();
                node.AddToClassList("wboard-node");
                var nameLabel = new Label(NameOf(clueId));
                nameLabel.AddToClassList("wboard-node-name");
                node.Add(nameLabel);

                // Restore saved position or assign a randomized, non-overlapping slot.
                if (_session.TryGetNodePosition(clueId, out var pos))
                {
                    node.style.left = pos.x;
                    node.style.top = pos.y;
                }
                else
                {
                    Vector2 baseSlot = slots[slotIdx++];
                    float rx = Mathf.Max(8f, baseSlot.x + UnityEngine.Random.Range(-12f, 22f));
                    float ry = Mathf.Max(8f, baseSlot.y + UnityEngine.Random.Range(-10f, 16f));
                    node.style.left = rx;
                    node.style.top = ry;
                    _session.SetNodePosition(clueId, rx, ry);
                }

                var captured = clueId;
                RegisterNodeInteractions(node, captured);
                node.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    ClampNodeElement(node, captured);
                    SyncPinPosition(captured, node);
                    _linkLayer?.MarkDirtyRepaint();
                });
                _nodes[clueId] = node;
                _canvas.Add(node);
                var pin = CreatePinElement();
                _pins[clueId] = pin;
                _pinLayer.Add(pin);
            }
            _canvas.Add(_linkLayer);
            _canvas.Add(_pinLayer);
            SyncPinPositions();
            RefreshNodeStates();
        }

        static VisualElement CreatePinElement()
        {
            var pin = new VisualElement { pickingMode = PickingMode.Ignore };
            pin.style.position = Position.Absolute;
            pin.style.width = 12f;
            pin.style.height = 12f;
            pin.style.borderTopLeftRadius = 6f;
            pin.style.borderTopRightRadius = 6f;
            pin.style.borderBottomLeftRadius = 6f;
            pin.style.borderBottomRightRadius = 6f;
            pin.style.backgroundColor = new Color(0.84f, 0.16f, 0.16f, 1f);
            pin.style.borderTopColor = new Color(0.42f, 0.05f, 0.05f, 1f);
            pin.style.borderRightColor = new Color(0.42f, 0.05f, 0.05f, 1f);
            pin.style.borderBottomColor = new Color(0.42f, 0.05f, 0.05f, 1f);
            pin.style.borderLeftColor = new Color(0.42f, 0.05f, 0.05f, 1f);
            pin.style.borderTopWidth = 1.5f;
            pin.style.borderRightWidth = 1.5f;
            pin.style.borderBottomWidth = 1.5f;
            pin.style.borderLeftWidth = 1.5f;
            return pin;
        }

        void RegisterNodeInteractions(VisualElement node, string clueId)
        {
            Vector2 dragStart = default;
            Vector2 nodeStart = default;
            bool dragging = false;
            bool linking = false;

            node.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 1 || (evt.button == 0 && evt.shiftKey))
                {
                    linking = true;
                    _dragLinkSource = clueId;
                    _dragLinkPointer = _canvas != null ? _canvas.WorldToLocal(evt.position) : (Vector2)evt.position;
                    node.CapturePointer(evt.pointerId);
                    _linkLayer?.MarkDirtyRepaint();
                    evt.StopPropagation();
                    return;
                }
                if (evt.button != 0) return;
                dragStart = evt.position;
                nodeStart = new Vector2(node.style.left.value.value, node.style.top.value.value);
                dragging = false;
                linking = false;
                node.CapturePointer(evt.pointerId);
            });
            node.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!node.HasPointerCapture(evt.pointerId)) return;
                if (linking)
                {
                    _dragLinkPointer = _canvas != null ? _canvas.WorldToLocal(evt.position) : (Vector2)evt.position;
                    _linkLayer?.MarkDirtyRepaint();
                    return;
                }
                var delta = (Vector2)evt.position - dragStart;
                if (!dragging && delta.magnitude > 4f) dragging = true;
                if (dragging)
                {
                    var clamped = ClampPositionToCanvas(node, nodeStart + delta);
                    node.style.left = clamped.x;
                    node.style.top = clamped.y;
                    SyncPinPosition(clueId, node);
                    _linkLayer?.MarkDirtyRepaint();
                }
            });
            node.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!node.HasPointerCapture(evt.pointerId)) return;
                if (linking)
                {
                    node.ReleasePointer(evt.pointerId);
                    linking = false;
                    _dragLinkSource = null;
                    string targetId = FindNodeAt(evt.position);
                    if (targetId != null && targetId != clueId)
                        DrawLink(clueId, targetId);
                    _linkLayer?.MarkDirtyRepaint();
                    return;
                }
                if (evt.button != 0) return;
                node.ReleasePointer(evt.pointerId);
                if (dragging)
                {
                    _session.SetNodePosition(clueId, node.style.left.value.value, node.style.top.value.value);
                    SyncPinPosition(clueId, node);
                    _linkLayer?.MarkDirtyRepaint();
                    StateChanged?.Invoke();
                }
                else
                {
                    OnNodeClicked(clueId);
                }
            });
        }

        void OnNodeClicked(string clueId)
        {
            if (_session == null) return;
            _inspectedClueId = clueId;
            RefreshClueInspector();
            if (_pendingSource == null)
            {
                _pendingSource = clueId;
                RefreshNodeStates();
                return;
            }
            if (_pendingSource == clueId)
            {
                _pendingSource = null;
                RefreshNodeStates();
                return;
            }
            string source = _pendingSource;
            _pendingSource = null;
            DrawLink(source, clueId);
        }

        void RefreshClueInspector()
        {
            if (_selectedClueTitle == null || _selectedClueDesc == null) return;
            if (string.IsNullOrEmpty(_inspectedClueId) || !_clueData.TryGetValue(_inspectedClueId, out var clue) || clue == null)
            {
                _selectedClueTitle.text = "Select a Clue Card to inspect details";
                if (_selectedClueMeta != null) _selectedClueMeta.text = "";
                _selectedClueDesc.text = "Click any card to read its highlighted forensic notes, then click a target card (or Right-Drag) to connect Cause → Effect.";
                return;
            }
            _selectedClueTitle.text = string.IsNullOrEmpty(clue.displayName) ? clue.id : clue.displayName;
            if (_selectedClueMeta != null)
            {
                string tool = clue.requiredTool != Tool.None ? $" • {ClueHighlightFormatter.FriendlyTool(clue.requiredTool)}" : "";
                _selectedClueMeta.text = $"{ClueHighlightFormatter.FriendlyCategory(clue)}{tool}";
            }
            string playerName = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session.PlayerName : "";
            _selectedClueDesc.text = ClueHighlightFormatter.FormatDescription(clue, playerName, darkBackground: false);
        }

        void RefreshNodeStates()
        {
            foreach (var pair in _nodes)
            {
                var node = pair.Value;
                node.EnableInClassList(NodeSelectedClass, pair.Key == _pendingSource);
                node.EnableInClassList(NodeHintClass, pair.Key == _hintTargetClue);
            }
        }

        void RebuildLinkList()
        {
            if (_linkList == null) return;
            _linkList.Clear();
            if (_session == null) return;
            foreach (var link in _session.State.Links)
            {
                var row = new VisualElement();
                row.AddToClassList("wboard-link-row");
                if (_graph != null && !_graph.IsAuthoredLink(link))
                    row.AddToClassList("wboard-link-row--invented");

                var text = new Label($"{NameOf(link.sourceId)}  →  {NameOf(link.targetId)}");
                text.AddToClassList("wboard-link-text");
                row.Add(text);

                var captured = link;
                row.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 1)
                    {
                        SeverLink(captured.sourceId, captured.targetId);
                        evt.StopPropagation();
                    }
                });
                _linkList.Add(row);
            }
            _linkLayer?.MarkDirtyRepaint();
        }

        Vector2 ClampPositionToCanvas(VisualElement node, Vector2 pos)
        {
            if (_canvas == null)
                return new Vector2(Mathf.Max(4f, pos.x), Mathf.Max(8f, pos.y));
            float cw = _canvas.layout.width;
            float ch = _canvas.layout.height;
            float nw = node.layout.width > 1f ? node.layout.width : 155f;
            float nh = node.layout.height > 1f ? node.layout.height : 48f;
            float maxX = cw > nw + 8f ? cw - nw - 4f : Mathf.Max(4f, pos.x);
            float maxY = ch > nh + 12f ? ch - nh - 4f : Mathf.Max(8f, pos.y);
            return new Vector2(Mathf.Clamp(pos.x, 4f, maxX), Mathf.Clamp(pos.y, 8f, maxY));
        }

        void ClampNodeElement(VisualElement node, string clueId)
        {
            if (_canvas == null || _canvas.layout.width <= 1f || _canvas.layout.height <= 1f) return;
            float left = node.style.left.keyword == StyleKeyword.Null || node.style.left.keyword == StyleKeyword.Undefined
                ? node.resolvedStyle.left : node.style.left.value.value;
            float top = node.style.top.keyword == StyleKeyword.Null || node.style.top.keyword == StyleKeyword.Undefined
                ? node.resolvedStyle.top : node.style.top.value.value;
            var clamped = ClampPositionToCanvas(node, new Vector2(left, top));
            if (Mathf.Abs(clamped.x - left) > 0.5f || Mathf.Abs(clamped.y - top) > 0.5f)
            {
                node.style.left = clamped.x;
                node.style.top = clamped.y;
                _session?.SetNodePosition(clueId, clamped.x, clamped.y);
            }
        }

        string FindNodeAt(Vector2 worldPos)
        {
            foreach (var pair in _nodes)
                if (pair.Value.worldBound.Contains(worldPos))
                    return pair.Key;
            return null;
        }

        static Vector2 NodePinPos(VisualElement node)
        {
            float w = node.resolvedStyle.width > 0 ? node.resolvedStyle.width : 150f;
            float left = node.style.left.keyword == StyleKeyword.Null || node.style.left.keyword == StyleKeyword.Undefined
                ? node.resolvedStyle.left
                : node.style.left.value.value;
            float top = node.style.top.keyword == StyleKeyword.Null || node.style.top.keyword == StyleKeyword.Undefined
                ? node.resolvedStyle.top
                : node.style.top.value.value;
            return new Vector2(left + w * 0.5f, top);
        }

        void SyncPinPosition(string clueId, VisualElement node)
        {
            if (!_pins.TryGetValue(clueId, out var pin)) return;
            Vector2 p = NodePinPos(node);
            pin.style.left = p.x - 6f;
            pin.style.top = p.y - 6f;
        }

        void SyncPinPositions()
        {
            foreach (var pair in _nodes)
                SyncPinPosition(pair.Key, pair.Value);
        }

        void OnGenerateLinkVisuals(MeshGenerationContext ctx)
        {
            if (_session == null) return;
            var painter = ctx.painter2D;
            painter.lineWidth = 2.5f;
            painter.lineCap = LineCap.Round;
            foreach (var link in _session.State.Links)
            {
                if (!_nodes.TryGetValue(link.sourceId, out var srcNode) ||
                    !_nodes.TryGetValue(link.targetId, out var dstNode)) continue;
                DrawLine(painter, NodePinPos(srcNode), NodePinPos(dstNode), new Color(1f, 1f, 1f, 0.92f));
            }
            if (_dragLinkSource != null && _nodes.TryGetValue(_dragLinkSource, out var dragSrc))
                DrawLine(painter, NodePinPos(dragSrc), _dragLinkPointer, new Color(1f, 1f, 1f, 0.65f));
        }

        static void DrawLine(Painter2D painter, Vector2 from, Vector2 to, Color color)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < 4f) return;
            painter.strokeColor = color;
            painter.BeginPath();
            painter.MoveTo(from);
            painter.LineTo(to);

            Vector2 dir = delta.normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            Vector2 tip = to - dir * 8f;
            Vector2 wingA = tip - dir * 10f + perp * 6f;
            Vector2 wingB = tip - dir * 10f - perp * 6f;
            painter.MoveTo(wingA);
            painter.LineTo(tip);
            painter.LineTo(wingB);
            painter.Stroke();
        }

        /// <summary>Arranges all board nodes into a clean grid and updates link lines.</summary>
        public void AutoAlignNodes()
        {
            if (_session == null) return;
            int idx = 0;
            foreach (var pair in _nodes)
            {
                float x = 24f + (idx % 3) * 200f;
                float y = 24f + (idx / 3) * 116f;
                pair.Value.style.left = x;
                pair.Value.style.top = y;
                _session.SetNodePosition(pair.Key, x, y);
                idx++;
            }
            SyncPinPositions();
            _linkLayer?.MarkDirtyRepaint();
            StateChanged?.Invoke();
        }

        void RefreshConclusions()
        {
            if (_conclusionList == null) return;
            _conclusionList.Clear();
            if (_session == null) return;
            foreach (var conclusion in _session.UnlockedConclusions())
            {
                var card = new Button();
                card.AddToClassList("wboard-conclusion");
                if (conclusion.id == _session.SelectedConclusionId)
                    card.AddToClassList("wboard-conclusion--selected");
                var text = new Label(conclusion.text);
                text.AddToClassList("wboard-conclusion-text");
                card.Add(text);
                var captured = conclusion.id;
                card.RegisterCallback<ClickEvent>(_ => SelectConclusion(captured));
                _conclusionList.Add(card);
            }
        }

        void RefreshLamp()
        {
            if (_lampLabel == null || _session == null) return;
            var cert = _session.LastCertificate ?? _session.Solve();
            _lampLabel.RemoveFromClassList("wboard-lamp--cold");
            _lampLabel.RemoveFromClassList("wboard-lamp--warm");
            _lampLabel.RemoveFromClassList("wboard-lamp--hot");
            string cls;
            string name;
            switch (cert.Band)
            {
                case SupportBand.Hot: cls = "wboard-lamp--hot"; name = "Hot"; break;
                case SupportBand.Warm: cls = "wboard-lamp--warm"; name = "Warm"; break;
                default: cls = "wboard-lamp--cold"; name = "Cold"; break;
            }
            _lampLabel.AddToClassList(cls);
            _lampLabel.text = $"Lamp: {name}";
        }

        string NameOf(string clueId) =>
            clueId != null && _clueNames.TryGetValue(clueId, out string n) ? n : clueId ?? "?";

        // --- Banner / hint popup ---------------------------------------------

        void ShowBanner(string bannerClass, string message, string hint = null)
        {
            _bannerClass = bannerClass;
            _bannerText = message;
            _bannerHint = hint;
            ApplyBanner();
        }

        void HideBanner()
        {
            _bannerClass = _bannerText = _bannerHint = null;
            ApplyBanner();
        }

        void ApplyBanner()
        {
            if (_feedbackBanner == null) return;
            _feedbackBanner.RemoveFromClassList(BannerErrorClass);
            _feedbackBanner.RemoveFromClassList(BannerInfoClass);
            if (!string.IsNullOrEmpty(_bannerClass)) _feedbackBanner.AddToClassList(_bannerClass);
            if (_feedbackLabel != null) _feedbackLabel.text = _bannerText ?? "";
            if (_hintLabel != null)
            {
                bool hasHint = !string.IsNullOrEmpty(_bannerHint);
                _hintLabel.text = _bannerHint ?? "";
                _hintLabel.style.display = hasHint ? DisplayStyle.Flex : DisplayStyle.None;
            }
            _feedbackBanner.style.display = string.IsNullOrEmpty(_bannerText)
                ? DisplayStyle.None : DisplayStyle.Flex;
        }

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
                        ? diagnosticSpeaker : hintSpeaker;
                if (_hintText != null) _hintText.text = _hintPopupText;
            }
            _hintPopup.style.display = _hintPopupShowing ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // --- UI reload --------------------------------------------------------

        void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            if (_uiVersion == version) return;
            _uiVersion = version;

            _root = root.Q<VisualElement>("WeightedBoard-Root");
            _canvas = root.Q<VisualElement>("NodeCanvas");
            if (_canvas != null)
                _canvas.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    SyncPinPositions();
                    _linkLayer?.MarkDirtyRepaint();
                });
            _linkList = root.Q<VisualElement>("LinkList");
            _conclusionList = root.Q<VisualElement>("ConclusionList");
            _feedbackBanner = root.Q<VisualElement>("FeedbackBanner");
            _hintPopup = root.Q<VisualElement>("HintPopup");
            _hintPortrait = root.Q<VisualElement>("HintPortrait");
            _caseTitleLabel = root.Q<Label>("CaseTitleLabel");
            _errorCountLabel = root.Q<Label>("ErrorCountLabel");
            _focusLabel = root.Q<Label>("FocusLabel");
            _lampLabel = root.Q<Label>("LampLabel");
            _feedbackLabel = root.Q<Label>("FeedbackLabel");
            _hintLabel = root.Q<Label>("HintLabel");
            _hintSpeaker = root.Q<Label>("HintSpeaker");
            _hintText = root.Q<Label>("HintText");
            _selectedClueTitle = root.Q<Label>("SelectedClueTitle");
            _selectedClueMeta = root.Q<Label>("SelectedClueMeta");
            _selectedClueDesc = root.Q<Label>("SelectedClueDesc");

            _submitButton = root.Q<Button>("SubmitButton");
            if (_submitButton != null)
                _submitButton.RegisterCallback<ClickEvent>(_ => SubmitConclusion());
            _hintClose = root.Q<Button>("HintClose");
            if (_hintClose != null)
                _hintClose.RegisterCallback<ClickEvent>(_ => HideHintPopup());
            var closeBoardButton = root.Q<Button>("CloseBoardButton");
            if (closeBoardButton != null)
                closeBoardButton.RegisterCallback<ClickEvent>(_ => Close());
            var autoAlignButton = root.Q<Button>("AutoAlignButton");
            if (autoAlignButton != null)
                autoAlignButton.RegisterCallback<ClickEvent>(_ => AutoAlignNodes());

            if (_root != null)
                _root.style.display = _isOpen ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshAll();
            BoardLoaded?.Invoke(_root);
        }
    }
}

using System;
using System.Collections.Generic;
using Morsoul.Board;
using Morsoul.Core;
using Morsoul.Data;
using Morsoul.Dialogue;
using Morsoul.Engine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Morsoul.Flow
{
    /// <summary>
    /// Multi-stage Case 5 Deduction Board controller for the Guild's emergency
    /// report to the Empirical Guards. Drives Stage 1 (Four-District Audit),
    /// Stage 2 (Guard Telemetry &amp; Overseer Doctrine), and Stage 3 (Final
    /// Verdict synthesizing Stage 1/2 conclusions with optional Runic Logs),
    /// with Eliza brainstorming dialogues between stages and grave Focus drain
    /// (25 per wrong connection) leading to Normal (2), True (3), or Bad (1) endings.
    /// </summary>
    public class Case5ReportController : MonoBehaviour
    {
        const float ErrorFocusDrain = 25f;

        struct NodeSpec
        {
            public string Id;
            public string Title;
            public string Desc;
            public bool IsValid;
        }

        struct ConclusionSpec
        {
            public string Id;
            public string Text;
            public int Kind; // 0 = wrong, 1 = stage pass, 2 = normal ending, 3 = true ending
        }

        GameFlow _flow;
        WeightedBoardController _standardBoard;
        DialogueController _dialogue;
        PanelRenderer _panelRenderer;

        VisualElement _root;
        VisualElement _canvas;
        VisualElement _linkLayer;
        VisualElement _pinLayer;
        VisualElement _linkList;
        VisualElement _conclusionList;
        VisualElement _feedbackBanner;
        Label _caseTitleLabel;
        Label _errorCountLabel;
        Label _focusLabel;
        Label _feedbackLabel;
        Label _selectedClueTitle;
        Label _selectedClueMeta;
        Label _selectedClueDesc;
        Button _submitButton;

        readonly Dictionary<string, VisualElement> _nodes = new Dictionary<string, VisualElement>();
        readonly Dictionary<string, VisualElement> _pins = new Dictionary<string, VisualElement>();
        readonly List<(string src, string dst)> _links = new List<(string, string)>();
        readonly Dictionary<string, NodeSpec> _specs = new Dictionary<string, NodeSpec>();
        readonly Dictionary<(string src, string dst), string> _stageSolutionEdges = new Dictionary<(string, string), string>();
        readonly List<ConclusionSpec> _conclusions = new List<ConclusionSpec>();
        string _inspectedNodeId;

        int _stage = 1;
        int _errors;
        float _focus = 100f;
        float _maxFocus = 100f;
        int _runicLogs;
        string _pendingSource;
        string _selectedConclusionId;
        string _dragLinkSource;
        Vector2 _dragLinkPointer;
        bool _active;
        public bool IsActive => _active;
        bool _waitingDialogue;
        int _uiVersion = -1;
        Action _activeDialogueOnEnd;
        DialogueData _activeDialogueData;
        Func<bool> _modalBlocker;

        public void Begin(GameFlow flow, WeightedBoardController board, DialogueController dialogue)
        {
            _flow = flow;
            _standardBoard = board;
            _dialogue = dialogue;

            var core = CoreRuntime.Instance;
            _maxFocus = core != null ? core.Focus.Maximum : 100f;
            float current = core != null ? core.Focus.Percent : _maxFocus;
            _focus = Mathf.Clamp(Mathf.Max(current, 60f), 0f, _maxFocus);
            core?.Focus.Restore(_focus, core.Focus.StrainedScanUsed, _maxFocus);
            _runicLogs = core != null ? core.Session.RunicLogCount : 0;

            _modalBlocker ??= () => _active;
            PauseMenuController.RegisterExternalModal(_modalBlocker);

            if (_standardBoard != null)
            {
                _standardBoard.Close();
                _standardBoard.enabled = false;
                _panelRenderer = _standardBoard.GetComponent<PanelRenderer>();
            }
            if (_panelRenderer == null)
                _panelRenderer = FindAnyObjectByType<PanelRenderer>(FindObjectsInactive.Include);

            if (_panelRenderer != null)
                _panelRenderer.RegisterUIReloadCallback(OnUIReload);

            _active = true;
            _stage = 1;
            _errors = 0;

            PlayBrainstormDialogue(
                "D_Case5_Stage1_Intro",
                () => OpenStage(1),
                ("Eliza", "Ovan's scouts gave us their full district files. Let's connect the four Guild audits first to prove this is one coordinated operation."),
                ("Golem", "[WARNING]: Focus drain per error is elevated under crisis conditions. Link only verified connections."));
        }

        void Update()
        {
            if (!_active || _waitingDialogue) return;
            var kb = Keyboard.current;
            if (kb != null && kb.f2Key.wasPressedThisFrame)
                ShowDevHint();
        }

        public string GetDevHintText() => _stage switch
        {
            1 => "[DEV F2] Stage 1 (Cause → Effect): Cathedral Plate → Whisperwood Siphon → Iron Hollow Breach, plus Market Solvent → Iron Hollow Breach; then select 'Coordinated First-Age Strike'.",
            2 => "[DEV F2] Stage 2 (Cause → Effect): Eliza's Siphon Readout → Inverted Siphon Pylons → Guard Seismic Log → Overseer's Cult Doctrine; then select 'Sanctum Core Breach'.",
            _ => _runicLogs >= EndingResolver.RequiredRunicLogs
                ? "[DEV F2] Stage 3 (Cause → Effect): Conclusion I → Conclusion II → Runic Logs (4/4), then select 'True Verdict'."
                : "[DEV F2] Stage 3 (Cause → Effect): Conclusion I → Conclusion II, then select 'Normal Verdict'."
        };

        void ShowDevHint()
        {
            foreach (var pair in _nodes)
            {
                bool valid = _specs.TryGetValue(pair.Key, out var spec) && spec.IsValid;
                pair.Value.EnableInClassList("wboard-node--hint", valid);
            }
            ShowBanner(GetDevHintText());
        }

        void OnDestroy()
        {
            if (_modalBlocker != null)
            {
                PauseMenuController.UnregisterExternalModal(_modalBlocker);
                _modalBlocker = null;
            }
            if (_panelRenderer != null)
                _panelRenderer.UnregisterUIReloadCallback(OnUIReload);
            if (_dialogue != null && _activeDialogueOnEnd != null)
                _dialogue.DialogueEnded -= _activeDialogueOnEnd;
            if (_activeDialogueData != null)
                Destroy(_activeDialogueData);
            _activeDialogueOnEnd = null;
            _activeDialogueData = null;
        }

        void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            if (_uiVersion == version) return;
            _uiVersion = version;

            _root = root.Q<VisualElement>("WeightedBoard-Root");
            _canvas = root.Q<VisualElement>("NodeCanvas");
            if (_canvas != null)
            {
                _canvas.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    SyncPinPositions();
                    _linkLayer?.MarkDirtyRepaint();
                });
            }
            _linkList = root.Q<VisualElement>("LinkList");
            _conclusionList = root.Q<VisualElement>("ConclusionList");
            _feedbackBanner = root.Q<VisualElement>("FeedbackBanner");
            _caseTitleLabel = root.Q<Label>("CaseTitleLabel");
            _errorCountLabel = root.Q<Label>("ErrorCountLabel");
            _focusLabel = root.Q<Label>("FocusLabel");
            _feedbackLabel = root.Q<Label>("FeedbackLabel");

            _submitButton = root.Q<Button>("SubmitButton");
            if (_submitButton != null)
                _submitButton.RegisterCallback<ClickEvent>(_ => Submit());

            var autoAlign = root.Q<Button>("AutoAlignButton");
            if (autoAlign != null)
                autoAlign.RegisterCallback<ClickEvent>(_ => AutoAlign());

            var closeBtn = root.Q<Button>("CloseBoardButton");
            if (closeBtn != null)
                closeBtn.style.display = DisplayStyle.None; // Finale report stays until resolved

            var lampRow = root.Q<VisualElement>("LampRow");
            if (lampRow != null) lampRow.style.display = DisplayStyle.None;
            var lampLabel = root.Q<Label>("LampLabel");
            if (lampLabel != null) lampLabel.style.display = DisplayStyle.None;
            var drawer = root.Q<VisualElement>("ClueInspectorDrawer");
            if (drawer != null) drawer.style.display = DisplayStyle.Flex;
            _selectedClueTitle = root.Q<Label>("SelectedClueTitle");
            _selectedClueMeta = root.Q<Label>("SelectedClueMeta");
            _selectedClueDesc = root.Q<Label>("SelectedClueDesc");
            var legend = root.Q<Label>("BoardControlsLegend");
            if (legend != null) legend.text = "[Drag] Move  |  [Click → Click] or [Right-Drag] Connect (Cause → Effect)  |  [R-Click Row] Sever  |  [Submit]";

            if (_active && !_waitingDialogue)
            {
                ShowBoard();
                RefreshAll();
            }
        }

        void OpenStage(int stage)
        {
            _stage = stage;
            _links.Clear();
            _pendingSource = null;
            _inspectedNodeId = null;
            _selectedConclusionId = null;
            _specs.Clear();
            _stageSolutionEdges.Clear();
            _conclusions.Clear();

            if (stage == 1)
            {
                AddSpec("s1_market", "Guild Audit: Market Solvent",
                    "First-Age <b>volatile synthetic solvent</b> field-tested in Ignatius's vault—identical to the reagent that dissolved <b>Iron Hollow's Drift 09 conduits</b>.", true);
                AddSpec("s1_cathedral", "Guild Audit: Cathedral Plate",
                    "<b>Acoustic muting plate</b> installed in Saint Althea's belfry to silence Mother Lyra's tremor inquiry so the <b>Whisperwood siphon</b> could run unopposed.", true);
                AddSpec("s1_whisper", "Guild Audit: Whisperwood Siphon",
                    "Root-tapping <b>Elder Tree siphon</b> that harvested forest mana into leather bladders bound for <b>Iron Hollow Drift 09</b>.", true);
                AddSpec("s1_iron", "Guild Audit: Iron Hollow Breach",
                    "Sub-facility drill and stolen <b>Whisperwood bladders</b> plus <b>Market solvent</b> breached the sealed pre-Cataclysm Sanctum corridor.", true);
                AddSpec("s1_rh", "Routine Freight Permit",
                    "Standard timber shipment logged by the dockmaster—unrelated to Veiled Circle mana transfers.", false);

                _stageSolutionEdges[("s1_cathedral", "s1_whisper")] =
                    "Muting Saint Althea's bells silenced Mother Lyra's tremor inquiry so the Whisperwood siphon could run unopposed.";
                _stageSolutionEdges[("s1_whisper", "s1_iron")] =
                    "Harvested Whisperwood mana bladders were shipped directly into Iron Hollow's Drift 09 sub-facility.";
                _stageSolutionEdges[("s1_market", "s1_iron")] =
                    "The First-Age solvent field-tested in the Market Vault was the same reagent used to dissolve Drift 09's conduits.";

                _conclusions.Add(new ConclusionSpec
                {
                    Id = "c_s1_ok",
                    Text = "Coordinated First-Age Strike: The Veiled Circle gathered seal-breakers across all four districts.",
                    Kind = 1
                });
                _conclusions.Add(new ConclusionSpec
                {
                    Id = "c_s1_bad",
                    Text = "Isolated Guild Fraud: Each incident was an unrelated local crime.",
                    Kind = 0
                });
            }
            else if (stage == 2)
            {
                AddSpec("s2_readout", "Eliza's Siphon Readout",
                    "Live telemetry proving the Sanctum's <b>siphon pylons</b> are locked in <b>inverted polarity</b>, back-feeding stolen mana downward.", true);
                AddSpec("s2_pylons", "Inverted Siphon Pylons",
                    "Four massive First-Age <b>siphon pylons</b> pulsing harvested district mana directly into the bedrock beneath <b>Drift 09</b>.", true);
                AddSpec("s2_seismic", "Empirical Guard Seismic Log",
                    "Deep-crust seismic logs beneath Drift 09 recording <b>synchronized pressure spikes</b> on every pylon pulse—confirming an engineered resonance assault.", true);
                AddSpec("s2_doctrine", "Overseer's Cult Doctrine",
                    "Redacted First-Age papers calling for a <b>forced cataclysmic purge</b>—exposing the Overseer's ideological hypocrisy.", true);
                AddSpec("s2_rh", "Copper Grounding Rod",
                    "Minor spark diverter rated for workshop static; cannot affect planetary mana surges.", false);

                _stageSolutionEdges[("s2_readout", "s2_pylons")] =
                    "Eliza's readout proves the Sanctum's siphon pylons are locked in inverted polarity, pumping harvested mana downward.";
                _stageSolutionEdges[("s2_pylons", "s2_seismic")] =
                    "Each inverted pylon pulse drives the synchronized deep-crust seismic spikes beneath Drift 09.";
                _stageSolutionEdges[("s2_seismic", "s2_doctrine")] =
                    "The engineered resonance assault fulfills the Overseer's Cult Doctrine—an intentional cataclysmic overload.";

                _conclusions.Add(new ConclusionSpec
                {
                    Id = "c_s2_ok",
                    Text = "Sanctum Core Breach: The Grand Overseer is routing harvested mana into the planetary fissure.",
                    Kind = 1
                });
                _conclusions.Add(new ConclusionSpec
                {
                    Id = "c_s2_bad",
                    Text = "Routine Mining Surge: Seismic echoes are harmless dwarven blasting.",
                    Kind = 0
                });
            }
            else
            {
                AddSpec("s3_c1", "Conclusion I: Coordinated Strike",
                    "All four district crimes formed one Veiled Circle supply pipeline feeding the <b>Sanctum Core</b>.", true);
                AddSpec("s3_c2", "Conclusion II: Sanctum Breach",
                    "The Grand Overseer is back-feeding harvested district mana through inverted pylons into the <b>planetary fissure</b>.", true);
                bool hasAllLogs = _runicLogs >= EndingResolver.RequiredRunicLogs;
                AddSpec("s3_logs",
                    hasAllLogs
                        ? "First-Age Runic Logs (4/4)"
                        : $"Incomplete Runic Logs ({_runicLogs}/4)",
                    hasAllLogs
                        ? "Decoded stabilizer formulas proving the central mana fissure is a <b>planetary containment seal</b> whose destruction obliterates magic and civilization."
                        : "Missing full First-Age translations—cannot prove the deeper cataclysmic intent.",
                    hasAllLogs);

                _stageSolutionEdges[("s3_c1", "s3_c2")] =
                    "The four-district supply pipeline directly feeds the Sanctum Core's inverted mana breach.";
                if (hasAllLogs)
                {
                    _stageSolutionEdges[("s3_c2", "s3_logs")] =
                        "The four First-Age Runic Logs prove the fissure is a planetary containment seal—shattering it obliterates magic and civilization.";
                }

                _conclusions.Add(new ConclusionSpec
                {
                    Id = "c_s3_normal",
                    Text = "Normal Verdict: The Overseer is trying to harness the Mana Gate's raw power.",
                    Kind = 2
                });
                if (hasAllLogs)
                {
                    _conclusions.Add(new ConclusionSpec
                    {
                        Id = "c_s3_true",
                        Text = "True Verdict (Runic Logs): The Overseer & Veiled Circle intend to destroy the main source of mana, eviscerating magic and most of the world.",
                        Kind = 3
                    });
                }
                _conclusions.Add(new ConclusionSpec
                {
                    Id = "c_s3_bad",
                    Text = "Dismissive Verdict: The Veiled Circle only seeks minor Guild extortion.",
                    Kind = 0
                });
            }

            ShowBoard();
            RefreshAll();
            ShowBanner($"Stage {_stage}/3 — Connect Cause → Effect links to build the Empirical Guard report.");
        }

        void AddSpec(string id, string title, string desc, bool valid)
        {
            _specs[id] = new NodeSpec { Id = id, Title = title, Desc = desc, IsValid = valid };
        }

        void ShowBoard()
        {
            if (_root != null) _root.style.display = DisplayStyle.Flex;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        void HideBoard()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
        }

        void RefreshAll()
        {
            if (_caseTitleLabel != null)
                _caseTitleLabel.text = $"Case 5 Report — Stage {_stage} of 3";
            if (_errorCountLabel != null)
                _errorCountLabel.text = $"Errors: {_errors}";
            if (_focusLabel != null)
                _focusLabel.text = $"Focus {_focus:0}/{_maxFocus:0}";
            RebuildNodes();
            RebuildLinkList();
            RebuildConclusions();
            RefreshClueInspector();
        }

        void RefreshClueInspector()
        {
            if (_selectedClueTitle == null || _selectedClueDesc == null) return;
            if (string.IsNullOrEmpty(_inspectedNodeId) || !_specs.TryGetValue(_inspectedNodeId, out var spec))
            {
                _selectedClueTitle.text = "Select a Dossier Card";
                if (_selectedClueMeta != null) _selectedClueMeta.text = $"Stage {_stage} of 3";
                _selectedClueDesc.text = "Click any card to read its full dossier notes, then click a target card (or Right-Drag) to connect Cause → Effect.";
                return;
            }
            _selectedClueTitle.text = spec.Title;
            if (_selectedClueMeta != null) _selectedClueMeta.text = $"Empirical Guard Dossier • Stage {_stage}";
            string playerName = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session.PlayerName : "";
            _selectedClueDesc.text = ClueHighlightFormatter.FormatRawText(spec.Desc, "", playerName, darkBackground: false);
        }

        void RebuildNodes()
        {
            if (_canvas == null) return;
            _canvas.Clear();
            _nodes.Clear();
            _pins.Clear();

            _linkLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            _linkLayer.style.position = Position.Absolute;
            _linkLayer.style.left = 0; _linkLayer.style.top = 0;
            _linkLayer.style.right = 0; _linkLayer.style.bottom = 0;
            _linkLayer.generateVisualContent += OnGenerateLinkVisuals;

            _pinLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            _pinLayer.style.position = Position.Absolute;
            _pinLayer.style.left = 0; _pinLayer.style.top = 0;
            _pinLayer.style.right = 0; _pinLayer.style.bottom = 0;

            var slots = new List<Vector2>();
            int slotCount = Mathf.Max(6, _specs.Count);
            for (int s = 0; s < slotCount; s++)
                slots.Add(new Vector2(24f + (s % 3) * 200f, 24f + (s / 3) * 124f));
            for (int s = slots.Count - 1; s > 0; s--)
            {
                int j = UnityEngine.Random.Range(0, s + 1);
                (slots[s], slots[j]) = (slots[j], slots[s]);
            }

            int i = 0;
            foreach (var kv in _specs)
            {
                var spec = kv.Value;
                var node = new VisualElement();
                node.AddToClassList("wboard-node");
                var titleLbl = new Label(spec.Title) { pickingMode = PickingMode.Ignore };
                titleLbl.AddToClassList("wboard-node-name");
                node.Add(titleLbl);
                var descLbl = new Label(spec.Desc) { pickingMode = PickingMode.Ignore };
                descLbl.style.fontSize = 10f;
                descLbl.style.color = new Color(0.24f, 0.18f, 0.12f, 0.9f);
                descLbl.style.whiteSpace = WhiteSpace.Normal;
                descLbl.style.marginTop = 3f;
                node.Add(descLbl);
                node.tooltip = spec.Desc;

                Vector2 slot = slots[i++];
                float x = Mathf.Max(8f, slot.x + UnityEngine.Random.Range(-10f, 16f));
                float y = Mathf.Max(8f, slot.y + UnityEngine.Random.Range(-8f, 12f));
                node.style.left = x;
                node.style.top = y;

                string capturedId = spec.Id;
                RegisterNodeInteractions(node, capturedId);
                node.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    SyncPinPosition(capturedId, node);
                    _linkLayer?.MarkDirtyRepaint();
                });
                _nodes[spec.Id] = node;
                _canvas.Add(node);

                var pin = CreatePinElement();
                _pins[spec.Id] = pin;
                _pinLayer.Add(pin);
            }

            _canvas.Add(_linkLayer);
            _canvas.Add(_pinLayer);
            SyncPinPositions();
        }

        void RegisterNodeInteractions(VisualElement node, string id)
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
                    _dragLinkSource = id;
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
                    var p = nodeStart + delta;
                    node.style.left = Mathf.Max(0, p.x);
                    node.style.top = Mathf.Max(0, p.y);
                    SyncPinPosition(id, node);
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
                    string target = FindNodeAt(evt.position);
                    if (target != null && target != id) TryAddLink(id, target);
                    _linkLayer?.MarkDirtyRepaint();
                    return;
                }
                if (evt.button != 0) return;
                node.ReleasePointer(evt.pointerId);
                if (!dragging) OnNodeClicked(id);
            });
        }

        void OnNodeClicked(string id)
        {
            _inspectedNodeId = id;
            RefreshClueInspector();
            if (_pendingSource == null)
            {
                _pendingSource = id;
            }
            else if (_pendingSource == id)
            {
                _pendingSource = null;
            }
            else
            {
                string src = _pendingSource;
                _pendingSource = null;
                TryAddLink(src, id);
            }
            foreach (var pair in _nodes)
                pair.Value.EnableInClassList("wboard-node--selected", pair.Key == _pendingSource);
        }

        void TryAddLink(string a, string b)
        {
            if (a == b) return;
            foreach (var l in _links)
                if (l.src == a && l.dst == b) return;

            if (_stageSolutionEdges.TryGetValue((a, b), out string rationale))
            {
                _links.Add((a, b));
                AudioCoordinator.PlayBankSfx(bank => bank.sfxBoardLink, 0.65f);
                RebuildLinkList();
                _linkLayer?.MarkDirtyRepaint();
                ShowBanner(rationale);
                return;
            }

            if (_stageSolutionEdges.ContainsKey((b, a)))
            {
                DrainFocusOnError("Causal direction reversed — link Cause → Effect!");
                return;
            }

            DrainFocusOnError("Unsupported or irrelevant connection! Focus drained heavily.");
        }

        void DrainFocusOnError(string msg)
        {
            _errors++;
            AudioCoordinator.PlayBankSfx(bank => bank.sfxBoardError, 0.65f);
            if (_focus < ErrorFocusDrain)
            {
                TriggerEnding(1); // Bad Ending: Focus depleted on grave error
                return;
            }
            _focus = Mathf.Max(0f, _focus - ErrorFocusDrain);
            CoreRuntime.Instance?.Focus.Sync(_focus, _maxFocus);
            if (_errorCountLabel != null) _errorCountLabel.text = $"Errors: {_errors}";
            if (_focusLabel != null) _focusLabel.text = $"Focus {_focus:0}/{_maxFocus:0}";
            ShowBanner($"{msg} (-{ErrorFocusDrain:0} Focus)");
            if (_focus <= 0f) TriggerEnding(1);
        }

        void RebuildLinkList()
        {
            if (_linkList == null) return;
            _linkList.Clear();
            foreach (var link in _links)
            {
                var row = new VisualElement();
                row.AddToClassList("wboard-link-row");
                row.AddToClassList("wboard-link-row--solution");
                var lbl = new Label($"{_specs[link.src].Title}  →  {_specs[link.dst].Title}");
                lbl.AddToClassList("wboard-link-text");
                row.Add(lbl);
                if (_stageSolutionEdges.TryGetValue((link.src, link.dst), out var rationale))
                    row.tooltip = rationale;
                var captured = link;
                row.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 1)
                    {
                        _links.Remove(captured);
                        RebuildLinkList();
                        _linkLayer?.MarkDirtyRepaint();
                        evt.StopPropagation();
                    }
                });
                _linkList.Add(row);
            }
        }

        void RebuildConclusions()
        {
            if (_conclusionList == null) return;
            _conclusionList.Clear();
            foreach (var c in _conclusions)
            {
                var card = new Button();
                card.AddToClassList("wboard-conclusion");
                if (c.Id == _selectedConclusionId) card.AddToClassList("wboard-conclusion--selected");
                var text = new Label(c.Text) { pickingMode = PickingMode.Ignore };
                text.AddToClassList("wboard-conclusion-text");
                card.Add(text);
                var capturedId = c.Id;
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    _selectedConclusionId = capturedId;
                    RebuildConclusions();
                });
                _conclusionList.Add(card);
            }
        }

        void Submit()
        {
            if (string.IsNullOrEmpty(_selectedConclusionId))
            {
                ShowBanner("Select a conclusion before submitting.");
                return;
            }
            var chosen = _conclusions.Find(c => c.Id == _selectedConclusionId);
            if (chosen.Kind == 0)
            {
                DrainFocusOnError("Wrong conclusion! The Empirical Guards reject the report.");
                return;
            }

            if (_stage == 1)
            {
                if (!HasAllDirectedLinks(("s1_cathedral", "s1_whisper"), ("s1_whisper", "s1_iron"), ("s1_market", "s1_iron")))
                {
                    ShowBanner("Complete the directed causal chain across all four Guild Audits before submitting Stage 1.");
                    return;
                }
                AudioCoordinator.PlayBankSfx(bank => bank.sfxBoardSolved, 0.7f);
                HideBoard();
                PlayBrainstormDialogue(
                    "D_Case5_Stage1_Done",
                    () => OpenStage(2),
                    ("Eliza", "That links every district together into one supply chain. Now let's cross-check the Empirical Guard seismic logs with the Sanctum pylons and Overseer's doctrine."),
                    ("Golem", "[UPDATING BOARD]: Adding Inverted Siphon Pylons, Guard telemetry, and Eliza's readout to Stage 2."));
                return;
            }

            if (_stage == 2)
            {
                if (!HasAllDirectedLinks(("s2_readout", "s2_pylons"), ("s2_pylons", "s2_seismic"), ("s2_seismic", "s2_doctrine")))
                {
                    ShowBanner("Complete the 4-clue causal chain from Eliza's Readout through the Pylons and Seismic Log to the Cult Doctrine.");
                    return;
                }
                AudioCoordinator.PlayBankSfx(bank => bank.sfxBoardSolved, 0.7f);
                HideBoard();
                string logHint = _runicLogs >= EndingResolver.RequiredRunicLogs
                    ? "You recovered all four First-Age Runic Logs—link Conclusion I → Conclusion II → Runic Logs to uncover the Overseer's true endgame!"
                    : $"We only have {_runicLogs}/4 Runic Logs. Link Conclusion I → Conclusion II to warn the Guards of the Mana Gate breach.";
                PlayBrainstormDialogue(
                    "D_Case5_Stage2_Done",
                    () => OpenStage(3),
                    ("Eliza", "So he's channeling Asterveil's stolen mana straight into the planetary fissure..."),
                    ("Eliza", logHint));
                return;
            }

            // Stage 3: Final Verdict
            if (!HasAllDirectedLinks(("s3_c1", "s3_c2")))
            {
                ShowBanner("Connect Conclusion I → Conclusion II first.");
                return;
            }
            if (chosen.Kind == 3)
            {
                if (_runicLogs < EndingResolver.RequiredRunicLogs || !HasAllDirectedLinks(("s3_c1", "s3_c2"), ("s3_c2", "s3_logs")))
                {
                    ShowBanner("Connect Conclusion I → Conclusion II → First-Age Runic Logs (4/4) to prove the True Verdict.");
                    return;
                }
                TriggerEnding(3);
                return;
            }
            TriggerEnding(2);
        }

        bool HasAllDirectedLinks(params (string src, string dst)[] required)
        {
            foreach (var edge in required)
            {
                bool found = false;
                foreach (var l in _links)
                    if (l.src == edge.src && l.dst == edge.dst) { found = true; break; }
                if (!found) return false;
            }
            return true;
        }

        void TriggerEnding(int ending)
        {
            _active = false;
            HideBoard();
            _flow?.RequestEnding(ending);
        }

        bool AreConnected(string a, string b) => AreAllValidConnected(new[] { a, b });

        bool AreAllValidConnected(string[] requiredIds)
        {
            if (requiredIds.Length == 0) return true;
            var adj = new Dictionary<string, List<string>>();
            foreach (string id in requiredIds) adj[id] = new List<string>();
            foreach (var (src, dst) in _links)
            {
                if (!adj.ContainsKey(src) || !adj.ContainsKey(dst)) continue;
                adj[src].Add(dst);
                adj[dst].Add(src);
            }
            var visited = new HashSet<string> { requiredIds[0] };
            var q = new Queue<string>();
            q.Enqueue(requiredIds[0]);
            while (q.Count > 0)
            {
                string cur = q.Dequeue();
                foreach (string nxt in adj[cur])
                    if (visited.Add(nxt)) q.Enqueue(nxt);
            }
            return visited.Count == requiredIds.Length;
        }

        void PlayBrainstormDialogue(string id, Action onDone, params (string speaker, string text)[] lines)
        {
            if (_dialogue == null)
            {
                onDone?.Invoke();
                return;
            }
            _waitingDialogue = true;
            var data = ScriptableObject.CreateInstance<DialogueData>();
            data.hideFlags = HideFlags.DontSave;
            data.id = id;
            data.startNodeId = "n0";
            data.nodes = new DialogueNodeData[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                data.nodes[i] = new DialogueNodeData
                {
                    id = $"n{i}",
                    speakerName = lines[i].speaker,
                    text = lines[i].text,
                    nextNodeId = i + 1 < lines.Length ? $"n{i + 1}" : ""
                };
            }
            void OnEnd()
            {
                if (_dialogue != null) _dialogue.DialogueEnded -= OnEnd;
                _activeDialogueOnEnd = null;
                if (_activeDialogueData != null) Destroy(_activeDialogueData);
                _activeDialogueData = null;
                _waitingDialogue = false;
                onDone?.Invoke();
            }
            _activeDialogueOnEnd = OnEnd;
            _activeDialogueData = data;
            _dialogue.DialogueEnded += OnEnd;
            _dialogue.SetDialogue(data);
            _dialogue.Play();
        }

        void AutoAlign()
        {
            int count = _nodes.Count;
            int cols = count <= 4 ? 2 : (count <= 6 ? 3 : 4);
            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)cols));
            float cw = _canvas != null && _canvas.layout.width > 120f ? _canvas.layout.width : 720f;
            float ch = _canvas != null && _canvas.layout.height > 120f ? _canvas.layout.height : 380f;
            float stepX = cols > 1 ? Mathf.Clamp((cw - 36f - 148f) / (cols - 1), 130f, 195f) : 165f;
            float stepY = rows > 1 ? Mathf.Clamp((ch - 40f - 56f) / (rows - 1), 76f, 124f) : 96f;
            int idx = 0;
            foreach (var pair in _nodes)
            {
                pair.Value.style.left = 18f + (idx % cols) * stepX;
                pair.Value.style.top = 20f + (idx / cols) * stepY;
                idx++;
            }
            SyncPinPositions();
            _linkLayer?.MarkDirtyRepaint();
        }

        string FindNodeAt(Vector2 worldPos)
        {
            foreach (var pair in _nodes)
                if (pair.Value.worldBound.Contains(worldPos)) return pair.Key;
            return null;
        }

        static VisualElement CreatePinElement()
        {
            var pin = new VisualElement { pickingMode = PickingMode.Ignore };
            pin.style.position = Position.Absolute;
            pin.style.width = 12f; pin.style.height = 12f;
            pin.style.borderTopLeftRadius = 6f; pin.style.borderTopRightRadius = 6f;
            pin.style.borderBottomLeftRadius = 6f; pin.style.borderBottomRightRadius = 6f;
            pin.style.backgroundColor = new Color(0.84f, 0.16f, 0.16f, 1f);
            pin.style.borderTopColor = new Color(0.42f, 0.05f, 0.05f, 1f);
            pin.style.borderRightColor = new Color(0.42f, 0.05f, 0.05f, 1f);
            pin.style.borderBottomColor = new Color(0.42f, 0.05f, 0.05f, 1f);
            pin.style.borderLeftColor = new Color(0.42f, 0.05f, 0.05f, 1f);
            pin.style.borderTopWidth = 1.5f; pin.style.borderRightWidth = 1.5f;
            pin.style.borderBottomWidth = 1.5f; pin.style.borderLeftWidth = 1.5f;
            return pin;
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

        void SyncPinPosition(string id, VisualElement node)
        {
            if (!_pins.TryGetValue(id, out var pin)) return;
            Vector2 p = NodePinPos(node);
            pin.style.left = p.x - 6f;
            pin.style.top = p.y - 6f;
        }

        void SyncPinPositions()
        {
            foreach (var pair in _nodes) SyncPinPosition(pair.Key, pair.Value);
        }

        void OnGenerateLinkVisuals(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            painter.lineWidth = 2.5f;
            painter.lineCap = LineCap.Round;
            foreach (var (src, dst) in _links)
            {
                if (!_nodes.TryGetValue(src, out var s) || !_nodes.TryGetValue(dst, out var d)) continue;
                DrawLine(painter, NodePinPos(s), NodePinPos(d), new Color(1f, 1f, 1f, 0.92f));
            }
            if (_dragLinkSource != null && _nodes.TryGetValue(_dragLinkSource, out var ds))
                DrawLine(painter, NodePinPos(ds), _dragLinkPointer, new Color(1f, 1f, 1f, 0.65f));
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

        void ShowBanner(string text)
        {
            if (_feedbackLabel != null) _feedbackLabel.text = text;
            if (_feedbackBanner != null) _feedbackBanner.style.display = DisplayStyle.Flex;
        }
    }
}

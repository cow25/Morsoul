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
        Button _submitButton;

        readonly Dictionary<string, VisualElement> _nodes = new Dictionary<string, VisualElement>();
        readonly Dictionary<string, VisualElement> _pins = new Dictionary<string, VisualElement>();
        readonly List<(string src, string dst)> _links = new List<(string, string)>();
        readonly Dictionary<string, NodeSpec> _specs = new Dictionary<string, NodeSpec>();
        readonly List<ConclusionSpec> _conclusions = new List<ConclusionSpec>();

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var kb = Keyboard.current;
            if (kb != null && kb.f2Key.wasPressedThisFrame)
                ShowDevHint();
#endif
        }

        void ShowDevHint()
        {
            foreach (var pair in _nodes)
            {
                bool valid = _specs.TryGetValue(pair.Key, out var spec) && spec.IsValid;
                pair.Value.EnableInClassList("wboard-node--hint", valid);
            }
            string hint = _stage switch
            {
                1 => "[DEV F2] Stage 1: Link all 4 Guild Audits (ignore Routine Freight Permit), then select 'Coordinated First-Age Strike'.",
                2 => "[DEV F2] Stage 2: Link Seismic Log + Cult Doctrine + Siphon Readout (ignore Copper Grounding Rod), then select 'Sanctum Core Breach'.",
                _ => _runicLogs >= EndingResolver.RequiredRunicLogs
                    ? "[DEV F2] Stage 3: Link Conclusion I + Conclusion II + Runic Logs (4/4), then select 'True Verdict'."
                    : "[DEV F2] Stage 3: Link Conclusion I + Conclusion II, then select 'Normal Verdict'."
            };
            ShowBanner(hint);
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
            var drawer = root.Q<VisualElement>("ClueInspectorDrawer");
            if (drawer != null) drawer.style.display = DisplayStyle.None;
            var legend = root.Q<Label>("BoardControlsLegend");
            if (legend != null) legend.text = "[Drag] Move  |  [Click → Click] or [Right-Drag] Connect  |  [R-Click Row] Sever  |  [Submit]";

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
            _selectedConclusionId = null;
            _specs.Clear();
            _conclusions.Clear();

            if (stage == 1)
            {
                AddSpec("s1_market", "Guild Audit: Market Solvent", "First-Age volatile reagent supplied to Ignatius.", true);
                AddSpec("s1_cathedral", "Guild Audit: Cathedral Plate", "Acoustic muting plate silenced Saint Althea.", true);
                AddSpec("s1_whisper", "Guild Audit: Whisperwood Siphon", "Root-tapping siphon harvested forest mana.", true);
                AddSpec("s1_iron", "Guild Audit: Iron Hollow Breach", "Sub-facility drill cracked pre-Cataclysm vaults.", true);
                AddSpec("s1_rh", "Routine Freight Permit", "Standard timber shipment—unrelated to mana.", false);

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
                AddSpec("s2_seismic", "Empirical Guard Seismic Log", "Massive mana-pressure spikes beneath Drift 09.", true);
                AddSpec("s2_doctrine", "Overseer's Cult Doctrine", "Redacted First-Age papers dismissing planetary mana limits.", true);
                AddSpec("s2_readout", "Eliza's Siphon Readout", "Harvested mana is back-feeding into the central fissure.", true);
                AddSpec("s2_rh", "Copper Grounding Rod", "Minor spark diverter; cannot hold planetary surges.", false);

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
                AddSpec("s3_c1", "Conclusion I: Coordinated Strike", "All four district crimes supplied First-Age seal-breakers.", true);
                AddSpec("s3_c2", "Conclusion II: Sanctum Breach", "The Overseer is back-feeding harvested mana into the fissure.", true);
                bool hasAllLogs = _runicLogs >= EndingResolver.RequiredRunicLogs;
                AddSpec("s3_logs",
                    hasAllLogs
                        ? "First-Age Runic Logs (4/4)"
                        : $"Incomplete Runic Logs ({_runicLogs}/4)",
                    hasAllLogs
                        ? "Decoded stabilizer formulas proving the central mana source is a planetary containment seal."
                        : "Missing full First-Age translations—cannot prove the deeper cataclysmic intent.",
                    hasAllLogs);

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
            ShowBanner($"Stage {_stage}/3 — Connect the evidence and submit a conclusion for the Empirical Guards.");
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
                if ((l.src == a && l.dst == b) || (l.src == b && l.dst == a)) return;

            if (!_specs[a].IsValid || !_specs[b].IsValid)
            {
                DrainFocusOnError("Irrelevant or unverified connection! Focus drained heavily.");
                return;
            }

            _links.Add((a, b));
            AudioCoordinator.PlayBankSfx(bank => bank.sfxBoardLink, 0.65f);
            RebuildLinkList();
            _linkLayer?.MarkDirtyRepaint();
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
                var lbl = new Label($"{_specs[link.src].Title}  —  {_specs[link.dst].Title}");
                lbl.AddToClassList("wboard-link-text");
                row.Add(lbl);
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
                if (!AreAllValidConnected(new[] { "s1_market", "s1_cathedral", "s1_whisper", "s1_iron" }))
                {
                    ShowBanner("Connect all four Guild Audit clues before submitting Stage 1.");
                    return;
                }
                AudioCoordinator.PlayBankSfx(bank => bank.sfxBoardSolved, 0.7f);
                HideBoard();
                PlayBrainstormDialogue(
                    "D_Case5_Stage1_Done",
                    () => OpenStage(2),
                    ("Eliza", "That links every district together. Now let's cross-check the Empirical Guard seismic logs with the Overseer's doctrine."),
                    ("Golem", "[UPDATING BOARD]: Adding Guard telemetry and Eliza's siphon readout to Stage 2."));
                return;
            }

            if (_stage == 2)
            {
                if (!AreAllValidConnected(new[] { "s2_seismic", "s2_doctrine", "s2_readout" }))
                {
                    ShowBanner("Connect the Guard Seismic Log, Cult Doctrine, and Eliza's Readout first.");
                    return;
                }
                AudioCoordinator.PlayBankSfx(bank => bank.sfxBoardSolved, 0.7f);
                HideBoard();
                string logHint = _runicLogs >= EndingResolver.RequiredRunicLogs
                    ? "You recovered all four First-Age Runic Logs—link them with our conclusions to uncover the Overseer's true endgame!"
                    : $"We only have {_runicLogs}/4 Runic Logs. Link our two conclusions to warn the Guards of the Mana Gate breach.";
                PlayBrainstormDialogue(
                    "D_Case5_Stage2_Done",
                    () => OpenStage(3),
                    ("Eliza", "So he's channeling Asterveil's stolen mana straight into the planetary fissure..."),
                    ("Eliza", logHint));
                return;
            }

            // Stage 3: Final Verdict
            if (!AreConnected("s3_c1", "s3_c2"))
            {
                ShowBanner("Connect Conclusion I and Conclusion II first.");
                return;
            }
            if (chosen.Kind == 3)
            {
                if (_runicLogs < EndingResolver.RequiredRunicLogs || !AreAllValidConnected(new[] { "s3_c1", "s3_c2", "s3_logs" }))
                {
                    ShowBanner("Connect the First-Age Runic Logs (4/4) with both conclusions to prove the True Verdict.");
                    return;
                }
                TriggerEnding(3);
                return;
            }
            TriggerEnding(2);
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
            int idx = 0;
            foreach (var pair in _nodes)
            {
                pair.Value.style.left = 28f + (idx % 3) * 200f;
                pair.Value.style.top = 28f + (idx / 3) * 116f;
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
            if ((to - from).sqrMagnitude < 4f) return;
            painter.strokeColor = color;
            painter.BeginPath();
            painter.MoveTo(from);
            painter.LineTo(to);
            painter.Stroke();
        }

        void ShowBanner(string text)
        {
            if (_feedbackLabel != null) _feedbackLabel.text = text;
            if (_feedbackBanner != null) _feedbackBanner.style.display = DisplayStyle.Flex;
        }
    }
}

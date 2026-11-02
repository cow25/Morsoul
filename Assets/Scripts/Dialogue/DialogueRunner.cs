using System;
using System.Collections.Generic;
using Morsoul.Data;
using UnityEngine;

namespace Morsoul.Dialogue
{
    /// <summary>
    /// Pure C# traversal engine for a <see cref="DialogueData"/> graph. Holds no
    /// UI or MonoBehaviour state so it can be exercised directly in EditMode
    /// tests. Instead of mutating external systems it raises events: subscribers
    /// register clues, persist flags and render nodes.
    /// </summary>
    public class DialogueRunner
    {
        /// <summary>Hard cap on node transitions; guards against graph cycles.</summary>
        public const int MaxHops = 1000;
        /// <summary>Literal placeholder replaced by <see cref="PlayerName"/>.</summary>
        public const string PlayerNameToken = "{PlayerName}";

        readonly DialogueData _data;
        readonly Dictionary<string, DialogueNodeData> _nodesById =
            new Dictionary<string, DialogueNodeData>(StringComparer.Ordinal);
        readonly ISet<string> _flags;
        readonly List<DialogueOptionData> _availableOptions = new List<DialogueOptionData>();
        int _hops;

        /// <summary>
        /// Builds the runner and shows the start node immediately (skipping any
        /// gated chain it leads through). Because events cannot be subscribed
        /// before construction, inspect <see cref="Current"/> after building.
        /// </summary>
        public DialogueRunner(DialogueData data, ISet<string> flags)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            _data = data;
            _flags = flags ?? new HashSet<string>(StringComparer.Ordinal);
            if (data.nodes != null)
            {
                foreach (var node in data.nodes)
                    if (node != null && !string.IsNullOrEmpty(node.id))
                        _nodesById[node.id] = node;
            }
            GoTo(data.startNodeId);
        }

        /// <summary>
        /// Fires when a node becomes current, after ClueRegistered/FlagSet for
        /// that node have been applied.
        /// </summary>
        public event Action<DialogueNodeData> NodeShown;
        /// <summary>Fires once when the dialogue reaches an end.</summary>
        public event Action DialogueEnded;
        /// <summary>Fires when a shown node declares registersClue.</summary>
        public event Action<ClueData> ClueRegistered;
        /// <summary>Fires when a shown node declares setsFlag, after the flag is added.</summary>
        public event Action<string> FlagSet;

        /// <summary>Current node; null after the dialogue has ended.</summary>
        public DialogueNodeData Current { get; private set; }
        /// <summary>True once the dialogue has ended.</summary>
        public bool IsEnded { get; private set; }
        /// <summary>The dialogue graph being traversed.</summary>
        public DialogueData Data => _data;
        /// <summary>Name substituted for the {PlayerName} token.</summary>
        public string PlayerName { get; set; } = "Detective";
        /// <summary>Options of the current node whose requiresFlag is satisfied.</summary>
        public IReadOnlyList<DialogueOptionData> CurrentOptions => _availableOptions;
        /// <summary>True when the current node offers at least one usable option.</summary>
        public bool HasOptions => _availableOptions.Count > 0;

        /// <summary>Replaces every literal {PlayerName} token with PlayerName.</summary>
        public string SubstituteName(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace(PlayerNameToken, PlayerName ?? "");
        }

        /// <summary>
        /// Advances past the current node via its nextNodeId. No-op while the
        /// current node offers options (options override nextNodeId) or after
        /// the dialogue has ended. An empty or dangling nextNodeId ends the
        /// dialogue.
        /// </summary>
        public void Advance()
        {
            if (IsEnded || Current == null || HasOptions) return;
            GoTo(Current.nextNodeId);
        }

        /// <summary>
        /// Chooses an option by index into <see cref="CurrentOptions"/> and jumps
        /// to its nextNodeId (empty or dangling ends the dialogue).
        /// </summary>
        public void Choose(int optionIndex)
        {
            if (IsEnded || Current == null) return;
            if (optionIndex < 0 || optionIndex >= _availableOptions.Count)
                throw new ArgumentOutOfRangeException(nameof(optionIndex));
            GoTo(_availableOptions[optionIndex].nextNodeId);
        }

        // --- Traversal ---------------------------------------------------------

        void GoTo(string nodeId)
        {
            // Follow nextNodeId links, skipping flag-gated nodes, until a
            // showable node is reached or the chain ends/loops forever.
            while (true)
            {
                if (string.IsNullOrEmpty(nodeId) ||
                    !_nodesById.TryGetValue(nodeId, out var node))
                {
                    End();
                    return;
                }
                if (++_hops > MaxHops)
                {
                    Debug.LogWarning($"DialogueRunner: exceeded {MaxHops} node hops in " +
                                     $"'{_data.id}'; ending dialogue (possible cycle).");
                    End();
                    return;
                }
                if (IsGateSatisfied(node.requiresFlag))
                {
                    Show(node);
                    return;
                }
                nodeId = node.nextNodeId;
            }
        }

        bool IsGateSatisfied(string requiresFlag) =>
            string.IsNullOrEmpty(requiresFlag) || _flags.Contains(requiresFlag);

        void Show(DialogueNodeData node)
        {
            Current = node;
            RebuildOptions(node);
            if (node.registersClue != null)
                ClueRegistered?.Invoke(node.registersClue);
            if (!string.IsNullOrEmpty(node.setsFlag))
            {
                _flags.Add(node.setsFlag);
                FlagSet?.Invoke(node.setsFlag);
            }
            NodeShown?.Invoke(node);
        }

        void RebuildOptions(DialogueNodeData node)
        {
            _availableOptions.Clear();
            var options = node.options;
            if (options == null) return;
            foreach (var option in options)
            {
                if (option == null) continue;
                if (!IsGateSatisfied(option.requiresFlag)) continue;
                _availableOptions.Add(option);
            }
        }

        void End()
        {
            if (IsEnded) return;
            IsEnded = true;
            Current = null;
            _availableOptions.Clear();
            DialogueEnded?.Invoke();
        }
    }
}

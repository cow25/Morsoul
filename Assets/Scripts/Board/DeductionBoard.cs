using System;
using System.Collections.Generic;
using System.Linq;
using Morsoul.Data;

namespace Morsoul.Board
{
    public class DeductionBoard
    {
        public const float WrongCausalFocusCost = 20f;
        public const float InvalidPairingFocusCost = 10f;
        public const int HintAfterMisses = 2;
        public const string InvalidPairingMessage = "These clues do not support a common inference.";

        readonly List<PremiseData> _premises = new List<PremiseData>();
        readonly HashSet<InquiryNodeData> _validatedNodes = new HashSet<InquiryNodeData>();
        readonly Dictionary<InquiryNodeData, int> _missCounts = new Dictionary<InquiryNodeData, int>();
        InquiryNodeData _pending;

        public DeductionBoard(CaseData caseData)
        {
            if (caseData == null) throw new ArgumentNullException(nameof(caseData));
            var errors = caseData.Validate();
            if (errors.Count > 0)
                throw new ArgumentException(
                    "CaseData failed validation: " + string.Join("; ", errors), nameof(caseData));
            Case = caseData;
        }

        public CaseData Case { get; }
        public InquiryNodeData PendingNode => _pending;
        public IReadOnlyList<PremiseData> Premises => _premises;
        public int WrongCausalAnswers { get; private set; }

        public IEnumerable<string> ValidatedNodeIds =>
            Case.inquiryNodes
                .Where(n => n != null && _validatedNodes.Contains(n))
                .Select(n => n.id)
                .ToList();

        public bool IsNodeValidated(InquiryNodeData node) =>
            node != null && _validatedNodes.Contains(node);

        public int GetMissCount(InquiryNodeData node) =>
            node != null && _missCounts.TryGetValue(node, out int count) ? count : 0;

        public PairingResult TryPair(ClueData a, ClueData b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (a == b) throw new ArgumentException("Cannot pair a clue with itself.");
            if (_pending != null)
                throw new InvalidOperationException("A causal prompt is already open.");

            foreach (var node in Case.inquiryNodes)
            {
                if (node == null) continue;
                bool matches = (node.slotA == a && node.slotB == b)
                               || (node.slotA == b && node.slotB == a);
                if (!matches) continue;
                if (IsNodeValidated(node))
                    return new PairingResult(PairingOutcome.AlreadyValidated, node, null, 0f, 0);
                _pending = node;
                return new PairingResult(PairingOutcome.OpensPrompt, node, null, 0f, 0);
            }

            foreach (var node in Case.inquiryNodes)
            {
                if (node == null || node.rejectedClues == null) continue;
                bool rejectedWithSlot =
                    (node.rejectedClues.Contains(a) && (node.slotA == b || node.slotB == b))
                    || (node.rejectedClues.Contains(b) && (node.slotA == a || node.slotB == a));
                if (rejectedWithSlot)
                {
                    GetInvalidPairingCost(out float focusCost, out int strikeCost);
                    return new PairingResult(PairingOutcome.Rejected, node,
                        node.rejectionMessage, focusCost, strikeCost);
                }
            }

            GetInvalidPairingCost(out float invalidFocus, out int invalidStrike);
            return new PairingResult(PairingOutcome.Invalid, null,
                InvalidPairingMessage, invalidFocus, invalidStrike);
        }

        public AnswerResult AnswerPrompt(int optionIndex)
        {
            if (_pending == null)
                throw new InvalidOperationException("No causal prompt is open.");
            var node = _pending;
            var options = node.prompt.options;
            if (optionIndex < 0 || optionIndex >= options.Length)
                throw new ArgumentOutOfRangeException(nameof(optionIndex));

            if (optionIndex == node.prompt.correctIndex)
            {
                _pending = null;
                _validatedNodes.Add(node);
                _premises.Add(node.producesPremise);
                return new AnswerResult(AnswerOutcome.Correct, node, node.producesPremise,
                    0f, 0, GetMissCount(node), null);
            }

            int missCount = _missCounts.TryGetValue(node, out int misses) ? misses + 1 : 1;
            _missCounts[node] = missCount;
            WrongCausalAnswers++;
            _pending = null;
            GetWrongCausalCost(out float focusCost, out int strikeCost);
            string hint = missCount >= HintAfterMisses ? node.prompt.hintText : null;
            return new AnswerResult(AnswerOutcome.Wrong, node, null,
                focusCost, strikeCost, missCount, hint);
        }

        public void CancelPrompt() => _pending = null;

        public HypothesisResult SubmitHypothesis()
        {
            var hypothesis = Case.hypothesis;
            if (!_premises.Contains(hypothesis.premise1) || !_premises.Contains(hypothesis.premise2))
                return new HypothesisResult(HypothesisOutcome.NotReady, null);
            if (hypothesis.unlocksInterrogation != null)
                return new HypothesisResult(HypothesisOutcome.UnlocksInterrogation,
                    hypothesis.unlocksInterrogation);
            return new HypothesisResult(HypothesisOutcome.RoutesToEnding, null);
        }

        public void Restore(IEnumerable<string> validatedNodeIds)
        {
            if (validatedNodeIds == null) throw new ArgumentNullException(nameof(validatedNodeIds));
            _pending = null;
            var matched = new HashSet<InquiryNodeData>();
            foreach (string id in validatedNodeIds)
            {
                var node = Case.inquiryNodes.FirstOrDefault(n => n != null && n.id == id);
                if (node == null)
                    throw new ArgumentException($"Unknown inquiry node id '{id}'.",
                        nameof(validatedNodeIds));
                matched.Add(node);
            }
            foreach (var node in Case.inquiryNodes)
                if (node != null && matched.Contains(node) && _validatedNodes.Add(node))
                    _premises.Add(node.producesPremise);
        }

        void GetInvalidPairingCost(out float focusCost, out int strikeCost)
        {
            focusCost = Case.caseId == 0 || !Case.hasInvestigationScene
                ? 0f
                : InvalidPairingFocusCost;
            strikeCost = 0;
        }

        void GetWrongCausalCost(out float focusCost, out int strikeCost)
        {
            if (Case.caseId == 0)
            {
                focusCost = 0f;
                strikeCost = 0;
            }
            else if (!Case.hasInvestigationScene)
            {
                focusCost = 0f;
                strikeCost = 1;
            }
            else
            {
                focusCost = WrongCausalFocusCost;
                strikeCost = 0;
            }
        }
    }
}

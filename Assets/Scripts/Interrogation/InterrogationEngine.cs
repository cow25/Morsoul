using System;
using System.Collections.Generic;
using System.Linq;
using Morsoul.Data;

namespace Morsoul.Interrogation
{
    public class InterrogationEngine
    {
        public const int MaxStrikes = 3;

        readonly HashSet<EvidenceData> _evidence;
        readonly HashSet<string> _pressedKeywords = new HashSet<string>(StringComparer.Ordinal);

        public InterrogationEngine(InterrogationData data,
            IEnumerable<EvidenceData> availableEvidence,
            bool guidedFirstPhase = false, bool emotionEchoUnlocked = false)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (availableEvidence == null)
                throw new ArgumentNullException(nameof(availableEvidence));
            if (data.phases == null || data.phases.Length == 0)
                throw new ArgumentException("InterrogationData has no phases.", nameof(data));

            Data = data;
            GuidedFirstPhase = guidedFirstPhase;
            EmotionEchoUnlocked = emotionEchoUnlocked;
            _evidence = new HashSet<EvidenceData>(availableEvidence.Where(e => e != null));
            Reset();
        }

        public InterrogationData Data { get; }
        public int PhaseIndex { get; private set; }
        public InterrogationPhase CurrentPhase =>
            Status == InterrogationStatus.Confessed ? null : Data.phases[PhaseIndex];
        public int SegmentsRemaining { get; private set; }
        public int StrikesLost { get; private set; }
        public int StrikesRemaining => MaxStrikes - StrikesLost;
        public bool ShieldActive { get; private set; }
        public InterrogationStatus Status { get; private set; }
        public bool GuidedFirstPhase { get; }
        public bool EmotionEchoUnlocked { get; set; }
        public IReadOnlyCollection<EvidenceData> AvailableEvidence => _evidence;

        public bool IsCoreContradictUnlocked
        {
            get
            {
                var phase = CurrentPhase;
                if (phase == null) return true;
                if (!AllMoves(phase).Any(m => m != null && m.isCoreContradiction)) return true;
                return _pressedKeywords.Overlaps(phase.keywords ?? Array.Empty<string>());
            }
        }

        public Aura PortraitAura
        {
            get
            {
                var phase = CurrentPhase;
                if (!EmotionEchoUnlocked || phase == null) return Aura.None;
                if (!_pressedKeywords.Overlaps(phase.keywords ?? Array.Empty<string>()))
                    return Aura.None;
                return FirstPressMove(phase)?.emotionAura ?? Aura.None;
            }
        }

        public void AddEvidence(EvidenceData e)
        {
            if (e != null) _evidence.Add(e);
        }

        public bool IsKeywordPressed(string keyword) =>
            keyword != null && _pressedKeywords.Contains(keyword);

        public Aura GetKeywordAura(string keyword)
        {
            var phase = CurrentPhase;
            if (!EmotionEchoUnlocked || phase == null || string.IsNullOrEmpty(keyword))
                return Aura.None;
            if (!(phase.keywords ?? Array.Empty<string>()).Contains(keyword, StringComparer.Ordinal))
                return Aura.None;
            return FirstPressMove(phase)?.emotionAura ?? Aura.None;
        }

        public MoveResult Press(string keyword)
        {
            EnsureInProgress();
            if (string.IsNullOrEmpty(keyword))
                throw new ArgumentException("Keyword must not be null or empty.", nameof(keyword));
            var phase = CurrentPhase;
            if (!(phase.keywords ?? Array.Empty<string>()).Contains(keyword, StringComparer.Ordinal))
                throw new ArgumentException(
                    $"Keyword '{keyword}' is not in the current claim.", nameof(keyword));

            _pressedKeywords.Add(keyword);
            var move = FirstPressMove(phase);
            return new MoveResult(MoveOutcome.PressResponse, move, move?.responseDialogue,
                PhaseIndex, false, false, false, Status);
        }

        public MoveResult Contradict(EvidenceData evidence) => DoMove(Verb.Contradict, evidence);

        public MoveResult PresentEvidence(EvidenceData evidence) => DoMove(Verb.PresentEvidence, evidence);

        public void Reset()
        {
            PhaseIndex = 0;
            SegmentsRemaining = Data.phases.Length;
            StrikesLost = 0;
            ShieldActive = Data.falsehoodShieldActive;
            _pressedKeywords.Clear();
            Status = InterrogationStatus.InProgress;
        }

        MoveResult DoMove(Verb verb, EvidenceData evidence)
        {
            EnsureInProgress();
            if (evidence == null) throw new ArgumentNullException(nameof(evidence));
            if (!_evidence.Contains(evidence))
                throw new ArgumentException("Evidence is not available.", nameof(evidence));

            int phaseIndex = PhaseIndex;
            var phase = CurrentPhase;
            var match = AllMoves(phase)
                .FirstOrDefault(m => m != null && m.verb == verb && m.evidence == evidence);

            if (match != null && match.breaksComposureSegment)
            {
                if (match.isCoreContradiction && !IsCoreContradictUnlocked)
                    return new MoveResult(MoveOutcome.Locked, match, match.responseDialogue,
                        phaseIndex, false, false, false, Status);

                SegmentsRemaining--;
                bool shieldShattered = phaseIndex == 0 && ShieldActive;
                if (shieldShattered) ShieldActive = false;
                PhaseIndex++;
                _pressedKeywords.Clear();
                if (PhaseIndex >= Data.phases.Length)
                    Status = InterrogationStatus.Confessed;
                return new MoveResult(MoveOutcome.Correct, match, match.responseDialogue,
                    phaseIndex, true, shieldShattered, false, Status);
            }

            bool strikeCounted = !(GuidedFirstPhase && phaseIndex == 0);
            if (strikeCounted)
            {
                StrikesLost++;
                if (StrikesLost >= MaxStrikes)
                    Status = InterrogationStatus.Mistrial;
            }
            return new MoveResult(MoveOutcome.Wrong, match, match?.responseDialogue,
                phaseIndex, false, false, strikeCounted, Status);
        }

        void EnsureInProgress()
        {
            if (Status != InterrogationStatus.InProgress)
                throw new InvalidOperationException($"Interrogation is {Status}.");
        }

        static IEnumerable<MoveData> AllMoves(InterrogationPhase phase) =>
            (phase.validMoves ?? Array.Empty<MoveData>())
                .Concat(phase.invalidMoves ?? Array.Empty<MoveData>());

        static MoveData FirstPressMove(InterrogationPhase phase) =>
            (phase.validMoves ?? Array.Empty<MoveData>())
                .FirstOrDefault(m => m != null && m.verb == Verb.Press);
    }
}

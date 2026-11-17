using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Morsoul.Data
{
    [CreateAssetMenu(menuName = "Morsoul/Case")]
    public class CaseData : ScriptableObject
    {
        public int caseId;
        public string title;
        public string region;
        public InquiryNodeData[] inquiryNodes;
        public HypothesisData hypothesis;
        public InterrogationData interrogation;
        public ClueData[] clues;
        public RunicLogData latentLog;
        public Tool toolUnlocked;
        public string uneaseFlagKey;
        public bool hasInvestigationScene;
        public bool hasInterrogation;
        public ClueData[] preloadedClues;
        public EvidenceGraphData evidenceGraph;

        public List<string> Validate()
        {
            var errors = new List<string>();
            var clueList = clues ?? new ClueData[0];
            var nodeList = inquiryNodes ?? new InquiryNodeData[0];

            // Rule 1: legacy slot-pair structure is required only for cases
            // without a weighted evidence graph. Weighted cases carry no
            // inquiry nodes or hypothesis.
            if (evidenceGraph == null)
            {
                if (hypothesis == null)
                    errors.Add("Case has no hypothesis.");
                if (nodeList.Length != 2)
                    errors.Add($"Case requires exactly 2 inquiry nodes, found {nodeList.Length}.");
                for (int i = 0; i < nodeList.Length; i++)
                    if (nodeList[i] == null)
                        errors.Add($"Inquiry node at index {i} is null.");
            }

            // Rule 2: clues are non-null, uniquely and non-emptily identified, belong to this case,
            // named, and backtracking-only clues are Latent.
            var clueIds = new HashSet<string>();
            for (int i = 0; i < clueList.Length; i++)
            {
                var clue = clueList[i];
                if (clue == null)
                {
                    errors.Add($"Clue at index {i} is null.");
                    continue;
                }
                string clueName = string.IsNullOrEmpty(clue.id) ? $"index {i}" : clue.id;
                if (string.IsNullOrEmpty(clue.id))
                    errors.Add($"Clue at index {i} has an empty id.");
                else if (!clueIds.Add(clue.id))
                    errors.Add($"Duplicate clue id '{clue.id}'.");
                if (clue.caseId != caseId)
                    errors.Add($"Clue '{clueName}' has caseId {clue.caseId}, expected {caseId}.");
                if (string.IsNullOrEmpty(clue.displayName))
                    errors.Add($"Clue '{clueName}' has an empty displayName.");
                if (clue.isBacktrackingOnly && clue.type != ClueType.Latent)
                    errors.Add($"Clue '{clueName}' is backtracking-only but its type is {clue.type}, not Latent.");
            }

            // Rule 3: per-node slot, premise, rejection and prompt checks.
            foreach (var node in nodeList)
            {
                if (node == null) continue;
                string nodeName = string.IsNullOrEmpty(node.id) ? "<unnamed node>" : node.id;

                if (node.slotA == null)
                    errors.Add($"Node '{nodeName}' has no slotA clue.");
                if (node.slotB == null)
                    errors.Add($"Node '{nodeName}' has no slotB clue.");
                if (node.slotA != null && node.slotA == node.slotB)
                    errors.Add($"Node '{nodeName}' uses the same clue in slotA and slotB.");
                foreach (var slot in new[] { node.slotA, node.slotB })
                {
                    if (slot == null) continue;
                    if (!clueList.Contains(slot))
                        errors.Add($"Node '{nodeName}' slot clue '{slot.id}' is not in the case's clue list.");
                    if (slot.type == ClueType.RedHerring)
                        errors.Add($"Node '{nodeName}' slot clue '{slot.id}' is a RedHerring.");
                }
                if (node.producesPremise == null)
                    errors.Add($"Node '{nodeName}' produces no premise.");

                var rejected = node.rejectedClues ?? new ClueData[0];
                foreach (var rejectedClue in rejected)
                {
                    if (rejectedClue == null || !clueList.Contains(rejectedClue))
                        errors.Add($"Node '{nodeName}' rejected clue is null or not in the case's clue list.");
                    else if (rejectedClue.type != ClueType.RedHerring)
                        errors.Add($"Node '{nodeName}' rejected clue '{rejectedClue.id}' is not a RedHerring.");
                }
                if (rejected.Length > 0 && string.IsNullOrEmpty(node.rejectionMessage))
                    errors.Add($"Node '{nodeName}' rejects clues but has no rejectionMessage.");

                if (node.prompt == null || node.prompt.IsEmpty)
                {
                    errors.Add($"Node '{nodeName}' requires a causal prompt.");
                }
                else
                {
                    var options = node.prompt.options;
                    if (options == null || options.Length != 3 || options.Any(string.IsNullOrEmpty))
                        errors.Add($"Node '{nodeName}' prompt must have exactly 3 non-empty options.");
                    if (node.prompt.correctIndex < 0 || node.prompt.correctIndex > 2)
                        errors.Add($"Node '{nodeName}' prompt correctIndex {node.prompt.correctIndex} is out of range [0,2].");
                    if (string.IsNullOrEmpty(node.prompt.hintText))
                        errors.Add($"Node '{nodeName}' prompt has an empty hintText.");
                }
            }

            // Rule 4: hypothesis premises equal the two nodes' produced premises.
            var producedPremises = nodeList
                .Where(n => n != null && n.producesPremise != null)
                .Select(n => n.producesPremise)
                .ToList();
            if (hypothesis != null && producedPremises.Count == nodeList.Length && nodeList.Length == 2)
            {
                if (hypothesis.premise1 == null || hypothesis.premise2 == null)
                    errors.Add("Hypothesis has a null premise.");
                else if (hypothesis.premise1 == hypothesis.premise2)
                    errors.Add("Hypothesis uses the same premise twice.");
                else if (!new HashSet<PremiseData> { hypothesis.premise1, hypothesis.premise2 }
                             .SetEquals(producedPremises))
                    errors.Add("Hypothesis premises do not match the premises produced by the inquiry nodes.");
            }

            // Rule 5: interrogation presence flag consistency.
            if (hasInterrogation != (interrogation != null))
                errors.Add($"hasInterrogation is {hasInterrogation} but interrogation is {(interrogation == null ? "null" : "set")}.");
            if (hypothesis != null && hypothesis.unlocksInterrogation != interrogation)
                errors.Add("hypothesis.unlocksInterrogation does not match the case's interrogation.");

            // Rule 6: interrogation structure.
            if (interrogation != null)
            {
                var phases = interrogation.phases ?? new InterrogationPhase[0];
                if (phases.Length != interrogation.composureSegments)
                    errors.Add($"Interrogation has {phases.Length} phases but composureSegments is {interrogation.composureSegments}.");

                var allowedEvidence = new HashSet<EvidenceData>(clueList.Where(c => c != null));
                allowedEvidence.UnionWith(producedPremises);
                int coreContradictionCount = 0;

                for (int i = 0; i < phases.Length; i++)
                {
                    var phase = phases[i];
                    if (phase == null)
                    {
                        errors.Add($"Interrogation phase {i} is null.");
                        continue;
                    }
                    var valid = phase.validMoves ?? new MoveData[0];
                    var invalid = phase.invalidMoves ?? new MoveData[0];

                    var breaking = valid.Concat(invalid).Where(m => m != null && m.breaksComposureSegment).ToList();
                    if (breaking.Count != 1)
                        errors.Add($"Phase {i} must have exactly one composure-breaking move, found {breaking.Count}.");
                    foreach (var move in breaking)
                    {
                        if (!valid.Contains(move))
                            errors.Add($"Phase {i} composure-breaking move must be in validMoves.");
                        if (move.strikeCost != 0)
                            errors.Add($"Phase {i} composure-breaking move must have strikeCost 0, found {move.strikeCost}.");
                    }

                    foreach (var move in valid.Concat(invalid))
                    {
                        if (move == null)
                        {
                            errors.Add($"Phase {i} contains a null move.");
                            continue;
                        }
                        bool isValid = valid.Contains(move);
                        if (move.verb == Verb.Press)
                        {
                            if (move.strikeCost != 0)
                                errors.Add($"Phase {i} Press move must have strikeCost 0.");
                            if (move.evidence != null)
                                errors.Add($"Phase {i} Press move must have null evidence.");
                            if (move.breaksComposureSegment)
                                errors.Add($"Phase {i} Press move cannot break a composure segment.");
                        }
                        if (isValid && move.verb != Verb.Press && !move.breaksComposureSegment)
                            errors.Add($"Phase {i} non-Press valid move must break the composure segment.");
                        if (!isValid)
                        {
                            if (move.strikeCost != 1)
                                errors.Add($"Phase {i} invalid move must have strikeCost 1, found {move.strikeCost}.");
                            if (move.breaksComposureSegment)
                                errors.Add($"Phase {i} invalid move cannot break a composure segment.");
                            if (move.verb == Verb.Press)
                                errors.Add($"Phase {i} invalid move cannot be a Press.");
                        }
                        if (move.isCoreContradiction)
                        {
                            coreContradictionCount++;
                            if (i != 0)
                                errors.Add($"Core contradiction move must be in phase 0, found in phase {i}.");
                            if (move.verb != Verb.Contradict)
                                errors.Add($"Phase {i} core contradiction move must use verb Contradict.");
                            if (!move.breaksComposureSegment)
                                errors.Add($"Phase {i} core contradiction move must break the composure segment.");
                        }
                        if (move.emotionAura == Aura.Crimson)
                            errors.Add($"Phase {i} move uses Aura.Crimson, which is cinematic-only.");
                        if (caseId < 2 && move.emotionAura != Aura.None)
                            errors.Add($"Phase {i} move uses aura {move.emotionAura}; Emotion Echo auras are not available before caseId 2.");
                        if (move.evidence != null && !allowedEvidence.Contains(move.evidence)
                            && !(evidenceGraph != null && move.evidence is PremiseData))
                            errors.Add($"Phase {i} move evidence '{move.evidence.id}' is not a clue of this case or a premise it produces.");
                    }
                }

                if (coreContradictionCount != 1)
                    errors.Add($"Interrogation must have exactly one core contradiction move, found {coreContradictionCount}.");

                if (phases.Length > 0 && phases[0] != null)
                {
                    var phase0Moves = (phases[0].validMoves ?? new MoveData[0])
                        .Concat(phases[0].invalidMoves ?? new MoveData[0]);
                    if (!phase0Moves.Any(m => m != null && m.verb == Verb.Press))
                        errors.Add("Phase 0 must contain a Press move (press-gated core contradiction).");
                    var keywords = phases[0].keywords;
                    if (keywords == null || keywords.Length == 0 || keywords.Any(string.IsNullOrEmpty))
                        errors.Add("Phase 0 must have non-empty keywords.");
                }
            }

            // Rule 7: a latent log requires a Latent clue and an unease flag key.
            if (latentLog != null)
            {
                if (!clueList.Any(c => c != null && c.type == ClueType.Latent))
                    errors.Add("Case has a latentLog but no Latent clue.");
                if (string.IsNullOrEmpty(uneaseFlagKey))
                    errors.Add("Case has a latentLog but an empty uneaseFlagKey.");
            }

            // Rule 8: board-only cases preload their clues.
            if (!hasInvestigationScene)
            {
                var preloaded = preloadedClues ?? new ClueData[0];
                if (preloaded.Length == 0)
                    errors.Add("Case has no investigation scene but no preloadedClues.");
                else if (preloaded.Any(c => c == null || !clueList.Contains(c)))
                    errors.Add("Every preloadedClues entry must be a clue of this case.");
            }

            // Rule 9: an attached weighted evidence graph must validate and
            // agree on the case id. Absence is allowed while cases migrate.
            if (evidenceGraph != null)
            {
                if (evidenceGraph.caseId != caseId)
                    errors.Add($"Evidence graph caseId {evidenceGraph.caseId} does not match case {caseId}.");
                errors.AddRange(evidenceGraph.Validate());
            }

            return errors;
        }
    }
}

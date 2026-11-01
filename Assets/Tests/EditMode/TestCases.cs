using System.Collections.Generic;
using Morsoul.Data;
using UnityEngine;

namespace Morsoul.Tests
{
    internal class Fixture
    {
        public CaseData caseData;
        public ClueData c1, c2, t1, t2, rh1;
        public PremiseData p1, p2;
        public InquiryNodeData n1, n2;
        public HypothesisData hypothesis;
        public InterrogationData interrogation;
        public readonly List<Object> assets = new List<Object>();
    }

    internal static class TestCases
    {
        static readonly List<Fixture> builtFixtures = new List<Fixture>();

        public static void DestroyAll()
        {
            foreach (var fixture in builtFixtures)
                foreach (var asset in fixture.assets)
                    if (asset != null)
                        Object.DestroyImmediate(asset);
            builtFixtures.Clear();
        }

        public static Fixture Build(int caseId, bool withInterrogation, bool boardOnly = false)
        {
            if (boardOnly) withInterrogation = false;

            var f = new Fixture();
            builtFixtures.Add(f);

            ClueData NewClue(string id, ClueType type)
            {
                var clue = ScriptableObject.CreateInstance<ClueData>();
                clue.id = id;
                clue.displayName = id + " Display Name";
                clue.type = type;
                clue.caseId = caseId;
                clue.description = "Description.";
                clue.dissonanceKeyword = "";
                f.assets.Add(clue);
                return clue;
            }

            f.c1 = NewClue("C1_0" + caseId, ClueType.Physical);
            f.c2 = NewClue("C2_0" + caseId, ClueType.Physical);
            f.t1 = NewClue("T1_0" + caseId, ClueType.Testimony);
            f.t2 = NewClue("T2_0" + caseId, ClueType.Testimony);
            f.rh1 = NewClue("RH1_0" + caseId, ClueType.RedHerring);

            PremiseData NewPremise(string id)
            {
                var premise = ScriptableObject.CreateInstance<PremiseData>();
                premise.id = id;
                premise.title = id;
                premise.proposition = "Proposition.";
                f.assets.Add(premise);
                return premise;
            }

            f.p1 = NewPremise("P1_0" + caseId);
            f.p2 = NewPremise("P2_0" + caseId);

            CausalPromptData Prompt() => new CausalPromptData
            {
                question = "What follows?",
                options = new[] { "Option A.", "Option B.", "Option C." },
                correctIndex = 0,
                hintText = "Hint.",
            };

            InquiryNodeData NewNode(string id, ClueData slotA, ClueData slotB, PremiseData produces,
                ClueData[] rejected, string rejectionMessage)
            {
                var node = ScriptableObject.CreateInstance<InquiryNodeData>();
                node.id = id;
                node.slotA = slotA;
                node.slotB = slotB;
                node.prompt = Prompt();
                node.producesPremise = produces;
                node.rejectedClues = rejected;
                node.rejectionMessage = rejectionMessage;
                f.assets.Add(node);
                return node;
            }

            f.n1 = NewNode("IN1_0" + caseId, f.c1, f.t1, f.p1, new[] { f.rh1 }, "Rejected.");
            f.n2 = NewNode("IN2_0" + caseId, f.c2, f.t2, f.p2, new ClueData[0], "");

            if (withInterrogation)
            {
                MoveData Press() => new MoveData
                {
                    verb = Verb.Press, evidence = null, breaksComposureSegment = false,
                    strikeCost = 0, isCoreContradiction = false,
                    responseDialogue = "Response.", emotionAura = Aura.None,
                };
                MoveData Correct(Verb verb, EvidenceData evidence, bool core = false) => new MoveData
                {
                    verb = verb, evidence = evidence, breaksComposureSegment = true,
                    strikeCost = 0, isCoreContradiction = core,
                    responseDialogue = "Response.", emotionAura = Aura.None,
                };
                MoveData Wrong(Verb verb, EvidenceData evidence) => new MoveData
                {
                    verb = verb, evidence = evidence, breaksComposureSegment = false,
                    strikeCost = 1, isCoreContradiction = false,
                    responseDialogue = "Response.", emotionAura = Aura.None,
                };

                f.interrogation = ScriptableObject.CreateInstance<InterrogationData>();
                f.interrogation.id = "INT_0" + caseId;
                f.interrogation.suspectName = "Suspect";
                f.interrogation.composureSegments = 3;
                f.interrogation.phases = new[]
                {
                    new InterrogationPhase
                    {
                        suspectClaim = "Claim.",
                        keywords = new[] { "keyword" },
                        validMoves = new[] { Press(), Correct(Verb.Contradict, f.c1, core: true) },
                        invalidMoves = new MoveData[0],
                    },
                    new InterrogationPhase
                    {
                        suspectClaim = "Claim.",
                        keywords = new string[0],
                        validMoves = new[] { Correct(Verb.PresentEvidence, f.p1) },
                        invalidMoves = new[] { Wrong(Verb.PresentEvidence, f.rh1) },
                    },
                    new InterrogationPhase
                    {
                        suspectClaim = "Claim.",
                        keywords = new string[0],
                        validMoves = new[] { Correct(Verb.Contradict, f.c2) },
                        invalidMoves = new MoveData[0],
                    },
                };
                f.assets.Add(f.interrogation);
            }

            f.hypothesis = ScriptableObject.CreateInstance<HypothesisData>();
            f.hypothesis.id = "H_0" + caseId;
            f.hypothesis.premise1 = f.p1;
            f.hypothesis.premise2 = f.p2;
            f.hypothesis.verdictProposition = "Verdict.";
            f.hypothesis.unlocksInterrogation = f.interrogation;
            f.assets.Add(f.hypothesis);

            f.caseData = ScriptableObject.CreateInstance<CaseData>();
            f.caseData.caseId = caseId;
            f.caseData.title = "Test Case";
            f.caseData.region = "Test Region";
            f.caseData.inquiryNodes = new[] { f.n1, f.n2 };
            f.caseData.hypothesis = f.hypothesis;
            f.caseData.interrogation = f.interrogation;
            f.caseData.clues = new[] { f.c1, f.c2, f.t1, f.t2, f.rh1 };
            f.caseData.latentLog = null;
            f.caseData.toolUnlocked = Morsoul.Data.Tool.None;
            f.caseData.uneaseFlagKey = "";
            f.caseData.hasInvestigationScene = !boardOnly;
            f.caseData.hasInterrogation = withInterrogation;
            f.caseData.preloadedClues = boardOnly
                ? new[] { f.c1, f.c2, f.t1, f.t2 }
                : new ClueData[0];
            f.assets.Add(f.caseData);

            return f;
        }
    }
}

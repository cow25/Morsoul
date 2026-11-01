using System.Collections.Generic;
using Morsoul.Data;
using Morsoul.Data.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Morsoul.Tests
{
    public class CaseDataValidationTests
    {
        class Fixture
        {
            public CaseData caseData;
            public ClueData c1, c2, t1, t2, rh1;
            public PremiseData p1, p2;
            public InquiryNodeData n1, n2;
            public HypothesisData hypothesis;
            public InterrogationData interrogation;
            public readonly List<Object> assets = new List<Object>();
        }

        readonly List<Fixture> fixtures = new List<Fixture>();

        [TearDown]
        public void TearDown()
        {
            foreach (var fixture in fixtures)
                foreach (var asset in fixture.assets)
                    if (asset != null)
                        Object.DestroyImmediate(asset);
            fixtures.Clear();
        }

        Fixture BuildValidCase(int caseId, bool withInterrogation)
        {
            var f = new Fixture();
            fixtures.Add(f);

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
            f.caseData.hasInvestigationScene = true;
            f.caseData.hasInterrogation = withInterrogation;
            f.caseData.preloadedClues = new ClueData[0];
            f.assets.Add(f.caseData);

            return f;
        }

        static void AssertValid(List<string> errors) =>
            Assert.That(errors, Is.Empty, string.Join("\n", errors));

        static void AssertInvalid(List<string> errors, string substring)
        {
            Assert.That(errors, Is.Not.Empty);
            Assert.That(errors, Has.Some.Contains(substring));
        }

        [Test]
        public void ValidCase_PassesValidation()
        {
            AssertValid(BuildValidCase(caseId: 1, withInterrogation: true).caseData.Validate());
        }

        [Test]
        public void PromptCorrectIndexOutOfRange_Fails()
        {
            var f = BuildValidCase(1, true);
            f.n1.prompt.correctIndex = 3;
            AssertInvalid(f.caseData.Validate(), "correctIndex");
        }

        [Test]
        public void PromptWithTwoOptions_Fails()
        {
            var f = BuildValidCase(1, true);
            f.n1.prompt.options = new[] { "Option A.", "Option B." };
            AssertInvalid(f.caseData.Validate(), "exactly 3");
        }

        [Test]
        public void PhaseWithTwoBreakingMoves_Fails()
        {
            var f = BuildValidCase(1, true);
            f.interrogation.phases[1].invalidMoves[0].breaksComposureSegment = true;
            AssertInvalid(f.caseData.Validate(), "exactly one composure-breaking");
        }

        [Test]
        public void PhaseWithZeroBreakingMoves_Fails()
        {
            var f = BuildValidCase(1, true);
            f.interrogation.phases[1].validMoves[0].breaksComposureSegment = false;
            AssertInvalid(f.caseData.Validate(), "exactly one composure-breaking");
        }

        [Test]
        public void InvalidMoveWithZeroStrikeCost_Fails()
        {
            var f = BuildValidCase(1, true);
            f.interrogation.phases[1].invalidMoves[0].strikeCost = 0;
            AssertInvalid(f.caseData.Validate(), "strikeCost 1");
        }

        [Test]
        public void PressMoveWithStrikeCost_Fails()
        {
            var f = BuildValidCase(1, true);
            f.interrogation.phases[0].validMoves[0].strikeCost = 1;
            AssertInvalid(f.caseData.Validate(), "Press");
        }

        [Test]
        public void HypothesisPremiseNotProducedByAnyNode_Fails()
        {
            var f = BuildValidCase(1, true);
            var stray = ScriptableObject.CreateInstance<PremiseData>();
            stray.id = "P_STRAY";
            f.assets.Add(stray);
            f.hypothesis.premise2 = stray;
            AssertInvalid(f.caseData.Validate(), "do not match");
        }

        [Test]
        public void RejectedClueNotRedHerring_Fails()
        {
            var f = BuildValidCase(1, true);
            f.n1.rejectedClues = new[] { f.t2 };
            AssertInvalid(f.caseData.Validate(), "not a RedHerring");
        }

        [Test]
        public void SlotClueNotInCaseClues_Fails()
        {
            var f = BuildValidCase(1, true);
            var outsider = ScriptableObject.CreateInstance<ClueData>();
            outsider.id = "C9_01";
            outsider.displayName = "Outsider";
            outsider.type = ClueType.Physical;
            outsider.caseId = 1;
            f.assets.Add(outsider);
            f.n1.slotA = outsider;
            AssertInvalid(f.caseData.Validate(), "not in the case's clue list");
        }

        [Test]
        public void AmberAuraInCaseBeforeEmotionEcho_Fails()
        {
            var f = BuildValidCase(1, true);
            f.interrogation.phases[1].validMoves[0].emotionAura = Aura.Amber;
            AssertInvalid(f.caseData.Validate(), "caseId 3");
        }

        [Test]
        public void CrimsonAura_Fails()
        {
            var f = BuildValidCase(1, true);
            f.interrogation.phases[0].validMoves[1].emotionAura = Aura.Crimson;
            AssertInvalid(f.caseData.Validate(), "Crimson");
        }

        [Test]
        public void CoreContradictionInLaterPhase_Fails()
        {
            var f = BuildValidCase(1, true);
            f.interrogation.phases[0].validMoves[1].isCoreContradiction = false;
            f.interrogation.phases[1].validMoves[0].isCoreContradiction = true;
            AssertInvalid(f.caseData.Validate(), "phase 0");
        }

        [Test]
        public void EmptyPromptOutsidePrologue_Fails()
        {
            var f = BuildValidCase(1, true);
            f.n1.prompt = new CausalPromptData();
            AssertInvalid(f.caseData.Validate(), "causal prompt");
        }

        [Test]
        public void HasInterrogationButInterrogationNull_Fails()
        {
            var f = BuildValidCase(1, true);
            f.caseData.interrogation = null;
            AssertInvalid(f.caseData.Validate(), "hasInterrogation");
        }

        [Test]
        public void PrologueEmptyPrompt_Fails()
        {
            var f = BuildValidCase(caseId: 0, withInterrogation: false);
            f.n1.prompt = new CausalPromptData();
            AssertInvalid(f.caseData.Validate(), "causal prompt");
        }

        [Test]
        public void GeneratedAssets_AreValid()
        {
            const string root = "Assets/__MorsoulTestTemp";
            // Guarantee the cold path: leftover assets from a previous run would mask
            // the import race where CreateAsset'd objects are not yet loadable.
            AssetDatabase.DeleteAsset(root);
            try
            {
                CaseDataGenerator.Generate(root);

                var case0 = AssetDatabase.LoadAssetAtPath<CaseData>($"{root}/Case00/Case_00.asset");
                var case1 = AssetDatabase.LoadAssetAtPath<CaseData>($"{root}/Case01/Case_01.asset");
                Assert.NotNull(case0, "Case_00.asset was not generated.");
                Assert.NotNull(case1, "Case_01.asset was not generated.");

                AssertValid(case0.Validate());
                AssertValid(case1.Validate());

                Assert.That(case1.clues.Length, Is.EqualTo(7));
                Assert.That(case1.interrogation.phases.Length, Is.EqualTo(3));

                var in1_00 = AssetDatabase.LoadAssetAtPath<InquiryNodeData>($"{root}/Case00/IN1_00.asset");
                var in2_00 = AssetDatabase.LoadAssetAtPath<InquiryNodeData>($"{root}/Case00/IN2_00.asset");
                Assert.NotNull(in1_00, "IN1_00.asset was not generated.");
                Assert.NotNull(in2_00, "IN2_00.asset was not generated.");
                Assert.That(in1_00.prompt.correctIndex, Is.EqualTo(1));
                Assert.That(in2_00.prompt.correctIndex, Is.EqualTo(2));
                Assert.That(in1_00.prompt.options[1],
                    Is.EqualTo("A catastrophic overload cut the power, leaving the security doors locked in an unpowered failsafe."));

                var prompt = case1.inquiryNodes[0].prompt;
                Assert.That(prompt.options[prompt.correctIndex],
                    Is.EqualTo("The vault was opened with one of the two registered keys \u2014 no forced breach occurred."));
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
            }
        }
    }
}

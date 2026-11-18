using System.Collections.Generic;
using System.Linq;
using Morsoul.Data;
using Morsoul.Data.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Morsoul.Tests
{
    public class CaseDataValidationTests
    {
        [TearDown]
        public void TearDown() => TestCases.DestroyAll();

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
            AssertValid(TestCases.Build(caseId: 1, withInterrogation: true).caseData.Validate());
        }

        [Test]
        public void PromptCorrectIndexOutOfRange_Fails()
        {
            var f = TestCases.Build(1, true);
            f.n1.prompt.correctIndex = 3;
            AssertInvalid(f.caseData.Validate(), "correctIndex");
        }

        [Test]
        public void PromptWithTwoOptions_Fails()
        {
            var f = TestCases.Build(1, true);
            f.n1.prompt.options = new[] { "Option A.", "Option B." };
            AssertInvalid(f.caseData.Validate(), "exactly 3");
        }

        [Test]
        public void PhaseWithTwoBreakingMoves_Fails()
        {
            var f = TestCases.Build(1, true);
            f.interrogation.phases[1].invalidMoves[0].breaksComposureSegment = true;
            AssertInvalid(f.caseData.Validate(), "exactly one composure-breaking");
        }

        [Test]
        public void PhaseWithZeroBreakingMoves_Fails()
        {
            var f = TestCases.Build(1, true);
            f.interrogation.phases[1].validMoves[0].breaksComposureSegment = false;
            AssertInvalid(f.caseData.Validate(), "exactly one composure-breaking");
        }

        [Test]
        public void InvalidMoveWithZeroStrikeCost_Fails()
        {
            var f = TestCases.Build(1, true);
            f.interrogation.phases[1].invalidMoves[0].strikeCost = 0;
            AssertInvalid(f.caseData.Validate(), "strikeCost 1");
        }

        [Test]
        public void PressMoveWithStrikeCost_Fails()
        {
            var f = TestCases.Build(1, true);
            f.interrogation.phases[0].validMoves[0].strikeCost = 1;
            AssertInvalid(f.caseData.Validate(), "Press");
        }

        [Test]
        public void HypothesisPremiseNotProducedByAnyNode_Fails()
        {
            var f = TestCases.Build(1, true);
            var stray = ScriptableObject.CreateInstance<PremiseData>();
            stray.id = "P_STRAY";
            f.assets.Add(stray);
            f.hypothesis.premise2 = stray;
            AssertInvalid(f.caseData.Validate(), "do not match");
        }

        [Test]
        public void RejectedClueNotRedHerring_Fails()
        {
            var f = TestCases.Build(1, true);
            f.n1.rejectedClues = new[] { f.t2 };
            AssertInvalid(f.caseData.Validate(), "not a RedHerring");
        }

        [Test]
        public void SlotClueNotInCaseClues_Fails()
        {
            var f = TestCases.Build(1, true);
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
            var f = TestCases.Build(1, true);
            f.interrogation.phases[1].validMoves[0].emotionAura = Aura.Amber;
            AssertInvalid(f.caseData.Validate(), "caseId 2");
        }

        [Test]
        public void CrimsonAura_Fails()
        {
            var f = TestCases.Build(1, true);
            f.interrogation.phases[0].validMoves[1].emotionAura = Aura.Crimson;
            AssertInvalid(f.caseData.Validate(), "Crimson");
        }

        [Test]
        public void CoreContradictionInLaterPhase_Fails()
        {
            var f = TestCases.Build(1, true);
            f.interrogation.phases[0].validMoves[1].isCoreContradiction = false;
            f.interrogation.phases[1].validMoves[0].isCoreContradiction = true;
            AssertInvalid(f.caseData.Validate(), "phase 0");
        }

        [Test]
        public void EmptyPromptOutsidePrologue_Fails()
        {
            var f = TestCases.Build(1, true);
            f.n1.prompt = new CausalPromptData();
            AssertInvalid(f.caseData.Validate(), "causal prompt");
        }

        [Test]
        public void HasInterrogationButInterrogationNull_Fails()
        {
            var f = TestCases.Build(1, true);
            f.caseData.interrogation = null;
            AssertInvalid(f.caseData.Validate(), "hasInterrogation");
        }

        [Test]
        public void PrologueEmptyPrompt_Fails()
        {
            var f = TestCases.Build(caseId: 0, withInterrogation: false);
            f.n1.prompt = new CausalPromptData();
            AssertInvalid(f.caseData.Validate(), "causal prompt");
        }

        [Test]
        public void GeneratedAssets_AreValid()
        {
            string root = "Assets/__MorsoulTest01_" + System.Guid.NewGuid().ToString("N");
            try
            {
                CaseDataGenerator.Generate(root);
                AssetDatabase.ImportAsset($"{root}/Case00/Case_00.asset", ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset($"{root}/Case01/Case_01.asset", ImportAssetOptions.ForceSynchronousImport);

                var case0 = AssetDatabase.LoadAssetAtPath<CaseData>($"{root}/Case00/Case_00.asset");
                var case1 = AssetDatabase.LoadAssetAtPath<CaseData>($"{root}/Case01/Case_01.asset");
                Assert.NotNull(case0, "Case_00.asset was not generated.");
                Assert.NotNull(case1, "Case_01.asset was not generated.");

                AssertValid(case0.Validate());
                AssertValid(case1.Validate());

                Assert.That(case1.clues.Length, Is.EqualTo(7));
                Assert.That(case1.interrogation.phases.Length, Is.EqualTo(3));

                // Weighted canon: generated cases link the canonical evidence
                // graph and carry no legacy inquiry nodes or hypothesis.
                Assert.That(case0.evidenceGraph, Is.Not.Null);
                Assert.That(case1.evidenceGraph, Is.Not.Null);
                Assert.That(case0.inquiryNodes, Is.Null.Or.Empty);
                Assert.That(case0.hypothesis, Is.Null);
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
            }
        }

        [Test]
        public void Case02_GeneratedAssets_AreValid()
        {
            string root = "Assets/__MorsoulTest02_" + System.Guid.NewGuid().ToString("N");
            try
            {
                CaseDataGenerator.GenerateCase2Assets(root);
                AssetDatabase.ImportAsset($"{root}/Case02/Case_02.asset", ImportAssetOptions.ForceSynchronousImport);

                var case2 = AssetDatabase.LoadAssetAtPath<CaseData>($"{root}/Case02/Case_02.asset");
                Assert.NotNull(case2, "Case_02.asset was not generated.");

                AssertValid(case2.Validate());

                Assert.That(case2.clues.Length, Is.EqualTo(7));
                Assert.That(case2.clues.Any(c => c != null && c.type == ClueType.Latent), Is.True);
                Assert.That(case2.latentLog, Is.Not.Null);

                Assert.That(case2.evidenceGraph, Is.Not.Null);

                var phases = case2.interrogation.phases;
                Assert.That(phases.Length, Is.EqualTo(3));
                foreach (var phase in phases)
                    Assert.That(phase.validMoves.Count(m => m != null && m.breaksComposureSegment),
                        Is.EqualTo(1));
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
            }
        }

        [Test]
        public void Finale_GeneratedAssets_MatchCaseContractsAndPreserveGuids()
        {
            string root = "Assets/__MorsoulFinaleTest_" + System.Guid.NewGuid().ToString("N");
            try
            {
                CaseDataGenerator.GenerateFinaleAssets(root);
                var case4 = AssetDatabase.LoadAssetAtPath<CaseData>($"{root}/Case04/Case_04.asset");
                var case5 = AssetDatabase.LoadAssetAtPath<CaseData>($"{root}/Case05/Case_05.asset");
                Assert.NotNull(case4);
                Assert.NotNull(case5);
                AssertValid(case4.Validate());
                AssertValid(case5.Validate());
                Assert.That(case4.clues.Length, Is.EqualTo(7));
                Assert.That(case4.toolUnlocked, Is.EqualTo(Morsoul.Data.Tool.AetherSight));
                Assert.That(case4.latentLog.id, Is.EqualTo(4));
                Assert.That(case4.latentLog.availableAfterCase, Is.EqualTo(4));
                Assert.That(case4.clues.Single(c => c.type == ClueType.Latent).isBacktrackingOnly, Is.False);
                Assert.That(case4.evidenceGraph, Is.Not.Null);
                Assert.That(case4.inquiryNodes, Is.Null.Or.Empty);
                Assert.That(case4.hypothesis, Is.Null);
                Assert.That(case4.interrogation.phases[0].validMoves.Single(m => m.verb == Verb.Press).emotionAura,
                    Is.EqualTo(Aura.Amber));
                Assert.That(case4.interrogation.phases[1].validMoves.Single().emotionAura, Is.EqualTo(Aura.Violet));
                Assert.That(case5.hasInvestigationScene, Is.False);
                Assert.That(case5.hasInterrogation, Is.False);
                Assert.That(case5.interrogation, Is.Null);
                Assert.That(case5.hypothesis, Is.Null);
                Assert.That(case5.evidenceGraph, Is.Not.Null);
                Assert.That(case5.latentLog, Is.Null);
                Assert.That(case5.clues.Length, Is.EqualTo(5));
                CollectionAssert.AreEquivalent(case5.clues, case5.preloadedClues);
                Assert.That(case5.clues.All(c => c.requiredTool == Morsoul.Data.Tool.None), Is.True);
                Assert.That(case5.clues.Any(c => c.type == ClueType.Latent), Is.False);
                string path = $"{root}/Case05/Case_05.asset";
                string guid = AssetDatabase.AssetPathToGUID(path);
                CaseDataGenerator.GenerateFinaleAssets(root);
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                AssertValid(AssetDatabase.LoadAssetAtPath<CaseData>(path).Validate());
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
            }
        }

        [Test]
        public void Case03_GeneratedAssets_AreValid()
        {
            string root = "Assets/__MorsoulTest03_" + System.Guid.NewGuid().ToString("N");
            try
            {
                CaseDataGenerator.GenerateCase3Assets(root);
                AssetDatabase.ImportAsset($"{root}/Case03/Case_03.asset", ImportAssetOptions.ForceSynchronousImport);

                var case3 = AssetDatabase.LoadAssetAtPath<CaseData>($"{root}/Case03/Case_03.asset");
                Assert.NotNull(case3, "Case_03.asset was not generated.");

                AssertValid(case3.Validate());

                Assert.That(case3.clues.Length, Is.EqualTo(7));
                Assert.That(case3.clues.Any(c => c != null && c.type == ClueType.Latent), Is.True);
                Assert.That(case3.latentLog, Is.Not.Null);
                Assert.That(case3.latentLog.id, Is.EqualTo(3));
                Assert.That(case3.toolUnlocked, Is.EqualTo(Morsoul.Data.Tool.EmotionEcho));

                Assert.That(case3.evidenceGraph, Is.Not.Null);

                var phases = case3.interrogation.phases;
                Assert.That(phases.Length, Is.EqualTo(3));
                foreach (var phase in phases)
                    Assert.That(phase.validMoves.Count(m => m != null && m.breaksComposureSegment),
                        Is.EqualTo(1));
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
            }
        }
    }
}

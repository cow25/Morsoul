using Morsoul.Board;
using Morsoul.Data;
using NUnit.Framework;
using UnityEngine;

namespace Morsoul.Tests
{
    public class DeductionBoardTests
    {
        [TearDown]
        public void TearDown() => TestCases.DestroyAll();

        [Test]
        public void Ctor_Throws_OnNullCase()
        {
            Assert.That(() => new DeductionBoard(null), Throws.ArgumentNullException);
        }

        [Test]
        public void Ctor_Throws_OnInvalidCase()
        {
            var f = TestCases.Build(1, true);
            f.caseData.interrogation = null;
            Assert.That(() => new DeductionBoard(f.caseData), Throws.ArgumentException);
        }

        [Test]
        public void TryPair_MatchingSlots_EitherOrder_OpensPrompt_AtZeroCost()
        {
            var f = TestCases.Build(1, true);
            var board = new DeductionBoard(f.caseData);

            var forward = board.TryPair(f.n1.slotA, f.n1.slotB);
            Assert.That(forward.Outcome, Is.EqualTo(PairingOutcome.OpensPrompt));
            Assert.That(forward.Node, Is.EqualTo(f.n1));
            Assert.That(forward.FocusCost, Is.EqualTo(0f));
            Assert.That(forward.StrikeCost, Is.EqualTo(0));
            Assert.That(board.PendingNode, Is.EqualTo(f.n1));

            board.CancelPrompt();

            var reverse = board.TryPair(f.n1.slotB, f.n1.slotA);
            Assert.That(reverse.Outcome, Is.EqualTo(PairingOutcome.OpensPrompt));
            Assert.That(reverse.Node, Is.EqualTo(f.n1));
            Assert.That(board.PendingNode, Is.EqualTo(f.n1));
        }

        [Test]
        public void CorrectAnswer_Validates_AddsPremise_ClearsPending()
        {
            var f = TestCases.Build(1, true);
            var board = new DeductionBoard(f.caseData);
            board.TryPair(f.n1.slotA, f.n1.slotB);

            var result = board.AnswerPrompt(f.n1.prompt.correctIndex);

            Assert.That(result.Outcome, Is.EqualTo(AnswerOutcome.Correct));
            Assert.That(result.Node, Is.EqualTo(f.n1));
            Assert.That(result.Premise, Is.EqualTo(f.p1));
            Assert.That(result.FocusCost, Is.EqualTo(0f));
            Assert.That(result.StrikeCost, Is.EqualTo(0));
            Assert.That(board.IsNodeValidated(f.n1), Is.True);
            Assert.That(board.Premises, Does.Contain(f.p1));
            Assert.That(board.PendingNode, Is.Null);
        }

        [Test]
        public void FirstWrongAnswer_Costs20_NoHint_ClearsPending()
        {
            var f = TestCases.Build(1, true);
            var board = new DeductionBoard(f.caseData);
            board.TryPair(f.n1.slotA, f.n1.slotB);

            var result = board.AnswerPrompt(1);

            Assert.That(result.Outcome, Is.EqualTo(AnswerOutcome.Wrong));
            Assert.That(result.Premise, Is.Null);
            Assert.That(result.FocusCost, Is.EqualTo(DeductionBoard.WrongCausalFocusCost));
            Assert.That(result.StrikeCost, Is.EqualTo(0));
            Assert.That(result.MissCount, Is.EqualTo(1));
            Assert.That(result.Hint, Is.Null);
            Assert.That(board.WrongCausalAnswers, Is.EqualTo(1));
            Assert.That(board.PendingNode, Is.Null);
            Assert.That(board.IsNodeValidated(f.n1), Is.False);
        }

        [Test]
        public void SecondWrongAnswer_SameNode_ShowsHint()
        {
            var f = TestCases.Build(1, true);
            var board = new DeductionBoard(f.caseData);

            board.TryPair(f.n1.slotA, f.n1.slotB);
            board.AnswerPrompt(1);
            board.TryPair(f.n1.slotA, f.n1.slotB);
            var result = board.AnswerPrompt(2);

            Assert.That(result.Outcome, Is.EqualTo(AnswerOutcome.Wrong));
            Assert.That(result.MissCount, Is.EqualTo(2));
            Assert.That(result.Hint, Is.EqualTo(f.n1.prompt.hintText));
            Assert.That(board.WrongCausalAnswers, Is.EqualTo(2));
        }

        [Test]
        public void MissCounts_ArePerNode()
        {
            var f = TestCases.Build(1, true);
            var board = new DeductionBoard(f.caseData);

            board.TryPair(f.n1.slotA, f.n1.slotB);
            board.AnswerPrompt(1);
            board.TryPair(f.n2.slotA, f.n2.slotB);
            var onN2 = board.AnswerPrompt(1);

            Assert.That(onN2.MissCount, Is.EqualTo(1));
            Assert.That(onN2.Hint, Is.Null);
            Assert.That(board.GetMissCount(f.n1), Is.EqualTo(1));
            Assert.That(board.GetMissCount(f.n2), Is.EqualTo(1));

            board.TryPair(f.n2.slotA, f.n2.slotB);
            var second = board.AnswerPrompt(2);
            Assert.That(second.MissCount, Is.EqualTo(2));
            Assert.That(second.Hint, Is.EqualTo(f.n2.prompt.hintText));
            Assert.That(board.GetMissCount(f.n1), Is.EqualTo(1));
        }

        [Test]
        public void RedHerringWithSlotClueOfRejectingNode_Rejected()
        {
            var f = TestCases.Build(1, true);
            var board = new DeductionBoard(f.caseData);

            var result = board.TryPair(f.rh1, f.n1.slotA);

            Assert.That(result.Outcome, Is.EqualTo(PairingOutcome.Rejected));
            Assert.That(result.Node, Is.EqualTo(f.n1));
            Assert.That(result.Message, Is.EqualTo(f.n1.rejectionMessage));
            Assert.That(result.FocusCost, Is.EqualTo(DeductionBoard.InvalidPairingFocusCost));
            Assert.That(result.StrikeCost, Is.EqualTo(0));
            Assert.That(board.PendingNode, Is.Null);
        }

        [Test]
        public void UnrelatedPair_Invalid()
        {
            var f = TestCases.Build(1, true);
            var board = new DeductionBoard(f.caseData);

            var result = board.TryPair(f.rh1, f.c2);

            Assert.That(result.Outcome, Is.EqualTo(PairingOutcome.Invalid));
            Assert.That(result.Node, Is.Null);
            Assert.That(result.Message, Is.EqualTo(DeductionBoard.InvalidPairingMessage));
            Assert.That(result.FocusCost, Is.EqualTo(DeductionBoard.InvalidPairingFocusCost));
            Assert.That(result.StrikeCost, Is.EqualTo(0));
        }

        [Test]
        public void RePairingValidatedNode_AlreadyValidated_ZeroCost()
        {
            var f = TestCases.Build(1, true);
            var board = new DeductionBoard(f.caseData);
            board.TryPair(f.n1.slotA, f.n1.slotB);
            board.AnswerPrompt(f.n1.prompt.correctIndex);

            var result = board.TryPair(f.n1.slotA, f.n1.slotB);

            Assert.That(result.Outcome, Is.EqualTo(PairingOutcome.AlreadyValidated));
            Assert.That(result.Node, Is.EqualTo(f.n1));
            Assert.That(result.FocusCost, Is.EqualTo(0f));
            Assert.That(result.StrikeCost, Is.EqualTo(0));
            Assert.That(board.PendingNode, Is.Null);
        }

        [Test]
        public void TryPair_WhilePromptPending_Throws()
        {
            var f = TestCases.Build(1, true);
            var board = new DeductionBoard(f.caseData);
            board.TryPair(f.n1.slotA, f.n1.slotB);

            Assert.That(() => board.TryPair(f.n2.slotA, f.n2.slotB),
                Throws.InvalidOperationException);
        }

        [Test]
        public void TryPair_SameOrNullClue_Throws()
        {
            var f = TestCases.Build(1, true);
            var board = new DeductionBoard(f.caseData);

            Assert.That(() => board.TryPair(f.c1, f.c1), Throws.ArgumentException);
            Assert.That(() => board.TryPair(null, f.c1), Throws.ArgumentNullException);
            Assert.That(() => board.TryPair(f.c1, null), Throws.ArgumentNullException);
        }

        [Test]
        public void AnswerPrompt_WithoutPending_Throws()
        {
            var f = TestCases.Build(1, true);
            var board = new DeductionBoard(f.caseData);

            Assert.That(() => board.AnswerPrompt(0), Throws.InvalidOperationException);
        }

        [Test]
        public void OutOfRangeIndex_Throws_PendingStaysOpen()
        {
            var f = TestCases.Build(1, true);
            var board = new DeductionBoard(f.caseData);
            board.TryPair(f.n1.slotA, f.n1.slotB);

            Assert.That(() => board.AnswerPrompt(3), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
            Assert.That(board.PendingNode, Is.EqualTo(f.n1));
            Assert.That(() => board.AnswerPrompt(-1), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
            Assert.That(board.PendingNode, Is.EqualTo(f.n1));
        }

        [Test]
        public void CancelPrompt_ClearsPending_WithoutCostOrMiss()
        {
            var f = TestCases.Build(1, true);
            var board = new DeductionBoard(f.caseData);
            board.TryPair(f.n1.slotA, f.n1.slotB);

            board.CancelPrompt();

            Assert.That(board.PendingNode, Is.Null);
            Assert.That(board.GetMissCount(f.n1), Is.EqualTo(0));
            Assert.DoesNotThrow(() => board.CancelPrompt());
        }

        [Test]
        public void Prologue_NoCosts_ButHintStillAppears()
        {
            var f = TestCases.Build(0, false);
            var board = new DeductionBoard(f.caseData);

            var invalid = board.TryPair(f.rh1, f.c2);
            Assert.That(invalid.Outcome, Is.EqualTo(PairingOutcome.Invalid));
            Assert.That(invalid.FocusCost, Is.EqualTo(0f));
            Assert.That(invalid.StrikeCost, Is.EqualTo(0));

            board.TryPair(f.n1.slotA, f.n1.slotB);
            var first = board.AnswerPrompt(1);
            Assert.That(first.FocusCost, Is.EqualTo(0f));
            Assert.That(first.StrikeCost, Is.EqualTo(0));

            board.TryPair(f.n1.slotA, f.n1.slotB);
            var second = board.AnswerPrompt(2);
            Assert.That(second.MissCount, Is.EqualTo(2));
            Assert.That(second.Hint, Is.EqualTo(f.n1.prompt.hintText));
            Assert.That(second.FocusCost, Is.EqualTo(0f));
            Assert.That(second.StrikeCost, Is.EqualTo(0));
        }

        [Test]
        public void BoardOnly_WrongAnswerCostsStrike_InvalidPairingFree_RoutesToEnding()
        {
            var f = TestCases.Build(5, false, boardOnly: true);
            var board = new DeductionBoard(f.caseData);

            var invalid = board.TryPair(f.rh1, f.c2);
            Assert.That(invalid.Outcome, Is.EqualTo(PairingOutcome.Invalid));
            Assert.That(invalid.FocusCost, Is.EqualTo(0f));
            Assert.That(invalid.StrikeCost, Is.EqualTo(0));

            board.TryPair(f.n1.slotA, f.n1.slotB);
            var wrong = board.AnswerPrompt(1);
            Assert.That(wrong.Outcome, Is.EqualTo(AnswerOutcome.Wrong));
            Assert.That(wrong.StrikeCost, Is.EqualTo(1));
            Assert.That(wrong.FocusCost, Is.EqualTo(0f));

            board.TryPair(f.n1.slotA, f.n1.slotB);
            board.AnswerPrompt(f.n1.prompt.correctIndex);
            board.TryPair(f.n2.slotA, f.n2.slotB);
            board.AnswerPrompt(f.n2.prompt.correctIndex);

            var hypothesis = board.SubmitHypothesis();
            Assert.That(hypothesis.Outcome, Is.EqualTo(HypothesisOutcome.RoutesToEnding));
            Assert.That(hypothesis.Interrogation, Is.Null);
        }

        [Test]
        public void SubmitHypothesis_NotReadyThenUnlocksInterrogation()
        {
            var f = TestCases.Build(1, true);
            var board = new DeductionBoard(f.caseData);

            Assert.That(board.SubmitHypothesis().Outcome, Is.EqualTo(HypothesisOutcome.NotReady));

            board.TryPair(f.n1.slotA, f.n1.slotB);
            board.AnswerPrompt(f.n1.prompt.correctIndex);
            Assert.That(board.SubmitHypothesis().Outcome, Is.EqualTo(HypothesisOutcome.NotReady));

            board.TryPair(f.n2.slotA, f.n2.slotB);
            board.AnswerPrompt(f.n2.prompt.correctIndex);
            var result = board.SubmitHypothesis();
            Assert.That(result.Outcome, Is.EqualTo(HypothesisOutcome.UnlocksInterrogation));
            Assert.That(result.Interrogation, Is.EqualTo(f.interrogation));
        }

        [Test]
        public void Restore_MarksValidated_AddsPremises_ClearsPending_ThrowsOnUnknownId()
        {
            var f = TestCases.Build(1, true);
            var board = new DeductionBoard(f.caseData);
            board.TryPair(f.n1.slotA, f.n1.slotB);

            board.Restore(new[] { f.n2.id });

            Assert.That(board.PendingNode, Is.Null);
            Assert.That(board.IsNodeValidated(f.n2), Is.True);
            Assert.That(board.IsNodeValidated(f.n1), Is.False);
            Assert.That(board.Premises, Does.Contain(f.p2));
            Assert.That(board.ValidatedNodeIds, Does.Contain(f.n2.id));

            Assert.That(() => board.Restore(new[] { "BOGUS_ID" }), Throws.ArgumentException);
        }
    }
}

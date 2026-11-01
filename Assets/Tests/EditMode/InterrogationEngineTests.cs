using Morsoul.Data;
using Morsoul.Interrogation;
using NUnit.Framework;
using UnityEngine;

namespace Morsoul.Tests
{
    public class InterrogationEngineTests
    {
        [TearDown]
        public void TearDown() => TestCases.DestroyAll();

        static EvidenceData[] Evidence(Fixture f) =>
            new EvidenceData[] { f.c1, f.c2, f.t1, f.t2, f.rh1, f.p1, f.p2 };

        static InterrogationEngine Engine(Fixture f, bool guided = false, bool echo = false) =>
            new InterrogationEngine(f.interrogation, Evidence(f), guided, echo);

        static void PressAndBreakPhase0(Fixture f, InterrogationEngine engine)
        {
            engine.Press("keyword");
            engine.Contradict(f.c1);
        }

        [Test]
        public void Ctor_Guards()
        {
            var f = TestCases.Build(1, true);
            Assert.That(() => new InterrogationEngine(null, Evidence(f)),
                Throws.ArgumentNullException);
            Assert.That(() => new InterrogationEngine(f.interrogation, null),
                Throws.ArgumentNullException);
            var noPhases = ScriptableObject.CreateInstance<InterrogationData>();
            noPhases.phases = new InterrogationPhase[0];
            f.assets.Add(noPhases);
            Assert.That(() => new InterrogationEngine(noPhases, Evidence(f)),
                Throws.ArgumentException);
        }

        [Test]
        public void CoreContradict_BeforePress_IsLocked()
        {
            var f = TestCases.Build(1, true);
            var engine = Engine(f);
            Assert.That(engine.IsCoreContradictUnlocked, Is.False);

            var result = engine.Contradict(f.c1);

            Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.Locked));
            Assert.That(result.StrikeCounted, Is.False);
            Assert.That(result.SegmentBroken, Is.False);
            Assert.That(engine.StrikesLost, Is.EqualTo(0));
            Assert.That(engine.PhaseIndex, Is.EqualTo(0));
            Assert.That(engine.Status, Is.EqualTo(InterrogationStatus.InProgress));
        }

        [Test]
        public void Press_UnknownKeyword_Throws()
        {
            var f = TestCases.Build(1, true);
            var engine = Engine(f);
            Assert.That(() => engine.Press("nope"), Throws.ArgumentException);
            Assert.That(() => engine.Press(null), Throws.ArgumentException);
            Assert.That(() => engine.Press(""), Throws.ArgumentException);
        }

        [Test]
        public void Press_ReturnsResponse_AndUnlocksCore()
        {
            var f = TestCases.Build(1, true);
            var engine = Engine(f);

            var result = engine.Press("keyword");

            Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.PressResponse));
            Assert.That(result.Move, Is.EqualTo(f.interrogation.phases[0].validMoves[0]));
            Assert.That(result.Response, Is.EqualTo("Response."));
            Assert.That(result.StrikeCounted, Is.False);
            Assert.That(engine.IsKeywordPressed("keyword"), Is.True);
            Assert.That(engine.IsCoreContradictUnlocked, Is.True);
            Assert.That(engine.StrikesLost, Is.EqualTo(0));
        }

        [Test]
        public void CorrectCore_ShieldShatters_AndAdvances()
        {
            var f = TestCases.Build(1, true);
            f.interrogation.falsehoodShieldActive = true;
            var engine = Engine(f);
            Assert.That(engine.ShieldActive, Is.True);

            engine.Press("keyword");
            var result = engine.Contradict(f.c1);

            Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.Correct));
            Assert.That(result.SegmentBroken, Is.True);
            Assert.That(result.ShieldShattered, Is.True);
            Assert.That(result.PhaseIndex, Is.EqualTo(0));
            Assert.That(result.StrikeCounted, Is.False);
            Assert.That(engine.ShieldActive, Is.False);
            Assert.That(engine.PhaseIndex, Is.EqualTo(1));
            Assert.That(engine.SegmentsRemaining, Is.EqualTo(2));
        }

        [Test]
        public void InvalidMove_Wrong_WithStrikeAndResponse()
        {
            var f = TestCases.Build(1, true);
            var engine = Engine(f);
            PressAndBreakPhase0(f, engine);

            var result = engine.PresentEvidence(f.rh1);

            Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.Wrong));
            Assert.That(result.Move, Is.EqualTo(f.interrogation.phases[1].invalidMoves[0]));
            Assert.That(result.Response, Is.EqualTo("Response."));
            Assert.That(result.StrikeCounted, Is.True);
            Assert.That(result.PhaseIndex, Is.EqualTo(1));
            Assert.That(engine.StrikesLost, Is.EqualTo(1));
            Assert.That(engine.PhaseIndex, Is.EqualTo(1));
        }

        [Test]
        public void UnmatchedEvidence_Wrong_WithNullMove()
        {
            var f = TestCases.Build(1, true);
            var engine = Engine(f);
            PressAndBreakPhase0(f, engine);

            var result = engine.PresentEvidence(f.c1);

            Assert.That(result.Outcome, Is.EqualTo(MoveOutcome.Wrong));
            Assert.That(result.Move, Is.Null);
            Assert.That(result.Response, Is.Null);
            Assert.That(result.StrikeCounted, Is.True);
            Assert.That(engine.StrikesLost, Is.EqualTo(1));
        }

        [Test]
        public void ThreeWrongs_Mistrial_ThenMovesThrow()
        {
            var f = TestCases.Build(1, true);
            var engine = Engine(f);
            PressAndBreakPhase0(f, engine);

            engine.PresentEvidence(f.rh1);
            engine.PresentEvidence(f.c1);
            var third = engine.PresentEvidence(f.t2);

            Assert.That(third.Status, Is.EqualTo(InterrogationStatus.Mistrial));
            Assert.That(engine.StrikesLost, Is.EqualTo(3));
            Assert.That(engine.StrikesRemaining, Is.EqualTo(0));
            Assert.That(() => engine.Press("keyword"), Throws.InvalidOperationException);
            Assert.That(() => engine.Contradict(f.c2), Throws.InvalidOperationException);
        }

        [Test]
        public void FullRun_Confesses()
        {
            var f = TestCases.Build(1, true);
            var engine = Engine(f);
            Assert.That(engine.SegmentsRemaining, Is.EqualTo(3));

            engine.Press("keyword");
            var r0 = engine.Contradict(f.c1);
            Assert.That(r0.PhaseIndex, Is.EqualTo(0));

            var r1 = engine.PresentEvidence(f.p1);
            Assert.That(r1.Outcome, Is.EqualTo(MoveOutcome.Correct));
            Assert.That(r1.PhaseIndex, Is.EqualTo(1));

            var r2 = engine.Contradict(f.c2);
            Assert.That(r2.Outcome, Is.EqualTo(MoveOutcome.Correct));
            Assert.That(r2.Status, Is.EqualTo(InterrogationStatus.Confessed));
            Assert.That(engine.SegmentsRemaining, Is.EqualTo(0));
            Assert.That(engine.CurrentPhase, Is.Null);
            Assert.That(engine.Status, Is.EqualTo(InterrogationStatus.Confessed));
            Assert.That(() => engine.Contradict(f.c1), Throws.InvalidOperationException);
        }

        [Test]
        public void GuidedFirstPhase_Phase0WrongsNotCounted_Phase1Counted()
        {
            var f = TestCases.Build(1, true);
            var engine = Engine(f, guided: true);

            var wrong0 = engine.PresentEvidence(f.rh1);
            Assert.That(wrong0.Outcome, Is.EqualTo(MoveOutcome.Wrong));
            Assert.That(wrong0.StrikeCounted, Is.False);
            Assert.That(engine.StrikesLost, Is.EqualTo(0));

            PressAndBreakPhase0(f, engine);

            var wrong1 = engine.PresentEvidence(f.rh1);
            Assert.That(wrong1.StrikeCounted, Is.True);
            Assert.That(engine.StrikesLost, Is.EqualTo(1));
        }

        [Test]
        public void EvidenceNotAvailable_Throws()
        {
            var f = TestCases.Build(1, true);
            var engine = new InterrogationEngine(f.interrogation,
                new EvidenceData[] { f.c2, f.t1, f.t2, f.p1, f.p2 });

            Assert.That(() => engine.Contradict(f.c1), Throws.ArgumentException);
            Assert.That(() => engine.Contradict(null), Throws.ArgumentNullException);
        }

        [Test]
        public void AddEvidence_ThenUsable()
        {
            var f = TestCases.Build(1, true);
            var engine = new InterrogationEngine(f.interrogation,
                new EvidenceData[] { f.c2, f.t1, f.t2, f.p1, f.p2 });

            engine.AddEvidence(null);
            engine.AddEvidence(f.c1);
            engine.AddEvidence(f.c1);
            Assert.That(engine.AvailableEvidence, Does.Contain(f.c1));

            engine.Press("keyword");
            Assert.That(engine.Contradict(f.c1).Outcome, Is.EqualTo(MoveOutcome.Correct));
        }

        [Test]
        public void Reset_RestoresEverything()
        {
            var f = TestCases.Build(1, true);
            f.interrogation.falsehoodShieldActive = true;
            var engine = Engine(f);
            PressAndBreakPhase0(f, engine);
            engine.PresentEvidence(f.rh1);

            engine.Reset();

            Assert.That(engine.PhaseIndex, Is.EqualTo(0));
            Assert.That(engine.SegmentsRemaining, Is.EqualTo(3));
            Assert.That(engine.StrikesLost, Is.EqualTo(0));
            Assert.That(engine.ShieldActive, Is.True);
            Assert.That(engine.Status, Is.EqualTo(InterrogationStatus.InProgress));
            Assert.That(engine.IsKeywordPressed("keyword"), Is.False);
            Assert.That(engine.IsCoreContradictUnlocked, Is.False);
            Assert.That(engine.AvailableEvidence, Does.Contain(f.c1));
        }

        [Test]
        public void EmotionEcho_AurasOnlyWhenUnlockedAndPressed()
        {
            var f = TestCases.Build(1, true);
            f.interrogation.phases[0].validMoves[0].emotionAura = Aura.Amber;

            var locked = Engine(f, echo: false);
            Assert.That(locked.GetKeywordAura("keyword"), Is.EqualTo(Aura.None));
            Assert.That(locked.PortraitAura, Is.EqualTo(Aura.None));

            var engine = Engine(f, echo: true);
            Assert.That(engine.GetKeywordAura("keyword"), Is.EqualTo(Aura.Amber));
            Assert.That(engine.GetKeywordAura("not-a-keyword"), Is.EqualTo(Aura.None));
            Assert.That(engine.PortraitAura, Is.EqualTo(Aura.None));

            engine.Press("keyword");
            Assert.That(engine.PortraitAura, Is.EqualTo(Aura.Amber));
        }
    }
}

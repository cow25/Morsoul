using Morsoul.Core;
using Morsoul.Hub;
using NUnit.Framework;
using UnityEngine;

namespace Morsoul.Tests
{
    public class PrologueTransitionTests
    {
        SessionState _session;

        [SetUp]
        public void SetUp() => _session = new SessionState();

        [Test]
        public void ElizaIntro_RequiresExitLoaded_Unseen_AndNoActiveCutscene()
        {
            Assert.IsFalse(PrologueTransitionRules.CanTriggerElizaIntro(_session));

            _session.PrologueExitLoaded = true;
            Assert.IsTrue(PrologueTransitionRules.CanTriggerElizaIntro(_session));

            _session.SetCutsceneContext(SessionState.ElizaIntroCutsceneId,
                SessionState.ElizaPreContactCheckpointId);
            Assert.IsFalse(PrologueTransitionRules.CanTriggerElizaIntro(_session));

            _session.SetCutsceneContext("", "");
            _session.ElizaIntroSeen = true;
            Assert.IsFalse(PrologueTransitionRules.CanTriggerElizaIntro(_session));
        }

        [Test]
        public void RuinsGate_RequiresSolvedBoard_AndGateUnlock()
        {
            Assert.IsFalse(PrologueTransitionRules.CanCrossRuinsGate(_session, 0));

            _session.GetOrCreateBoardState(0).BoardSolved = true;
            Assert.IsFalse(PrologueTransitionRules.CanCrossRuinsGate(_session, 0));

            _session.PrologueGateUnlocked = true;
            Assert.IsTrue(PrologueTransitionRules.CanCrossRuinsGate(_session, 0));
            Assert.IsFalse(PrologueTransitionRules.CanCrossRuinsGate(_session, 1));
        }

        [Test]
        public void InterruptedElizaIntro_RestoresPreContactState()
        {
            _session.PrologueExitLoaded = true;
            _session.ElizaIntroTriggered = true;
            _session.EasternRouteUnlocked = true;
            _session.SetCutsceneContext(SessionState.ElizaIntroCutsceneId,
                SessionState.ElizaPreContactCheckpointId);
            _session.SetPlayerContext(SceneMode.Mode2D,
                SessionState.PrologueWildernessSceneId,
                new Vector3(12f, 0f, 0f), PlayerFacing2D.Left);

            Assert.IsTrue(_session.ResumeInterruptedElizaIntro());

            Assert.IsTrue(_session.PrologueExitLoaded);
            Assert.IsFalse(_session.ElizaIntroTriggered);
            Assert.IsFalse(_session.ElizaIntroSeen);
            Assert.IsFalse(_session.EasternRouteUnlocked);
            Assert.AreEqual("", _session.ActiveCutsceneId);
            Assert.AreEqual("", _session.PreCutsceneCheckpointId);
            Assert.AreEqual(SceneMode.Mode2D, _session.CurrentSceneMode);
            Assert.AreEqual(SessionState.PrologueWildernessSceneId, _session.CurrentSceneId);
            Assert.AreEqual(new Vector3(6f, 0f, 0f), _session.PlayerPosition);
            Assert.AreEqual(PlayerFacing2D.Right, _session.PlayerFacing2D);
            Assert.IsTrue(PrologueTransitionRules.CanTriggerElizaIntro(_session));
        }

        [Test]
        public void InterruptedElizaIntro_DoesNotRewindCompletedOrUnrelatedCutscene()
        {
            _session.PrologueExitLoaded = true;
            _session.ElizaIntroTriggered = true;
            _session.ElizaIntroSeen = true;
            _session.EasternRouteUnlocked = true;
            _session.SetCutsceneContext(SessionState.ElizaIntroCutsceneId,
                SessionState.ElizaPreContactCheckpointId);

            Assert.IsFalse(_session.ResumeInterruptedElizaIntro());
            Assert.IsTrue(_session.EasternRouteUnlocked);

            _session.ElizaIntroSeen = false;
            _session.SetCutsceneContext("OTHER_CUTSCENE", "other_checkpoint");
            Assert.IsFalse(_session.ResumeInterruptedElizaIntro());
            Assert.AreEqual("OTHER_CUTSCENE", _session.ActiveCutsceneId);
        }
    }
}

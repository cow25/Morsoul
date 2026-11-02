using Morsoul.Core;
using NUnit.Framework;

namespace Morsoul.Tests
{
    public class FlowStateMachineTests
    {
        FlowStateMachine _fsm;

        [SetUp]
        public void SetUp() => _fsm = new FlowStateMachine();

        [Test]
        public void StartsAtBoot()
        {
            Assert.AreEqual(GameFlowState.Boot, _fsm.Current);
        }

        [Test]
        public void LegalChain_RunsEndToEnd()
        {
            Assert.IsTrue(_fsm.TryTransition(GameFlowState.Title));
            Assert.IsTrue(_fsm.TryTransition(GameFlowState.Opening));
            Assert.IsTrue(_fsm.TryTransition(GameFlowState.Hub));
            Assert.IsTrue(_fsm.TryTransition(GameFlowState.Investigation));
            Assert.IsTrue(_fsm.TryTransition(GameFlowState.Board));
            Assert.IsTrue(_fsm.TryTransition(GameFlowState.Interrogation));
            Assert.IsTrue(_fsm.TryTransition(GameFlowState.Resolution));
            Assert.IsTrue(_fsm.TryTransition(GameFlowState.Ending));
            Assert.AreEqual(GameFlowState.Ending, _fsm.Current);
        }

        [Test]
        public void IllegalTransition_Rejected_StateUnchanged()
        {
            Assert.IsTrue(_fsm.TryTransition(GameFlowState.Title));
            Assert.IsFalse(_fsm.TryTransition(GameFlowState.Interrogation));
            Assert.AreEqual(GameFlowState.Title, _fsm.Current);
        }

        [Test]
        public void MistrialRetry_InterrogationBackToInvestigation()
        {
            _fsm.TryTransition(GameFlowState.Title);
            _fsm.TryTransition(GameFlowState.Hub);
            _fsm.TryTransition(GameFlowState.Investigation);
            _fsm.TryTransition(GameFlowState.Board);
            _fsm.TryTransition(GameFlowState.Interrogation);
            Assert.IsTrue(_fsm.TryTransition(GameFlowState.Investigation));
        }

        [Test]
        public void Ending_ReturnsToTitle()
        {
            _fsm.TryTransition(GameFlowState.Title);
            _fsm.TryTransition(GameFlowState.Hub);
            _fsm.TryTransition(GameFlowState.Board);
            _fsm.TryTransition(GameFlowState.Ending);
            Assert.IsTrue(_fsm.TryTransition(GameFlowState.Title));
        }

        [Test]
        public void StateChanged_FiresWithPrevAndNext()
        {
            GameFlowState prev = GameFlowState.Resolution;
            GameFlowState next = GameFlowState.Resolution;
            int calls = 0;
            _fsm.StateChanged += (p, n) => { prev = p; next = n; calls++; };
            _fsm.TryTransition(GameFlowState.Title);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(GameFlowState.Boot, prev);
            Assert.AreEqual(GameFlowState.Title, next);
        }

        [Test]
        public void StateChanged_NotFiredOnIllegalTransition()
        {
            int calls = 0;
            _fsm.StateChanged += (p, n) => calls++;
            _fsm.TryTransition(GameFlowState.Ending);
            Assert.AreEqual(0, calls);
        }
    }

    public class SceneRouteTableTests
    {
        [Test]
        public void Resolve_HitAndMiss()
        {
            var table = new SceneRouteTable();
            table.Set("title", "Title");
            table.Set("hub.1", "Hub_LowerMarket");
            Assert.AreEqual("Title", table.Resolve("title"));
            Assert.AreEqual("Hub_LowerMarket", table.Resolve(RouteKeys.Hub(1)));
            Assert.IsNull(table.Resolve("investigation.1"));
            Assert.IsFalse(table.Contains("nope"));
        }

        [Test]
        public void Set_UpdatesExistingKey()
        {
            var table = new SceneRouteTable();
            table.Set("opening", "Old");
            table.Set("opening", "New");
            Assert.AreEqual("New", table.Resolve("opening"));
            Assert.AreEqual(1, table.routes.Count);
        }

        [Test]
        public void BlankEntries_Ignored()
        {
            var table = new SceneRouteTable();
            table.routes.Add(new SceneRoute { key = "title", sceneName = "" });
            table.routes.Add(new SceneRoute { key = null, sceneName = "X" });
            Assert.IsNull(table.Resolve("title"));
        }
    }
}

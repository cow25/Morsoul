using Morsoul.Core;
using NUnit.Framework;

namespace Morsoul.Tests
{
    public class FocusModelTests
    {
        FocusModel _focus;

        [SetUp]
        public void SetUp() => _focus = new FocusModel();

        [Test]
        public void StartsAtMax()
        {
            Assert.AreEqual(100f, _focus.Percent);
            Assert.IsFalse(_focus.StrainedScanUsed);
        }

        [Test]
        public void Scan_DrainsPerTick()
        {
            _focus.TickScan(0.3f); // three 0.1s ticks
            Assert.AreEqual(97f, _focus.Percent, 0.001f);
        }

        [Test]
        public void Scan_SubTickTime_Accumulates()
        {
            _focus.TickScan(0.05f);
            _focus.TickScan(0.05f); // crosses one tick
            Assert.AreEqual(99f, _focus.Percent, 0.001f);
        }

        [Test]
        public void Scan_ClampsAtZero()
        {
            _focus.TickScan(15f); // 150 ticks > 100%
            Assert.AreEqual(0f, _focus.Percent);
            Assert.IsTrue(_focus.IsDepleted);
        }

        [Test]
        public void ScanAtZero_LatchesStrainedScan_CostsNothing()
        {
            _focus.Restore(0f, false);
            _focus.TickScan(0.5f);
            Assert.AreEqual(0f, _focus.Percent);
            Assert.IsTrue(_focus.StrainedScanUsed);
        }

        [Test]
        public void ScanAboveZero_DoesNotLatchStrained()
        {
            _focus.TickScan(0.1f);
            Assert.IsFalse(_focus.StrainedScanUsed);
        }

        [Test]
        public void Rest_RegensUpToMax()
        {
            _focus.Restore(50f, false);
            _focus.TickRest(2f); // +20%
            Assert.AreEqual(70f, _focus.Percent, 0.001f);
            _focus.TickRest(10f);
            Assert.AreEqual(100f, _focus.Percent);
        }

        [Test]
        public void WrongCausal_CostsTwenty()
        {
            _focus.ApplyWrongCausalCost();
            Assert.AreEqual(80f, _focus.Percent);
        }

        [Test]
        public void InvalidPairing_CostsTen()
        {
            _focus.ApplyInvalidPairingCost();
            Assert.AreEqual(90f, _focus.Percent);
        }

        [Test]
        public void BoardCosts_AtZero_StillResolveAndClamp()
        {
            _focus.Restore(5f, false);
            _focus.ApplyWrongCausalCost();
            Assert.AreEqual(0f, _focus.Percent);
            _focus.ApplyInvalidPairingCost();
            Assert.AreEqual(0f, _focus.Percent);
        }

        [Test]
        public void BoardCosts_DoNotLatchStrained()
        {
            _focus.Restore(0f, false);
            _focus.ApplyWrongCausalCost();
            Assert.IsFalse(_focus.StrainedScanUsed);
        }

        [Test]
        public void Restore_ClampsAndClearsAccumulator()
        {
            _focus.Restore(140f, true);
            Assert.AreEqual(100f, _focus.Percent);
            Assert.IsTrue(_focus.StrainedScanUsed);
        }
    }
}

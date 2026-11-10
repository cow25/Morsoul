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
        public void Rest_RegensPerFocusTick_CapsAtMax()
        {
            _focus.Restore(50f, false);
            _focus.TickRest(FocusTuning.FocusTickSeconds - 0.001f); // no tick yet
            Assert.AreEqual(50f, _focus.Percent, 0.001f);
            _focus.TickRest(FocusTuning.FocusTickSeconds); // crosses one tick -> +1
            Assert.AreEqual(51f, _focus.Percent, 0.001f);
            _focus.TickRest(FocusTuning.FocusTickSeconds * 100f); // capped at max
            Assert.AreEqual(100f, _focus.Percent);
        }

        [Test]
        public void Rest_SubTickRemainder_PersistsAcrossTicks()
        {
            _focus.Restore(50f, false);
            _focus.TickRest(FocusTuning.FocusTickSeconds * 0.6f);
            _focus.TickRest(FocusTuning.FocusTickSeconds * 0.4f); // two halves -> one tick
            Assert.AreEqual(51f, _focus.Percent, 0.001f);
            Assert.AreEqual(0f, _focus.RestRemainderSeconds, 0.001f);
        }

        [Test]
        public void IncreaseMaximum_RaisesCap_RefillFills()
        {
            _focus.Restore(40f, false);
            _focus.IncreaseMaximum(10f);
            Assert.AreEqual(110f, _focus.Maximum);
            Assert.AreEqual(40f, _focus.Percent);
            _focus.Refill();
            Assert.AreEqual(110f, _focus.Percent);
            Assert.AreEqual(1f, _focus.Normalized, 0.001f);
        }

        [Test]
        public void OfflineRecovery_GrantsFloorMinutesHalved_Once()
        {
            var t0 = new System.DateTime(2026, 11, 7, 10, 0, 0, System.DateTimeKind.Utc);
            _focus.Restore(50f, false);
            _focus.MarkClosed(t0);
            float gain = _focus.ApplyOfflineRecovery(t0.AddMinutes(7));
            Assert.AreEqual(3f, gain); // floor(7/2)
            Assert.AreEqual(53f, _focus.Percent);
            // Same interval cannot be credited twice.
            Assert.AreEqual(0f, _focus.ApplyOfflineRecovery(t0.AddMinutes(7)));
            // A later reopen only credits the delta after the last application.
            Assert.AreEqual(0f, _focus.ApplyOfflineRecovery(t0.AddMinutes(8)));
        }

        [Test]
        public void TrustedNow_BackwardsSystemClock_KeepsWatermark()
        {
            var t0 = new System.DateTime(2026, 11, 7, 10, 0, 0, System.DateTimeKind.Utc);
            var trusted = _focus.StampTrustedNow(t0);
            Assert.AreEqual(t0, trusted);
            trusted = _focus.StampTrustedNow(t0.AddMinutes(-10));
            Assert.AreEqual(t0, trusted);
            trusted = _focus.StampTrustedNow(t0.AddMinutes(10));
            Assert.AreEqual(t0.AddMinutes(10), trusted);
        }

        [Test]
        public void OfflineRecovery_BackwardsClock_GrantsZero_AndConsumesClose()
        {
            var t0 = new System.DateTime(2026, 11, 7, 10, 0, 0, System.DateTimeKind.Utc);
            _focus.Restore(50f, false, lastTrustedUtc: t0);
            _focus.MarkClosed(t0);
            float gain = _focus.ApplyOfflineRecovery(t0.AddMinutes(-30));
            Assert.AreEqual(0f, gain);
            Assert.AreEqual(50f, _focus.Percent);
            Assert.IsNull(_focus.ClosedAtUtc);
            Assert.AreEqual(t0, _focus.LastOfflineRecoveryAppliedAtUtc);
        }

        [Test]
        public void OfflineRecovery_CapsAtMaximum()
        {
            var t0 = new System.DateTime(2026, 11, 7, 10, 0, 0, System.DateTimeKind.Utc);
            _focus.Restore(95f, false);
            _focus.MarkClosed(t0);
            _focus.ApplyOfflineRecovery(t0.AddMinutes(120));
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
        public void SetStrainedScanUsed_SwitchesPerCaseLatch()
        {
            _focus.Restore(0f, false);
            _focus.TickScan(FocusTuning.FocusTickSeconds);
            Assert.IsTrue(_focus.StrainedScanUsed);
            _focus.SetStrainedScanUsed(false);
            Assert.IsFalse(_focus.StrainedScanUsed);
            _focus.SetStrainedScanUsed(true);
            Assert.IsTrue(_focus.StrainedScanUsed);
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

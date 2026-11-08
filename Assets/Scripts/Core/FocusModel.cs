using System;
using Morsoul.Engine;

namespace Morsoul.Core
{
    /// <summary>
    /// Single source of truth for persistent Focus (GDD §6.4–§6.6,
    /// Algorithm.md B2, Data_Schemas FocusState).
    ///
    /// - Energy persists across scenes/cases/hubs and NEVER auto-refills;
    ///   recovery comes only from rest ticks and closed-game recovery.
    /// - Maximum starts at 100 and grows +10 per supported complete
    ///   submission (awarded once per case via ProgressionRules).
    /// - Scanning drains 1 per FocusTickSeconds while above 0.
    /// - At 0 a held scan is "strained scanning": it still works and costs
    ///   nothing, but latches StrainedScanUsed — the case is then ineligible
    ///   for Clean.
    /// - Rest grants 1 per completed Focus tick (fractional time persists as
    ///   a remainder for the save layer).
    /// - Closed-game recovery grants floor(whole minutes closed / 2), capped
    ///   at max; timestamps prevent crediting an interval twice.
    /// </summary>
    public class FocusModel
    {
        float _tickAccumulator;
        float _restRemainderSeconds;

        /// <summary>Current energy, 0–Maximum.</summary>
        public float Percent { get; private set; } = FocusTuning.FocusMax;

        /// <summary>Current energy cap — grows on supported complete submissions.</summary>
        public float Maximum { get; private set; } = FocusTuning.FocusMax;

        /// <summary>0–1 fill fraction for bars that normalize.</summary>
        public float Normalized => Maximum > 0f ? Percent / Maximum : 0f;

        /// <summary>True once a scan ran at 0 Focus — forfeits Clean.</summary>
        public bool StrainedScanUsed { get; private set; }

        /// <summary>True when energy has hit zero.</summary>
        public bool IsDepleted => Percent <= 0f;

        /// <summary>Un-credited rest time carried toward the next Focus tick.</summary>
        public float RestRemainderSeconds => _restRemainderSeconds;

        /// <summary>When the game last closed, for offline recovery. Null once consumed.</summary>
        public DateTime? ClosedAtUtc { get; private set; }

        /// <summary>Last time offline recovery was credited — double-credit guard.</summary>
        public DateTime? LastOfflineRecoveryAppliedAtUtc { get; private set; }

        /// <summary>
        /// Advances the held-scan drain by <paramref name="deltaSeconds"/>.
        /// At 0 the scan still functions but latches StrainedScanUsed.
        /// </summary>
        public void TickScan(float deltaSeconds)
        {
            _tickAccumulator += deltaSeconds;
            while (_tickAccumulator >= FocusTuning.FocusTickSeconds)
            {
                _tickAccumulator -= FocusTuning.FocusTickSeconds;
                if (IsDepleted)
                {
                    StrainedScanUsed = true;
                }
                else
                {
                    Percent = Math.Max(0f, Percent - FocusTuning.FocusDrainPerTick);
                }
            }
        }

        /// <summary>
        /// Rest-spot regen: +1 per completed Focus tick. Sub-tick time is
        /// kept in the remainder so saves and scene transitions lose nothing.
        /// </summary>
        public void TickRest(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;
            _restRemainderSeconds += deltaSeconds;
            while (_restRemainderSeconds >= FocusTuning.FocusTickSeconds)
            {
                _restRemainderSeconds -= FocusTuning.FocusTickSeconds;
                Percent = Math.Min(Maximum, Percent + ProgressionConstants.RestEnergyPerTick);
            }
        }

        /// <summary>Wrong causal-prompt answer on the board (−20%).</summary>
        public void ApplyWrongCausalCost()
        {
            Percent = Math.Max(0f, Percent - FocusTuning.WrongCausalCost);
        }

        /// <summary>Invalid pairing / red herring on the board (−10%).</summary>
        public void ApplyInvalidPairingCost()
        {
            Percent = Math.Max(0f, Percent - FocusTuning.InvalidPairingCost);
        }

        /// <summary>Arbitrary Focus spend (e.g. a reported board cost); clamps at 0.</summary>
        public void Spend(float amount)
        {
            if (amount > 0f) Percent = Math.Max(0f, Percent - amount);
        }

        /// <summary>Raises the cap (supported complete submission, +10).</summary>
        public void IncreaseMaximum(float bonus)
        {
            if (bonus > 0f) Maximum += bonus;
        }

        /// <summary>Refills energy to the current maximum.</summary>
        public void Refill()
        {
            Percent = Maximum;
        }

        /// <summary>
        /// Mirrors externally-computed energy + maximum into the model
        /// (weighted-board progression mutates its own runtime state; the
        /// persistent FocusModel stays authoritative for everything else).
        /// </summary>
        public void Sync(float energy, float maximum)
        {
            Maximum = Math.Max(1f, maximum);
            Percent = Math.Clamp(energy, 0f, Maximum);
        }

        /// <summary>Records the close timestamp used by offline recovery.</summary>
        public void MarkClosed(DateTime closedUtc)
        {
            ClosedAtUtc = closedUtc;
        }

        /// <summary>
        /// Closed-game recovery: +floor(whole minutes / 2), capped at max,
        /// measured from the later of close time and the last credit so an
        /// interval is never recovered twice. Returns the energy granted.
        /// </summary>
        public float ApplyOfflineRecovery(DateTime reopenedUtc)
        {
            if (ClosedAtUtc == null) return 0f;
            var from = LastOfflineRecoveryAppliedAtUtc.HasValue &&
                       LastOfflineRecoveryAppliedAtUtc.Value > ClosedAtUtc.Value
                ? LastOfflineRecoveryAppliedAtUtc.Value
                : ClosedAtUtc.Value;
            int wholeMinutes = (int)Math.Floor((reopenedUtc - from).TotalMinutes);
            if (wholeMinutes <= 0) return 0f;

            float gain = wholeMinutes / ProgressionConstants.ClosedMinutesPerEnergy;
            float before = Percent;
            Percent = Math.Min(Maximum, Percent + gain);
            LastOfflineRecoveryAppliedAtUtc = reopenedUtc;
            return Percent - before;
        }

        /// <summary>Restores state from a SaveState.</summary>
        public void Restore(float percent, bool strainedScanUsed, float? maximum = null,
            float restRemainderSeconds = 0f, DateTime? closedAtUtc = null,
            DateTime? lastOfflineRecoveryAppliedAtUtc = null)
        {
            Maximum = maximum ?? FocusTuning.FocusMax;
            Percent = Math.Clamp(percent, 0f, Maximum);
            StrainedScanUsed = strainedScanUsed;
            _tickAccumulator = 0f;
            _restRemainderSeconds = Math.Max(0f, restRemainderSeconds);
            ClosedAtUtc = closedAtUtc;
            LastOfflineRecoveryAppliedAtUtc = lastOfflineRecoveryAppliedAtUtc;
        }
    }
}

using System;

namespace Morsoul.Core
{
    /// <summary>
    /// Single source of truth for persistent Focus (GDD §6.4–§6.6).
    ///
    /// - Percent persists across scenes/cases/hubs and NEVER auto-refills;
    ///   the only recovery is TickRest at a rest spot.
    /// - Scanning drains 1% per FocusTickSeconds while above 0.
    /// - At 0% a held scan is "strained scanning": it still works and costs
    ///   nothing, but latches StrainedScanUsed — the case is then ineligible
    ///   for Clean.
    /// - Board costs (wrong causal / invalid pairing) resolve even at 0%;
    ///   they simply clamp the bar at 0.
    /// </summary>
    public class FocusModel
    {
        float _tickAccumulator;

        /// <summary>0–100, persistent.</summary>
        public float Percent { get; private set; } = FocusTuning.FocusMax;

        /// <summary>True once a scan ran at 0% Focus — forfeits Clean.</summary>
        public bool StrainedScanUsed { get; private set; }

        /// <summary>True when the bar has hit zero.</summary>
        public bool IsDepleted => Percent <= 0f;

        /// <summary>
        /// Advances the held-scan drain by <paramref name="deltaSeconds"/>.
        /// At 0% the scan still functions but latches StrainedScanUsed.
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

        /// <summary>Rest-spot regen — the only way Focus recovers.</summary>
        public void TickRest(float deltaSeconds)
        {
            Percent = Math.Min(FocusTuning.FocusMax,
                Percent + FocusTuning.RestRegenPerSecond * deltaSeconds);
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

        /// <summary>Restores state from a SaveState.</summary>
        public void Restore(float percent, bool strainedScanUsed)
        {
            Percent = Math.Clamp(percent, 0f, FocusTuning.FocusMax);
            StrainedScanUsed = strainedScanUsed;
            _tickAccumulator = 0f;
        }
    }
}

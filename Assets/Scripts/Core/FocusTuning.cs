namespace Morsoul.Core
{
    /// <summary>
    /// Focus tuning constants (GDD §6.4/§6.6, Data_Schemas FocusTuning) —
    /// Cases 1–4 only. Case 5 has no Focus; the Prologue tutorial is exempt.
    /// </summary>
    public static class FocusTuning
    {
        /// <summary>Starts at 100%, persists across scenes/cases/hubs, clamps at 0.</summary>
        public const float FocusMax = 100f;
        /// <summary>Held-Tab Golem Eye drain per tick; registration is free — the drain is the cost.</summary>
        public const float FocusDrainPerTick = 1f;
        /// <summary>Seconds per drain/regen tick — engine-tunable, not a fixed design rule.</summary>
        public const float FocusTickSeconds = 0.1f;
        /// <summary>SUPERSEDED — rest now grants 5/minute via ProgressionConstants.RestEnergyPerMinute; retained for reference only.</summary>
        public const float RestRegenPerSecond = 10f;
        /// <summary>Wrong causal-prompt answer, Cases 1–4 (slot pair clears).</summary>
        public const float WrongCausalCost = 20f;
        /// <summary>Invalid pairing including red herring, Cases 1–4.</summary>
        public const float InvalidPairingCost = 10f;
    }
}

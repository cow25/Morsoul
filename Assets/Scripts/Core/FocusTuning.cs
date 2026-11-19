namespace Morsoul.Core
{
    /// <summary>
    /// Focus tuning constants (GDD §6.4/§6.6, Data_Schemas FocusTuning) —
    /// persistent scan/rest tuning; board error costs live in ProgressionConstants.
    /// </summary>
    public static class FocusTuning
    {
        /// <summary>Starts at 100%, persists across scenes/cases/hubs, clamps at 0.</summary>
        public const float FocusMax = 100f;
        /// <summary>Held-Tab Golem Eye drain per tick (0.8 / 0.1s = 8 Focus/sec).</summary>
        public const float FocusDrainPerTick = 0.8f;
        /// <summary>Active Aether Sight drain per tick (0.3 / 0.1s = 3 Focus/sec).</summary>
        public const float AetherSightDrainPerTick = 0.3f;
        /// <summary>Seconds per scan drain/rest regen tick.</summary>
        public const float FocusTickSeconds = 0.1f;
        /// <summary>Rest regen per completed tick (0.5 / 0.1s = 5 Focus/sec).</summary>
        public const float RestEnergyPerTick = 0.5f;
        /// <summary>Maximum hub rest activations allowed per active case.</summary>
        public const int RestMaxPerCase = 2;
        /// <summary>Maximum Focus recovered during a single rest session.</summary>
        public const float RestGainCapPerUse = 50f;
        /// <summary>Minimum real-time seconds between rest activations.</summary>
        public const float RestCooldownSeconds = 30f;
        /// <summary>Wrong causal-prompt answer, Cases 1–4 (slot pair clears).</summary>
        public const float WrongCausalCost = 20f;
        /// <summary>Invalid pairing including red herring, Cases 1–4.</summary>
        public const float InvalidPairingCost = 10f;
    }
}

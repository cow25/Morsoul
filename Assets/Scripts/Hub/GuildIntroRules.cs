using Morsoul.Core;

namespace Morsoul.Hub
{
    /// <summary>
    /// Pure gating logic for the prologue -> Lower Market -> Guild first-visit
    /// chain. Kept static and SessionState-only so EditMode tests can exercise
    /// it without a scene.
    /// </summary>
    public static class GuildIntroRules
    {
        /// <summary>Latched when the guided market arrival has played.</summary>
        public const string ArrivedFlag = "Market.ArrivalSeen";
        /// <summary>Set by the arrival; consumed by the Guild introduction.</summary>
        public const string IntroPendingFlag = "Guild.IntroPending";
        /// <summary>Latched when the in-Guild introduction has completed.</summary>
        public const string IntroSeenFlag = "Guild.IntroSeen";

        /// <summary>
        /// The guided arrival only fires straight out of the prologue. A save
        /// already past Case 1 (or a repeat visit) skips it — the flag latches
        /// anyway so it never replays on later arrivals.
        /// </summary>
        public static bool CanPlayMarketArrival(SessionState session) =>
            session != null && session.PrologueCompleted &&
            session.CurrentCase <= 1 && !session.HasFlag(ArrivedFlag);

        /// <summary>The Guild introduction only runs when the arrival sent us.</summary>
        public static bool CanPlayGuildIntro(SessionState session) =>
            session != null && session.HasFlag(IntroPendingFlag) &&
            !session.HasFlag(IntroSeenFlag);
    }
}

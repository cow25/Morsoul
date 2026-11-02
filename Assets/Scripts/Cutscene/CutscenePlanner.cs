namespace Morsoul.Cutscene
{
    /// <summary>
    /// Pure shot-iteration logic for the cutscene player. Shots with neither an
    /// image nor a video are not playable and are skipped automatically.
    /// Kept free of engine/UI dependencies so it can be unit tested.
    /// </summary>
    public static class CutscenePlanner
    {
        /// <summary>True when the shot carries any displayable visual.</summary>
        public static bool HasVisual(CutsceneShotData shot) =>
            shot != null && (shot.image != null || shot.video != null);

        /// <summary>True when the shot carries a non-empty caption.</summary>
        public static bool HasCaption(CutsceneShotData shot) =>
            shot != null && !string.IsNullOrEmpty(shot.caption);

        /// <summary>True when the shot can be shown (has a visual).</summary>
        public static bool IsPlayable(CutsceneShotData shot) => HasVisual(shot);

        /// <summary>
        /// Returns the index of the next playable shot after
        /// <paramref name="currentIndex"/>, or -1 when none remains.
        /// Null and visual-less shots are skipped.
        /// </summary>
        public static int AdvanceIndex(CutsceneShotData[] shots, int currentIndex)
        {
            if (shots == null) return -1;
            for (int i = currentIndex + 1; i < shots.Length; i++)
            {
                if (IsPlayable(shots[i])) return i;
            }
            return -1;
        }

        /// <summary>Index of the first playable shot, or -1.</summary>
        public static int FirstIndex(CutsceneShotData[] shots) => AdvanceIndex(shots, -1);
    }
}

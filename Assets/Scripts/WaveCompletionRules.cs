namespace TapTapGameJam.Combat
{
    public static class WaveCompletionRules
    {
        // An empty/unlimited wave, a gap between spawns, and a dead base cannot win.
        public static bool HasWon(int baseHealth, int plannedCount, int spawnedCount, int livingCount)
            => baseHealth > 0 && plannedCount > 0 && spawnedCount >= plannedCount && livingCount == 0;
    }
}

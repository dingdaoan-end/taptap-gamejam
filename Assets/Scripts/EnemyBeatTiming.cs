using System;

namespace TapTapGameJam.Combat
{
    public static class EnemyBeatTiming
    {
        public const double SpawnIntervalBeats = 4;
        public static double Distance(double currentBeat, double birthBeat) => Math.Max(0, currentBeat - birthBeat);
        public static double FirstBirth(double currentBeat, double interval = SpawnIntervalBeats) => Math.Ceiling(currentBeat / Math.Max(1, interval)) * Math.Max(1, interval);
        public static double Advance(double distance, double previousBeat, double currentBeat, double speed) =>
            distance + Math.Max(0, currentBeat - previousBeat) * Math.Max(0, speed);
    }
}

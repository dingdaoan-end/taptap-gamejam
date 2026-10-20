using System;

namespace TapTapGameJam.Combat
{
    // Beat positions are zero based: {0, 2} means beats 1 and 3 of a four-beat bar.
    public static class AttackBeatGrid
    {
        // One hit at the audio segment start, then one follow-up one beat later.
        // The remaining beats of the segment do not deal damage.
        public static double NextDoubleHit(double segmentStart, int segmentBeats, double afterBeat)
        {
            if (segmentBeats <= 0) return double.PositiveInfinity;
            for (int hit = 0; hit < 2 && hit < segmentBeats; hit++)
            {
                double beat = segmentStart + hit;
                if (beat > afterBeat + 1e-5) return beat;
            }
            return double.PositiveInfinity;
        }

        public static double Next(double earliestBeat, double cycleBeats, float[] offsets)
        {
            if (offsets == null || cycleBeats <= 0 || double.IsNaN(cycleBeats) ||
                double.IsInfinity(cycleBeats) || double.IsNaN(earliestBeat) || double.IsInfinity(earliestBeat))
                return double.PositiveInfinity;
            earliestBeat = Math.Max(0, earliestBeat);
            double cycle = Math.Floor(earliestBeat / cycleBeats) * cycleBeats;
            double next = double.PositiveInfinity;
            foreach (float offset in offsets)
            {
                if (float.IsNaN(offset) || offset < 0 || offset >= cycleBeats) continue;
                double candidate = cycle + offset;
                if (candidate < earliestBeat - 1e-7) candidate += cycleBeats;
                next = Math.Min(next, candidate);
            }
            return next;
        }
    }
}

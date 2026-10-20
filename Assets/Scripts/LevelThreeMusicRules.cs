using System;
using TapTapGameJam.Combat;

namespace TapTapGameJam.Music
{
    public sealed class LevelThreeVariantAllocator
    {
        readonly int[] next = new int[3];
        public int Allocate(TowerInstrument instrument)
        {
            int key = (int)instrument;
            int result = next[key];
            next[key] = (result + 1) % LevelThreeMusicRules.VariantCount(instrument);
            return result;
        }
        public void Reset() => Array.Clear(next, 0, next.Length);
    }

    // Pure rules shared by the audio reservations and their damage schedule.
    public static class LevelThreeMusicRules
    {
        public const int SegmentBeats = 8;
        public static int VariantCount(TowerInstrument instrument) =>
            instrument == TowerInstrument.Bell ? 3 : instrument == TowerInstrument.Duduk ? 2 : 1;

        public static int GlobalSegment(double beat, int count) =>
            (int)(Math.Floor(Math.Max(0, beat) / SegmentBeats) % count);

        public static double SegmentStart(double beat) =>
            Math.Floor(Math.Max(0, beat) / SegmentBeats) * SegmentBeats;

        public static double NextAttack(TowerInstrument instrument, double segmentBeat,
            double reservedStartBeat, double reservedEndBeat, double afterBeat)
        {
            for (int offset = 0; offset < SegmentBeats; offset++)
            {
                if (instrument == TowerInstrument.Bell && offset % 4 >= 2) continue;
                if (instrument == TowerInstrument.Duduk && offset % 2 != 0) continue;
                double beat = segmentBeat + offset;
                if (beat >= reservedStartBeat - 1e-7 && beat < reservedEndBeat - 1e-7 && beat > afterBeat + 1e-5)
                    return beat;
            }
            return double.PositiveInfinity;
        }
    }
}

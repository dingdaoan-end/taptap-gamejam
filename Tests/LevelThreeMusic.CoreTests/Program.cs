using System;
using TapTapGameJam.Combat;
using TapTapGameJam.Music;

static class Program
{
    static int checks;
    static void Equal(double expected, double actual, string reason)
    {
        checks++;
        if (expected != actual && Math.Abs(expected - actual) > 1e-7)
            throw new Exception(reason + ": expected " + expected + ", got " + actual);
    }
    static void Main()
    {
        var allocator = new LevelThreeVariantAllocator();
        for (int i = 0; i < 12; i++)
        {
            Equal(i % 3, allocator.Allocate(TowerInstrument.Bell), "bell order independent of other instruments");
            Equal(i % 2, allocator.Allocate(TowerInstrument.Duduk), "duduk cycles its two variants");
            Equal(0, allocator.Allocate(TowerInstrument.Chord), "chord has one shared variant");
        }
        allocator.Allocate(TowerInstrument.Bell); allocator.Allocate(TowerInstrument.Duduk);
        allocator.Reset();
        Equal(0, allocator.Allocate(TowerInstrument.Bell), "retry resets bell order");
        Equal(0, allocator.Allocate(TowerInstrument.Duduk), "retry resets duduk order");
        var timeline = new MusicTimeline(); timeline.Start(100, 116);
        foreach (var instrument in new[] { TowerInstrument.Bell, TowerInstrument.Chord, TowerInstrument.Duduk })
        {
            int[] offsets = instrument == TowerInstrument.Bell ? new[] { 0, 1, 4, 5 } :
                instrument == TowerInstrument.Duduk ? new[] { 0, 2, 4, 6 } : new[] { 0, 1, 2, 3, 4, 5, 6, 7 };
            foreach (double segment in new[] { 0.0, 3, 8, 32, 40, 100000 })
            {
                double last = double.NegativeInfinity;
                foreach (int offset in offsets)
                {
                    double beat = LevelThreeMusicRules.NextAttack(instrument, segment, segment, segment + 8, last);
                    Equal(segment + offset, beat, "correct attack offsets within a reservation");
                    Equal(offset * 60.0 / 116, timeline.DspTimeForBeat(beat) - timeline.DspTimeForBeat(segment), "116 BPM sample window timing");
                    last = beat;
                }
                Equal(double.PositiveInfinity, LevelThreeMusicRules.NextAttack(instrument, segment, segment, segment + 8, last), "no extra hit at clip end");
                Equal(double.PositiveInfinity, LevelThreeMusicRules.NextAttack(instrument, segment, segment, segment, -1), "empty reservation never hits");
            }
        }
        Equal(5, LevelThreeMusicRules.NextAttack(TowerInstrument.Bell, 0, 0, 8, 4.2), "late frame does not replay old hits");
        Equal(6, LevelThreeMusicRules.NextAttack(TowerInstrument.Duduk, 0, 5, 8, -1), "late audio entry cannot hit before its reservation");
        Equal(11, LevelThreeMusicRules.NextAttack(TowerInstrument.Chord, 8, 11, 16, -1), "chord joins current phrase without replaying its first three beats");
        foreach (double beat in new[] { 0.0, 7, 8, 15, 16, 31, 32, 100000 })
        {
            Equal(Math.Floor(beat / 8) % 4, LevelThreeMusicRules.GlobalSegment(beat, 4), "global chord segment");
            Equal(Math.Floor(beat / 8) * 8, LevelThreeMusicRules.SegmentStart(beat), "shared chord origin");
        }
        timeline.Pause(102); timeline.Resume(200);
        Equal(200 + (8 * 60.0 / 116 - 2), timeline.DspTimeForBeat(8), "pause retains global phase");
        Console.WriteLine("LEVEL3_MUSIC_CORE_PASS checks=" + checks);
    }
}

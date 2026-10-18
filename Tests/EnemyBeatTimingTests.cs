using System;
using TapTapGameJam.Combat;
using TapTapGameJam.Music;

public static class EnemyBeatTimingTests
{
    static void Equal(double actual, double expected)
    {
        if (Math.Abs(actual - expected) > 0.000001) throw new Exception(actual + " != " + expected);
    }
    public static void Main()
    {
        foreach (double bpm in new[] { 100.0, 116.0 })
        {
            var timeline = new MusicTimeline();
            timeline.Start(10, bpm);
            double now = 10 + 9.5 * 60 / bpm;
            double beat = timeline.BeatAt(now);
            Equal(EnemyBeatTiming.Distance(beat, 0), 9.5);
            Equal(EnemyBeatTiming.Distance(beat, 0) - EnemyBeatTiming.Distance(beat, 4), 4);
            timeline.Pause(now);
            Equal(EnemyBeatTiming.Distance(timeline.BeatAt(now + 20), 4), 5.5);
            timeline.Resume(now + 20);
            Equal(EnemyBeatTiming.Distance(timeline.BeatAt(now + 20 + 60 / bpm), 4), 6.5);
            // A late frame derives the same spacing from absolute birth beats.
            Equal(EnemyBeatTiming.Distance(81.75, 72) - EnemyBeatTiming.Distance(81.75, 76), 4);
        }
        Equal(EnemyBeatTiming.FirstBirth(0), 0);
        Equal(EnemyBeatTiming.FirstBirth(4), 4);
        Equal(EnemyBeatTiming.FirstBirth(4.1), 8);
        Equal(EnemyBeatTiming.Distance(0, 4), 0);
        Equal(EnemyBeatTiming.FirstBirth(4.1, 3), 6);
        double distance = EnemyBeatTiming.Advance(0, 0, 5, 1);
        Equal(distance, 5);
        Equal(EnemyBeatTiming.Advance(distance, 5, 5, 2), 5); // change does not teleport
        Equal(EnemyBeatTiming.Advance(distance, 5, 6, 2), 7);
        Equal(EnemyBeatTiming.Advance(distance, 5, 8, 0), 5); // zero speed freezes
        Console.WriteLine("ENEMY_BEAT_TIMING_TESTS_PASS");
    }
}

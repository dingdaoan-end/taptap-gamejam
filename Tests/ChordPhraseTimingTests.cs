using System;
using TapTapGameJam.Music;

public static class ChordPhraseTimingTests
{
    private static void Equal(double actual, double expected)
    {
        if (Math.Abs(actual - expected) > 0.000001) throw new Exception(actual + " != " + expected);
    }
    public static void Main()
    {
        foreach (var beat in new[] { 0.0, 7.999, 8, 15.999, 16, 24, 31.999, 32, 42, 32042 })
        {
            Equal(MusicPhraseGrid.Index(beat), Math.Floor(beat / 8) % 4);
            Equal(MusicPhraseGrid.Offset(beat), beat % 8);
            Equal(MusicPhraseGrid.EndBeat(beat) - beat, 8 - beat % 8);
        }
        var clock = new MusicTimeline();
        clock.Start(100, 100);
        double start = clock.DspTimeForBeat(10);
        double beatAtStart = Math.Round((start - clock.DspTimeForBeat(0)) / clock.SecondsPerBeat);
        Equal(MusicPhraseGrid.Index(beatAtStart), 1);
        Equal(MusicPhraseGrid.Offset(beatAtStart) * clock.SecondsPerBeat, 1.2);
        Equal(clock.DspTimeForBeat(MusicPhraseGrid.EndBeat(beatAtStart)), clock.DspTimeForBeat(16));
        clock.Pause(clock.DspTimeForBeat(10.5));
        clock.Resume(200);
        Equal(MusicPhraseGrid.Index(clock.BeatAt(200)), 1);
        Equal(MusicPhraseGrid.Offset(clock.BeatAt(200)), 2.5);
        Console.WriteLine("CHORD_GLOBAL_PHRASE_TESTS_PASS");
    }
}

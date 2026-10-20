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
        foreach (var beat in new[] { 0.0, 3.999, 4, 7.999, 8, 15.999, 16, 24, 28, 31.999, 32, 42, 32042 })
        {
            Equal(MusicPhraseGrid.Index(beat), Math.Floor(beat / 4) % 8);
            Equal(MusicPhraseGrid.Offset(beat), beat % 4);
            Equal(MusicPhraseGrid.EndBeat(beat) - beat, 4 - beat % 4);
        }
        CheckTempo(100);
        CheckTempo(116);
        Console.WriteLine("CHORD_GLOBAL_PHRASE_TESTS_PASS (100 and 116 BPM)");
    }

    private static void CheckTempo(double bpm)
    {
        var clock = new MusicTimeline();
        clock.Start(100, bpm);
        Equal(clock.SecondsPerBeat, 60.0 / bpm);
        Equal(clock.DspTimeForBeat(32) - clock.DspTimeForBeat(0), 32 * 60.0 / bpm);
        double start = clock.DspTimeForBeat(10);
        double beatAtStart = Math.Round((start - clock.DspTimeForBeat(0)) / clock.SecondsPerBeat);
        Equal(MusicPhraseGrid.Index(beatAtStart), 2);
        Equal(MusicPhraseGrid.Offset(beatAtStart) * clock.SecondsPerBeat, 2 * 60.0 / bpm);
        Equal(clock.DspTimeForBeat(MusicPhraseGrid.EndBeat(beatAtStart)), clock.DspTimeForBeat(12));
        clock.Pause(clock.DspTimeForBeat(10.5));
        clock.Resume(200);
        Equal(MusicPhraseGrid.Index(clock.BeatAt(200)), 2);
        Equal(MusicPhraseGrid.Offset(clock.BeatAt(200)), 2.5);
        // Late joins use the global position, including the eighth-to-first wrap.
        for (int beat = 0; beat <= 96; beat++)
        {
            double scheduled = clock.NextGridTime(clock.DspTimeForBeat(beat) + .01, 1);
            double joinedBeat = Math.Round((scheduled - clock.DspTimeForBeat(0)) / clock.SecondsPerBeat);
            if (joinedBeat < 10.5) continue;
            Equal(MusicPhraseGrid.Index(joinedBeat), Math.Floor(joinedBeat / 4) % 8);
            Equal(MusicPhraseGrid.Offset(joinedBeat), joinedBeat % 4);
        }
        // An eight-segment, four-beat native-tempo clip joins at the same source
        // offset after a pause, with no playback-speed conversion.
        double nativeFourBeatClipSeconds = 4 * 60.0 / bpm;
        double offsetSeconds = MusicPhraseGrid.Offset(clock.BeatAt(200)) * clock.SecondsPerBeat;
        Equal(nativeFourBeatClipSeconds - offsetSeconds,
            clock.DspTimeForBeat(12) - 200);
    }
}

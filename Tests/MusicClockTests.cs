using System;
using TapTapGameJam.Music;

// Standalone console checks; compile together with Assets/Scripts/MusicTimeline.cs.
static class MusicClockTests
{
    static void Equal(double expected, double actual, string message)
    {
        if (Math.Abs(expected - actual) > 1e-8)
            throw new Exception(message + ": expected " + expected + ", got " + actual);
    }
    static void Main()
    {
        var clock = new MusicTimeline();
        clock.Start(10, 100);
        Equal(0, clock.BeatAt(9.8), "Scheduled start must not advance early");
        Equal(1, clock.BeatAt(10.6), "100 BPM quarter note");
        Equal(29.2, clock.DspTimeForBeat(32), "Eight-bar loop");
        Equal(10.3, clock.NextGridTime(10.21, 2), "Eighth-note grid");
        Equal(10.6, clock.NextGridTime(10.59, 1), "Next full beat");
        Equal(1000, clock.BeatAt(610), "Long frame stall keeps original phase");
        Equal(610.6, clock.NextGridTime(610.1, 1), "No overdue burst after stall");

        clock.Pause(10.9);
        Equal(1.5, clock.BeatAt(99), "Pause freezes fractional beat");
        clock.Resume(100);
        Equal(1.5, clock.BeatAt(99.9), "Resume lead-in stays frozen");
        Equal(1.5, clock.BeatAt(100), "Resume preserves phase");
        Equal(2, clock.BeatAt(100.3), "Resumed progression");
        Equal(100.3, clock.NextGridTime(100.01, 1), "Resumed attack grid");
        clock.Stop();
        Equal(0, clock.BeatAt(110), "Stop clears elapsed state");

        clock.Start(200, 116);
        Equal(32, clock.BeatAt(200 + 32 * 60.0 / 116), "116 BPM loop");
        clock.Pause(199);
        clock.Resume(220);
        Equal(0, clock.BeatAt(219), "Pause before first scheduled audio");
        Equal(220, clock.NextGridTime(219.9, 1), "Restarted first grid point");
        bool rejected = false;
        try { clock.Start(0, 0); } catch (ArgumentOutOfRangeException) { rejected = true; }
        if (!rejected) throw new Exception("Invalid tempo accepted");
        Console.WriteLine("MUSIC_CLOCK_TESTS_PASS: tempo, subdivision, long stall, pause/resume, stop and validation");
    }
}

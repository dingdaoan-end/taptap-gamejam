using System;
using TapTapGameJam.Combat;
using TapTapGameJam.Music;

static class Program
{
    static int checks;
    static void Equal(double want, double got, string message)
    {
        if (double.IsNaN(got) || (double.IsInfinity(want) ? want != got : Math.Abs(want - got) > 1e-7))
            throw new Exception(message + ": expected " + want + ", got " + got);
        checks++;
    }
    static void Main()
    {
        float[] regular = { 0, 1, 2, 3 };
        Equal(2, AttackBeatGrid.Next(1.25, 4, regular), "entry between beats waits for the global next beat");
        Equal(4, AttackBeatGrid.Next(3.8, 4, new float[] { 0 }), "bar downbeat wraps globally");
        Equal(3.5, AttackBeatGrid.Next(2.6, 4, new float[] { .5f, 1.5f, 2.5f, 3.5f }), "offbeat pattern uses half-beats");
        Equal(4.5, AttackBeatGrid.Next(3.6, 4, new float[] { .5f, 1.5f, 2.5f, 3.5f }), "fractional pattern wraps");
        Equal(8, AttackBeatGrid.Next(8, 4, regular), "exact downbeat remains eligible");
        Equal(8, AttackBeatGrid.Next(8 + 1e-10, 4, regular), "DSP boundary roundoff does not skip a beat");
        Equal(1001, AttackBeatGrid.Next(1000.2, 4, regular), "long frame stall rejoins rather than replaying history");
        Equal(2, AttackBeatGrid.Next(1.1, 4, new float[] { 3, 2, 0, 2 }), "unordered duplicate marks cannot duplicate attacks");
        Equal(double.PositiveInfinity, AttackBeatGrid.Next(2, 4, Array.Empty<float>()), "empty pattern is silence");
        Equal(double.PositiveInfinity, AttackBeatGrid.Next(2, 4, null), "missing pattern is silence");
        Equal(double.PositiveInfinity, AttackBeatGrid.Next(2, 4, new float[] { -1, 4, float.NaN, float.PositiveInfinity }), "invalid offsets never create attacks");
        Equal(double.PositiveInfinity, AttackBeatGrid.Next(2, 0, regular), "invalid cycle does not schedule");
        var music = new MusicTimeline(); music.Start(10, 100);
        double beat = AttackBeatGrid.Next((10.7 + .2 - 10) / music.SecondsPerBeat, 4, regular);
        Equal(11.2, music.DspTimeForBeat(beat), "attack honors same audio scheduling lead at 100 BPM");
        music.Pause(10.9); music.Resume(100);
        Equal(100.3, music.DspTimeForBeat(2), "resume retains shared musical phase");
        music.Start(200, 116);
        Equal(201.0344827586207, music.DspTimeForBeat(2), "116 BPM follows background clock rather than fixed seconds");
        Console.WriteLine("BEAT_ATTACK_CORE_PASS checks=" + checks);
    }
}

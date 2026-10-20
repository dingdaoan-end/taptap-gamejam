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
        for (int offset = 0; offset < 4; offset++)
        {
            double previous = double.NegativeInfinity;
            for (int hit = 0; hit < 2; hit++)
            {
                double next = AttackBeatGrid.NextDoubleHit(offset, 4, previous);
                Equal(offset + hit, next, "bell segment supplies two hits one beat apart");
                previous = next;
            }
            Equal(double.PositiveInfinity, AttackBeatGrid.NextDoubleHit(offset, 4, previous), "no third hit in the same audio segment");
            for (int rest = 1; rest < 4; rest++)
                Equal(double.PositiveInfinity, AttackBeatGrid.NextDoubleHit(offset, 4, offset + rest), "no further damage after the second hit");
            Equal(offset + 4, AttackBeatGrid.NextDoubleHit(offset + 4, 4, previous), "next segment restores the original four-beat attack cadence");
        }
        Equal(1, AttackBeatGrid.NextDoubleHit(0, 4, 0), "second hit is independently scheduled one beat later");
        Equal(1, AttackBeatGrid.NextDoubleHit(0, 4, .5), "half-beat mark waits for the second hit");
        Equal(double.PositiveInfinity, AttackBeatGrid.NextDoubleHit(0, 1, 0), "short segment cannot hit outside its reservation");
        Equal(double.PositiveInfinity, AttackBeatGrid.NextDoubleHit(0, 0, -1), "empty audio segment cannot hit");
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
        foreach (double bpm in new[] { 100.0, 116.0 })
        {
            music.Start(300, bpm);
            Equal(300, music.NextBeatTime(299.9), "join the scheduled opening beat");
            foreach (double acquiredBeat in new[] { .01, .7, .99, 1.1, 2.8, 3.99, 4.5, 19.9 })
            {
                double now = music.DspTimeForBeat(acquiredBeat);
                double nextTime = music.NextBeatTime(now);
                double nextBeat = Math.Floor(acquiredBeat) + 1;
                Equal(music.DspTimeForBeat(nextBeat), nextTime,
                    "target acquisition joins the nearest next beat without pattern delay");
                Equal(nextBeat, AttackBeatGrid.NextDoubleHit(nextBeat, 4, double.NegativeInfinity),
                    "bell first hit matches audio start on any beat");
                Equal(nextBeat + 1, AttackBeatGrid.NextDoubleHit(nextBeat, 4, nextBeat),
                    "bell follow-up remains one beat after the new entry");
            }
            double first = AttackBeatGrid.NextDoubleHit(8, 4, double.NegativeInfinity);
            double second = AttackBeatGrid.NextDoubleHit(8, 4, first);
            Equal(60.0 / bpm, music.DspTimeForBeat(second) - music.DspTimeForBeat(first),
                "bell's second hit follows one shared beat at " + bpm + " BPM");
            Equal(double.PositiveInfinity, AttackBeatGrid.NextDoubleHit(8, 4, second),
                "tempo change does not add bell hits");
            double previous = AttackBeatGrid.Next(8, 4, regular);
            for (int i = 1; i <= 8; i++)
            {
                double next = AttackBeatGrid.Next(previous + 1e-5, 4, regular);
                Equal(60.0 / bpm, music.DspTimeForBeat(next) - music.DspTimeForBeat(previous),
                    "regular tower attack follows shared beat across segment boundaries");
                previous = next;
            }
        }
        Console.WriteLine("BEAT_ATTACK_CORE_PASS checks=" + checks);
    }
}

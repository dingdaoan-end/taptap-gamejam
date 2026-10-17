using System;

namespace TapTapGameJam.Music
{
    // Pure timing model: a frame stall never moves the musical origin.
    public sealed class MusicTimeline
    {
        public double SecondsPerBeat { get; private set; }
        public double OriginDspTime { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsPaused { get; private set; }
        double heldSeconds;

        public void Start(double startDspTime, double bpm)
        {
            if (double.IsNaN(bpm) || double.IsInfinity(bpm) || bpm <= 0)
                throw new ArgumentOutOfRangeException(nameof(bpm));
            SecondsPerBeat = 60.0 / bpm;
            OriginDspTime = startDspTime;
            heldSeconds = 0;
            IsRunning = true;
            IsPaused = false;
        }

        public double ElapsedSeconds(double now) => !IsRunning || IsPaused
            ? heldSeconds : Math.Max(heldSeconds, now - OriginDspTime);

        public double BeatAt(double now) => SecondsPerBeat > 0 ? ElapsedSeconds(now) / SecondsPerBeat : 0;

        public void Pause(double now)
        {
            if (!IsRunning || IsPaused) return;
            heldSeconds = ElapsedSeconds(now);
            IsPaused = true;
        }

        public void Resume(double resumeDspTime)
        {
            if (!IsRunning || !IsPaused) return;
            OriginDspTime = resumeDspTime - heldSeconds;
            IsPaused = false;
        }

        public void Stop()
        {
            IsRunning = false;
            IsPaused = false;
            heldSeconds = 0;
        }

        public double DspTimeForBeat(double beat)
        {
            if (!IsRunning || IsPaused) throw new InvalidOperationException("Music is not running.");
            return OriginDspTime + beat * SecondsPerBeat;
        }

        // subdivision=1: quarter-note beat; 2: eighth note; 4: sixteenth note.
        public double NextGridTime(double earliestDspTime, int subdivision)
        {
            if (subdivision < 1) throw new ArgumentOutOfRangeException(nameof(subdivision));
            double position = Math.Max(heldSeconds / SecondsPerBeat,
                Math.Max(0, (earliestDspTime - OriginDspTime) / SecondsPerBeat));
            double beat = Math.Ceiling(position * subdivision) / subdivision;
            return DspTimeForBeat(beat);
        }
    }
}

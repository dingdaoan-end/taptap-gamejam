namespace TapTapGameJam.PianoDefense
{
    // Audio is scheduled ahead, while visual beat events are presented at their DSP timestamp.
    public sealed class BeatClock
    {
        public const double LookAhead = 0.08;
        public bool Running { get; private set; }
        public bool Paused { get; private set; }
        public bool Scheduled { get; private set; }
        public double NextTime { get; private set; }
        public double LastTime { get; private set; }
        public void Start(double now) { Running = true; Paused = false; Scheduled = false; NextTime = now + 0.2; LastTime = now; }
        public void Stop() { Running = false; Paused = false; Scheduled = false; }
        public void Pause() { if (!Running) return; Paused = true; Scheduled = false; }
        public void Resume(double now) { if (!Running) return; Paused = false; Scheduled = false; NextTime = now + 0.2; }
        public bool TrySchedule(double now)
        {
            if (!Running || Paused || Scheduled || now + LookAhead < NextTime) return false;
            if (NextTime < now + 0.015) NextTime = now + LookAhead;
            Scheduled = true; return true;
        }
        public bool TryPresent(double now)
        {
            if (!Running || Paused || !Scheduled || now < NextTime) return false;
            LastTime = NextTime; NextTime += DefenseSimulation.SecondsPerBeat; Scheduled = false;
            // Do not compress all missed beats into one audible burst after a window drag/stall.
            if (NextTime <= now + LookAhead) NextTime = now + 0.2;
            return true;
        }
    }
}

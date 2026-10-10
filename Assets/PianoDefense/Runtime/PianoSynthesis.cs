using System;

namespace TapTapGameJam.PianoDefense
{
    public static class PianoSynthesis
    {
        static readonly int[] Semitones = { 0, 2, 4, 5, 7, 9, 11, 12, 14, 16, 3, -2, 17, 19, 21, 23, 24, 26, 28 };
        public static double Frequency(int pitch) { return 261.625565 * Math.Pow(2, Semitones[pitch] / 12.0); }
        public static float[] Render(int pitch, int sampleRate)
        {
            const double seconds = 2.0;
            float[] samples = new float[(int)(sampleRate * seconds)];
            double frequency = Frequency(pitch);
            for (int i = 0; i < samples.Length; i++)
            {
                double t = (double)i / sampleRate, signal = 0;
                for (int partial = 1; partial <= 8; partial++)
                {
                    // Slight inharmonicity and faster high-partial decay approximate a struck string.
                    double f = frequency * partial * Math.Sqrt(1 + 0.00012 * partial * partial);
                    if (f >= sampleRate * 0.45) break;
                    double envelope = Math.Exp(-t * (2.8 + partial * 0.7));
                    signal += Math.Sin(2 * Math.PI * f * t) * envelope / Math.Pow(partial, 1.65);
                }
                double attack = Math.Min(1, t / 0.003);
                double release = Math.Min(1, (seconds - t) / 0.04);
                samples[i] = (float)(signal * attack * release * 0.48);
            }
            return samples;
        }
    }
}

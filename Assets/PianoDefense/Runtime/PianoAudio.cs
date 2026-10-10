using System;
using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.PianoDefense
{
    public sealed class PianoAudio : MonoBehaviour
    {
        readonly List<AudioSource> voices = new List<AudioSource>();
        readonly AudioClip[] keys = new AudioClip[DefenseSimulation.PitchNames.Length];
        AudioClip click;
        int voice;
        public bool Metronome = true;
        public bool Muted { get; private set; }
        public int ScheduledNotes { get; private set; }
        public int LateSchedules { get; private set; }

        void Awake()
        {
            const int rate = 44100;
            for (int p = 0; p < keys.Length; p++)
            {
                var data = PianoSynthesis.Render(p, rate);
                keys[p] = AudioClip.Create("Piano " + DefenseSimulation.PitchNames[p], data.Length, 1, rate, false);
                keys[p].SetData(data, 0);
            }
            float[] tick = new float[2646];
            for (int i = 0; i < tick.Length; i++)
            {
                double t = (double)i / rate;
                tick[i] = (float)(Math.Sin(2 * Math.PI * 1400 * t) * Math.Exp(-t * 100) * Math.Min(1, t / 0.001) * 0.2);
            }
            click = AudioClip.Create("Soft metronome", tick.Length, 1, rate, false); click.SetData(tick, 0);
            for (int i = 0; i < 48; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false; source.spatialBlend = 0; source.loop = false;
                voices.Add(source);
            }
        }

        public void Schedule(IReadOnlyList<NoteEvent> notes, int beat, double time)
        {
            if (time < AudioSettings.dspTime) LateSchedules++;
            if (Metronome) Play(click, time, beat % 4 == 0 ? 0.32f : 0.16f);
            // 同拍和声逐轨叠加易削波，按复音数衰减单音音量。
            float volume = notes.Count >= 3 ? 0.34f : (notes.Count == 2 ? 0.44f : 0.55f);
            foreach (var note in notes) { Play(keys[note.Pitch], time, volume); ScheduledNotes++; }
        }

        public void Preview(int pitch) { Play(keys[pitch], AudioSettings.dspTime + 0.025, 0.65f); }
        void Play(AudioClip clip, double time, float volume)
        {
            var source = voices[voice++ % voices.Count];
            source.Stop(); source.clip = clip; source.volume = volume; source.mute = Muted;
            source.PlayScheduled(time);
        }
        public void SetMuted(bool muted) { Muted = muted; foreach (var source in voices) source.mute = muted; }
        public void StopAll() { foreach (var source in voices) source.Stop(); }
        void OnDestroy()
        {
            StopAll(); foreach (var key in keys) if (key != null) Destroy(key);
            if (click != null) Destroy(click);
        }
    }
}

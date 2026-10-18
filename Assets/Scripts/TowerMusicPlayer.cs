using System;
using TapTapGameJam.Combat;
using UnityEngine;

namespace TapTapGameJam.Music
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class TowerMusicPlayer : MonoBehaviour
    {
        [SerializeField] private MusicClock clock;
        [SerializeField] private TowerEnemyDetector detector;
        [SerializeField] private AudioSource musicSource;
        [Tooltip("Length of this musical variant in beats, not the WAV file length.")]
        [SerializeField, Min(1)] private int beatsPerLoop = 32;
        private AudioSource secondSource;
        private AudioClip activeClip;
        private MusicClock subscribedClock;
        private bool scheduled;
        private bool warned;
        private int nextSource;
        private double nextLoopBeat;
        private double nextLoopTime;
        public bool IsScheduled => scheduled;
        public double ScheduledStart { get; private set; }

        private void Awake()
        {
            if (musicSource == null) musicSource = GetComponent<AudioSource>();
            if (detector == null) detector = GetComponentInChildren<TowerEnemyDetector>();
            musicSource.playOnAwake = false;
            musicSource.Stop();
            secondSource = gameObject.AddComponent<AudioSource>();
            secondSource.playOnAwake = false;
        }

        private void Update()
        {
            if (clock == null) clock = FindObjectOfType<MusicClock>();
            if (subscribedClock != clock)
            {
                if (subscribedClock != null) subscribedClock.TransportChanged -= Cancel;
                Cancel();
                subscribedClock = clock;
                if (subscribedClock != null) subscribedClock.TransportChanged += Cancel;
            }
            if (clock == null || !clock.IsRunning || clock.IsPaused ||
                detector == null || !detector.isActiveAndEnabled || !detector.HasTarget ||
                musicSource.clip == null || !musicSource.enabled)
            {
                Cancel();
                return;
            }
            if (activeClip != musicSource.clip)
            {
                Cancel();
                activeClip = musicSource.clip;
                warned = false;
            }
            if (activeClip.loadState == AudioDataLoadState.Unloaded) activeClip.LoadAudioData();
            if (activeClip.loadState != AudioDataLoadState.Loaded) return;
            double duration = Math.Max(1, beatsPerLoop) * clock.SecondsPerBeat;
            if (!warned && Math.Abs(activeClip.samples / (double)activeClip.frequency - duration) > 0.02)
            {
                warned = true;
                Debug.LogWarning("Tower clip length differs from its musical loop. Long clips are cut at the loop boundary; short clips leave silence. Check BPM and Beats Per Loop.", this);
            }
            if (!scheduled)
            {
                double start = clock.GetNextGridDspTime();
                double beat = Math.Round((start - clock.GetDspTimeForBeat(0)) / clock.SecondsPerBeat);
                double loopBeat = beat % Math.Max(1, beatsPerLoop);
                Schedule(musicSource, start, loopBeat * clock.SecondsPerBeat, (Math.Max(1, beatsPerLoop) - loopBeat) * clock.SecondsPerBeat);
                ScheduledStart = start;
                nextLoopBeat = beat + Math.Max(1, beatsPerLoop) - loopBeat;
                nextLoopTime = clock.GetDspTimeForBeat(nextLoopBeat);
                nextSource = 1;
                scheduled = true;
            }
            // Recover from a long frame stall by joining the current phase again.
            if (nextLoopTime <= AudioSettings.dspTime)
            {
                Cancel();
                return;
            }
            if (nextLoopTime <= AudioSettings.dspTime + clock.ScheduleLeadSeconds * 2)
            {
                Schedule(nextSource == 0 ? musicSource : secondSource, nextLoopTime, 0, duration);
                nextSource = 1 - nextSource;
                nextLoopBeat += Math.Max(1, beatsPerLoop);
                nextLoopTime = clock.GetDspTimeForBeat(nextLoopBeat);
            }
        }

        private void Schedule(AudioSource source, double start, double offset, double remaining)
        {
            source.Stop();
            source.clip = activeClip;
            source.loop = false;
            source.pitch = 1;
            source.spatialBlend = 0;
            source.volume = musicSource.volume;
            source.mute = musicSource.mute;
            source.outputAudioMixerGroup = musicSource.outputAudioMixerGroup;
            source.priority = musicSource.priority;
            if (offset >= activeClip.samples / (double)activeClip.frequency) return;
            source.timeSamples = Math.Min(activeClip.samples - 1, (int)Math.Round(offset * activeClip.frequency));
            source.PlayScheduled(start);
            source.SetScheduledEndTime(start + remaining);
        }

        private void Cancel()
        {
            if (musicSource != null) musicSource.Stop();
            if (secondSource != null) secondSource.Stop();
            scheduled = false;
            ScheduledStart = 0;
        }

        private void OnDisable()
        {
            Cancel();
            if (subscribedClock != null) subscribedClock.TransportChanged -= Cancel;
            subscribedClock = null;
        }

        private void OnDestroy()
        {
            if (secondSource != null) Destroy(secondSource);
        }
    }
}

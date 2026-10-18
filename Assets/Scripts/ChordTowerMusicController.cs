using System;
using System.Collections.Generic;
using TapTapGameJam.Combat;
using UnityEngine;

namespace TapTapGameJam.Music
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class ChordTowerMusicController : MonoBehaviour
    {
        public MusicClock clock;
        public TowerEnemyDetector detector;
        public AudioSource audioSettings;
        [Tooltip("Four ordered eight-beat clips (32-beat loop). Independent of map Section.")]
        public AudioClip[] segments = new AudioClip[4];
        public int NextSegment => clock != null ? clock.CurrentPhrase % MusicPhraseGrid.PhraseCount + 1 : 1;
        public bool IsPhrasePlaying => CurrentSegment > 0;
        public int CurrentSegment { get; private set; }
        private sealed class Phrase
        {
            public AudioSource source;
            public double start, end;
            public int index;
            public bool started;
        }
        private readonly List<Phrase> phrases = new List<Phrase>();
        private readonly AudioSource[] voices = new AudioSource[2];
        private MusicClock subscribedClock;
        private bool warned;
        private bool HasTarget => detector != null && detector.isActiveAndEnabled && detector.HasTarget;

        private void Awake()
        {
            if (audioSettings == null) audioSettings = GetComponent<AudioSource>();
            if (detector == null) detector = GetComponentInChildren<TowerEnemyDetector>();
            var oldPlayer = GetComponent<TowerMusicPlayer>();
            if (oldPlayer != null) oldPlayer.enabled = false;
            audioSettings.Stop();
            audioSettings.playOnAwake = false;
            for (int i = 0; i < voices.Length; i++)
            {
                var child = new GameObject("Chord phrase voice " + i);
                child.transform.SetParent(transform, false);
                voices[i] = child.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
            }
        }

        private void Update()
        {
            if (clock == null) clock = FindObjectOfType<MusicClock>();
            if (clock != subscribedClock)
            {
                if (subscribedClock != null) subscribedClock.TransportChanged -= OnTransport;
                StopPhrases();
                subscribedClock = clock;
                if (clock != null) clock.TransportChanged += OnTransport;
            }
            if (clock == null || !clock.IsRunning || clock.IsPaused) return;
            double now = AudioSettings.dspTime;
            // Playback state follows scheduled DSP times, not a per-tower counter.
            foreach (var phrase in phrases)
                if (!phrase.started && now >= phrase.start)
                {
                    phrase.started = true;
                }
            for (int i = phrases.Count - 1; i >= 0; i--)
                if (now >= phrases[i].end || (!HasTarget && now < phrases[i].start))
                { phrases[i].source.Stop(); phrases.RemoveAt(i); }
            CurrentSegment = phrases.Count > 0 && phrases[0].started ? phrases[0].index + 1 : 0;
            if (!HasTarget || phrases.Count >= 2) return;
            double start;
            if (phrases.Count == 0) start = clock.GetNextGridDspTime();
            else
            {
                var current = phrases[0];
                if (!current.started || current.end - now > clock.ScheduleLeadSeconds * 2) return;
                start = current.end - now >= 0.02 ? current.end : clock.GetNextGridDspTime();
            }
            // All starts lie on whole beats. Round away DSP floating-point noise
            // so a boundary such as beat 8 never resolves to the previous phrase.
            double beat = Math.Round((start - clock.GetDspTimeForBeat(0)) / clock.SecondsPerBeat);
            int index = MusicPhraseGrid.Index(beat);
            double offsetSeconds = MusicPhraseGrid.Offset(beat) * clock.SecondsPerBeat;
            double end = clock.GetDspTimeForBeat(MusicPhraseGrid.EndBeat(beat));
            var clips = segments;
            if (clips == null || clips.Length != MusicPhraseGrid.PhraseCount)
            {
                if (!warned) { Debug.LogWarning("Assign exactly four chord phrases in global phrase order.", this); warned = true; }
                return;
            }
            var clip = clips[index];
            if (clip == null)
            {
                if (!warned) { Debug.LogWarning("Chord sequence has a missing segment. Assign all clips in order.", this); warned = true; }
                return;
            }
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            if (clip.loadState != AudioDataLoadState.Loaded) return;
            double duration = 8 * clock.SecondsPerBeat;
            if (Math.Abs(clip.samples / (double)clip.frequency - duration) > 0.02)
            {
                if (!warned) { Debug.LogWarning("Chord segment must match eight beats at MusicClock BPM. Playback withheld; check BPM and clips.", this); warned = true; }
                return;
            }
            AudioSource voice = phrases.Count == 0 || phrases[0].source != voices[0] ? voices[0] : voices[1];
            voice.Stop();
            voice.clip = clip;
            voice.loop = false;
            voice.pitch = 1;
            voice.spatialBlend = 0;
            voice.volume = audioSettings.volume;
            voice.mute = audioSettings.mute;
            voice.outputAudioMixerGroup = audioSettings.outputAudioMixerGroup;
            voice.timeSamples = Math.Min(clip.samples - 1, (int)Math.Round(offsetSeconds * clip.frequency));
            voice.PlayScheduled(start);
            voice.SetScheduledEndTime(end);
            phrases.Add(new Phrase { source = voice, start = start, end = end, index = index });
        }

        private void OnTransport()
        {
            // Resume/restart rejoins the global phrase and offset on the next beat.
            StopPhrases();
        }
        private void StopPhrases()
        {
            foreach (var voice in voices) if (voice != null) voice.Stop();
            phrases.Clear();
            CurrentSegment = 0;
        }
        private void OnDisable()
        {
            StopPhrases();
            if (subscribedClock != null) subscribedClock.TransportChanged -= OnTransport;
            subscribedClock = null;
        }
        private void OnDestroy()
        {
            foreach (var voice in voices) if (voice != null) Destroy(voice.gameObject);
        }
    }
}

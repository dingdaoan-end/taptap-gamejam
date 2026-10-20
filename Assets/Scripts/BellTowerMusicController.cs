using System;
using System.Collections.Generic;
using TapTapGameJam.Combat;
using UnityEngine;

namespace TapTapGameJam.Music
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class BellTowerMusicController : MonoBehaviour
    {
        public MusicClock clock;
        public TowerEnemyDetector detector;
        public AudioSource audioSettings;
        public const int LoopCount = 5;
        public const int BeatsPerSegment = 4;
        [Serializable]
        public sealed class Loop
        {
            public string name;
            [Tooltip("Four ordered four-beat slices: one 16-beat bell loop.")]
            public AudioClip[] segments = new AudioClip[4];
        }
        [Tooltip("Loops 01 through 05, allocated once per tower in creation order.")]
        public Loop[] loops = new Loop[LoopCount];
        public int AssignedLoop => assignedLoop + 1; // 0 until a scene clock is available.
        public double CurrentSegmentStartBeat => phrases.Count > 0 ? phrases[0].beat : double.NaN;
        public AudioClip[] ActiveSegments => assignedLoop >= 0 && loops != null && assignedLoop < loops.Length
            && loops[assignedLoop] != null ? loops[assignedLoop].segments : null;
        public int NextSegment => ActiveSegments != null && ActiveSegments.Length > 0
            ? nextSegment % ActiveSegments.Length + 1 : 1;
        public int CurrentSegment { get; private set; }
        private sealed class Phrase
        {
            public AudioSource source;
            public double start, end, beat;
            public int index;
            public bool started;
        }
        private readonly List<Phrase> phrases = new List<Phrase>();
        private readonly AudioSource[] voices = new AudioSource[2];
        private MusicClock subscribedClock;
        private int nextSegment;
        private int assignedLoop = -1;
        private bool warned;
        private bool hadTarget;
        private bool HasTarget => detector != null && detector.isActiveAndEnabled && detector.HasTarget;

        private void Awake()
        {
            FindClockAndAssignLoop();
            if (audioSettings == null) audioSettings = GetComponent<AudioSource>();
            if (detector == null) detector = GetComponentInChildren<TowerEnemyDetector>();
            var oldPlayer = GetComponent<TowerMusicPlayer>();
            if (oldPlayer != null) oldPlayer.enabled = false;
            audioSettings.Stop();
            audioSettings.playOnAwake = false;
            for (int i = 0; i < voices.Length; i++)
            {
                var child = new GameObject("Bell phrase voice " + i);
                child.transform.SetParent(transform, false);
                voices[i] = child.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
            }
        }

        private void Update()
        {
            FindClockAndAssignLoop();
            if (clock != subscribedClock)
            {
                if (subscribedClock != null) subscribedClock.TransportChanged -= OnTransport;
                StopPhrases();
                subscribedClock = clock;
                if (clock != null) clock.TransportChanged += OnTransport;
            }
            if (clock == null || !clock.isActiveAndEnabled || !clock.IsRunning || clock.IsPaused) return;
            if (detector != null) detector.RefreshTargets();
            bool hasTarget = HasTarget;
            // Reacquisition starts on the next beat even if an old segment's
            // audio tail is still playing after the previous target left.
            if (hasTarget && !hadTarget) StopPhrases();
            hadTarget = hasTarget;
            double now = AudioSettings.dspTime;
            // Commit sequence advancement only when a phrase actually starts.
            foreach (var phrase in phrases)
                if (!phrase.started && now >= phrase.start)
                {
                    phrase.started = true;
                    nextSegment = phrase.index + 1;
                }
            for (int i = phrases.Count - 1; i >= 0; i--)
                if (now >= phrases[i].end || (!HasTarget && now < phrases[i].start))
                { phrases[i].source.Stop(); phrases.RemoveAt(i); }
            CurrentSegment = phrases.Count > 0 && phrases[0].started ? phrases[0].index + 1 : 0;
            if (!HasTarget || phrases.Count >= 2) return;
            var clips = ActiveSegments;
            if (clips == null || clips.Length == 0) return;
            int index = nextSegment % clips.Length;
            var clip = clips[index];
            if (clip == null)
            {
                if (!warned) { Debug.LogWarning("Bell loop " + AssignedLoop + " has a missing segment. Assign its clips in order.", this); warned = true; }
                return;
            }
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            if (clip.loadState != AudioDataLoadState.Loaded) return;
            double duration = BeatsPerSegment * clock.SecondsPerBeat;
            if (Math.Abs(clip.samples / (double)clip.frequency - duration) > 0.02)
            {
                if (!warned) { Debug.LogWarning("Bell segment must match four beats at MusicClock BPM. Playback withheld; check BPM and clips.", this); warned = true; }
                return;
            }
            double start;
            if (phrases.Count == 0) start = clock.GetNextBeatDspTime();
            else
            {
                var current = phrases[0];
                if (!current.started || current.end - now > clock.ScheduleLeadSeconds * 2) return;
                // If the scheduling deadline was missed, rejoin on a later beat.
                start = current.end - now >= 0.02 ? current.end : clock.GetNextBeatDspTime();
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
            voice.timeSamples = 0;
            voice.PlayScheduled(start);
            voice.SetScheduledEndTime(start + duration);
            double beat = Math.Round((start - clock.GetDspTimeForBeat(0)) / clock.SecondsPerBeat);
            phrases.Add(new Phrase { source = voice, start = start, end = start + duration, beat = beat, index = index });
        }

        // Each reserved four-beat segment supplies only two hits: at its start and
        // one beat later. No reservation means no damage, including after pause.
        public double GetScheduledAttackBeat(double afterBeat)
        {
            if (!isActiveAndEnabled || clock == null || !clock.IsRunning || clock.IsPaused)
                return double.PositiveInfinity;
            double next = double.PositiveInfinity;
            foreach (var phrase in phrases)
                next = Math.Min(next, AttackBeatGrid.NextDoubleHit(phrase.beat, BeatsPerSegment, afterBeat));
            return next;
        }

        private void FindClockAndAssignLoop()
        {
            if (clock == null)
                foreach (var candidate in FindObjectsOfType<MusicClock>())
                    if (candidate.gameObject.scene == gameObject.scene) { clock = candidate; break; }
            // Assignment is independent of target acquisition and playback. Enabling a
            // tower again, pausing, or destroying another tower does not consume an index.
            if (assignedLoop >= 0 || clock == null) return;
            if (loops == null || loops.Length != LoopCount)
            {
                if (!warned) { Debug.LogWarning("Assign the five bell loops in 01-05 order.", this); warned = true; }
                return;
            }
            assignedLoop = clock.AllocateBellLoop();
            // Load the assigned loop before target acquisition so the first
            // attack does not wait for audio loading on its upcoming beat.
            var clips = ActiveSegments;
            if (clips != null)
                foreach (var clip in clips)
                    if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        }
        private void OnTransport()
        {
            // Replay an interrupted phrase in full after resume; restart resets the sequence.
            foreach (var phrase in phrases)
                if (phrase.started && AudioSettings.dspTime < phrase.end) { nextSegment = phrase.index; break; }
            StopPhrases();
            hadTarget = false;
            if (clock != null && !clock.IsRunning) nextSegment = 0;
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
            hadTarget = false;
            if (subscribedClock != null) subscribedClock.TransportChanged -= OnTransport;
            subscribedClock = null;
        }
        private void OnDestroy()
        {
            foreach (var voice in voices) if (voice != null) Destroy(voice.gameObject);
        }
    }
}

using System;
using System.Collections.Generic;
using TapTapGameJam.Combat;
using UnityEngine;

namespace TapTapGameJam.Music
{
    [DisallowMultipleComponent, RequireComponent(typeof(AudioSource), typeof(TowerBeatAttack))]
    public sealed class LevelThreeTowerMusicController : MonoBehaviour
    {
        [Serializable]
        public sealed class Variant
        {
            public string name;
            public AudioClip[] segments;
        }

        [SerializeField] MusicClock clock;
        [SerializeField] TowerEnemyDetector detector;
        [SerializeField] AudioSource audioSettings;
        [Tooltip("Level 3 and 4: ordered eight-beat segments, variants assigned in placement order.")]
        public Variant[] variants;
        public MusicClock Clock => clock;
        public bool IsActiveForLevel => isActiveAndEnabled && clock != null && clock.SelectedLevel >= 3;
        public int AssignedVariant => assignedVariant + 1;
        public int CurrentSegment { get; private set; }
        public AudioClip[] ActiveSegments => assignedVariant >= 0 && variants != null && assignedVariant < variants.Length
            && variants[assignedVariant] != null ? variants[assignedVariant].segments : null;

        sealed class Reservation
        {
            public AudioSource source;
            public double start, end, segmentBeat, startBeat, endBeat;
            public int index;
            public bool started;
        }

        readonly List<Reservation> reservations = new List<Reservation>();
        readonly AudioSource[] voices = new AudioSource[2];
        MusicClock subscribedClock;
        TowerInstrument instrument;
        int assignedVariant = -1, nextSegment;
        bool hadTarget, warned, legacyStopped;
        bool HasTarget => detector != null && detector.isActiveAndEnabled && detector.HasTarget;

        void Awake()
        {
            instrument = GetComponent<TowerBeatAttack>().Instrument;
            if (audioSettings == null) audioSettings = GetComponent<AudioSource>();
            if (detector == null) detector = GetComponentInChildren<TowerEnemyDetector>();
            for (int i = 0; i < voices.Length; i++)
            {
                var child = new GameObject("Level 3 melody voice " + i);
                child.transform.SetParent(transform, false);
                voices[i] = child.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
            }
            FindClockAndAssignVariant();
            StopLegacyPlayers();
        }

        void StopLegacyPlayers()
        {
            if (!IsActiveForLevel || legacyStopped) return;
            var bell = GetComponent<BellTowerMusicController>();
            var chord = GetComponent<ChordTowerMusicController>();
            var oldPlayer = GetComponent<TowerMusicPlayer>();
            if (bell != null) bell.enabled = false;
            if (chord != null) chord.enabled = false;
            if (oldPlayer != null) oldPlayer.enabled = false;
            legacyStopped = true;
        }

        void FindClockAndAssignVariant()
        {
            if (clock == null)
                foreach (var candidate in FindObjectsOfType<MusicClock>())
                    if (candidate.gameObject.scene == gameObject.scene) { clock = candidate; break; }
            if (!IsActiveForLevel || assignedVariant >= 0) return;
            if (variants == null || variants.Length != LevelThreeMusicRules.VariantCount(instrument))
            { Warn("Assign the complete level 3 variant bank."); return; }
            assignedVariant = clock.AllocateLevel3Variant(instrument);
            var clips = ActiveSegments;
            if (clips != null)
                foreach (var clip in clips)
                    if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        }

        void Update()
        {
            FindClockAndAssignVariant();
            StopLegacyPlayers();
            if (clock != subscribedClock)
            {
                if (subscribedClock != null) subscribedClock.TransportChanged -= OnTransport;
                StopReservations();
                subscribedClock = clock;
                if (clock != null) clock.TransportChanged += OnTransport;
            }
            if (!IsActiveForLevel || !clock.isActiveAndEnabled || !clock.IsRunning || clock.IsPaused)
            { StopReservations(); hadTarget = false; return; }
            if (detector != null) detector.RefreshTargets();
            bool hasTarget = HasTarget;
            // Bell/Duduk restart the next sequential clip on reacquisition.
            // Chords always rejoin the shared phrase and sample offset.
            if (hasTarget && !hadTarget && instrument != TowerInstrument.Chord) StopReservations();
            hadTarget = hasTarget;
            double now = AudioSettings.dspTime;
            foreach (var reservation in reservations)
                if (!reservation.started && now >= reservation.start)
                { reservation.started = true; nextSegment = reservation.index + 1; }
            for (int i = reservations.Count - 1; i >= 0; i--)
                if (now >= reservations[i].end || (!hasTarget && now < reservations[i].start))
                { reservations[i].source.Stop(); reservations.RemoveAt(i); }
            CurrentSegment = reservations.Count > 0 && reservations[0].started ? reservations[0].index + 1 : 0;
            if (!hasTarget || reservations.Count >= 2) return;
            var clips = ActiveSegments;
            if (clips == null || clips.Length == 0) { Warn("The assigned melody variant has no clips."); return; }

            double startBeat;
            if (reservations.Count == 0)
                startBeat = Math.Round((clock.GetNextBeatDspTime() - clock.GetDspTimeForBeat(0)) / clock.SecondsPerBeat);
            else
            {
                var current = reservations[0];
                if (!current.started || current.end - now > clock.ScheduleLeadSeconds * 2) return;
                startBeat = current.segmentBeat + LevelThreeMusicRules.SegmentBeats;
                if (clock.GetDspTimeForBeat(startBeat) - now < .02)
                    startBeat = Math.Round((clock.GetNextBeatDspTime() - clock.GetDspTimeForBeat(0)) / clock.SecondsPerBeat);
            }
            bool global = instrument == TowerInstrument.Chord;
            double segmentBeat = global ? LevelThreeMusicRules.SegmentStart(startBeat) : startBeat;
            int index = global ? LevelThreeMusicRules.GlobalSegment(startBeat, clips.Length) : nextSegment % clips.Length;
            var clip = clips[index];
            if (clip == null) { Warn("Missing level 3 melody segment."); return; }
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            if (clip.loadState != AudioDataLoadState.Loaded) return;
            double duration = clip.samples / (double)clip.frequency;
            if (Math.Abs(duration - LevelThreeMusicRules.SegmentBeats * clock.SecondsPerBeat) > .02)
            { Warn("Level 3 melody must be eight beats at the selected MusicClock BPM."); return; }
            int offsetSamples = (int)Math.Round((startBeat - segmentBeat) * clock.SecondsPerBeat * clip.frequency);
            if (offsetSamples >= clip.samples) return;
            double start = clock.GetDspTimeForBeat(startBeat);
            // Actual PCM length controls the reservation; next starts stay on the DSP
            // beat grid so the +/- one-sample slice rounding cannot accumulate drift.
            double end = start + (clip.samples - offsetSamples) / (double)clip.frequency;
            AudioSource voice = reservations.Count == 0 || reservations[0].source != voices[0] ? voices[0] : voices[1];
            voice.Stop();
            voice.clip = clip;
            voice.loop = false;
            voice.pitch = 1;
            voice.spatialBlend = 0;
            voice.volume = audioSettings.volume;
            voice.mute = audioSettings.mute;
            voice.outputAudioMixerGroup = audioSettings.outputAudioMixerGroup;
            voice.timeSamples = offsetSamples;
            voice.PlayScheduled(start);
            voice.SetScheduledEndTime(end);
            reservations.Add(new Reservation { source = voice, start = start, end = end, segmentBeat = segmentBeat,
                startBeat = startBeat, endBeat = startBeat + (end - start) / clock.SecondsPerBeat, index = index });
        }

        public double GetScheduledAttackBeat(double afterBeat)
        {
            if (!IsActiveForLevel || !clock.isActiveAndEnabled || !clock.IsRunning || clock.IsPaused || !HasTarget)
                return double.PositiveInfinity;
            double next = double.PositiveInfinity;
            foreach (var reservation in reservations)
                next = Math.Min(next, LevelThreeMusicRules.NextAttack(instrument, reservation.segmentBeat,
                    reservation.startBeat, reservation.endBeat, afterBeat));
            return next;
        }

        void OnTransport()
        {
            foreach (var reservation in reservations)
                if (reservation.started && AudioSettings.dspTime < reservation.end)
                { nextSegment = reservation.index; break; }
            StopReservations();
            hadTarget = false;
            if (clock == null || !clock.IsRunning) nextSegment = 0;
        }
        void StopReservations()
        {
            foreach (var voice in voices) if (voice != null) voice.Stop();
            reservations.Clear();
            CurrentSegment = 0;
        }
        void Warn(string message) { if (!warned) { Debug.LogWarning(message, this); warned = true; } }
        void OnDisable()
        {
            StopReservations(); hadTarget = false;
            if (subscribedClock != null) subscribedClock.TransportChanged -= OnTransport;
            subscribedClock = null;
        }
        void OnDestroy() { foreach (var voice in voices) if (voice != null) Destroy(voice.gameObject); }
    }
}

using System;
using System.Collections;
using UnityEngine;

namespace TapTapGameJam.Music
{
    [DisallowMultipleComponent]
    public sealed class MusicClock : MonoBehaviour
    {
        [Header("Musical grid (changes apply on restart)")]
        [SerializeField, Min(1)] float bpm = 100;
        [Tooltip("Tempo used from level 3 onward. The BPM above is for levels 1 and 2.")]
        [SerializeField, Min(1)] float level3Bpm = 116;
        [SerializeField, Min(1)] int beatsPerBar = 4;
        [SerializeField, Min(1)] int barsPerLoop = 8;
        [SerializeField, Min(0.1f)] float scheduleLeadSeconds = 0.2f;
        [Header("Castle drum")]
        [SerializeField] AudioSource drumSource;
        [SerializeField] AudioClip level3DrumClip;
        [SerializeField, Range(0, 1)] float dialogueVolumeMultiplier = 0.5f;
        [SerializeField] bool playOnStart = true;
        [SerializeField] bool pauseOnFocusLoss = true;

        readonly MusicTimeline timeline = new MusicTimeline();
        Coroutine loading;
        bool focusPaused;
        int activeBeatsPerBar = 4, activeBarsPerLoop = 8;
        AudioClip activeClip;
        AudioClip initialDrumClip;
        float normalDrumVolume;
        bool drumInitialized;
        int selectedLevel = 1;
        float SelectedBpm => Math.Max(1, selectedLevel >= 3 ? level3Bpm : bpm);
        int nextBellLoop;
        readonly LevelThreeVariantAllocator level3Variants = new LevelThreeVariantAllocator();
        public int SelectedLevel => selectedLevel;

        public int AllocateLevel3Variant(TapTapGameJam.Combat.TowerInstrument instrument)
        {
            return level3Variants.Allocate(instrument);
        }

        // Scene-owned build order. Audio stop/start and pause must not reset towers
        // already placed during preparation. Only clearing a level resets this order.
        public int AllocateBellLoop()
        {
            int loop = nextBellLoop;
            nextBellLoop = (nextBellLoop + 1) % BellTowerMusicController.LoopCount;
            return loop;
        }
        public void ResetBellLoopAssignments()
        {
            nextBellLoop = 0;
            level3Variants.Reset();
        }

        // Future tower schedulers must cancel old queued audio/attacks on this event.
        public event Action TransportChanged;
        public int Revision { get; private set; }
        public bool IsRunning => timeline.IsRunning;
        public bool IsPaused => timeline.IsPaused;
        public bool IsPreparing => loading != null;
        public double SecondsPerBeat => timeline.SecondsPerBeat;
        public double Bpm => IsRunning ? 60.0 / SecondsPerBeat : SelectedBpm;
        public double CurrentBeat => timeline.BeatAt(AudioSettings.dspTime);
        public int CurrentPhrase => MusicPhraseGrid.Index(CurrentBeat) + 1;
        public double BeatInPhrase => MusicPhraseGrid.Offset(CurrentBeat);
        public int BeatsPerBar => activeBeatsPerBar;
        public int BeatsPerLoop => activeBeatsPerBar * activeBarsPerLoop;
        public double LoopBeat => CurrentBeat % BeatsPerLoop;
        public double LoopDuration => BeatsPerLoop * SecondsPerBeat;
        public double ScheduleLeadSeconds => Math.Max(0.1, scheduleLeadSeconds);

        void Awake()
        {
            InitializeDrum();
        }

        void InitializeDrum()
        {
            if (drumInitialized) return;
            if (drumSource == null) drumSource = GetComponent<AudioSource>();
            if (drumSource == null) return;
            drumSource.playOnAwake = false;
            initialDrumClip = drumSource.clip;
            normalDrumVolume = drumSource.volume;
            drumInitialized = true;
        }

        public void SetDialogueActive(bool active)
        {
            InitializeDrum();
            if (drumSource != null)
                drumSource.volume = normalDrumVolume * (active ? dialogueVolumeMultiplier : 1f);
        }

        // Level transitions clear the old towers before restarting the shared transport.
        // Keep the selected clip for retries and pause/resume, including later levels.
        public void SetLevel(int level)
        {
            InitializeDrum();
            float previousBpm = SelectedBpm;
            selectedLevel = Math.Max(1, level);
            if (drumSource == null) return;
            var clip = level >= 3 && level3DrumClip != null ? level3DrumClip : initialDrumClip;
            if (clip == null || (drumSource.clip == clip && previousBpm == SelectedBpm)) return;
            bool restart = IsRunning || IsPreparing;
            drumSource.clip = clip;
            if (restart) StartPlayback();
        }

        void Start() { if (playOnStart) EnsurePlayback(); }

        // Phase changes keep the same drum loop and shared musical position.
        public void EnsurePlayback()
        {
            if (IsPaused) ResumePlayback();
            else if (!IsRunning && !IsPreparing) StartPlayback();
        }

        [ContextMenu("Music/Start or Restart")]
        public void StartPlayback()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            StopPlayback();
            if (drumSource == null || drumSource.clip == null)
            {
                Debug.LogError("MusicClock needs the Castle AudioSource and a drum clip.", this);
                return;
            }
            loading = StartCoroutine(PrepareAndStart());
        }

        IEnumerator PrepareAndStart()
        {
            // Yield once so the coroutine handle is assigned even for a preloaded clip.
            yield return null;
            var clip = drumSource.clip;
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            while (clip.loadState == AudioDataLoadState.Loading) yield return null;
            loading = null;
            if (clip.loadState != AudioDataLoadState.Loaded)
            {
                Debug.LogError("Could not load the Castle drum audio.", this);
                yield break;
            }
            activeClip = clip;
            activeBeatsPerBar = Math.Max(1, beatsPerBar);
            activeBarsPerLoop = Math.Max(1, barsPerLoop);
            double start = AudioSettings.dspTime + ScheduleLeadSeconds;
            timeline.Start(start, SelectedBpm);
            ScheduleDrum(start);
            NotifyTransportChanged();
            if (Math.Abs(clip.samples / (double)clip.frequency - LoopDuration) > 0.02)
                Debug.LogWarning("Drum file length differs from the musical loop (" +
                    LoopDuration.ToString("F3") + " s). The clock stays on tempo; the current drum " +
                    "loops at its original file length until the corrected audio is supplied.", this);
        }

        void ScheduleDrum(double start)
        {
            drumSource.Stop();
            drumSource.clip = activeClip;
            drumSource.playOnAwake = false;
            drumSource.loop = true;
            drumSource.pitch = 1;
            drumSource.spatialBlend = 0;
            double duration = activeClip.samples / (double)activeClip.frequency;
            double position = timeline.ElapsedSeconds(AudioSettings.dspTime) % duration;
            drumSource.timeSamples = Math.Min(activeClip.samples - 1, (int)(position * activeClip.frequency));
            drumSource.PlayScheduled(start);
        }

        [ContextMenu("Music/Pause")]
        public void PausePlayback()
        {
            // A manual pause takes ownership from focus-loss auto-pause.
            focusPaused = false;
            if (!IsRunning || IsPaused) return;
            timeline.Pause(AudioSettings.dspTime);
            drumSource.Stop();
            NotifyTransportChanged();
        }

        [ContextMenu("Music/Resume")]
        public void ResumePlayback()
        {
            if (!IsRunning || !IsPaused) return;
            double start = AudioSettings.dspTime + ScheduleLeadSeconds;
            timeline.Resume(start);
            ScheduleDrum(start);
            focusPaused = false;
            NotifyTransportChanged();
        }

        [ContextMenu("Music/Stop")]
        public void StopPlayback()
        {
            if (loading != null) { StopCoroutine(loading); loading = null; }
            if (drumSource != null) drumSource.Stop();
            timeline.Stop();
            focusPaused = false;
            NotifyTransportChanged();
        }

        public double GetDspTimeForBeat(double absoluteBeat) => timeline.DspTimeForBeat(absoluteBeat);
        public double GetNextBeatDspTime() => timeline.NextBeatTime(AudioSettings.dspTime);
        public double GetNextGridDspTime(int subdivisionsPerBeat = 1) =>
            timeline.NextGridTime(AudioSettings.dspTime + ScheduleLeadSeconds, subdivisionsPerBeat);

        void NotifyTransportChanged() { Revision++; TransportChanged?.Invoke(); }
        void OnApplicationFocus(bool focused)
        {
            if (!pauseOnFocusLoss) return;
            if (!focused && IsRunning && !IsPaused) { PausePlayback(); focusPaused = true; }
            else if (focused && focusPaused) ResumePlayback();
        }
        void OnDisable() { StopPlayback(); }
    }
}

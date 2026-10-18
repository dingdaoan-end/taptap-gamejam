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
        [Serializable] public sealed class Variant
        {
            public AudioClip[] segments = new AudioClip[8];
        }
        public MusicClock clock;
        public TowerEnemyDetector detector;
        public AudioSource audioSettings;
        private Transform deploymentSlot;
        public string DeploymentSection { get; private set; } = "Unassigned";
        public int SelectedVariation { get; private set; }
        public Variant[] variants = { new Variant(), new Variant(), new Variant() };
        public int NextSegment => variants != null && activeVariant >= 0 && activeVariant < variants.Length &&
            variants[activeVariant] != null && variants[activeVariant].segments != null && variants[activeVariant].segments.Length > 0
            ? nextSegment % variants[activeVariant].segments.Length + 1 : 1;
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
        private int nextSegment, activeVariant = -1;
        private bool warned;
        private bool HasTarget => detector != null && detector.isActiveAndEnabled && detector.HasTarget;

        // Built towers are instantiated at scene root, so BuildSlot supplies context
        // explicitly. Preplaced child towers can also resolve their own ancestors.
        public void BindDeploymentSlot(Transform slot)
        {
            deploymentSlot = slot;
            ResolveVariant();
        }

        private int ResolveVariant()
        {
            DeploymentSection = "Unassigned";
            SelectedVariation = 0;
            for (Transform ancestor = deploymentSlot != null ? deploymentSlot : transform.parent;
                 ancestor != null; ancestor = ancestor.parent)
            {
                string section = ancestor.name.Replace(" ", "").Replace("_", "").Replace("-", "");
                int selected;
                if (string.Equals(section, "SectionA", StringComparison.OrdinalIgnoreCase)) selected = 0;
                else if (string.Equals(section, "SectionB", StringComparison.OrdinalIgnoreCase)) selected = 1;
                else if (string.Equals(section, "SectionC", StringComparison.OrdinalIgnoreCase)) selected = 2;
                else continue;
                DeploymentSection = ancestor.name;
                SelectedVariation = selected + 1;
                return selected;
            }
            return -1;
        }
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
                var child = new GameObject("Bell phrase voice " + i);
                child.transform.SetParent(transform, false);
                voices[i] = child.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
            }
        }

        private void Update()
        {
            int selected = ResolveVariant();
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
            // Rebinding to another section preserves the currently playing phrase.
            if (selected != activeVariant)
            {
                for (int i = phrases.Count - 1; i >= 0; i--)
                    if (!phrases[i].started) { phrases[i].source.Stop(); phrases.RemoveAt(i); }
                if (phrases.Count > 0) return;
                activeVariant = selected;
                nextSegment = 0;
                warned = false;
            }
            if (!HasTarget || phrases.Count >= 2) return;
            if (activeVariant < 0 || variants == null || activeVariant >= variants.Length || variants[activeVariant] == null) return;
            var clips = variants[activeVariant].segments;
            if (clips == null || clips.Length == 0) return;
            int index = nextSegment % clips.Length;
            var clip = clips[index];
            if (clip == null)
            {
                if (!warned) { Debug.LogWarning("Bell variant has a missing segment. Assign all clips in order.", this); warned = true; }
                return;
            }
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            if (clip.loadState != AudioDataLoadState.Loaded) return;
            double duration = 4 * clock.SecondsPerBeat;
            if (Math.Abs(clip.samples / (double)clip.frequency - duration) > 0.02)
            {
                if (!warned) { Debug.LogWarning("Bell segment must match four beats at MusicClock BPM. Playback withheld; check BPM and clips.", this); warned = true; }
                return;
            }
            double start;
            if (phrases.Count == 0) start = clock.GetNextGridDspTime();
            else
            {
                var current = phrases[0];
                if (!current.started || current.end - now > clock.ScheduleLeadSeconds * 2) return;
                // If the scheduling deadline was missed, rejoin on a later beat.
                start = current.end - now >= 0.02 ? current.end : clock.GetNextGridDspTime();
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
            voice.PlayScheduled(start);
            voice.SetScheduledEndTime(start + duration);
            phrases.Add(new Phrase { source = voice, start = start, end = start + duration, index = index });
        }

        private void OnTransport()
        {
            // Replay an interrupted phrase in full after resume; restart resets the sequence.
            foreach (var phrase in phrases)
                if (phrase.started && AudioSettings.dspTime < phrase.end) { nextSegment = phrase.index; break; }
            StopPhrases();
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
            if (subscribedClock != null) subscribedClock.TransportChanged -= OnTransport;
            subscribedClock = null;
        }
        private void OnDestroy()
        {
            foreach (var voice in voices) if (voice != null) Destroy(voice.gameObject);
        }
    }
}

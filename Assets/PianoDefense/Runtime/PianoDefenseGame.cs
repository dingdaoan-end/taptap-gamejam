using System;
using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.PianoDefense
{
    [RequireComponent(typeof(PianoAudio))]
    public sealed class PianoDefenseGame : MonoBehaviour
    {
        public LevelDefinition Level { get; private set; }
        public string SelectedLevelId { get; private set; } = "canon";
        public DefenseSimulation Simulation { get; private set; }
        public DefenseSimulation Display { get; private set; }
        public PianoAudio Sound { get; private set; }
        public bool Started { get; private set; }
        public bool Finished { get; private set; }
        public bool Replaying { get; private set; }
        public bool Paused { get { return clock.Paused; } }
        public int AudibleBeat { get; private set; } = -1;
        public int SelectedTowerId { get; private set; } = -1;
        public bool BuildMode { get; set; } = true;
        public string Notice { get; private set; }
        public float NoticeUntil { get; private set; }
        public readonly float[] KeyFlash = new float[DefenseSimulation.PitchNames.Length];
        public readonly List<VisualShot> Shots = new List<VisualShot>();
        public double BeatTime { get { return clock.LastTime; } }
        readonly BeatClock clock = new BeatClock();
        BeatResult pending;
        int replayBeat, replayNoteIndex;
        readonly List<NoteEvent> replayNotes = new List<NoteEvent>();
        readonly Queue<Action> queuedInput = new Queue<Action>();

        public sealed class VisualShot { public ShotEvent Shot; public float Time; }

        void Awake()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            Sound = GetComponent<PianoAudio>();
            NewGame(true);
        }

        public void NewGame(bool example)
        {
            var asset = Resources.Load<TextAsset>("Levels/" + SelectedLevelId);
            if (asset == null) throw new InvalidOperationException("Missing level: " + SelectedLevelId);
            var definition = JsonUtility.FromJson<LevelDefinition>(asset.text);
            var simulation = new DefenseSimulation(definition);
            clock.Stop(); Sound.StopAll(); pending = null;
            Level = definition;
            Simulation = simulation; if (example) Simulation.ApplyExampleLayout();
            Display = Simulation.Snapshot(); queuedInput.Clear();
            Started = Finished = Replaying = false; AudibleBeat = -1; SelectedTowerId = -1; BuildMode = true;
            Shots.Clear(); Array.Clear(KeyFlash, 0, KeyFlash.Length);
            ShowNotice(example ? "已载入 " + Level.title + "。第一行主旋律，下方三行伴奏。" : "点击空地放置琴塔，每座 8 币；准备好后开始守夜。");
        }

        public void SelectLevel(string id)
        {
            if (id != "canon" && id != "progression-4536251") return;
            SelectedLevelId = id;
            NewGame(true);
        }

        public void StartBattle()
        {
            if (Started || Finished) return;
            Started = true; clock.Start(AudioSettings.dspTime);
            ShowNotice("敌人从右向左前进。第一行每拍一个旋律音，伴奏每组两击，结尾 C 四击。");
        }

        public void TogglePause()
        {
            if (!Started || (Finished && !Replaying)) return;
            if (clock.Paused) clock.Resume(AudioSettings.dspTime);
            else { clock.Pause(); Sound.StopAll(); }
        }

        public void ToggleReplay()
        {
            if (!Finished) return;
            if (Replaying) { Replaying = false; clock.Stop(); Sound.StopAll(); AudibleBeat = Simulation.Beat; return; }
            if (Simulation.Notes.Count == 0) { ShowNotice("本局尚未留下音符。重新布置琴塔，再演奏一次吧。"); return; }
            Sound.StopAll(); Replaying = true; replayBeat = replayNoteIndex = 0; AudibleBeat = -1;
            pending = null; Shots.Clear(); Array.Clear(KeyFlash, 0, KeyFlash.Length);
            clock.Start(AudioSettings.dspTime);
        }

        void Update()
        {
            double now = AudioSettings.dspTime;
            if (clock.TrySchedule(now))
            {
                if (Replaying)
                {
                    replayNotes.Clear();
                    // Only advance the log cursor after this beat is presented, so pausing cannot lose it.
                    for (int i = replayNoteIndex; i < Simulation.Notes.Count && Simulation.Notes[i].Beat <= replayBeat; i++)
                        if (Simulation.Notes[i].Beat == replayBeat) replayNotes.Add(Simulation.Notes[i]);
                    Sound.Schedule(replayNotes, replayBeat, clock.NextTime);
                }
                else
                {
                    if (pending == null) pending = Simulation.Step();
                    Sound.Schedule(pending.Notes, pending.Beat, clock.NextTime);
                }
            }
            if (clock.TryPresent(now))
            {
                if (Replaying)
                {
                    AudibleBeat = replayBeat;
                    foreach (var note in replayNotes) KeyFlash[note.Pitch] = Time.unscaledTime;
                    replayNoteIndex += replayNotes.Count;
                    replayBeat++;
                    if (replayBeat > Simulation.Beat) { Replaying = false; clock.Stop(); }
                }
                else
                {
                    AudibleBeat = pending.Beat;
                    foreach (var note in pending.Notes) KeyFlash[note.Pitch] = Time.unscaledTime;
                    foreach (var shot in pending.Shots) Shots.Add(new VisualShot { Shot = shot, Time = Time.unscaledTime });
                    if (pending.Leaks.Count > 0) ShowNotice("漏过 " + pending.Leaks.Count + " 个音符，据点受损！");
                    if (pending.PhraseProgress == 4) ShowNotice("完整旋律！终止式清除 " + pending.CadenceVictims.Count + " 个敌人，祝福 4 层。");
                    else if (pending.PhraseProgress > 0) ShowNotice("乐句祝福 " + pending.PhraseProgress + " 层：+" + pending.PhraseProgress + " 币" + (pending.Healed > 0 ? "，据点回复 1" : "") + "。");
                    if (Simulation.Finished)
                    {
                        Finished = true; clock.Stop();
                        Debug.Log("PIANO_ROUND_END win=" + Simulation.Won + " notes=" + Simulation.Notes.Count + " lateAudio=" + Sound.LateSchedules);
                    }
                    pending = null;
                    while (queuedInput.Count > 0) queuedInput.Dequeue()();
                    Display = Simulation.Snapshot();
                }
            }
            Shots.RemoveAll(s => Time.unscaledTime - s.Time > 0.4f);
        }

        public void SelectOrBuild(int x, int y)
        {
            if (Finished || Paused) return;
            // Selection is UI state, so a following Delete always addresses the newly selected tower.
            var tower = Display.TowerAt(x, y);
            if (tower != null) { SelectedTowerId = tower.Id; BuildMode = false; return; }
            if (!BuildMode) { SelectedTowerId = -1; return; }
            // Capture the build intent now; toggling B later must not change this click's meaning.
            RunOrQueue(() => ApplyBuild(x, y));
        }

        void ApplyBuild(int x, int y)
        {
            if (Finished) return;
            if (!DefenseSimulation.IsBuildCell(x, y)) { ShowNotice("琴塔要建在深色空地上，四条亮色通道留给音符敌人。"); return; }
            if (!Simulation.TryBuild(x, y)) { ShowNotice(Simulation.Coins < 8 ? "需要 8 币。击杀敌人可获得金币。" : "同时最多放置 8 座琴塔。"); return; }
            SelectedTowerId = Simulation.TowerAt(x, y).Id;
            ShowNotice("琴塔已就位。可在右侧选择只演奏哪些音高。");
        }

        public void SellSelected()
        {
            int selected = SelectedTowerId;
            RunOrQueue(() => { if (Simulation.Sell(selected)) { if (SelectedTowerId == selected) SelectedTowerId = -1; ShowNotice("琴塔已回收。"); } });
        }

        public void SetMode(int id, TargetMode mode)
        {
            RunOrQueue(() => { var t = Simulation.TowerById(id); if (t != null) Simulation.SetFilter(id, mode, t.Pitch); });
        }

        public void SetPitch(int id, int pitch)
        {
            RunOrQueue(() => { var t = Simulation.TowerById(id); if (t != null) Simulation.SetFilter(id, t.Mode, pitch); });
        }

        void RunOrQueue(Action action)
        {
            if (Finished || Paused) return;
            // The pending beat is already scheduled. Apply its result before accepting new orders.
            if (pending != null) queuedInput.Enqueue(action);
            else { action(); Display = Simulation.Snapshot(); }
        }

        public void PreviewKey(int pitch)
        {
            if (Started && !Finished) { ShowNotice("守夜中，钢琴由命中敌人触发。"); return; }
            if (Replaying) return;
            Sound.Preview(pitch); KeyFlash[pitch] = Time.unscaledTime;
        }

        public void ShowNotice(string text) { Notice = text; NoticeUntil = Time.unscaledTime + 5; }
        void OnApplicationFocus(bool focus) { if (!focus && Started && !Paused && (!Finished || Replaying)) TogglePause(); }
        void OnApplicationPause(bool paused) { if (paused && Started && !Paused && (!Finished || Replaying)) TogglePause(); }
        void OnDisable() { clock.Stop(); if (Sound != null) Sound.StopAll(); }
    }
}

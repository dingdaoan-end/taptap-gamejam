using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TapTapGameJam.Combat;
using TapTapGameJam.Music;
using TapTapGameJam.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Real DSP playback with authored prefabs and a stationary target; runtime only.
public static class TowerMusicLoopPlayChecks
{
    const string Key = "TowerMusicLoop.PlayCheck";
    static int step, checks, lastFrame;
    static double began, changed;
    static string failure;
    static GameObject fixture, enemy;
    static BellTowerMusicController bell;
    static ChordTowerMusicController chordA, chordB;
    static readonly HashSet<int> seenChords = new HashSet<int>();
    static readonly List<int> bellOrder = new List<int>();
    static int lastBell, lastChord;
    static bool wrapped;
    static BellTowerMusicController[] timingBells;
    static int[] lastShots, phaseStartShots;
    static double[] lastHits;
    static readonly HashSet<int> heardTowers = new HashSet<int>();

    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        step = checks = 0; lastFrame = -1; lastBell = lastChord = 0;
        failure = null; wrapped = false; seenChords.Clear(); bellOrder.Clear();
        timingBells = null; heardTowers.Clear();
        began = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Poll; EditorApplication.update += Poll;
        Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
    }
    [MenuItem("Tools/Audio Loops/Run Bell and Chord Play Checks")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Map.unity" || scene.isDirty) throw new InvalidOperationException("Open and save Map first.");
        SessionState.SetBool(Key, true); Resume(); EditorApplication.EnterPlaymode();
    }
    static void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failure = message;
    }
    static void Expect(bool condition, string message)
    {
        if (!condition) throw new Exception(message); checks++;
    }
    static T Spawn<T>(string path) where T : Component
    {
        var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), new Vector3(10000, 10000, 0), Quaternion.identity, fixture.transform);
        go.GetComponent<AudioSource>().mute = true;
        return go.GetComponent<T>();
    }
    static BellTowerMusicController SpawnBell() => Spawn<BellTowerMusicController>("Assets/Prefabs/Tower_Bell.prefab");
    static ChordTowerMusicController SpawnChord() => Spawn<ChordTowerMusicController>("Assets/Prefabs/Tower_Chord.prefab");
    static void RefreshTargets()
    {
        Physics2D.SyncTransforms();
        foreach (var d in fixture.GetComponentsInChildren<TowerEnemyDetector>()) d.RefreshTargets();
    }
    static AudioSource Playing(ChordTowerMusicController c) => c.GetComponentsInChildren<AudioSource>()
        .FirstOrDefault(a => c.CurrentSegment > 0 && a.isPlaying && a.clip == c.segments[c.CurrentSegment - 1]);
    static bool SafeOffset(MusicClock clock) => clock.BeatInPhrase > .35 && clock.BeatInPhrase < 3.65;
    static void MonitorBellTiming(MusicClock clock)
    {
        if (timingBells == null || !clock.IsRunning || clock.IsPaused) return;
        for (int i = 0; i < timingBells.Length; i++)
        {
            var b = timingBells[i]; var attack = b.GetComponent<TowerBeatAttack>();
            double offset = clock.CurrentBeat - b.CurrentSegmentStartBeat;
            if (b.CurrentSegment > 0 && offset > .35 && offset < 3.65)
            {
                var voice = b.GetComponentsInChildren<AudioSource>().FirstOrDefault(a => a.isPlaying && a.clip == b.ActiveSegments[b.CurrentSegment - 1]);
                if (voice == null || Math.Abs(voice.timeSamples / (double)voice.clip.frequency - offset * clock.SecondsPerBeat) > .15)
                    throw new Exception("Bell audio is off its scheduled starting beat.");
                heardTowers.Add(i + 1);
            }
            if (attack.ShotsFired == lastShots[i]) continue;
            double hitOffset = attack.LastAttackBeat - b.CurrentSegmentStartBeat;
            if (Math.Abs(hitOffset) > .00001 && Math.Abs(hitOffset - 1) > .00001)
                throw new Exception("Bell hit on the wrong beat: " + attack.LastAttackBeat);
            if (Math.Abs(b.GetScheduledAttackBeat(attack.LastAttackBeat - .001) - attack.LastAttackBeat) > .00001)
                throw new Exception("Bell hit without the matching audio reservation.");
            double expectedGap = Math.Abs(hitOffset - 1) < .00001 ? 1 : 3;
            if (step >= 4 && step <= 6 && lastShots[i] > 0 && Math.Abs(attack.LastAttackBeat - lastHits[i] - expectedGap) > .00001)
                throw new Exception("Bell must hit twice per four-beat segment, separated by one beat.");
            lastShots[i] = attack.ShotsFired; lastHits[i] = attack.LastAttackBeat;
        }
    }
    static bool AllBellsHitAgain() => timingBells.Select((b, i) => b.GetComponent<TowerBeatAttack>().ShotsFired > phaseStartShots[i]).All(v => v);
    static void RememberShots() => phaseStartShots = timingBells.Select(b => b.GetComponent<TowerBeatAttack>().ShotsFired).ToArray();
    static bool Synced(MusicClock clock)
    {
        if (chordA.CurrentSegment == 0 || chordB.CurrentSegment == 0 || !SafeOffset(clock)) return false;
        var a = Playing(chordA); var b = Playing(chordB);
        if (a == null || b == null) return false;
        Expect(chordA.CurrentSegment == chordB.CurrentSegment && chordA.CurrentSegment == clock.CurrentPhrase, "new/existing chord use same global segment");
        Expect(Math.Abs(a.timeSamples - b.timeSamples) < 4410, "new/existing chord sample offsets match within 100 ms");
        Expect(Math.Abs(a.timeSamples / (double)a.clip.frequency - clock.BeatInPhrase * clock.SecondsPerBeat) < .15, "actual chord audio offset matches global phase");
        return true;
    }
    static void Monitor(MusicClock clock)
    {
        if (bell != null && bell.CurrentSegment > 0 && bell.CurrentSegment != lastBell)
        {
            bellOrder.Add(bell.CurrentSegment); lastBell = bell.CurrentSegment;
            if (bell.AssignedLoop != 1) throw new Exception("Bell loop assignment changed while playing.");
            if (bell.CurrentSegment != (bellOrder.Count - 1) % 4 + 1) throw new Exception("Bell loop skipped or reordered a segment.");
        }
        if (chordA != null && chordA.CurrentSegment > 0 && SafeOffset(clock))
        {
            if (chordA.CurrentSegment != clock.CurrentPhrase) throw new Exception("Chord drifted from global segment.");
            seenChords.Add(chordA.CurrentSegment);
            if (lastChord == 8 && chordA.CurrentSegment == 1) wrapped = true;
            lastChord = chordA.CurrentSegment;
        }
    }
    static void Poll()
    {
        if (EditorApplication.timeSinceStartup - began > 80) { Finish(false, "Timed out at step " + step); return; }
        if (!EditorApplication.isPlaying || Time.frameCount < 3 || Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            if (failure != null) throw new Exception(failure);
            var flow = UnityEngine.Object.FindObjectOfType<MapUIFlow>();
            if (flow == null) throw new Exception("Missing MapUIFlow");
            var clock = flow.clock;
            if (step >= 4 && step <= 10) MonitorBellTiming(clock);
            switch (step)
            {
                case 0:
                    flow.dialogue.nextButton.onClick.Invoke(); step++; break;
                case 1:
                    flow.dialogue.nextButton.onClick.Invoke(); step++; break;
                case 2:
                    Expect(flow.CurrentPhase == MapUIFlow.Phase.Preparation, "test begins in preparation");
                    var fields = new SerializedObject(clock);
                    fields.FindProperty("pauseOnFocusLoss").boolValue = false; fields.ApplyModifiedPropertiesWithoutUndo();
                    clock.GetComponent<AudioSource>().mute = true;
                    fixture = new GameObject("Tower music runtime check fixture");
                    var bells = Enumerable.Range(0, 7).Select(_ => SpawnBell()).ToArray(); bell = bells[0];
                    Expect(bells.Select(b => b.AssignedLoop).SequenceEqual(new[] { 1, 2, 3, 4, 5, 1, 2 }), "bell creation order is 01-05-01-02 before any targets");
                    Expect(bells.All(b => double.IsNaN(b.CurrentSegmentStartBeat)), "no attack beat is reserved before target acquisition");
                    Expect(bells.All(b => b.loops.Length == 5 && b.loops.All(l => l.segments.Length == 4 && l.segments.All(c => c != null && Math.Abs(c.length - 2.4) < .001))), "five complete 16-beat bell loops configured");
                    var attack = new SerializedObject(bell.GetComponent<TowerBeatAttack>());
                    Expect(attack.FindProperty("patternBeats").intValue == 4 && attack.FindProperty("attackBeats").arraySize == 1 && attack.FindProperty("attackBeats").GetArrayElementAtIndex(0).floatValue == 0, "bell prefab retains its four-beat segment metadata");
                    bells[2].gameObject.SetActive(false); UnityEngine.Object.Destroy(bells[2].gameObject);
                    var replacement = SpawnBell();
                    Expect(replacement.AssignedLoop == 3, "destroying tower does not rewind loop allocation");
                    bell.gameObject.SetActive(false); bell.gameObject.SetActive(true);
                    var afterEnable = SpawnBell();
                    Expect(bell.AssignedLoop == 1 && afterEnable.AssignedLoop == 4, "reenabling preserves loop allocation without consuming the next slot");
                    timingBells = new[] { bells[0], bells[1], bells[6], bells[3] };
                    lastShots = new int[4]; lastHits = new double[4];
                    chordA = SpawnChord();
                    Expect(chordA.segments.Length == 8 && chordA.segments.Select((c, i) => c != null && c.name == "Lv1_Chords_4Beats_" + (i+1).ToString("00") && Math.Abs(c.length - 2.4) < .001).All(v => v), "chord prefab contains all eight four-beat clips in order");
                    flow.StartBattle(); step++; break;
                case 3:
                    if (!clock.IsRunning || clock.CurrentBeat < .5) return;
                    Expect(bell.CurrentSegment == 0 && chordA.CurrentSegment == 0 && !fixture.GetComponentsInChildren<AudioSource>().Any(a => a.isPlaying), "targetless towers remain silent");
                    enemy = new GameObject("Stationary music check target"); enemy.transform.SetParent(fixture.transform);
                    enemy.SetActive(false);
                    enemy.layer = LayerMask.NameToLayer("Enemy");
                    enemy.transform.position = bell.detector.GetComponent<BoxCollider2D>().bounds.center;
                    enemy.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                    enemy.AddComponent<BoxCollider2D>().size = Vector2.one * .1f;
                    var health = new SerializedObject(enemy.AddComponent<EnemyHealth>());
                    health.FindProperty("maxHealth").intValue = 100000; health.ApplyModifiedPropertiesWithoutUndo();
                    enemy.SetActive(true);
                    RefreshTargets();
                    Expect(bell.detector.HasTarget && chordA.detector.HasTarget, "real collider targets detected");
                    step++; break;
                case 4:
                    Monitor(clock);
                    if (clock.CurrentBeat < 6.2) return;
                    chordB = SpawnChord(); RefreshTargets(); step++; break;
                case 5:
                    Monitor(clock);
                    if (!Synced(clock)) return;
                    Expect(chordB.CurrentSegment != 1 && Playing(chordB).timeSamples > 44100 / 2, "late chord joins middle of current segment instead of restarting 01");
                    step++; break;
                case 6:
                    Monitor(clock);
                    if (!wrapped || bellOrder.Count < 5) return;
                    Expect(seenChords.Count == 8, "all eight chord segments played and wrapped 08 to 01");
                    Expect(bellOrder.Take(5).SequenceEqual(new[] { 1, 2, 3, 4, 1 }), "assigned bell loop plays 01-02-03-04-01");
                    Expect(heardTowers.Count == 4 && lastShots.All(s => s >= 5), "all four voices play with two hits per four-beat segment");
                    RememberShots();
                    flow.TogglePause();
                    Expect(clock.IsPaused && !fixture.GetComponentsInChildren<AudioSource>().Any(a => a.isPlaying), "pause cancels scheduled tower voices");
                    changed = EditorApplication.timeSinceStartup; step++; break;
                case 7:
                    if (EditorApplication.timeSinceStartup - changed < .4) return;
                    Expect(timingBells.Select((b, i) => b.GetComponent<TowerBeatAttack>().ShotsFired == phaseStartShots[i]).All(v => v), "paused towers deal no damage");
                    flow.TogglePause(); step++; break;
                case 8:
                    if (!AllBellsHitAgain()) return;
                    if (!Synced(clock)) return;
                    Expect(bell.AssignedLoop == 1 && timingBells.All(b => !double.IsNaN(b.CurrentSegmentStartBeat)), "pause/resume retains loops and rejoins shared beats");
                    RememberShots();
                    enemy.SetActive(false); RefreshTargets(); changed = EditorApplication.timeSinceStartup; step++; break;
                case 9:
                    if (EditorApplication.timeSinceStartup - changed < 3) return;
                    Expect(bell.CurrentSegment == 0 && chordA.CurrentSegment == 0 && chordB.CurrentSegment == 0 && !fixture.GetComponentsInChildren<AudioSource>().Any(a => a.isPlaying), "no target schedules no new segments after current tails finish");
                    Expect(timingBells.Select((b, i) => b.GetComponent<TowerBeatAttack>().ShotsFired == phaseStartShots[i]).All(v => v), "target loss cancels damage");
                    enemy.SetActive(true); RefreshTargets(); step++; break;
                case 10:
                    if (!AllBellsHitAgain()) return;
                    if (!Synced(clock)) return;
                    Expect(heardTowers.Count == 4, "target reacquisition keeps audio and attacks synchronized");
                    fixture.SetActive(false); UnityEngine.Object.Destroy(fixture);
                    flow.RestartCurrentLevel();
                    fixture = new GameObject("Restart allocation check");
                    var restarted = Enumerable.Range(0, 5).Select(_ => SpawnBell()).ToArray();
                    Expect(restarted.Select(b => b.AssignedLoop).SequenceEqual(new[] { 1, 2, 3, 4, 5 }), "level restart resets loop allocation");
                    Finish(true, checks + " checks passed; real prefab playback, 8-segment wrap, late joins, pause/resume, target reacquisition, allocation and restart.");
                    break;
            }
        }
        catch (Exception e) { Finish(false, "Step " + step + ": " + e); }
    }
    static void Finish(bool success, string message)
    {
        EditorApplication.update -= Poll; Application.logMessageReceived -= OnLog; SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Artifacts/TowerMusicLoops");
        File.WriteAllText("Artifacts/TowerMusicLoops/play-check.txt", (success ? "TOWER_MUSIC_PLAY_PASS\n" : "TOWER_MUSIC_PLAY_FAIL\n") + message);
        if (success) Debug.Log("TOWER_MUSIC_PLAY_PASS " + message); else Debug.LogError("TOWER_MUSIC_PLAY_FAIL " + message);
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }
}

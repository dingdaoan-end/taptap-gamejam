using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TapTapGameJam.Combat;
using TapTapGameJam.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Real Map integration; speed up the musical clock and freeze test actors only after testing defeat.
public static class LaterLevelWavePlayChecks
{
    const string Key = "LaterLevelWaves.Play";
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static int stage, lastFrame, groupMask, mostSpawned;
    static double began, anchor, pausedAt, heldBeat, clearedAt;
    static string failure;
    static GameObject heldEnemy;
    static bool sawBoss;

    public static void RunBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Map.unity", OpenSceneMode.Single);
        var flow = UnityEngine.Object.FindObjectOfType<MapUIFlow>();
        var clockFields = new SerializedObject(flow.clock);
        clockFields.FindProperty("bpm").floatValue = 600;
        clockFields.FindProperty("level3Bpm").floatValue = 600;
        clockFields.FindProperty("pauseOnFocusLoss").boolValue = false;
        clockFields.ApplyModifiedPropertiesWithoutUndo();
        var flowFields = new SerializedObject(flow);
        flowFields.FindProperty("cameraMoveDuration").floatValue = .1f;
        flowFields.ApplyModifiedPropertiesWithoutUndo();
        SessionState.SetBool(Key, true);
        Resume();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        stage = 0; lastFrame = -1; failure = null;
        began = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Poll; EditorApplication.update += Poll;
        Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
    }
    static void OnLog(string text, string trace, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failure = text; }
    static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
    static EnemyWaveGroupGate Gate(MapUIFlow flow) => (EnemyWaveGroupGate)typeof(MapUIFlow).GetField("waveGroupGate", Private).GetValue(flow);
    static GameObject[] Living(EnemyAudioTestSpawner source) =>
        ((List<GameObject>)typeof(EnemyAudioTestSpawner).GetField("livingEnemies", Private).GetValue(source))
        .Where(e => e != null && e.activeInHierarchy).ToArray();
    static bool Boss(GameObject enemy) => new SerializedObject(enemy.GetComponent<EnemyHealth>()).FindProperty("resistance").intValue == (int)EnemyResistance.Boss;
    static void Kill(GameObject enemy)
    {
        var health = enemy.GetComponent<EnemyHealth>();
        foreach (TowerInstrument instrument in Enum.GetValues(typeof(TowerInstrument)))
            if (health.IsAlive) health.ReceiveHit(health.MaxHealth, instrument, AudioSettings.dspTime);
    }

    static void BeginScenario(MapUIFlow flow)
    {
        foreach (var source in flow.spawners) source.unitsPerBeat = 0;
        flow.StartBattle();
        Require(Gate(flow) != null, "Level " + flow.CurrentLevel + " must use a shared clear gate across all three roads");
        anchor = Gate(flow).GroupStartBeat;
        heldEnemy = null; groupMask = 1; mostSpawned = 0; sawBoss = false;
        stage = 2;
    }

    static void Poll()
    {
        try
        {
            if (failure != null) throw new Exception(failure);
            if (EditorApplication.timeSinceStartup - began > 100) throw new Exception("Timed out at stage " + stage);
            if (!EditorApplication.isPlaying || Time.frameCount < 4 || Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            var flow = UnityEngine.Object.FindObjectOfType<MapUIFlow>();
            if (stage == 0)
            {
                if (flow.dialogue.IsPlaying) { flow.dialogue.Skip(); return; }
                if (flow.CurrentPhase != MapUIFlow.Phase.Preparation) return;
                if (flow.CurrentLevel < 3) { flow.SkipCurrentLevel(); return; }
                Require(flow.spawners.Length == 3 && flow.spawners.Sum(s => s.PlannedCount) == 36, "Level 3 must be playable");
                // Exercise actual road arrival/base damage before the later gate-only fixtures.
                foreach (var source in flow.spawners) source.unitsPerBeat = 1000;
                flow.StartBattle(); stage = 1; return;
            }
            if (stage == 1)
            {
                if (flow.CurrentPhase != MapUIFlow.Phase.Result) return;
                Require(!flow.LevelWon && flow.CurrentBaseHealth == 0, "Real arrivals must cause defeat");
                flow.RetryButton.onClick.Invoke();
                Require(flow.CurrentPhase == MapUIFlow.Phase.Preparation && flow.CurrentBaseHealth == 5 &&
                    flow.spawners.All(s => s.SpawnedCount == 0 && s.LivingCount == 0 && !s.enabled), "Retry must reset health and every road");
                BeginScenario(flow); return;
            }
            if (flow.CurrentPhase == MapUIFlow.Phase.Transition) return;
            if (stage == 2)
            {
                Require(flow.CurrentPhase == MapUIFlow.Phase.Battle, "A temporary clear gap must not finish the level");
                if (heldEnemy == null) heldEnemy = Living(flow.spawners.Last()).FirstOrDefault();
                foreach (var enemy in flow.spawners.SelectMany(Living).ToArray()) if (enemy != heldEnemy) Kill(enemy);
                if (flow.clock.CurrentBeat < anchor + 34) return;
                int firstCount = flow.CurrentLevel == 3 ? 4 : 5;
                Require(heldEnemy != null && Gate(flow).ActiveGroup == 0 && flow.spawners.All(s => s.SpawnedCount == firstCount) &&
                    flow.spawners.Sum(s => s.LivingCount) == 1, "One surviving road must hold every second-wave birth after the rest expires");
                flow.TogglePause();
                heldBeat = flow.clock.CurrentBeat; pausedAt = EditorApplication.timeSinceStartup;
                stage = 3; return;
            }
            if (stage == 3)
            {
                if (EditorApplication.timeSinceStartup - pausedAt < .3) return;
                Require(flow.CurrentPhase == MapUIFlow.Phase.Paused && flow.clock.IsPaused &&
                    Math.Abs(flow.clock.CurrentBeat - heldBeat) < .00001 && Gate(flow).ActiveGroup == 0,
                    "Pause must freeze the shared wave schedule");
                flow.TogglePause();
                clearedAt = flow.clock.CurrentBeat;
                Kill(heldEnemy); stage = 4; return;
            }
            if (stage == 4)
            {
                if (Gate(flow).ActiveGroup == 0) return;
                Require(Gate(flow).GroupStartBeat >= Math.Ceiling(clearedAt) && flow.spawners.All(s =>
                    s.SpawnedCount <= (flow.CurrentLevel == 3 ? 5 : 6)), "Delayed clear must re-anchor every road without an overdue burst");
                stage = 5;
            }
            if (stage == 5)
            {
                if (flow.CurrentPhase == MapUIFlow.Phase.Result)
                {
                    int expected = flow.CurrentLevel == 3 ? 36 : 46;
                    Require(flow.LevelWon && mostSpawned == expected && groupMask == 7 &&
                        flow.spawners.All(s => s.LivingCount == 0 && !s.enabled), "Exactly three complete waves must settle in victory");
                    Debug.Log("LATER_WAVE_LEVEL_PLAY_PASS level=" + flow.CurrentLevel + " total=" + mostSpawned);
                    if (flow.CurrentLevel == 3) { flow.NextLevelButton.onClick.Invoke(); stage = 6; return; }
                    Require(sawBoss && !flow.NextLevelButton.gameObject.activeSelf, "Final victory must include the boss and no fifth level");
                    flow.ReplayLevelButton.onClick.Invoke();
                    Require(flow.CurrentLevel == 4 && flow.CurrentPhase == MapUIFlow.Phase.Preparation && flow.CurrentBaseHealth == 5 &&
                        flow.spawners.All(s => s.SpawnedCount == 0 && s.LivingCount == 0), "Final replay must reset all three roads");
                    flow.StartBattle();
                    Require(Gate(flow).ActiveGroup == 0 && Gate(flow).GroupStartBeat >= flow.clock.CurrentBeat - .1, "Replay must create a fresh gate");
                    Finish(true, "Real Map: level 3 defeat/retry, levels 3/4 three-road clear gates, pause, delayed births, 36/46 enemies, final Boss duet damage, victory/next/replay passed.");
                    return;
                }
                Require(flow.CurrentPhase == MapUIFlow.Phase.Battle, "Unexpected battle state");
                mostSpawned = Math.Max(mostSpawned, flow.spawners.Sum(s => s.SpawnedCount));
                groupMask |= 1 << Gate(flow).ActiveGroup;
                var enemies = flow.spawners.SelectMany(Living).ToArray();
                foreach (var enemy in enemies.Where(e => !Boss(e))) Kill(enemy);
                foreach (var enemy in enemies.Where(Boss))
                {
                    Require(flow.CurrentLevel == 4 && Gate(flow).ActiveGroup == 2 && flow.spawners.All(s => s.SpawningComplete) &&
                        !flow.LevelWon, "Terminal boss must keep the completed final wave in Battle");
                    var health = enemy.GetComponent<EnemyHealth>();
                    health.ReceiveHit(health.MaxHealth, TowerInstrument.Duduk, AudioSettings.dspTime);
                    Require(health.IsAlive, "Boss must resist a single instrument");
                    health.ReceiveHit(health.MaxHealth, TowerInstrument.Bell, AudioSettings.dspTime);
                    Require(!health.IsAlive, "Boss must resolve after a simultaneous instrument duet");
                    sawBoss = true;
                }
                return;
            }
            if (stage == 6)
            {
                if (flow.dialogue.IsPlaying) { flow.dialogue.Skip(); return; }
                if (flow.CurrentPhase != MapUIFlow.Phase.Preparation) return;
                Require(flow.CurrentLevel == 4 && flow.spawners.Sum(s => s.PlannedCount) == 46 &&
                    flow.spawners.All(s => !s.enabled && s.LivingCount == 0), "Next level must prepare the correct three roads without early spawning");
                BeginScenario(flow);
            }
        }
        catch (Exception e) { Finish(false, "stage=" + stage + " " + e); }
    }

    static void Finish(bool pass, string message)
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Poll; Application.logMessageReceived -= OnLog;
        if (pass) Debug.Log("LATER_WAVE_PLAY_PASS " + message); else Debug.LogError("LATER_WAVE_PLAY_FAIL " + message);
        EditorApplication.Exit(pass ? 0 : 1);
    }
}

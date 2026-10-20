using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TapTapGameJam.Combat;
using TapTapGameJam.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Runs real spawners and movement; resolves enemies by typed hits to test the shared gate.
public static class MapUIFlowWaveGateChecks
{
    const string Key = "MapUIFlow.WaveGateCheck";
    static int step;
    static double began, startBeat, clearBeat;
    [MenuItem("Tools/Enemies/Run Level 2 Clear Gate Play Checks")]
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorSceneManager.GetActiveScene().path != "Assets/Scenes/Map.unity" ||
            EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Open and save Map in Edit mode first.");
        SessionState.SetBool(Key, true); Resume(); EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        step = 0; began = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Poll; EditorApplication.update += Poll;
    }
    static void Expect(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    static EnemyWaveGroupGate Gate(MapUIFlow flow) => (EnemyWaveGroupGate)typeof(MapUIFlow)
        .GetField("waveGroupGate", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(flow);
    static void Resolve(EnemyAudioTestSpawner spawner)
    {
        var enemies = (List<GameObject>)typeof(EnemyAudioTestSpawner)
            .GetField("livingEnemies", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(spawner);
        foreach (var enemy in enemies.ToArray())
        {
            if (enemy == null) continue;
            var health = enemy.GetComponent<EnemyHealth>();
            health.ReceiveHit(health.MaxHealth, TowerInstrument.Bell, AudioSettings.dspTime);
            if (health.IsAlive) health.ReceiveHit(health.MaxHealth, TowerInstrument.Chord, AudioSettings.dspTime);
        }
    }
    static void Poll()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - began > 65) throw new Exception("Timed out at step " + step);
            if (!EditorApplication.isPlaying || Time.frameCount < 3) return;
            var flow = UnityEngine.Object.FindObjectOfType<MapUIFlow>();
            if (step == 0)
            {
                if (flow.dialogue.IsPlaying) { flow.dialogue.skipButton.onClick.Invoke(); return; }
                if (flow.CurrentPhase != MapUIFlow.Phase.Preparation) return;
                flow.SkipCurrentLevel(); step++; return;
            }
            if (flow.CurrentPhase == MapUIFlow.Phase.Transition) return;
            if (step == 1)
            {
                Expect(flow.CurrentLevel == 2 && flow.spawners.Sum(s => s.PlannedCount) == 20, "Expected two ten-enemy waves");
                flow.StartBattle(); startBeat = Gate(flow).GroupStartBeat; step++; return;
            }
            if (step == 2)
            {
                if (flow.clock.CurrentBeat < startBeat + 34) return;
                Expect(flow.CurrentPhase == MapUIFlow.Phase.Battle && flow.spawners.All(s => s.SpawnedCount == 5 && s.LivingCount == 5),
                    "Both roads must remain at five enemies after the fixed rest expires");
                Expect(Gate(flow).ActiveGroup == 0, "Live enemies must hold the shared gate");
                Resolve(flow.spawners[0]); step++; return;
            }
            if (step == 3)
            {
                if (flow.clock.CurrentBeat < startBeat + 36) return;
                Expect(flow.spawners[0].LivingCount == 0 && flow.spawners[1].LivingCount == 5 &&
                    flow.spawners.All(s => s.SpawnedCount == 5), "Clearing one road must not release either second wave");
                Resolve(flow.spawners[1]); clearBeat = flow.clock.CurrentBeat; step++; return;
            }
            if (step == 4)
            {
                if (flow.spawners.Sum(s => s.SpawnedCount) == 10) return;
                Expect(Gate(flow).GroupStartBeat >= Math.Ceiling(clearBeat) && flow.spawners.All(s => s.SpawnedCount == 6),
                    "Clearance must start both roads together without an overdue spawn burst");
                step++; return;
            }
            if (step == 5)
            {
                if (flow.spawners.Any(s => !s.SpawningComplete)) return;
                Expect(flow.spawners.All(s => s.SpawnedCount == 10 && s.LivingCount == 5) && !flow.LevelWon,
                    "Second wave has all ten enemies and must wait for their resolution");
                foreach (var spawner in flow.spawners) Resolve(spawner);
                step++; return;
            }
            if (step == 6)
            {
                if (flow.CurrentPhase != MapUIFlow.Phase.Result) return;
                Expect(flow.LevelWon, "Resolving both waves should win the level");
                flow.ReplayLevelButton.onClick.Invoke();
                Expect(flow.CurrentLevel == 2 && flow.CurrentPhase == MapUIFlow.Phase.Preparation &&
                    flow.spawners.All(s => s.SpawnedCount == 0 && s.LivingCount == 0), "Replay resets both waves");
                flow.StartBattle();
                Expect(Gate(flow).ActiveGroup == 0 && Gate(flow).GroupStartBeat >= flow.clock.CurrentBeat,
                    "Replay creates a fresh first-wave gate");
                Finish(true, "Real Map: expired rest waits for both roads; shared delayed start has no burst; exactly two waves; victory and replay passed.");
            }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Finish(bool passed, string message)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Poll;
        Directory.CreateDirectory("Artifacts/EnemyWave");
        string report = (passed ? "LEVEL_TWO_GATE_PLAY_PASS\n" : "LEVEL_TWO_GATE_PLAY_FAIL\n") + message;
        File.WriteAllText("Artifacts/EnemyWave/clear-gate-play.txt", report);
        if (passed) Debug.Log(report); else Debug.LogError(report);
        EditorApplication.ExitPlaymode();
    }
}

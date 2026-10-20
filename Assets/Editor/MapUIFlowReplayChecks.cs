using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TapTapGameJam.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Isolates result UI from placement tests; simulates settlement, then invokes authored buttons.
public static class MapUIFlowReplayChecks
{
    const string Key = "MapUIFlow.ReplayCheck";
    static int step;
    static double began;

    [MenuItem("Tools/UI Flow/Run Result Replay Play Checks")]
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorSceneManager.GetActiveScene().path != "Assets/Scenes/Map.unity" ||
            EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Open and save Map in Edit mode first.");
        SessionState.SetBool(Key, true);
        Resume();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        step = 0;
        began = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
    }

    static void Expect(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void Result(MapUIFlow flow, bool won) => typeof(MapUIFlow)
        .GetMethod("ShowResult", BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(flow, new object[] { won });

    static void Poll()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - began > 40) throw new Exception("Timed out at step " + step);
            if (!EditorApplication.isPlaying || Time.frameCount < 3) return;
            var flow = UnityEngine.Object.FindObjectOfType<MapUIFlow>();
            if (step == 0)
            {
                if (flow.dialogue.IsPlaying) { flow.dialogue.skipButton.onClick.Invoke(); return; }
                if (flow.CurrentPhase != MapUIFlow.Phase.Preparation) return;
                flow.SkipCurrentLevel(); step = 1; return;
            }
            if (flow.CurrentPhase == MapUIFlow.Phase.Transition) return;
            if (step == 1)
            {
                Expect(flow.CurrentLevel == 2, "Expected level two");
                flow.StartBattle(); step = 2; return;
            }
            if (step == 2)
            {
                if (flow.spawners.All(s => s.LivingCount == 0)) return;
                Result(flow, true);
                Expect(flow.ReplayLevelButton != null && flow.ReplayLevelButton.gameObject.activeInHierarchy &&
                    flow.NextLevelButton.gameObject.activeInHierarchy && !flow.RetryButton.gameObject.activeSelf,
                    "Victory must display replay and next level");
                var a = (RectTransform)flow.ReplayLevelButton.transform;
                var b = (RectTransform)flow.NextLevelButton.transform;
                Expect(Mathf.Abs(a.anchoredPosition.x - b.anchoredPosition.x) >= (a.rect.width + b.rect.width) / 2,
                    "Result buttons overlap");
                int capacity = flow.selection.MaxTowerCount;
                flow.ReplayLevelButton.onClick.Invoke();
                Expect(flow.CurrentLevel == 2 && flow.CurrentPhase == MapUIFlow.Phase.Preparation &&
                    !flow.LevelWon && !flow.resultRoot.activeSelf && flow.CurrentBaseHealth == 5 &&
                    flow.startBattleButton.gameObject.activeInHierarchy && flow.selection.InteractionAllowed &&
                    flow.spawners.All(s => s.Level == 2 && !s.enabled && s.LivingCount == 0 && s.SpawnedCount == 0),
                    "Replay must reset level two and return to preparation");
                Result(flow, true);
                Expect(flow.selection.MaxTowerCount == capacity, "Replaying must not grant capacity again");
                flow.ReplayLevelButton.onClick.Invoke();
                Result(flow, false);
                Expect(flow.RetryButton.gameObject.activeInHierarchy && !flow.ReplayLevelButton.gameObject.activeSelf &&
                    !flow.NextLevelButton.gameObject.activeSelf, "Defeat must hide victory buttons");
                flow.RetryButton.onClick.Invoke();
                flow.SkipCurrentLevel(); step = 3; return;
            }
            if (step == 3) { flow.SkipCurrentLevel(); step = 4; return; }
            Expect(flow.CurrentLevel == 4, "Expected final level");
            Result(flow, true);
            Expect(flow.ReplayLevelButton.gameObject.activeInHierarchy && !flow.NextLevelButton.gameObject.activeSelf,
                "Final victory must offer replay without next level");
            flow.ReplayLevelButton.onClick.Invoke();
            Expect(flow.CurrentLevel == 4 && flow.CurrentPhase == MapUIFlow.Phase.Preparation, "Final replay changed level");
            Finish(true, "Level 2 replay visibility, layout, click, health/wave reset, capacity, defeat and final-level replay passed.");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }

    static void Finish(bool passed, string message)
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Poll;
        Directory.CreateDirectory("Artifacts/UIFlow");
        string report = (passed ? "MAP_REPLAY_PLAY_PASS\n" : "MAP_REPLAY_PLAY_FAIL\n") + message;
        File.WriteAllText("Artifacts/UIFlow/replay-check.txt", report);
        if (passed) Debug.Log(report); else Debug.LogError(report);
        EditorApplication.ExitPlaymode();
    }
}

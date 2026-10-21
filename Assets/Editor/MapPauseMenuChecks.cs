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
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class MapPauseMenuChecks
{
    const string Key = "MapPauseMenu.Check";
    static int stage, lastFrame;
    static double began, pausedAt, beat, quitAt;
    static string failure;
    static Vector3 enemyPosition, cameraPosition;
    static EnemyPathMover enemy;
    static int spawned;
    [MenuItem("Tools/UI Flow/Run Escape Menu Play Checks")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Map.unity" || scene.isDirty) throw new InvalidOperationException("Open and save Map first.");
        SessionState.SetBool(Key, true); Resume(); EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]
    static void Resume()
    {
        EditorApplication.playModeStateChanged -= OnPlayState;
        EditorApplication.playModeStateChanged += OnPlayState;
        if (!SessionState.GetBool(Key, false)) return;
        stage = 0; lastFrame = -1; failure = null; began = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Poll; EditorApplication.update += Poll;
        Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
    }
    static void OnLog(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failure = message; }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Paused(MapUIFlow f)
    {
        Require(f.CurrentPhase == MapUIFlow.Phase.Paused && Time.timeScale == 0 && f.pauseMenuRoot.activeInHierarchy &&
            !f.selection.InteractionAllowed && !f.startBattleButton.gameObject.activeSelf, "pause must freeze and block the map");
        Require(f.clock.IsPaused, "pause must freeze music");
    }
    static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var point = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center));
        var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && hits[0].gameObject == button.gameObject, "menu button must be the top raycast target: " + button.name + "; hits=" + string.Join(",", hits.Select(h => h.gameObject.name)));
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
    static void Poll()
    {
        try
        {
            if (failure != null) throw new Exception(failure);
            if (EditorApplication.timeSinceStartup - began > 75) throw new Exception("Pause menu timed out at stage " + stage);
            if (!EditorApplication.isPlaying || Time.frameCount == lastFrame || Time.frameCount < 5) return;
            lastFrame = Time.frameCount;
            var f = UnityEngine.Object.FindObjectOfType<MapUIFlow>();
            if (f == null || !f.clock.IsRunning) return;
            switch (stage)
            {
                case 0:
                    Require(f.CurrentPhase == MapUIFlow.Phase.Dialogue && !f.pauseMenuRoot.activeSelf, "initial dialogue, menu hidden");
                    Require(f.pauseMenuRoot.GetComponentsInChildren<Button>(true).Length == 3, "exactly three menu choices");
                    Require(f.pauseMenuRoot.transform.parent.Find("BattleControlsRoot") == null, "old floating controls removed");
                    f.TogglePause(); Paused(f);
                    Require(f.dialogue.IsPlaying && !f.dialogue.panel.activeSelf, "dialogue retained behind menu");
                    f.TogglePause(); Require(f.dialogue.panel.activeSelf && f.CurrentPhase == MapUIFlow.Phase.Dialogue && !f.selection.InteractionAllowed, "dialogue resumes without build input");
                    f.dialogue.Skip(); stage++; break;
                case 1:
                    Require(f.CurrentPhase == MapUIFlow.Phase.Preparation && !f.pauseMenuRoot.activeSelf, "preparation hides menu");
                    f.TogglePause(); Paused(f); stage = 10; break;
                case 10:
                    Capture(f);
                    f.TogglePause(); Require(f.startBattleButton.gameObject.activeSelf && Time.timeScale == 1, "preparation resumes");
                    f.StartBattle(); stage = 2; break;
                case 2:
                    if (f.spawners.Sum(s => s.LivingCount) == 0) return;
                    f.TogglePause(); Paused(f);
                    enemy = UnityEngine.Object.FindObjectOfType<EnemyPathMover>(); enemyPosition = enemy.transform.position;
                    spawned = f.spawners.Sum(s => s.SpawnedCount); beat = f.clock.CurrentBeat; pausedAt = EditorApplication.timeSinceStartup;
                    stage++; break;
                case 3:
                    if (EditorApplication.timeSinceStartup - pausedAt < .4) return;
                    Require(enemy.transform.position == enemyPosition && Math.Abs(f.clock.CurrentBeat - beat) < .00001 && f.spawners.Sum(s => s.SpawnedCount) == spawned, "real time pause freezes enemy, clock and spawns");
                    Click(f.restartButton);
                    Require(Time.timeScale == 1 && f.CurrentPhase == MapUIFlow.Phase.Preparation && !f.pauseMenuRoot.activeSelf && f.spawners.All(s => !s.enabled && s.LivingCount == 0), "menu restart resets battle and closes pause");
                    stage++; break;
                case 4:
                    // Failure dialogue survives Esc without restoring the removed result overlay.
                    typeof(MapUIFlow).GetMethod("ShowResult", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(f, new object[] { false });
                    f.TogglePause(); Paused(f); Require(!f.resultRoot.activeSelf, "result hidden behind menu");
                    f.TogglePause(); Require(!f.resultRoot.activeSelf && f.CurrentPhase == MapUIFlow.Phase.Dialogue && f.dialogue.panel.activeSelf, "failure dialogue restored");
                    f.TogglePause(); f.RestartCurrentLevel(); f.TogglePause(); stage = 40; break;
                case 40:
                    Click(f.skipLevelButton);
                    Require(f.CurrentLevel == 2 && f.CurrentPhase == MapUIFlow.Phase.Transition && Time.timeScale == 1 && !f.pauseMenuRoot.activeSelf, "skip resumes time and enters transition");
                    f.TogglePause(); Paused(f); cameraPosition = f.selection.WorldCamera.transform.position;
                    Require(!f.skipLevelButton.interactable && !f.restartButton.interactable && f.quitButton.interactable, "transition menu only permits quit");
                    pausedAt = EditorApplication.timeSinceStartup; stage = 5; break;
                case 5:
                    if (EditorApplication.timeSinceStartup - pausedAt < .3) return;
                    Require(f.selection.WorldCamera.transform.position == cameraPosition, "camera transition freezes while paused");
                    f.TogglePause(); stage++; break;
                case 6:
                    if (f.CurrentPhase == MapUIFlow.Phase.Transition) return;
                    Require(f.CurrentPhase == MapUIFlow.Phase.Preparation && !f.pauseMenuRoot.activeSelf, "transition resumes to preparation");
                    f.TogglePause(); stage = 60; break;
                case 60:
                    Click(f.skipLevelButton); stage = 7; break;
                case 7:
                    if (f.CurrentPhase == MapUIFlow.Phase.Transition) return;
                    Require(f.CurrentLevel == 3, "third level reached");
                    f.TogglePause(); stage = 70; break;
                case 70:
                    Click(f.skipLevelButton); stage = 8; break;
                case 8:
                    if (f.CurrentPhase == MapUIFlow.Phase.Transition) return;
                    f.TogglePause(); Paused(f);
                    Require(f.CurrentLevel == 4 && !f.skipLevelButton.interactable, "last level skip disabled");
                    f.SkipCurrentLevel(); Require(f.CurrentLevel == 4, "direct skip cannot overflow");
                    stage = 80; break;
                case 80:
                    quitAt = EditorApplication.timeSinceStartup; stage = 9; Click(f.quitButton);
                    Require(!f.quitButton.interactable, "quit rejects duplicate clicks"); break;
            }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void OnPlayState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false) || state != PlayModeStateChange.ExitingPlayMode) return;
        Finish(stage == 9 && failure == null && Time.timeScale == 1 && EditorApplication.timeSinceStartup - quitAt >= .2,
            "Dialogue/preparation/battle/result/transition pause, clock/enemy freeze, three button raycasts, restart, level skips, final-level guard and quit tested.");
    }
    static void Capture(MapUIFlow f)
    {
        var canvas = f.pauseMenuRoot.GetComponentInParent<Canvas>(); var camera = f.selection.WorldCamera;
        var target = RenderTexture.GetTemporary(1280, 720, 24);
        int sortOrder = canvas.sortingOrder;
        var mode = canvas.renderMode; var worldCamera = canvas.worldCamera; float distance = canvas.planeDistance;
        var oldTarget = camera.targetTexture; var active = RenderTexture.active;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            canvas.sortingOrder = 32767; camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            Directory.CreateDirectory("Artifacts/PauseMenu"); File.WriteAllBytes("Artifacts/PauseMenu/preview.png", image.EncodeToPNG());
        }
        finally
        {
            canvas.sortingOrder = sortOrder; canvas.renderMode = mode; canvas.worldCamera = worldCamera; canvas.planeDistance = distance;
            camera.targetTexture = oldTarget; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(image); Canvas.ForceUpdateCanvases();
        }
    }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Poll; Application.logMessageReceived -= OnLog;
        Directory.CreateDirectory("Artifacts/PauseMenu"); File.WriteAllText("Artifacts/PauseMenu/play-check.txt", (success ? "PAUSE_MENU_PLAY_PASS\n" : "PAUSE_MENU_PLAY_FAIL\n") + message);
        if (success) Debug.Log("PAUSE_MENU_PLAY_PASS"); else Debug.LogError("PAUSE_MENU_PLAY_FAIL: " + message);
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }
}

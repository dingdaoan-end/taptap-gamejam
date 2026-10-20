using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using TapTapGameJam.Combat;
using TapTapGameJam.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Exercises the authored Map in Play mode; never saves runtime state to the scene.
public static class MapUIFlowChecks
{
    const string Key = "MapUIFlow.Check";
    static int step, checks, lastFrame;
    static double began;
    static int clockRevision;
    static string failure;
    static double pausedBeat, pausedAt;
    static EnemyPathMover pausedEnemy;
    static Vector3 pausedPosition;
    static int pausedCount;
    static Vector3 transitionStart;
    static bool sawCameraMove;
    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        step = checks = 0; lastFrame = -1; failure = null;
        began = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Poll; EditorApplication.update += Poll;
        Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
    }
    [MenuItem("Tools/UI Flow/Run Map UI Play Checks")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        if (EditorSceneManager.GetActiveScene().path != "Assets/Scenes/Map.unity") throw new InvalidOperationException("Open Map first.");
        if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save Map before testing.");
        SessionState.SetBool(Key, true); Resume(); EditorApplication.EnterPlaymode();
    }
    static void OnLog(string text, string trace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) failure = text;
    }
    static void Expect(bool condition, string label)
    {
        if (!condition) throw new Exception(label); checks++;
    }
    static void Poll()
    {
        if (EditorApplication.timeSinceStartup - began > 60) { Finish(false, "Timed out at step " + step); return; }
        if (!EditorApplication.isPlaying || Time.frameCount < 3 || Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            if (failure != null) throw new Exception(failure);
            var flow = UnityEngine.Object.FindObjectOfType<MapUIFlow>();
            if (flow == null) throw new Exception("Missing flow");
            var player = flow.dialogue;
            var canvas = flow.resultRoot.transform.parent;
            var bellButton = canvas.Find("BuildMenu/Button_1").gameObject;
            var dudukButton = canvas.Find("BuildMenu/Button_2").gameObject;
            var chordButton = canvas.Find("BuildMenu/Button_3").gameObject;
            switch (step)
            {
                case 0:
                    Expect(flow.CurrentPhase == MapUIFlow.Phase.Dialogue && player.IsPlaying, "opening dialogue");
                    Expect(!flow.selection.InteractionAllowed && !flow.selection.SelectSite(default), "dialogue rejects world selection");
                    Expect(!flow.selection.TryBuildSelected(flow.bellPrefab), "dialogue rejects direct build");
                    Expect((flow.clock.IsRunning || flow.clock.IsPreparing) && flow.spawners.All(s => !s.enabled && s.LivingCount == 0), "drum starts during dialogue without spawning enemies");
                    Expect(!flow.startBattleButton.gameObject.activeSelf && !flow.resultRoot.activeSelf && !flow.transitionRoot.activeSelf, "modal visibility");
                    Expect(canvas.GetComponentsInChildren<TMP_Text>(true).All(t => !t.raycastTarget), "decorative text passes raycasts");
                    Expect(flow.levelDialogues.Length == 4 && flow.levelDialogues.All(l => l.victory.IsValid && l.defeat.IsValid), "all level placeholder resources");
                    Expect(player.content.font.HasCharacters(player.content.text), "Chinese glyphs resolve");
                    var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Dialogue/DialogueChinese.asset");
                    Expect(canvas.Find("BuildMenu").GetComponentsInChildren<TMP_Text>(true).All(t => t.font == font && t.fontSharedMaterial == font.material), "all four build menu fonts and materials unified");
                    Expect(!flow.pauseMenuRoot.activeSelf, "battle controls hidden during dialogue");
                    player.nextButton.onClick.Invoke(); player.nextButton.onClick.Invoke();
                    Expect(player.LineIndex == 1 && player.IsPlaying, "same-frame double click advances once");
                    step++; break;
                case 1:
                    Expect(player.skipButton != null, "authored SkipButton is bound");
                    player.skipButton.onClick.Invoke();
                    player.skipButton.onClick.Invoke();
                    Expect(!player.IsPlaying && flow.CurrentPhase == MapUIFlow.Phase.Preparation, "dialogue enters preparation");
                    Expect(!flow.selection.SelectSite(default), "completion click does not select the map in same frame");
                    player.nextButton.onClick.Invoke();
                    Expect(flow.CurrentPhase == MapUIFlow.Phase.Preparation, "duplicate completion harmless");
                    step++; break;
                case 2:
                    Expect(flow.selection.InteractionAllowed && flow.startBattleButton.gameObject.activeSelf, "build controls available");
                    if (!flow.clock.IsRunning) return;
                    Expect(!flow.clock.IsPaused && flow.spawners.All(s => !s.enabled), "drum continues during preparation without enemies");
                    Expect(!flow.pauseMenuRoot.activeSelf,
                        "preparation keeps the Esc menu hidden");
                    flow.TogglePause();
                    Expect(flow.CurrentPhase == MapUIFlow.Phase.Paused && Time.timeScale == 0 && flow.clock.IsPaused &&
                        !flow.startBattleButton.gameObject.activeSelf, "preparation can pause without allowing battle start");
                    flow.startBattleButton.onClick.Invoke();
                    Expect(flow.CurrentPhase == MapUIFlow.Phase.Paused && flow.spawners.All(s => !s.enabled),
                        "paused preparation rejects direct start calls");
                    flow.TogglePause();
                    Expect(flow.CurrentPhase == MapUIFlow.Phase.Preparation && Time.timeScale == 1 && !flow.clock.IsPaused &&
                        flow.startBattleButton.gameObject.activeSelf && flow.spawners.All(s => !s.enabled),
                        "resume returns to preparation without spawning enemies");
                    Expect(flow.selection.IsUnlocked(flow.bellPrefab) && !flow.selection.IsUnlocked(flow.chordPrefab) && !flow.selection.IsUnlocked(flow.dudukPrefab), "initial unlocks");
                    var validator = flow.selection.GetComponent<BuildPlacementValidator>();
                    var camera = flow.selection.WorldCamera;
                    bool found = false;
                    for (int y = 2; y < 18 && !found; y++) for (int x = 2; x < 18 && !found; x++)
                    {
                        var ray = camera.ViewportPointToRay(new Vector3(x / 20f, y / 20f, 0));
                        if (!new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float distance)) continue;
                        if (validator.TryFootprint(ray.GetPoint(distance), out var site) && validator.Validate(site, camera).Allowed)
                            found = flow.selection.SelectSite(site);
                    }
                    Expect(found, "real valid build site can be selected");
                    Expect(bellButton.activeSelf && !chordButton.activeSelf && !dudukButton.activeSelf, "only bell button visible");
                    Expect(!flow.selection.TryBuildSelected(flow.chordPrefab), "hidden chord cannot be built by direct call");
                    Expect(flow.selection.TryBuildSelected(flow.bellPrefab), "build a real bell tower for restart cleanup");
                    flow.selection.SetUnlockedPrefabs(new[] { flow.bellPrefab, flow.chordPrefab });
                    Expect(flow.selection.IsUnlocked(flow.chordPrefab) && !flow.selection.IsUnlocked(flow.dudukPrefab), "unlock preserves correct prefab mapping");
                    flow.selection.SetUnlockedPrefabs(new[] { flow.bellPrefab });
                    flow.selection.SetInteractionAllowed(false);
                    Expect(!canvas.Find("BuildMenu").gameObject.activeSelf, "lock clears existing selection and menu");
                    flow.selection.SetInteractionAllowed(true);
                    clockRevision = flow.clock.Revision;
                    flow.startBattleButton.onClick.Invoke();
                    Expect(flow.clock.Revision == clockRevision, "starting battle preserves drum playback and musical phase");
                    flow.startBattleButton.onClick.Invoke();
                    Expect(flow.clock.Revision == clockRevision, "duplicate start does not restart clock");
                    step++; break;
                case 3:
                    if (!flow.clock.IsRunning || flow.spawners.All(s => s.LivingCount == 0)) return;
                    Expect(flow.CurrentPhase == MapUIFlow.Phase.Battle && flow.selection.InteractionAllowed, "battle keeps building enabled");
                    Expect(!flow.startBattleButton.gameObject.activeSelf, "start hidden in battle");
                    Expect(flow.spawners.Length == 1 && flow.spawners[0].LivingCount > 0, "only authored first entry starts spawning");
                    Expect(UnityEngine.Object.FindObjectsOfType<EnemyAudioTestSpawner>(true).Count(s => s.enabled) == 1, "other routes remain disabled");
                    Expect(!flow.pauseMenuRoot.activeInHierarchy, "battle keeps the Esc menu hidden");
                    flow.TogglePause();
                    Expect(flow.CurrentPhase == MapUIFlow.Phase.Paused && flow.clock.IsPaused && Time.timeScale == 0, "pause freezes DSP and game time");
                    Expect(!flow.selection.InteractionAllowed && !flow.selection.TryBuildSelected(flow.bellPrefab), "pause blocks map actions");
                    Expect(flow.pauseMenuRoot.activeSelf && flow.restartButton.interactable && flow.quitButton.interactable, "pause exposes menu actions");
                    pausedBeat = flow.clock.CurrentBeat; pausedAt = EditorApplication.timeSinceStartup;
                    pausedCount = flow.spawners[0].LivingCount;
                    pausedEnemy = UnityEngine.Object.FindObjectOfType<EnemyPathMover>(); pausedPosition = pausedEnemy.transform.position;
                    // Manual pause must survive focus being restored by the OS.
                    flow.clock.SendMessage("OnApplicationFocus", true);
                    step++; break;
                case 4:
                    if (EditorApplication.timeSinceStartup - pausedAt < .7) return;
                    Expect(Math.Abs(flow.clock.CurrentBeat - pausedBeat) < .00001 && pausedEnemy.transform.position == pausedPosition && flow.spawners[0].LivingCount == pausedCount, "pause retains enemies and freezes beat and movement across real time");
                    flow.TogglePause();
                    Expect(flow.CurrentPhase == MapUIFlow.Phase.Battle && !flow.clock.IsPaused && Time.timeScale == 1 && flow.selection.InteractionAllowed, "resume restores clock and interaction");
                    pausedAt = EditorApplication.timeSinceStartup;
                    step++; break;
                case 5:
                    if (EditorApplication.timeSinceStartup - pausedAt < .7) return;
                    Expect(flow.clock.CurrentBeat > pausedBeat && pausedEnemy.transform.position != pausedPosition, "resumed beat and enemy movement advance");
                    clockRevision = flow.clock.Revision;
                    flow.restartButton.onClick.Invoke();
                    Expect(flow.CurrentPhase == MapUIFlow.Phase.Preparation && flow.clock.IsRunning && flow.clock.Revision == clockRevision && flow.spawners.All(s => !s.enabled && s.LivingCount == 0), "restart clears enemies while retaining drum transport");
                    Expect(flow.startBattleButton.gameObject.activeSelf && !flow.pauseMenuRoot.activeSelf && flow.selection.InteractionAllowed && !player.IsPlaying, "restart returns directly to preparation with usable controls");
                    clockRevision = flow.clock.Revision; flow.restartButton.onClick.Invoke();
                    Expect(flow.clock.Revision == clockRevision, "duplicate restart harmless");
                    step++; break;
                case 6:
                    Expect(!UnityEngine.Object.FindObjectsOfType<BuildSlot>(true).Any(s => s.GeneratedPlacement) && !UnityEngine.Object.FindObjectsOfType<TapTapGameJam.Music.BellTowerMusicController>(true).Any(), "restart removes generated slots and their separately rooted towers");
                    flow.startBattleButton.onClick.Invoke();
                    flow.restartButton.onClick.Invoke();
                    Expect(flow.clock.IsRunning && !flow.clock.IsPaused, "immediate battle restart preserves drum playback");
                    flow.startBattleButton.onClick.Invoke(); step++; break;
                case 7:
                    if (!flow.clock.IsRunning) return;
                    flow.TogglePause(); flow.restartButton.onClick.Invoke();
                    Expect(Time.timeScale == 1 && flow.CurrentPhase == MapUIFlow.Phase.Preparation && !flow.clock.IsPaused && flow.clock.IsRunning, "restart from pause restores time, drum and preparation");
                    flow.startBattleButton.onClick.Invoke(); step++; break;
                case 8:
                    if (!flow.clock.IsRunning) return;
                    Expect(flow.CurrentBaseHealth == 5 && flow.MaxBaseHealth == 5, "base starts with five lives");
                    var source = flow.spawners[0];
                    var spawn = typeof(EnemyAudioTestSpawner).GetMethod("Spawn", BindingFlags.Instance | BindingFlags.NonPublic);
                    var living = (List<GameObject>)typeof(EnemyAudioTestSpawner).GetField("livingEnemies", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(source);
                    var move = typeof(EnemyPathMover).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                    for (int remaining = 4; remaining >= 0; remaining--)
                    {
                        // Spawn far enough along the real route to arrive on the next
                        // movement update, exercising its deduplicated arrival event.
                        spawn.Invoke(source, new object[] { flow.clock.CurrentBeat - 10000 });
                        var arriving = living[living.Count - 1].GetComponent<EnemyPathMover>();
                        move.Invoke(arriving, null);
                        source.NotifyArrival(arriving);
                        Expect(flow.CurrentBaseHealth == remaining, "one life lost per enemy, duplicate arrival ignored");
                    }
                    Expect(flow.CurrentPhase == MapUIFlow.Phase.Result && flow.resultRoot.activeInHierarchy &&
                        flow.RetryButton.gameObject.activeInHierarchy && !flow.selection.InteractionAllowed &&
                        flow.spawners.All(s => !s.enabled && s.LivingCount == 0), "defeat shows retry and stops the battle");
                    flow.RetryButton.onClick.Invoke();
                    Expect(flow.CurrentBaseHealth == 5 && flow.CurrentPhase == MapUIFlow.Phase.Preparation &&
                        !flow.resultRoot.activeSelf && flow.spawners.All(s => s.SpawnedCount == 0), "retry resets health and wave for preparation");
                    flow.startBattleButton.onClick.Invoke();
                    var checkVictory = typeof(MapUIFlow).GetMethod("CheckVictory", BindingFlags.Instance | BindingFlags.NonPublic);
                    for (int i = 0; i < 10; i++)
                    {
                        spawn.Invoke(source, new object[] { flow.clock.CurrentBeat });
                        var target = living[living.Count - 1].GetComponent<EnemyHealth>();
                        target.ReceiveHit(target.MaxHealth, TowerInstrument.Bell, AudioSettings.dspTime);
                        checkVictory.Invoke(flow, null);
                        Expect(flow.LevelWon == (i == 9), "only the tenth resolved enemy completes the wave");
                    }
                    Expect(flow.CurrentPhase == MapUIFlow.Phase.Result && flow.resultRoot.activeSelf &&
                        flow.NextLevelButton.gameObject.activeSelf && !flow.RetryButton.gameObject.activeSelf,
                        "victory offers next level instead of retry");
                    flow.NextLevelButton.onClick.Invoke();
                    flow.NextLevelButton.onClick.Invoke();
                    Expect(flow.CurrentPhase == MapUIFlow.Phase.Dialogue && flow.CurrentLevel == 1 && player.IsPlaying,
                        "next level waits for one victory dialogue completion");
                    step++; break;
                case 9:
                    transitionStart = flow.selection.WorldCamera.transform.position;
                    sawCameraMove = false;
                    player.skipButton.onClick.Invoke();
                    player.skipButton.onClick.Invoke();
                    Expect(flow.CurrentLevel == 2 && flow.CurrentPhase == MapUIFlow.Phase.Transition && !player.IsPlaying &&
                        flow.spawners.All(s => s.Level == 2 && !s.enabled) && flow.selection.IsUnlocked(flow.chordPrefab),
                        "normal victory starts one camera transition after dialogue");
                    Expect(flow.selection.WorldCamera.transform.position == transitionStart, "normal transition does not teleport the camera");
                    ExpectTransitionLocked(flow);
                    step++; break;
                case 10:
                    if (!CameraTransitionFinished(flow)) return;
                    transitionStart = flow.selection.WorldCamera.transform.position;
                    sawCameraMove = false;
                    flow.TogglePause();
                    flow.skipLevelButton.onClick.Invoke();
                    flow.skipLevelButton.onClick.Invoke();
                    Expect(flow.CurrentLevel == 3 && flow.CurrentPhase == MapUIFlow.Phase.Transition && Time.timeScale == 1 &&
                        !flow.resultRoot.activeSelf && !player.IsPlaying && flow.selection.MaxTowerCount == 5 &&
                        flow.spawners.All(s => s.Level == 3 && !s.enabled),
                        "skip from paused preparation grants capacity and starts the same camera transition");
                    Expect(flow.selection.WorldCamera.transform.position == transitionStart, "skip does not teleport the camera");
                    ExpectTransitionLocked(flow);
                    step++; break;
                case 11:
                    if (!CameraTransitionFinished(flow)) return;
                    transitionStart = flow.selection.WorldCamera.transform.position;
                    sawCameraMove = false;
                    flow.startBattleButton.onClick.Invoke();
                    flow.skipLevelButton.onClick.Invoke();
                    Expect(flow.CurrentLevel == 4 && flow.CurrentPhase == MapUIFlow.Phase.Transition &&
                        flow.selection.MaxTowerCount == 6 && !flow.skipLevelButton.interactable,
                        "battle skip starts the final level transition and disables further skips");
                    ExpectTransitionLocked(flow);
                    step++; break;
                case 12:
                    if (!CameraTransitionFinished(flow)) return;
                    flow.skipLevelButton.onClick.Invoke();
                    Expect(flow.CurrentLevel == 4, "final level rejects direct skip calls");
                    flow.startBattleButton.onClick.Invoke();
                    flow.TogglePause();
                    flow.enabled = false;
                    Expect(Time.timeScale == 1, "shutdown from pause restores global time scale");
                    Expect(!flow.clock.IsRunning && !flow.selection.InteractionAllowed && flow.spawners.All(s => !s.enabled && s.LivingCount == 0), "shutdown cancels clock, enemies and map input");
                    Finish(true, "Real Map Play mode: " + checks + " checks passed.");
                    break;
            }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }

    static void ExpectTransitionLocked(MapUIFlow flow)
    {
        int level = flow.CurrentLevel;
        Expect(!flow.selection.InteractionAllowed && !flow.startBattleButton.gameObject.activeSelf &&
            !flow.pauseMenuRoot.activeSelf && !flow.resultRoot.activeSelf &&
            flow.spawners.All(s => !s.enabled && s.LivingCount == 0), "camera travel locks building, spawning and battle controls");
        flow.StartBattle(); flow.TogglePause(); flow.RestartCurrentLevel(); flow.SkipCurrentLevel(); flow.NextLevel(); flow.TogglePause();
        Expect(flow.CurrentPhase == MapUIFlow.Phase.Transition && flow.CurrentLevel == level,
            "direct and duplicate actions cannot interrupt camera travel");
    }

    static bool CameraTransitionFinished(MapUIFlow flow)
    {
        if (flow.CurrentPhase == MapUIFlow.Phase.Transition)
        {
            ExpectTransitionLocked(flow);
            sawCameraMove |= (flow.selection.WorldCamera.transform.position - transitionStart).sqrMagnitude > 0.0001f;
            return false;
        }
        Expect(sawCameraMove, "camera moves across frames before preparation");
        Expect(flow.CurrentPhase == MapUIFlow.Phase.Preparation && flow.selection.InteractionAllowed &&
            flow.startBattleButton.gameObject.activeSelf && !flow.pauseMenuRoot.activeSelf &&
            flow.spawners.All(s => !s.enabled), "camera arrival enables preparation without starting enemies");
        return true;
    }
    static void Finish(bool passed, string message)
    {
        EditorApplication.update -= Poll; Application.logMessageReceived -= OnLog;
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Artifacts/UIFlow");
        File.WriteAllText("Artifacts/UIFlow/play-check.txt", (passed ? "MAP_UI_PLAY_PASS\n" : "MAP_UI_PLAY_FAIL\n") + message);
        if (passed) Debug.Log("MAP_UI_PLAY_PASS " + message); else Debug.LogError("MAP_UI_PLAY_FAIL " + message);
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }
}

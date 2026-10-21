using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Text;
using TapTapGameJam.Combat;
using TapTapGameJam.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Exercises defeat choices and direct victory progression in the real Map scene.
public static class MapUIFlowReplayChecks
{
    const string Key = "MapUIFlow.ReplayCheck";
    static int step;
    static double began;
    static string failure;
    static readonly StringBuilder evidence = new StringBuilder();
    static readonly List<int> blockedLevels = new List<int>();
    static bool levelTwoOnly;
    static bool rewardsOnly;

    public static void RunBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Map.unity");
        Run();
    }

    [MenuItem("Tools/UI Flow/Run Outcome Dialogue Play Checks")]
    static void Run() => StartCheck(false);

    [MenuItem("Tools/UI Flow/Run Level 2 Retry and Progression Checks")]
    static void RunLevelTwo() => StartCheck(true);

    [MenuItem("Tools/UI Flow/Run Reward Panel Play Checks")]
    static void RunRewards() => StartCheck(false, true);

    static void StartCheck(bool checkLevelTwoOnly, bool checkRewardsOnly = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorSceneManager.GetActiveScene().path != "Assets/Scenes/Map.unity" ||
            EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Open and save Map in Edit mode first.");
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + ".LevelTwoOnly", checkLevelTwoOnly);
        SessionState.SetBool(Key + ".RewardsOnly", checkRewardsOnly);
        Resume();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        levelTwoOnly = SessionState.GetBool(Key + ".LevelTwoOnly", false);
        rewardsOnly = SessionState.GetBool(Key + ".RewardsOnly", false);
        step = 0;
        lastFrame = -1;
        failure = null;
        evidence.Clear();
        blockedLevels.Clear();
        began = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
    }

    static void Expect(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failure = message;
    }

    static List<GameObject> Enemies(EnemyAudioTestSpawner spawner) => (List<GameObject>)typeof(EnemyAudioTestSpawner)
        .GetField("livingEnemies", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(spawner);

    static void Spawn(EnemyAudioTestSpawner spawner, double beat) => typeof(EnemyAudioTestSpawner)
        .GetMethod("Spawn", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(spawner, new object[] { beat });

    static void Defeat(MapUIFlow flow)
    {
        var spawner = flow.spawners[0];
        int lives = flow.CurrentBaseHealth;
        for (int i = 0; i < lives; i++)
        {
            // Accelerate real arrivals while retaining mover -> spawner -> base-health events.
            Spawn(spawner, flow.clock.CurrentBeat - 10000);
            var enemy = Enemies(spawner).Last().GetComponent<EnemyPathMover>();
            typeof(EnemyPathMover).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(enemy, null);
            spawner.NotifyArrival(enemy);
            Expect(flow.CurrentBaseHealth == lives - i - 1, "Each arrival loses exactly one life");
        }
    }

    static void Win(MapUIFlow flow)
    {
        // Accelerate births and damage; the production wave-completion predicate decides victory.
        foreach (var spawner in flow.spawners)
        {
            Expect(spawner.PlannedCount > 0, "Finite wave exists");
            var wave = new SerializedObject(spawner).FindProperty("waveSpawns");
            while (spawner.SpawnedCount < spawner.PlannedCount)
            {
                if (wave.arraySize == 0) Spawn(spawner, flow.clock.CurrentBeat);
                else
                {
                    var prefab = wave.GetArrayElementAtIndex(spawner.SpawnedCount).FindPropertyRelative("prefab").objectReferenceValue as GameObject;
                    typeof(EnemyAudioTestSpawner).GetMethod("SpawnPrefab", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(spawner, new object[] { flow.clock.CurrentBeat, prefab });
                }
            }
            foreach (var enemy in Enemies(spawner).ToArray())
            {
                if (enemy == null) continue;
                var health = enemy.GetComponent<EnemyHealth>();
                Expect(health != null, "Enemy health exists");
                double hitTime = AudioSettings.dspTime;
                foreach (TowerInstrument instrument in Enum.GetValues(typeof(TowerInstrument)))
                    if (health.IsAlive) health.ReceiveHit(health.MaxHealth, instrument, hitTime);
                Expect(!health.IsAlive, "Accelerated damage resolved enemy");
            }
        }
        typeof(MapUIFlow).GetMethod("CheckVictory", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(flow, null);
    }

    static void Click(Button button)
    {
        Expect(button != null && button.isActiveAndEnabled && button.IsInteractable(), "Button is clickable");
        // Check the real Canvas raycast so an invisible overlay cannot mask a valid listener.
        Canvas.ForceUpdateCanvases();
        var canvas = button.GetComponentInParent<Canvas>();
        var rect = (RectTransform)button.transform;
        var point = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            rect.TransformPoint(rect.rect.center));
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
        Expect(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, "Button is not covered: " + button.name);
        button.onClick.Invoke();
    }

    static void CheckLine(DialoguePlayer player)
    {
        Expect(player.IsPlaying && player.panel.activeInHierarchy, "Dialogue visible");
        var line = player.Current.lines[player.LineIndex];
        Expect(player.content.text == line.text && player.speakerName.text == line.character.displayName, "Speaker and text match");
        var portraitPath = line.character.characterId == "player"
            ? "Assets/Sprites/Character/默认表情.PNG" : "Assets/Sprites/Character/导师立绘.png";
        Expect(player.portrait.isActiveAndEnabled && player.portrait.sprite == AssetDatabase.LoadAssetAtPath<Sprite>(portraitPath), "Correct character portrait");
        Expect(player.panel.GetComponentsInChildren<Image>().Count(i => i.sprite == player.portrait.sprite && i.enabled) == 1,
            "Exactly one active speaker portrait");
        string expected = !string.IsNullOrEmpty(line.nextButtonLabel) ? line.nextButtonLabel
            : player.LineIndex + 1 < player.Current.lines.Length ? "继续"
            : player.Current.dialogueId == "Common_Fail" ? "重新开始" : "结束对话";
        Expect(player.nextLabel.text == expected, "Correct next-button label");
        player.content.ForceMeshUpdate();
        Expect(!player.content.isTextOverflowing, "Dialogue text fits the box");
        if (rewardsOnly)
        {
            string tower = player.Current.dialogueId == "L1_Intro" && player.LineIndex == 4 ? "Bell"
                : player.Current.dialogueId == "L1_Clear" ? "Chords"
                : player.Current.dialogueId == "L2_Clear" ? "Duduk"
                : player.Current.dialogueId == "L3_Clear" ? "Bell" : null;
            Expect(player.rewardPanel != null && player.rewardTower != null, "Reward UI references assigned");
            if (tower == null) CheckRewardHidden(player);
            else
            {
                Expect(player.rewardPanel.activeInHierarchy && player.rewardTower.isActiveAndEnabled &&
                    player.rewardTower.sprite == AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Building/Tower_" + tower + ".png"),
                    "Correct reward tower: " + tower);
                Expect(player.rewardTower.preserveAspect &&
                    player.rewardPanel.GetComponentsInChildren<Image>().All(i => !i.raycastTarget), "Reward keeps aspect and does not intercept clicks");
                evidence.AppendLine("REWARD PASS: " + player.Current.dialogueId + " line " + (player.LineIndex + 1) + " / " + tower);
            }
        }
        evidence.AppendLine(player.Current.dialogueId + " line " + (player.LineIndex + 1) + ": " + line.character.displayName + " / " + expected + " / portrait PASS");
    }

    static void CheckRewardHidden(DialoguePlayer player)
    {
        if (!rewardsOnly) return;
        Expect(!player.rewardPanel.activeSelf && !player.rewardTower.enabled && player.rewardTower.sprite == null,
            "Reward hidden and cleared outside reward lines");
    }

    static int lastFrame = -1;
    static int testedLevel = 1;
    static bool simulateBlockedOutcome;

    static bool ValidateBindings(MapUIFlow flow)
    {
        bool valid = true;
        foreach (var spawner in flow.spawners)
        {
            var serialized = new SerializedObject(spawner);
            var prefab = serialized.FindProperty("enemyPrefab").objectReferenceValue as GameObject;
            if (prefab == null)
            {
                evidence.AppendLine("BLOCKED LEVEL " + testedLevel + ": " + spawner.name + " default enemyPrefab missing");
                valid = false;
            }
            else
            {
                var mover = prefab.GetComponent<EnemyPathMover>();
                if (mover != null && !mover.DamagesBaseOnArrival)
                {
                    evidence.AppendLine("BLOCKED LEVEL " + testedLevel + ": " + spawner.name + " uses " + prefab.name + " with base damage disabled");
                    valid = false;
                }
            }
            if (spawner.PlannedCount <= 0)
            {
                evidence.AppendLine("BLOCKED LEVEL " + testedLevel + ": " + spawner.name + " has no finite wave (PlannedCount=0); victory predicate cannot complete");
                valid = false;
            }
            var wave = serialized.FindProperty("waveSpawns");
            if (levelTwoOnly && testedLevel == 2)
            {
                var yellow = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy_Yellow.prefab");
                var blue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy_Blue.prefab");
                Expect(yellow != null && blue != null && wave.arraySize == 10, "Level two has ten valid authored spawns per road");
                int yellowCount = 0;
                for (int i = 0; i < wave.arraySize; i++)
                {
                    var enemyPrefab = wave.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue;
                    Expect(enemyPrefab == yellow || enemyPrefab == blue, "Level two only references normal yellow and blue prefabs");
                    if (enemyPrefab == yellow) yellowCount++;
                }
                Expect(yellowCount == 2, "Each road retains two yellow spawns");
                evidence.AppendLine("LEVEL 2 REFERENCES PASS: " + spawner.name + ", 2 normal yellow + 8 blue; yellow HP=" + yellow.GetComponent<EnemyHealth>().MaxHealth);
            }
            for (int i = 0; i < wave.arraySize; i++)
            {
                var entry = wave.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("prefab").objectReferenceValue != null) continue;
                evidence.AppendLine("BLOCKED LEVEL " + testedLevel + ": " + spawner.name + " beat " +
                    entry.FindPropertyRelative("beat").intValue + " prefab missing");
                valid = false;
            }
        }
        return valid;
    }

    static void Poll()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - began > 120) throw new Exception("Timed out at step " + step);
            if (!EditorApplication.isPlaying || Time.frameCount < 3 || Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            if (failure != null) throw new Exception(failure);
            var flow = UnityEngine.Object.FindObjectOfType<MapUIFlow>();
            var player = flow.dialogue;
            Expect(!flow.resultRoot.activeSelf, "Settlement panel must stay hidden");
            switch (step)
            {
                case 0:
                    testedLevel = 1;
                    if (player.IsPlaying)
                    {
                        CheckLine(player);
                        int index = player.LineIndex;
                        Click(player.nextButton);
                        player.nextButton.onClick.Invoke();
                        Expect(!player.IsPlaying || player.LineIndex == index + 1, "Double click advances once");
                        return;
                    }
                    Expect(flow.CurrentPhase == MapUIFlow.Phase.Preparation && !player.portrait.enabled && player.portrait.sprite == null,
                        "Opening ends in preparation with portrait cleared");
                    evidence.AppendLine("OPENING PASS: 7 displayed lines, response button, same-frame double-click protection, portrait cleanup.");
                    CheckRewardHidden(player);
                    step = 1; return;
                case 1:
                    if (flow.CurrentPhase == MapUIFlow.Phase.Transition) return;
                    Expect(flow.CurrentLevel == testedLevel, "Level advancement");
                    simulateBlockedOutcome = rewardsOnly || !ValidateBindings(flow);
                    if (simulateBlockedOutcome)
                    {
                        if (!rewardsOnly) blockedLevels.Add(testedLevel);
                        // Check the blocked level's UI independently without repairing or replacing authored references.
                        typeof(MapUIFlow).GetMethod("ShowResult", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(flow, new object[] { false });
                        step = 2;
                        return;
                    }
                    Click(flow.startBattleButton);
                    step = 10; return;
                case 10:
                    if (!flow.clock.IsRunning || flow.clock.IsPaused) return;
                    Defeat(flow);
                    Expect(player.IsPlaying && flow.CurrentPhase == MapUIFlow.Phase.Dialogue, "Failure immediately opens dialogue");
                    step = 2; return;
                case 2:
                    CheckLine(player);
                    if (player.LineIndex == 0) { Click(player.nextButton); return; }
                    Click(player.skipButton);
                    Expect(player.nextLabel.text == "重新开始" && player.LineIndex == player.Current.lines.Length - 1,
                        "Skipping failure reaches restart choice");
                    step = 3; return;
                case 3:
                    CheckLine(player);
                    Expect(player.skipButton.GetComponentInChildren<TMP_Text>().text == "否", "Decline label");
                    Click(player.skipButton);
                    Expect(flow.CurrentPhase == MapUIFlow.Phase.Paused && player.IsPlaying && !player.panel.activeSelf,
                        "No opens pause menu and preserves choice");
                    flow.TogglePause();
                    Expect(player.panel.activeSelf && player.nextLabel.text == "重新开始", "Resume restores retry choice");
                    step = 4; return;
                case 4:
                    CheckLine(player);
                    Click(player.nextButton);
                    Expect(flow.CurrentPhase == MapUIFlow.Phase.Preparation && flow.CurrentBaseHealth == 5 &&
                        flow.CurrentLevel == testedLevel && !player.IsPlaying && flow.spawners.All(s => !s.enabled && s.SpawnedCount == 0),
                        "Restart clears the current level and restores health");
                    Expect(!player.panel.activeSelf && !player.portrait.enabled && player.portrait.sprite == null, "Retry clears portrait");
                    CheckRewardHidden(player);
                    evidence.AppendLine("LEVEL " + testedLevel + " DEFEAT/RETRY UI PASS: " +
                        (simulateBlockedOutcome ? "SIMULATED outcome (UI-only check)" : "five real arrival events") +
                        ", no settlement, decline/pause/resume, restart, full health.");
                    step = 5; return;
                case 5:
                    if (simulateBlockedOutcome)
                    {
                        typeof(MapUIFlow).GetMethod("ShowResult", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(flow, new object[] { true });
                        step = 6;
                        return;
                    }
                    Click(flow.startBattleButton);
                    step = 11; return;
                case 11:
                    if (!flow.clock.IsRunning || flow.clock.IsPaused) return;
                    Win(flow);
                    Expect(player.IsPlaying && player.Current == flow.levelDialogues[testedLevel - 1].victory &&
                        flow.CurrentPhase == MapUIFlow.Phase.Dialogue, "Victory directly plays matching dialogue");
                    step = 6; return;
                case 6:
                    Expect(player.Current == flow.levelDialogues[testedLevel - 1].victory, "Correct level victory sequence");
                    CheckLine(player);
                    Click(player.nextButton);
                    if (player.IsPlaying) return;
                    Expect(!player.portrait.enabled && player.portrait.sprite == null, "Victory clears portrait");
                    CheckRewardHidden(player);
                    evidence.AppendLine("LEVEL " + testedLevel + " VICTORY UI PASS: " +
                        (simulateBlockedOutcome ? "SIMULATED outcome (UI-only check)" : "finite enemies resolved") +
                        ", direct dialogue, no settlement, portrait cleared.");
                    if (testedLevel < 4)
                    {
                        Expect(flow.CurrentLevel == testedLevel + 1, "Victory advances once");
                        if (levelTwoOnly && testedLevel == 2) { step = 20; return; }
                        testedLevel++;
                        step = 1;
                        return;
                    }
                    Expect(flow.CurrentPhase == MapUIFlow.Phase.Result && !player.IsPlaying, "Final victory completes without settlement");
                    flow.TogglePause();
                    flow.RestartCurrentLevel();
                    Expect(flow.CurrentLevel == 4 && flow.CurrentPhase == MapUIFlow.Phase.Preparation, "Final level remains replayable from pause menu");
                    if (rewardsOnly) { step = 30; return; }
                    Finish(blockedLevels.Count == 0, blockedLevels.Count == 0
                        ? "All four levels passed. Births and damage were accelerated; full combat timing and tower balance were not tested."
                        : "Partial verification only. Blocked levels: " + string.Join(", ", blockedLevels) + ". Other levels checked independently with accelerated births and damage.");
                    return;
                case 30:
                    player.Play(AssetDatabase.LoadAssetAtPath<DialogueSequence>("Assets/Dialogue/L1_Clear.asset"), null);
                    CheckLine(player);
                    step = 31; return;
                case 31:
                    Click(player.skipButton);
                    CheckRewardHidden(player);
                    player.Play(AssetDatabase.LoadAssetAtPath<DialogueSequence>("Assets/Dialogue/L2_Clear.asset"), null);
                    CheckLine(player);
                    player.Cancel();
                    CheckRewardHidden(player);
                    Finish(true, "Reward panel passed at all four CSV positions, normal lines, completion, skip, cancel and failure/retry. Outcomes were simulated for UI verification; combat configuration was unchanged.");
                    return;
                case 20:
                    if (flow.CurrentPhase == MapUIFlow.Phase.Transition) return;
                    Expect(blockedLevels.Count == 0 && flow.CurrentLevel == 3 && flow.CurrentPhase == MapUIFlow.Phase.Preparation,
                        "Second-level victory finishes camera transition into level-three preparation");
                    Expect(!player.IsPlaying && !player.panel.activeSelf && !flow.resultRoot.activeSelf &&
                        flow.CurrentBaseHealth == 5 && flow.spawners.All(s => s.Level == 3 && !s.enabled && s.SpawnedCount == 0),
                        "Level three begins with health restored, stopped spawners and no old settlement UI");
                    Expect(flow.startBattleButton.gameObject.activeInHierarchy && flow.selection.InteractionAllowed &&
                        flow.selection.IsUnlocked(flow.dudukPrefab), "Level three preparation and Duduk unlock available");
                    evidence.AppendLine("LEVEL 2 -> LEVEL 3 PASS: camera transition completed, preparation available, full health, Duduk unlocked, no settlement panel.");
                    Finish(true, "Level-two references, actual arrival/death events, retry, victory dialogue and transition passed. Enemy births and damage were accelerated; wave timing/tower balance not tested. Level-three/four test configuration was not changed.");
                    return;
            }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }

    static void Finish(bool passed, string message)
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Poll;
        Application.logMessageReceived -= OnLog;
        Directory.CreateDirectory("Artifacts/UIFlow");
        string report = (passed ? "MAP_REPLAY_PLAY_PASS\n" : "MAP_REPLAY_PLAY_FAIL\n") + message + "\nStep=" + step + ", Level=" + testedLevel + "\n" + evidence;
        File.WriteAllText(rewardsOnly ? "Artifacts/UIFlow/reward-panel-check.txt" : levelTwoOnly ? "Artifacts/UIFlow/level-two-dialogue-check.txt" : "Artifacts/UIFlow/replay-check.txt", report);
        if (passed) Debug.Log(report); else Debug.LogError(report);
        EditorApplication.ExitPlaymode();
        if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
    }
}

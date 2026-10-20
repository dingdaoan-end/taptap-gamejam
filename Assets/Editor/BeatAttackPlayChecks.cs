using System;
using System.Linq;
using TapTapGameJam.Combat;
using TapTapGameJam.Music;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One real Play-mode smoke check in the existing scene. Scene changes are not saved.
public static class BeatAttackPlayChecks
{
    const string Key = "BeatAttack.PlaySmoke";
    static double started;
    static bool sawLivingEnemy, sawDamage;
    static EnemyHealth firstEnemy;

    [InitializeOnLoadMethod]
    static void Initialize()
    {
        if (!SessionState.GetBool(Key, false)) return;
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
    }
    public static void RunBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MusicTest.unity", OpenSceneMode.Single);
        var clock = UnityEngine.Object.FindObjectOfType<MusicClock>();
        var fields = new SerializedObject(clock);
        fields.FindProperty("pauseOnFocusLoss").boolValue = false;
        fields.ApplyModifiedPropertiesWithoutUndo();
        var spawner = UnityEngine.Object.FindObjectOfType<EnemyAudioTestSpawner>();
        if (spawner == null || spawner.spawnPoint == null) throw new Exception("Smoke check needs the authored enemy spawner.");
        spawner.unitsPerBeat = .2f;
        var section = UnityEngine.Object.FindObjectsOfType<Transform>().First(t => t.name == "SectionA");
        var tower = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tower_Bell.prefab"),
            spawner.spawnPoint.position + Vector3.up * .5f, Quaternion.identity, section);
        tower.name = "BeatAttack smoke tower";
        SessionState.SetBool(Key, true); Initialize(); EditorApplication.EnterPlaymode();
    }
    static void Poll()
    {
        if (!EditorApplication.isPlaying)
        {
            if (EditorApplication.timeSinceStartup - started > 45) Finish(false, "Play mode did not start.");
            return;
        }
        try
        {
            var tower = UnityEngine.Object.FindObjectsOfType<TowerBeatAttack>().FirstOrDefault(t => t.name == "BeatAttack smoke tower");
            if (tower == null) throw new Exception("Smoke tower lost its attack component.");
            var clock = UnityEngine.Object.FindObjectOfType<MusicClock>();
            foreach (var health in UnityEngine.Object.FindObjectsOfType<EnemyHealth>())
            {
                if (!health.IsAlive) continue;
                if (!sawLivingEnemy) { sawLivingEnemy = true; firstEnemy = health; }
                if (health.CurrentHealth < health.MaxHealth) sawDamage = true;
            }
            if (tower.ShotsFired >= 3)
            {
                bool dead = firstEnemy == null || !firstEnemy.IsAlive;
                if (!sawLivingEnemy || !sawDamage || !dead)
                    throw new Exception("Real lifecycle did not produce spawn, damage and death.");
                Finish(true, "real MusicTest Play: spawned enemy, three beat hits and enemy death; last beat=" + tower.LastAttackBeat);
                return;
            }
            if (EditorApplication.timeSinceStartup - started > 30)
                throw new Exception("Real combat timed out: running=" + clock.IsRunning + ", paused=" + clock.IsPaused + ", beat=" + clock.CurrentBeat + ", shots=" + tower.ShotsFired);
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Poll;
        if (success) Debug.Log("BEAT_ATTACK_PLAY_PASS " + message);
        else Debug.LogError("BEAT_ATTACK_PLAY_FAILED " + message);
        EditorApplication.Exit(success ? 0 : 1);
    }
}

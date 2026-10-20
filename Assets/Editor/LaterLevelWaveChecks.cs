using System;
using System.Linq;
using TapTapGameJam.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LaterLevelWaveChecks
{
    static int checks;
    static void Require(bool ok, string message)
    {
        if (!ok) throw new Exception("LATER_WAVE_CHECK_FAIL: " + message);
        checks++;
    }

    public static void RunBatch()
    {
        try
        {
            checks = 0;
            EditorSceneManager.OpenScene("Assets/Scenes/Map.unity", OpenSceneMode.Single);
            var sources = UnityEngine.Object.FindObjectsOfType<EnemyAudioTestSpawner>(true);
            foreach (int level in new[] { 2, 3, 4 })
            {
                var roads = sources.Where(s => s.Level == level).OrderBy(s => s.name).ToArray();
                Require(roads.Length == (level == 2 ? 2 : 3), "level " + level + " route count");
                foreach (var road in roads)
                {
                    Require(road.enemyPrefab != null, road.name + " missing fallback prefab after asset cleanup");
                    Require(road.HasAuthoredWave && road.PlannedCount > 0, road.name + " must have a finite playable wave");
                    Require(road.route != null && road.route.TryPrepare(out _), road.name + " invalid authored route");
                    var entries = new SerializedObject(road).FindProperty("waveSpawns");
                    int previous = 0, bosses = 0;
                    for (int i = 0; i < entries.arraySize; i++)
                    {
                        var entry = entries.GetArrayElementAtIndex(i);
                        int beat = entry.FindPropertyRelative("beat").intValue;
                        var prefab = entry.FindPropertyRelative("prefab").objectReferenceValue as GameObject;
                        Require(beat >= previous && beat >= 1 && (beat - 1) % 32 < 16, road.name + " ordered births stay out of rest beats");
                        Require(prefab != null && !prefab.scene.IsValid(), road.name + " missing wave prefab");
                        Require(prefab.GetComponent<EnemyPathMover>().DamagesBaseOnArrival, road.name + " must use real enemies");
                        Require(prefab.GetComponent<EnemyHealth>().MaxHealth > 0, road.name + " enemy health");
                        if (prefab.name == "Enemy_Boss") { bosses++; Require(level == 4 && beat > 64, "boss belongs to final group"); }
                        previous = beat;
                    }
                    Require(bosses <= 1, "at most one terminal boss per road");
                }
                int expected = level == 2 ? 20 : level == 3 ? 36 : 46;
                Require(roads.Sum(r => r.PlannedCount) == expected, "level " + level + " finite population");
            }
            EnemyAllChecks.Run();
            Debug.Log("LATER_WAVE_CHECK_PASS checks=" + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}

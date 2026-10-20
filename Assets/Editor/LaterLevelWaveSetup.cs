using System;
using System.Collections.Generic;
using System.Linq;
using TapTapGameJam.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LaterLevelWaveSetup
{
    public static void ConfigureBatch()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Map.unity", OpenSceneMode.Single);
            LevelTwoWaveSetup.Configure();
            Configure();
            Debug.Log("LATER_WAVE_SETUP_PASS level2=20 level3=36 level4=46");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    [MenuItem("Tools/Enemies/Configure Levels 3 And 4 Waves")]
    public static void Configure()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit Play mode before editing waves.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Map.unity") throw new InvalidOperationException("Open Map first.");
        var colors = new[] { "Red", "Blue", "Yellow" }.Select(n =>
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy_" + n + ".prefab")).ToArray();
        var boss = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy_Boss.prefab");
        foreach (var prefab in colors.Concat(new[] { boss }))
            if (prefab == null || !prefab.GetComponent<EnemyPathMover>().DamagesBaseOnArrival)
                throw new InvalidOperationException("Playable enemy prefab is missing or harmless.");
        var all = UnityEngine.Object.FindObjectsOfType<EnemyAudioTestSpawner>(true);
        foreach (int level in new[] { 3, 4 })
        {
            var roads = all.Where(s => s.gameObject.scene == scene && s.Level == level).OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
            if (roads.Length != 3) throw new InvalidOperationException("Expected three roads for level " + level);
            for (int road = 0; road < roads.Length; road++)
            {
                var source = roads[road];
                if (source.route == null || !source.route.TryPrepare(out var _))
                    throw new InvalidOperationException("Invalid road: " + source.name);
                var events = new List<(int beat, GameObject prefab)>();
                int[] offsets = level == 3 ? new[] { 1, 5, 9, 13 } : new[] { 1, 3, 7, 9, 13 };
                for (int group = 0; group < 3; group++)
                {
                    for (int i = 0; i < offsets.Length; i++)
                        events.Add((group * 32 + offsets[i], colors[(road + group + i) % colors.Length]));
                    if (level == 4 && group == 2 && road == 1) events.Add((group * 32 + 15, boss));
                }
                Undo.RecordObject(source, "Configure later-level waves");
                source.enemyPrefab = colors[road];
                source.unitsPerBeat = .5f;
                source.enabled = false;
                var serialized = new SerializedObject(source);
                serialized.FindProperty("spawnLimit").intValue = 0;
                var wave = serialized.FindProperty("waveSpawns");
                wave.arraySize = events.Count;
                for (int i = 0; i < events.Count; i++)
                {
                    var item = wave.GetArrayElementAtIndex(i);
                    item.FindPropertyRelative("beat").intValue = events[i].beat;
                    item.FindPropertyRelative("prefab").objectReferenceValue = events[i].prefab;
                }
                serialized.ApplyModifiedProperties();
                Debug.Log("LATER_WAVE_AUTHORED level=" + level + " road=" + source.name + " count=" + source.PlannedCount);
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save waves to Map.");
    }
}

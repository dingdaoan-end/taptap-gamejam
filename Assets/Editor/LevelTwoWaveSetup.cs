using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TapTapGameJam.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LevelTwoWaveSetup
{
    const string YellowPath = "Assets/Prefabs/Enemy_Yellow.prefab";
    const string BluePath = "Assets/Prefabs/Enemy_Blue.prefab";
    const int GroupBeats = 16;
    const int RestBeats = 16;

    [MenuItem("Tools/Enemies/Configure Level 2 Two Waves With Clear Gate")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode first.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Map.unity") throw new InvalidOperationException("Open Map first.");
        var yellow = AssetDatabase.LoadAssetAtPath<GameObject>(YellowPath);
        var blue = AssetDatabase.LoadAssetAtPath<GameObject>(BluePath);
        if (yellow == null || blue == null || yellow.GetComponent<EnemyHealth>().MaxHealth != 8 ||
            blue.GetComponent<EnemyHealth>().MaxHealth != 8)
            throw new InvalidOperationException("Expected yellow 8 HP and blue 8 HP prefab assets.");
        var routes = UnityEngine.Object.FindObjectsOfType<EnemyAudioTestSpawner>(true)
            .Where(s => s.gameObject.scene == scene && s.Level == 2).ToArray();
        if (routes.Length != 2) throw new InvalidOperationException("Expected exactly two level-two routes.");
        var ordered = new[] { "NorthEast entry 1", "NorthEast entry 2" }
            .Select(name => routes.Single(s => s.name == name)).ToArray();
        var report = new List<string> { "LEVEL_TWO_WAVE_SYNC_PASS", Application.dataPath };
        for (int road = 0; road < ordered.Length; road++)
        {
            var spawner = ordered[road];
            if (spawner.route == null || spawner.spawnPoint == null)
                throw new InvalidOperationException("Missing road or spawn point.");
            var events = new List<(int beat, GameObject prefab)>();
            for (int group = 0; group < 2; group++)
            {
                int groupStart = group * (GroupBeats + RestBeats);
                foreach (int offset in new[] { 1, 2, 9, 10 }) events.Add((groupStart + offset, blue));
                events.Add((groupStart + (road == 0 ? 5 : 13), yellow));
            }
            events = events.OrderBy(e => e.beat).ToList();
            Undo.RecordObject(spawner, "Configure level two mixed waves");
            spawner.enemyPrefab = blue;
            var serialized = new SerializedObject(spawner);
            serialized.FindProperty("unitsPerBeat").floatValue = .5f;
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
            // Read Unity's serialized state back, rather than reporting the input list alone.
            serialized.Update();
            wave = serialized.FindProperty("waveSpawns");
            if (spawner.PlannedCount != 10 || spawner.UnitsPerBeat != .5f) throw new Exception("Wave settings did not apply.");
            report.Add(spawner.name + ": 10 enemies; speed=" + spawner.UnitsPerBeat + "; second wave waits for both roads to clear");
            for (int i = 0; i < wave.arraySize; i++)
            {
                var item = wave.GetArrayElementAtIndex(i);
                int beat = item.FindPropertyRelative("beat").intValue;
                var prefab = (GameObject)item.FindPropertyRelative("prefab").objectReferenceValue;
                if (beat != events[i].beat || prefab != events[i].prefab) throw new Exception("Wave readback mismatch.");
                report.Add("  Beat " + beat + ": " + prefab.name + ", HP=" + prefab.GetComponent<EnemyHealth>().MaxHealth);
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Map.");
        Directory.CreateDirectory("Artifacts/EnemyWave");
        File.WriteAllLines("Artifacts/EnemyWave/level-two-sync.txt", report);
        Debug.Log(string.Join("\n", report));
        Selection.activeGameObject = ordered[0].gameObject;
    }
}

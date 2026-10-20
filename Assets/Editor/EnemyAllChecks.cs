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
using UnityEngine.SceneManagement;

public static class EnemyAllChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Expect(bool condition, string message)
    { if (!condition) throw new Exception("Enemy_All: " + message); }

    [MenuItem("Tools/Combat/Check Enemy All Test Setup")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit Play mode before running the check.");
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy_All.prefab");
        Expect(asset != null && PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Variant, "must import as a prefab variant");
        var preview = SceneManager.GetSceneByPath("Assets/Scenes/Map.unity");
        bool openedScene = !preview.isLoaded;
        if (openedScene) preview = EditorSceneManager.OpenScene("Assets/Scenes/Map.unity", OpenSceneMode.Additive);
        var enemy = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Enemy_All.prefab");
        var regular = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Enemy.prefab");
        var fixture = new GameObject("Enemy All check fixture");
        fixture.SetActive(false);
        try
        {
            var health = enemy.GetComponent<EnemyHealth>();
            typeof(EnemyHealth).GetMethod("Awake", Private).Invoke(health, null);
            typeof(EnemyHealth).GetMethod("OnEnable", Private).Invoke(health, null);
            Expect(health.MaxHealth == 4 && health.CurrentHealth == 4, "default HP must be four");
            foreach (TowerInstrument instrument in Enum.GetValues(typeof(TowerInstrument)))
                Expect(health.ReceiveHit(1, instrument, 0), "every tower must be able to hit");
            Expect(health.CurrentHealth == 1, "all three instruments must actually deal damage");
            var mover = enemy.GetComponent<EnemyPathMover>();
            Expect(!mover.DamagesBaseOnArrival, "test enemy must not hurt base");
            Expect(new SerializedObject(mover).FindProperty("unitsPerBeat").floatValue == .5f, "prefab speed must be half a cell per beat");
            var flow = fixture.AddComponent<MapUIFlow>();
            typeof(MapUIFlow).GetField("<CurrentPhase>k__BackingField", Private).SetValue(flow, MapUIFlow.Phase.Battle);
            var owner = fixture.AddComponent<EnemyAudioTestSpawner>();
            var living = (List<GameObject>)typeof(EnemyAudioTestSpawner).GetField("livingEnemies", Private).GetValue(owner);
            var handler = typeof(MapUIFlow).GetMethod("OnEnemyArrived", Private);
            owner.EnemyArrived += arriving => handler.Invoke(flow, new object[] { arriving });
            living.Add(enemy);
            typeof(EnemyPathMover).GetField("<HasArrived>k__BackingField", Private).SetValue(mover, true);
            owner.NotifyArrival(mover); owner.NotifyArrival(mover);
            Expect(owner.ArrivedCount == 1 && owner.LivingCount == 0, "harmless arrival still removes/counts exactly once");
            Expect(flow.CurrentBaseHealth == 5, "test arrival must preserve base HP");
            var normalMover = regular.GetComponent<EnemyPathMover>();
            Expect(normalMover.DamagesBaseOnArrival, "regular enemies must still hurt base");
            handler.Invoke(flow, new object[] { normalMover });
            Expect(flow.CurrentBaseHealth == 4, "normal arrival still takes one base life");
            var sources = preview.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnemyAudioTestSpawner>(true)).ToArray();
            foreach (int level in new[] { 3, 4 })
            {
                var routes = sources.Where(s => s.Level == level).ToArray();
                Expect(routes.Length == 3, "each test level needs three routes");
                foreach (var source in routes)
                    Expect(source.enemyPrefab == asset && source.UnitsPerBeat == .5f && source.spawnIntervalBeats == 1 &&
                        source.PlannedCount == 0 && !source.HasAuthoredWave, "each route must continuously spawn Enemy_All every beat");
            }
            Directory.CreateDirectory("Artifacts/EnemyAll");
            File.WriteAllText("Artifacts/EnemyAll/check.txt", "ENEMY_ALL_CHECK_PASS: prefab import, HP, all instruments, speed, harmless/normal arrivals, six spawner bindings.");
            Debug.Log("ENEMY_ALL_CHECK_PASS");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fixture);
            PrefabUtility.UnloadPrefabContents(enemy);
            PrefabUtility.UnloadPrefabContents(regular);
            if (openedScene) EditorSceneManager.CloseScene(preview, true);
        }
    }
}

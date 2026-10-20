using System;
using System.Linq;
using TapTapGameJam.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class EnemyRouteBranchChecks
{
    public static void RepairAndVerify()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Map.unity", OpenSceneMode.Single);
            EnemyMovementSetup.ConfigureCurrentScene();
            Verify();
            EnemyMovementSetup.ConfigureCurrentScene();
            Verify(); // Re-running the menu must not duplicate branches.
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("ENEMY_ROUTE_BRANCH_CHECKS_PASS levels=1,2,3,3 total=9");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static void Require(bool ok, string message)
    {
        if (!ok) throw new Exception("Route branch regression: " + message);
    }

    static void Verify()
    {
        var spawners = UnityEngine.Object.FindObjectsOfType<EnemyAudioTestSpawner>(true);
        var roads = UnityEngine.Object.FindObjectsOfType<Tilemap>().Single(t => t.name == "Paths");
        var rocks = UnityEngine.Object.FindObjectsOfType<Tilemap>().Single(t => t.name == "Obstacle");
        var walkable = EnemyRoadPathfinder.WalkableCells(roads, rocks);
        var counts = new[] { 1, 2, 3, 3 };
        for (int level = 1; level <= 4; level++)
            Require(spawners.Count(s => s.Level == level) == counts[level - 1], "level " + level + " route count");
        foreach (var s in spawners)
        {
            Require(s.route.TryPrepare(out var reason), s.name + ": " + reason);
            Require((s.route.Point(0) - (Vector2)s.spawnPoint.position).sqrMagnitude < .001f, "spawn matches route");
            for (int i = 0; i < s.route.Count; i++)
            {
                var cell = roads.WorldToCell(s.route.Point(i));
                Require(walkable.Contains(cell), "route remains on unblocked road");
                if (i > 0) Require(EnemyRoadPathfinder.Neighbors(cell).Contains(roads.WorldToCell(s.route.Point(i - 1))), "adjacent waypoints");
            }
        }
        foreach (int level in new[] { 2, 4 })
        {
            var shared = spawners.Where(s => s.Level == level).GroupBy(s => s.spawnPoint).Single(g => g.Count() == 2).ToArray();
            Require(shared[0].route.Point(shared[0].route.Count - 1) != shared[1].route.Point(shared[1].route.Count - 1), "shared entrance has different exits");
        }
    }
}

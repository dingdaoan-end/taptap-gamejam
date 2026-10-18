using System.Collections.Generic;
using TapTapGameJam.Combat;
using TapTapGameJam.Music;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class EnemyAudioTestSetup
{
    [MenuItem("Tools/Enemy Audio Test/Create Route And Spawner")]
    public static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("Exit Play mode before configuring the route."); return; }
        var scene = EditorSceneManager.GetActiveScene();
        GameObject enemy = null;
        Tilemap road = null;
        MusicClock clock = null;
        EnemyAudioTestSpawner spawner = null;
        Transform spawnPoint = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var existing in root.GetComponentsInChildren<EnemyAudioTestSpawner>(true))
            {
                if (spawner != null)
                { Debug.LogError("Multiple audio test spawners exist. Keep one before rebuilding the route."); return; }
                spawner = existing;
            }
            foreach (var body in root.GetComponentsInChildren<Rigidbody2D>(true))
                if (body.name == "Enemy") enemy = body.gameObject;
            foreach (var map in root.GetComponentsInChildren<Tilemap>(true))
                if (map.name == "Paths") road = map;
            var found = root.GetComponentInChildren<MusicClock>(true);
            if (found != null) clock = found;
        }
        // Rebuild from the bound template, even if the user has renamed it.
        if (spawner != null)
        {
            if (spawner.enemyPrefab != null) enemy = spawner.enemyPrefab;
            if (spawner.clock != null) clock = spawner.clock;
            spawnPoint = spawner.spawnPoint != null ? spawner.spawnPoint : spawner.transform.Find("SpawnPoint");
        }
        if (enemy != null && enemy.scene.IsValid())
            enemy = PrefabUtility.GetCorrespondingObjectFromSource(enemy);
        if (enemy == null || road == null || clock == null || spawnPoint == null)
        { Debug.LogError("Assign Enemy Prefab and create SpawnPoint under EnemyAudioTest. The scene also needs Paths and MusicClock."); return; }

        var cells = new HashSet<Vector3Int>();
        foreach (var cell in road.cellBounds.allPositionsWithin) if (road.HasTile(cell)) cells.Add(cell);
        if (cells.Count == 0) { Debug.LogError("Paths has no road tiles."); return; }
        var start = Nearest(cells, road, spawnPoint.position);
        var end = Nearest(cells, road, clock.transform.position);
        var queue = new Queue<Vector3Int>();
        var previous = new Dictionary<Vector3Int, Vector3Int>();
        var directions = new[] { Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left };
        queue.Enqueue(start);
        previous[start] = start;
        while (queue.Count > 0 && !previous.ContainsKey(end))
        {
            var cell = queue.Dequeue();
            foreach (var direction in directions)
            {
                var next = cell + direction;
                if (!cells.Contains(next) || previous.ContainsKey(next)) continue;
                previous[next] = cell;
                queue.Enqueue(next);
            }
        }
        if (!previous.ContainsKey(end) || start == end)
        { Debug.LogError("No connected road route from SpawnPoint to the base. Check Paths tiles and SpawnPoint."); return; }
        var path = new List<Vector3Int>();
        for (var cell = end; ; cell = previous[cell]) { path.Add(cell); if (cell == start) break; }
        path.Reverse();
        var positions = new List<Vector3>();
        foreach (var cell in path)
        {
            var position = road.GetCellCenterWorld(cell);
            if (positions.Count == 0 || (position - positions[positions.Count - 1]).sqrMagnitude > 0.000001f)
                positions.Add(position);
        }
        // Validate the new path before touching the old route. Undo restores it.
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create or rebuild enemy audio test");
        if (spawner == null)
        {
            var testObject = new GameObject("EnemyAudioTest");
            Undo.RegisterCreatedObjectUndo(testObject, "Create test");
            spawner = Undo.AddComponent<EnemyAudioTestSpawner>(testObject);
        }
        var rootObject = spawner.gameObject;
        var route = spawner.route;
        if (route == null)
        {
            var routeObject = new GameObject("Route");
            Undo.RegisterCreatedObjectUndo(routeObject, "Create route");
            routeObject.transform.SetParent(rootObject.transform);
            route = Undo.AddComponent<EnemyTestRoute>(routeObject);
        }
        else
        {
            for (int i = route.transform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(route.transform.GetChild(i).gameObject);
        }
        for (int i = 0; i < positions.Count; i++)
        {
            var point = new GameObject("Waypoint " + i.ToString("D3"));
            Undo.RegisterCreatedObjectUndo(point, "Create waypoint");
            point.transform.SetParent(route.transform);
            point.transform.position = positions[i];
        }
        Undo.RecordObject(spawner, "Update test bindings");
        spawner.enemyPrefab = enemy;
        spawner.spawnPoint = spawnPoint;
        spawner.route = route;
        spawner.clock = clock;
        EditorUtility.SetDirty(spawner);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = rootObject;
        Debug.Log("Created/rebuilt " + positions.Count + " waypoints from SpawnPoint at " + spawnPoint.position +
            ". Existing waypoints were replaced; live speed and spawn interval settings were preserved. Review yellow Gizmos and save the scene.", rootObject);
    }

    private static Vector3Int Nearest(HashSet<Vector3Int> cells, Tilemap road, Vector3 position)
    {
        var nearest = default(Vector3Int);
        float best = float.PositiveInfinity;
        foreach (var cell in cells)
        {
            float distance = (road.GetCellCenterWorld(cell) - position).sqrMagnitude;
            if (distance >= best) continue;
            best = distance;
            nearest = cell;
        }
        return nearest;
    }
}

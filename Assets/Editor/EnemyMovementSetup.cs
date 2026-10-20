using System;
using System.Collections.Generic;
using System.Linq;
using TapTapGameJam.Combat;
using TapTapGameJam.Music;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class EnemyMovementSetup
{
    sealed class Entry
    {
        public Vector3Int source, goal;
        public int region;
    }
    static readonly string[] Regions = { "NorthWest", "NorthEast", "SouthWest", "SouthEast" };

    public static void ConfigureProject()
    {
        ConfigurePrefab();
        EditorSceneManager.OpenScene("Assets/Scenes/Map.unity", OpenSceneMode.Single);
        ConfigureCurrentScene();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }
    static void ConfigurePrefab()
    {
        const string path = "Assets/Prefabs/Enemy.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var mover = root.GetComponent<EnemyPathMover>();
            if (mover == null) mover = root.AddComponent<EnemyPathMover>();
            var fields = new SerializedObject(mover);
            fields.FindProperty("sprite").objectReferenceValue = root.GetComponentInChildren<SpriteRenderer>(true);
            fields.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Tools/Enemies/Configure Map Movement")]
    public static void ConfigureCurrentScene()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit Play mode before configuring enemy routes.");
        var scene = EditorSceneManager.GetActiveScene();
        var maps = UnityEngine.Object.FindObjectsOfType<Tilemap>().Where(t => t.gameObject.scene == scene).ToArray();
        var roads = maps.Single(t => t.name == "Paths");
        var rocks = maps.FirstOrDefault(t => t.name == "Obstacle");
        var clock = UnityEngine.Object.FindObjectsOfType<MusicClock>().Single(c => c.gameObject.scene == scene);
        var existing = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Enemy Movement");
        var remaining = EnemyRoadPathfinder.WalkableCells(roads, rocks);
        var entries = new List<Entry>();
        while (remaining.Count > 0)
        {
            var component = new HashSet<Vector3Int>();
            var queue = new Queue<Vector3Int>(); queue.Enqueue(remaining.OrderBy(c => c.x).ThenBy(c => c.y).First());
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue(); if (!remaining.Remove(cell)) continue;
                component.Add(cell);
                foreach (var next in EnemyRoadPathfinder.Neighbors(cell)) if (remaining.Contains(next)) queue.Enqueue(next);
            }
            if (component.Count < 2) continue;
            var endpoints = component.Where(c => EnemyRoadPathfinder.Neighbors(c).Count(component.Contains) <= 1).ToArray();
            var candidates = endpoints.Length >= 2 ? endpoints : component.ToArray();
            Vector2 castle = clock.transform.position;
            var ordered = candidates.OrderBy(c => ((Vector2)roads.GetCellCenterWorld(c) - castle).sqrMagnitude).ThenBy(c => c.x).ThenBy(c => c.y).ToArray();
            var source = ordered[ordered.Length - 1]; var goal = ordered[0];
            Vector2 origin = roads.GetCellCenterWorld(source);
            int region = origin.x < castle.x ? (origin.y >= castle.y ? 0 : 2) : (origin.y >= castle.y ? 1 : 3);
            // A connected road can fork from one entrance to several base exits.
            // Preserve every exit instead of choosing only the closest one.
            foreach (var exit in endpoints.Length >= 2 ? ordered.Where(c => c != source) : new[] { goal })
                entries.Add(new Entry { source = source, goal = exit, region = region });
        }
        if (entries.Count == 0) throw new InvalidOperationException("No connected road routes found.");
        if (existing != null)
        {
            foreach (var entry in entries)
            {
                var spawners = existing.GetComponentsInChildren<EnemyAudioTestSpawner>(true);
                var template = spawners.FirstOrDefault(s => s.spawnPoint != null &&
                    roads.WorldToCell(s.spawnPoint.position) == entry.source);
                // Keep manually authored entrances and all their wave/prefab settings.
                if (template == null) continue;
                bool present = spawners.Any(s => s.spawnPoint != null && s.route != null &&
                    roads.WorldToCell(s.spawnPoint.position) == entry.source &&
                    GetGoalCell(s.route, roads) == entry.goal);
                if (present) continue;
                var copy = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
                Undo.RegisterCreatedObjectUndo(copy, "Add missing road branch");
                copy.name = Regions[entry.region] + " entry " +
                    (spawners.Count(s => s.transform.parent == template.transform.parent) + 1);
                var added = copy.GetComponent<EnemyAudioTestSpawner>();
                // Both routes deliberately share the same editable spawn marker.
                var duplicateSpawn = added.spawnPoint;
                added.spawnPoint = template.spawnPoint;
                var fields = new SerializedObject(added.route);
                fields.FindProperty("entrance").objectReferenceValue = template.spawnPoint;
                var exit = (Transform)fields.FindProperty("goal").objectReferenceValue;
                exit.position = roads.GetCellCenterWorld(entry.goal);
                fields.ApplyModifiedPropertiesWithoutUndo();
                if (duplicateSpawn != template.spawnPoint) Undo.DestroyObjectImmediate(duplicateSpawn.gameObject);
            }
            foreach (var spawner in existing.GetComponentsInChildren<EnemyAudioTestSpawner>(true)) RebuildPreview(spawner);
            EditorSceneManager.MarkSceneDirty(scene); return;
        }
        var root = new GameObject("Enemy Movement"); Undo.RegisterCreatedObjectUndo(root, "Add enemy movement routes");
        var groups = Regions.Select(n => { var g = new GameObject(n); g.transform.SetParent(root.transform, false); return g.transform; }).ToArray();
        int[] counts = new int[4]; bool enabledFirst = false;
        foreach (var entry in entries.OrderBy(e => e.region).ThenBy(e => e.source.x).ThenBy(e => e.source.y))
        {
            var obj = new GameObject(Regions[entry.region] + " entry " + (++counts[entry.region])); obj.transform.SetParent(groups[entry.region], false);
            var spawner = obj.AddComponent<EnemyAudioTestSpawner>();
            var spawn = new GameObject("SpawnPoint").transform; spawn.SetParent(obj.transform); spawn.position = roads.GetCellCenterWorld(entry.source);
            var goal = new GameObject("Goal").transform; goal.SetParent(obj.transform); goal.position = roads.GetCellCenterWorld(entry.goal);
            var route = new GameObject("Route", typeof(EnemyTestRoute)).GetComponent<EnemyTestRoute>(); route.transform.SetParent(obj.transform, false);
            var fields = new SerializedObject(route);
            fields.FindProperty("roadTiles").objectReferenceValue = roads;
            fields.FindProperty("obstacleTiles").objectReferenceValue = rocks;
            fields.FindProperty("entrance").objectReferenceValue = spawn;
            fields.FindProperty("goal").objectReferenceValue = goal;
            fields.FindProperty("rebuildFromRoadOnStart").boolValue = true; fields.ApplyModifiedPropertiesWithoutUndo();
            spawner.enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy.prefab");
            spawner.spawnPoint = spawn; spawner.route = route; spawner.clock = clock;
            var spawnerFields = new SerializedObject(spawner);
            spawnerFields.FindProperty("level").intValue = new[] { 1, 2, 4, 3 }[entry.region];
            spawnerFields.ApplyModifiedPropertiesWithoutUndo();
            spawner.unitsPerBeat = 1; spawner.spawnIntervalBeats = 4;
            spawner.enabled = !enabledFirst && entry.region == 0;
            if (spawner.enabled) enabledFirst = true;
            RebuildPreview(spawner);
        }
        if (!enabledFirst) root.GetComponentsInChildren<EnemyAudioTestSpawner>().First().enabled = true;
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("ENEMY_MOVEMENT_CONFIGURED routes=" + entries.Count + "; only the first NorthWest entry starts by default.");
    }

    static Vector3Int GetGoalCell(EnemyTestRoute route, Tilemap roads)
    {
        var goal = (Transform)new SerializedObject(route).FindProperty("goal").objectReferenceValue;
        return goal != null ? roads.WorldToCell(goal.position) : new Vector3Int(int.MinValue, 0, 0);
    }

    static void RebuildPreview(EnemyAudioTestSpawner spawner)
    {
        string reason = "missing route";
        if (spawner.route == null || !spawner.route.TryPrepare(out reason))
            throw new InvalidOperationException("Cannot rebuild enemy route: " + reason);
        var points = new Vector2[spawner.route.Count];
        for (int i = 0; i < points.Length; i++) points[i] = spawner.route.Point(i);
        var parent = spawner.route.transform;
        for (int i = parent.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
        for (int i = 0; i < points.Length; i++)
        {
            var point = new GameObject("Waypoint " + i.ToString("D3"));
            Undo.RegisterCreatedObjectUndo(point, "Rebuild route preview");
            point.transform.SetParent(parent); point.transform.position = points[i];
        }
    }
}

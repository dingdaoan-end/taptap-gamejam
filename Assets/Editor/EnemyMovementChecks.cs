using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TapTapGameJam.Combat;
using TapTapGameJam.Music;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class EnemyMovementChecks
{
    static int checks;
    static MusicClock clock;
    static MusicTimeline timeline;
    static EnemyAudioTestSpawner owner;
    static EnemyTestRoute route;
    static EnemyPathMover mover;
    static double bpm = 100;
    static void Expect(bool condition, string message)
    {
        if (!condition) throw new Exception("ENEMY_MOVEMENT_FAILED: " + message);
        checks++; Debug.Log("ENEMY_MOVEMENT_PASS " + message);
    }
    static void Invoke(object obj, string method) => obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(obj, null);
    static void Beat(double beat) => timeline.Start(AudioSettings.dspTime - beat * 60 / bpm, bpm);
    static void Step(double beat)
    {
        Beat(beat); Invoke(mover, "FixedUpdate"); Physics2D.SyncTransforms(); Physics2D.Simulate(.02f);
    }
    static void Near(Vector2 want, Vector2 got, string message) => Expect(Vector2.Distance(want, got) < .025f, message + " at " + got);
    static void Fixture(Vector2[] points = null, double tempo = 100, double birth = .25)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); bpm = tempo;
        clock = new GameObject("Clock", typeof(AudioSource), typeof(MusicClock)).GetComponent<MusicClock>(); Invoke(clock, "Awake");
        timeline = (MusicTimeline)typeof(MusicClock).GetField("timeline", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(clock);
        Beat(.25);
        route = new GameObject("Route", typeof(EnemyTestRoute)).GetComponent<EnemyTestRoute>();
        foreach (var p in points ?? new[] { Vector2.zero, Vector2.right * 2, new Vector2(2, 2) })
        { var point = new GameObject("Waypoint"); point.transform.SetParent(route.transform); point.transform.position = p; }
        owner = new GameObject("Spawner", typeof(EnemyAudioTestSpawner)).GetComponent<EnemyAudioTestSpawner>(); owner.unitsPerBeat = 1;
        var enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy.prefab"), Vector3.zero, Quaternion.identity);
        var health = enemy.GetComponent<EnemyHealth>(); Invoke(health, "Awake"); Invoke(health, "OnEnable");
        mover = enemy.GetComponent<EnemyPathMover>(); mover.Initialize(route, birth, clock, owner, Vector2.zero);
        ((List<GameObject>)typeof(EnemyAudioTestSpawner).GetField("livingEnemies", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner)).Add(enemy);
    }
    static void GeometryChecks()
    {
        var points = new[] { Vector2.zero, Vector2.right * 2, new Vector2(2, 2) };
        var path = new EnemyRoutePath(Vector2.zero, points);
        Near(new Vector2(1, 0), path.Sample(1, out var direction, out bool arrived), "first road segment is sampled by distance");
        Expect(direction == Vector2.right && !arrived, "direction follows first segment");
        Near(new Vector2(2, 0), path.Sample(2, out direction, out arrived), "exact corner stays at road junction");
        Expect(direction == Vector2.up && !arrived, "corner turns toward next segment");
        Near(new Vector2(2, 1), path.Sample(3, out direction, out arrived), "multi-segment travel follows the bend");
        Near(new Vector2(2, 2), path.Sample(100, out direction, out arrived), "overshoot clamps to the goal"); Expect(arrived, "goal arrival detected");
        points[1] = new Vector2(100, 100);
        Near(new Vector2(1, 0), path.Sample(1, out direction, out arrived), "caller edits cannot mutate path snapshot");
        var repeats = new EnemyRoutePath(Vector2.zero, new[] { Vector2.zero, Vector2.zero, Vector2.right, Vector2.right });
        Near(new Vector2(.5f, 0), repeats.Sample(.5, out direction, out arrived), "duplicate waypoints do not divide by zero");
        Near(Vector2.zero, repeats.Sample(-10, out direction, out arrived), "negative distance clamps to route start");
    }
    static void MovementChecks()
    {
        foreach (double tempo in new[] { 100d, 116d })
        {
            Fixture(tempo: tempo); Step(1.25); Near(new Vector2(1, 0), mover.GetComponent<Rigidbody2D>().position, "one musical beat moves one unit at " + tempo + " BPM");
            Step(2.25); Near(new Vector2(2, 0), mover.GetComponent<Rigidbody2D>().position, "movement reaches corner at " + tempo + " BPM");
            Step(3.25); Near(new Vector2(2, 1), mover.GetComponent<Rigidbody2D>().position, "movement turns without cutting road at " + tempo + " BPM");
        }
        Fixture(); route.transform.GetChild(1).position = Vector3.up * 2;
        Step(1.25); Near(Vector2.right, mover.GetComponent<Rigidbody2D>().position, "original RED: editing route does not redirect existing enemy");
        UnityEngine.Object.DestroyImmediate(route.gameObject);
        Step(2.25); Near(Vector2.right * 2, mover.GetComponent<Rigidbody2D>().position, "destroyed authoring route does not invalidate snapshot");
        Fixture(new[] { Vector2.zero, Vector2.right * 20, new Vector2(20, 20) });
        Step(10.25); Near(new Vector2(10, 0), mover.GetComponent<Rigidbody2D>().position, "long DSP hitch recovers without physics speed-cap drift");
        Fixture(new[] { Vector2.zero, Vector2.right * 20 });
        owner.unitsPerBeat = 2; Step(.25); Step(1.25);
        Near(new Vector2(2, 0), mover.GetComponent<Rigidbody2D>().position, "live speed changes affect future travel");
        owner.unitsPerBeat = 0; Step(1.25); Step(2.25);
        Near(new Vector2(2, 0), mover.GetComponent<Rigidbody2D>().position, "zero movement speed holds position");
        Fixture(new[] { Vector2.zero, Vector2.right * 20 }); Step(1.25);
        var body = mover.GetComponent<Rigidbody2D>(); var held = body.position;
        var visual = mover.GetComponentInChildren<SpriteRenderer>().transform;
        var pose = visual.localPosition; timeline.Pause(AudioSettings.dspTime); Invoke(mover, "FixedUpdate"); Physics2D.Simulate(.02f);
        Near(held, body.position, "music pause freezes position"); Expect(visual.localPosition == pose, "music pause freezes walk pose");
        timeline.Resume(AudioSettings.dspTime + .2); Invoke(mover, "FixedUpdate"); Physics2D.Simulate(.02f);
        Near(held, body.position, "resume lead does not move early");
        Step(2.25); Near(new Vector2(2, 0), body.position, "resume retains musical route progress");
        Fixture(); Step(5.25);
        Expect(mover.HasArrived && !mover.gameObject.activeSelf && owner.ArrivedCount == 1, "arrival reports once and removes enemy");
        Invoke(mover, "FixedUpdate"); owner.NotifyArrival(mover); Expect(owner.ArrivedCount == 1, "duplicate arrival cannot be counted twice");
        Fixture(); var health = mover.GetComponent<EnemyHealth>(); health.TakeDamage(health.MaxHealth);
        Step(5.25); Expect(owner.ArrivedCount == 0 && mover.DistanceTravelled < .1, "killed enemy stops moving and cannot arrive");
        Fixture(); var sprite = mover.transform.Find("EnemyVisual").GetComponent<SpriteRenderer>(); var walker = mover.GetComponent<EnemyWalkVisual>();
        var scale = sprite.transform.localScale;
        var position = sprite.transform.localPosition;
        var rotation = sprite.transform.localRotation;
        for (int level = 1; level <= 4; level++)
        {
            walker.SetSpawnFacing(level);
            bool expectedFlip = level == 1 || level == 4;
            Expect(sprite.flipX == expectedFlip, "level " + level + " sets spawn facing");
            Step(.25 + level * .25);
            Expect(sprite.flipX == expectedFlip, "movement preserves level " + level + " spawn facing");
            Expect(sprite.transform.localPosition == position && sprite.transform.localRotation == rotation,
                "movement does not bob or rotate the enemy sprite");
        }
        Expect(sprite.transform.localScale == scale, "walk presentation preserves artist scale");
    }
    static void RoadChecks()
    {
        foreach (var swizzle in new[] { GridLayout.CellSwizzle.XYZ, GridLayout.CellSwizzle.YXZ })
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var grid = new GameObject("Grid", typeof(Grid)).GetComponent<Grid>(); grid.cellSwizzle = swizzle;
            var roads = new GameObject("Paths", typeof(Tilemap)).GetComponent<Tilemap>(); roads.transform.SetParent(grid.transform, false);
            var rocks = new GameObject("Obstacle", typeof(Tilemap)).GetComponent<Tilemap>(); rocks.transform.SetParent(grid.transform, false);
            var tile = ScriptableObject.CreateInstance<Tile>();
            for (int y = 0; y < 2; y++) for (int x = 0; x < 3; x++) roads.SetTile(new Vector3Int(x, y, 0), tile);
            rocks.SetTile(Vector3Int.right, tile);
            Vector2 start = roads.GetCellCenterWorld(Vector3Int.zero), goal = roads.GetCellCenterWorld(Vector3Int.right * 2);
            Expect(EnemyRoadPathfinder.TryFind(roads, rocks, start, goal, out var path, out _) && path.Length == 5, "road routing detours around stone in " + swizzle);
            Expect(path.All(p => !rocks.HasTile(rocks.WorldToCell(p))), "routed path never uses stone cells in " + swizzle);
            roads.SetTile(Vector3Int.up, null);
            Expect(!EnemyRoadPathfinder.TryFind(roads, rocks, start, goal, out _, out _), "disconnected road is rejected in " + swizzle);
            Expect(!EnemyRoadPathfinder.TryFind(roads, rocks, new Vector2(100, 100), goal, out _, out _), "off-road entrance is rejected in " + swizzle);
        }
    }
    static void MapChecks()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Map.unity", OpenSceneMode.Single);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy.prefab");
        Expect(prefab.GetComponent<EnemyPathMover>() != null && prefab.GetComponent<EnemyWalkVisual>() != null && prefab.GetComponent<EnemyHealth>() != null, "real Minion prefab includes movement, presentation and combat health");
        Expect(AssetDatabase.GetAssetPath(prefab.GetComponentInChildren<SpriteRenderer>().sprite) == "Assets/Sprites/Enemy/Minion.PNG", "latest GitHub Minion art retained");
        var spawners = UnityEngine.Object.FindObjectsOfType<EnemyAudioTestSpawner>();
        Expect(spawners.Length == 9, "all road branches have editable entry routes, including shared entrances");
        Expect(spawners.Count(s => s.enabled) == 1 && spawners.Single(s => s.enabled).name.Contains("NorthWest"), "only upper-left entry starts by default");
        var roads = UnityEngine.Object.FindObjectsOfType<Tilemap>().Single(m => m.name == "Paths");
        var rocks = UnityEngine.Object.FindObjectsOfType<Tilemap>().Single(m => m.name == "Obstacle");
        int total = 0;
        foreach (var spawner in spawners)
        {
            Expect(spawner.route.TryPrepare(out _) && spawner.route.Count > 1 && spawner.enemyPrefab == prefab && spawner.clock != null, "route prepared and real assets bound: " + spawner.name);
            Vector3Int previous = default;
            for (int i = 0; i < spawner.route.Count; i++)
            {
                Vector2 p = spawner.route.Point(i); var cell = roads.WorldToCell(p);
                if (!roads.HasTile(cell) || rocks.HasTile(rocks.WorldToCell(p))) throw new Exception("ENEMY_MOVEMENT_FAILED: actual route leaves roads or enters rocks");
                if (i > 0 && Math.Abs(cell.x - previous.x) + Math.Abs(cell.y - previous.y) != 1) throw new Exception("ENEMY_MOVEMENT_FAILED: actual route cuts a road corner");
                previous = cell; total++;
            }
        }
        Expect(total > 100, "all actual route points follow adjacent road centers: " + total);
    }
    static void ClearFixtureEnemies()
    {
        var living = (List<GameObject>)typeof(EnemyAudioTestSpawner).GetField("livingEnemies", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        var objects = living.ToArray(); living.Clear();
        foreach (var obj in objects) if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
    }
    static void RoadSpawnerFixture()
    {
        Fixture(); ClearFixtureEnemies(); UnityEngine.Object.DestroyImmediate(route.gameObject);
        var grid = new GameObject("Grid", typeof(Grid)).GetComponent<Grid>();
        var roads = new GameObject("Paths", typeof(Tilemap)).GetComponent<Tilemap>(); roads.transform.SetParent(grid.transform, false);
        var tile = ScriptableObject.CreateInstance<Tile>();
        foreach (var cell in new[] { Vector3Int.zero, Vector3Int.right, Vector3Int.right * 2, new Vector3Int(2, 1, 0), new Vector3Int(2, 2, 0) }) roads.SetTile(cell, tile);
        route = new GameObject("Road route", typeof(EnemyTestRoute)).GetComponent<EnemyTestRoute>();
        var spawn = new GameObject("SpawnPoint").transform; spawn.position = roads.GetCellCenterWorld(Vector3Int.zero);
        var goal = new GameObject("Goal").transform; goal.position = roads.GetCellCenterWorld(new Vector3Int(2, 2, 0));
        var fields = new SerializedObject(route);
        fields.FindProperty("roadTiles").objectReferenceValue = roads;
        fields.FindProperty("entrance").objectReferenceValue = spawn;
        fields.FindProperty("goal").objectReferenceValue = goal;
        fields.FindProperty("rebuildFromRoadOnStart").boolValue = true; fields.ApplyModifiedPropertiesWithoutUndo();
        owner.spawnPoint = spawn; owner.route = route; owner.clock = clock;
        owner.enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy.prefab");
        Invoke(owner, "Start"); Invoke(owner, "Update");
    }
    static void ReviewChecks()
    {
        var failures = new List<string>();
        void Record(bool condition, string message)
        {
            if (condition) Expect(true, message);
            else { failures.Add(message); Debug.LogError("ENEMY_MOVEMENT_REVIEW_RED " + message); }
        }
        Fixture(new[] { Vector2.zero, Vector2.right * 100 });
        owner.unitsPerBeat = 12; Step(.25); Step(.5);
        Record(Mathf.Abs(mover.GetComponent<Rigidbody2D>().position.x - 3) < .025f, "short stall above physics displacement cap retains physical music phase");
        RoadSpawnerFixture(); Beat(4.005); Invoke(owner, "Update");
        owner.spawnPoint.position = new Vector3(2.5f, 1.5f); Beat(8.005); Invoke(owner, "Update");
        var living = (List<GameObject>)typeof(EnemyAudioTestSpawner).GetField("livingEnemies", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        var newest = living.Last().GetComponent<EnemyPathMover>();
        Record(newest.MoveDirection.y > .9f && Mathf.Abs(newest.MoveDirection.x) < .01f, "moving entrance around L road sends later enemy forward along road, not diagonally to stale origin");
        ClearFixtureEnemies(); RoadSpawnerFixture(); Beat(4.005); Invoke(owner, "Update");
        living = (List<GameObject>)typeof(EnemyAudioTestSpawner).GetField("livingEnemies", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        var existingEnemy = living.Single(); owner.spawnPoint.position = new Vector3(100, 100);
        Debug.Log("ENEMY_MOVEMENT_EXPECTED_INVALID_ENTRANCE"); Beat(8.005); Invoke(owner, "Update");
        Record(owner.LivingCount == 1 && existingEnemy.activeSelf && owner.enabled, "off-road live entrance skips new births while preserving existing enemies");
        ClearFixtureEnemies();
        if (failures.Count > 0) throw new Exception("ENEMY_MOVEMENT_FAILED review regressions: " + string.Join("; ", failures));
    }
    public static void RunReviewRegressions()
    {
        var previous = Physics2D.simulationMode; int result = 0;
        try { Physics2D.simulationMode = SimulationMode2D.Script; ReviewChecks(); Debug.Log("ENEMY_MOVEMENT_REVIEW_CHECK_SUCCESS"); }
        catch (Exception e) { Debug.LogException(e); result = 1; }
        finally { Physics2D.simulationMode = previous; }
        EditorApplication.Exit(result);
    }
    public static void VerifySavedScene()
    {
        try { MapChecks(); Debug.Log("ENEMY_MOVEMENT_SAVED_SCENE_PASS checks=" + checks); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    public static void RunBatch()
    {
        var previousSimulation = Physics2D.simulationMode;
        int exitCode = 0;
        try
        {
            EnemyMovementSetup.ConfigureProject(); Physics2D.simulationMode = SimulationMode2D.Script;
            GeometryChecks(); MovementChecks(); RoadChecks(); ReviewChecks(); MapChecks();
            Debug.Log("ENEMY_MOVEMENT_CHECK_SUCCESS checks=" + checks);
        }
        catch (Exception e) { Debug.LogException(e); exitCode = 1; }
        finally { Physics2D.simulationMode = previousSimulation; }
        EditorApplication.Exit(exitCode);
    }
    public static void SnapshotRed()
    {
        var previousSimulation = Physics2D.simulationMode;
        int exitCode = 0;
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Physics2D.simulationMode = SimulationMode2D.Script;
            var clock = new GameObject("Clock", typeof(AudioSource), typeof(MusicClock)).GetComponent<MusicClock>();
            var timeline = (MusicTimeline)typeof(MusicClock).GetField("timeline", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(clock);
            timeline.Start(AudioSettings.dspTime - .25 * .6, 100);
            var route = new GameObject("Route", typeof(EnemyTestRoute)).GetComponent<EnemyTestRoute>();
            foreach (var p in new[] { Vector3.zero, Vector3.right * 2, new Vector3(2, 2, 0) })
            { var point = new GameObject("Waypoint"); point.transform.SetParent(route.transform); point.transform.position = p; }
            var owner = new GameObject("Spawner", typeof(EnemyAudioTestSpawner)).GetComponent<EnemyAudioTestSpawner>(); owner.unitsPerBeat = 1;
            var mover = new GameObject("Walking enemy", typeof(Rigidbody2D), typeof(EnemyPathMover)).GetComponent<EnemyPathMover>();
            mover.Initialize(route, clock.CurrentBeat, clock, owner, Vector2.zero);
            route.transform.GetChild(1).position = Vector3.up * 2;
            timeline.Start(AudioSettings.dspTime - 1.25 * .6, 100);
            typeof(EnemyPathMover).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(mover, null);
            Physics2D.SyncTransforms(); Physics2D.Simulate(.02f);
            if (mover.GetComponent<Rigidbody2D>().position.x < .9f)
                throw new Exception("ENEMY_MOVEMENT_RED: editing a waypoint redirects an already spawned enemy away from its original road.");
            Debug.Log("ENEMY_MOVEMENT_SNAPSHOT_CHECK_PASSED");
        }
        catch (Exception e) { Debug.LogException(e); exitCode = 1; }
        finally { Physics2D.simulationMode = previousSimulation; }
        EditorApplication.Exit(exitCode);
    }
    public static void InspectAndRed()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Map.unity", OpenSceneMode.Single);
            var maps = UnityEngine.Object.FindObjectsOfType<Tilemap>();
            var roads = maps.Single(t => t.name == "Paths");
            var rocks = maps.Single(t => t.name == "Obstacle");
            var castle = UnityEngine.Object.FindObjectOfType<MusicClock>();
            Debug.Log("MOVEMENT_INSPECT castle=" + castle.transform.position + " roadGrid=" + roads.layoutGrid.cellSwizzle + " cell=" + roads.layoutGrid.cellSize);
            var remaining = new HashSet<Vector3Int>();
            int blocked = 0;
            foreach (var cell in roads.cellBounds.allPositionsWithin)
                if (roads.HasTile(cell))
                {
                    if (rocks.HasTile(rocks.WorldToCell(roads.GetCellCenterWorld(cell)))) blocked++;
                    else remaining.Add(cell);
                }
            var directions = new[] { Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left };
            int component = 0;
            while (remaining.Count > 0)
            {
                var componentCells = new HashSet<Vector3Int>();
                var queue = new Queue<Vector3Int>(); queue.Enqueue(remaining.First());
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue(); if (!remaining.Remove(cell)) continue;
                    componentCells.Add(cell);
                    foreach (var direction in directions) if (remaining.Contains(cell + direction)) queue.Enqueue(cell + direction);
                }
                var endpoints = componentCells.Where(c => directions.Count(d => componentCells.Contains(c + d)) <= 1).ToArray();
                Debug.Log("MOVEMENT_INSPECT component=" + component++ + " cells=" + componentCells.Count + " endpoints=" + string.Join("; ", endpoints.Select(c => c + " world=" + roads.GetCellCenterWorld(c))));
            }
            Debug.Log("MOVEMENT_INSPECT roads blocked by rocks=" + blocked);
            if (UnityEngine.Object.FindObjectOfType<EnemyAudioTestSpawner>() == null)
                throw new Exception("ENEMY_MOVEMENT_RED: latest Map has no enemy route/spawner; new Minion art cannot enter and walk to the base.");
            Debug.Log("ENEMY_MOVEMENT_RED_CHECK_PASSED"); EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}

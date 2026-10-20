using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Linq;
using TapTapGameJam.Combat;

public static class BuildPlacementChecks
{
    static Tile tile;
    static Tilemap ground;
    static BuildPlacementValidator validator;
    static Camera camera;
    static BuildFootprint site;
    static int checks;

    static void Expect(bool value, string message)
    {
        if (!value) throw new Exception("BUILD_PLACEMENT_FAILED: " + message);
        checks++; Debug.Log("BUILD_PASS " + message);
    }
    static void Failure(BuildFailure failure, string message) => Expect(validator.Validate(site, camera).failure == failure, message);
    static Tilemap Map(string name, Transform parent)
    {
        var obj = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        obj.transform.SetParent(parent, false); return obj.GetComponent<Tilemap>();
    }
    static void Fill(Tilemap map)
    {
        for (int y = -5; y < 8; y++) for (int x = -5; x < 8; x++) map.SetTile(new Vector3Int(x, y, 0), tile);
    }
    static void Fixture(GridLayout.CellSwizzle swizzle = GridLayout.CellSwizzle.XYZ)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        tile = ScriptableObject.CreateInstance<Tile>();
        var grid = new GameObject("Grid", typeof(Grid)).GetComponent<Grid>(); grid.cellSwizzle = swizzle;
        ground = Map("Ground level1", grid.transform); Fill(ground);
        camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>(); camera.tag = "MainCamera";
        camera.orthographic = true; camera.orthographicSize = 4; camera.aspect = 1; camera.transform.position = new Vector3(1, 1, -10);
        validator = new GameObject("BuildManager").AddComponent<BuildPlacementValidator>(); validator.RefreshSurfaces();
        site = new BuildFootprint { surface = ground, anchor = Vector3Int.zero };
    }
    static BuildSlot Slot(BuildFootprint footprint)
    {
        var obj = new GameObject("Check slot", typeof(BoxCollider2D)); obj.GetComponent<BoxCollider2D>().size = new Vector2(2, 2);
        var slot = obj.AddComponent<BuildSlot>(); slot.ConfigurePlacement(validator, footprint); return slot;
    }
    static BuildSelection Selection()
    {
        var selection = validator.gameObject.AddComponent<BuildSelection>();
        var fields = new SerializedObject(selection);
        fields.FindProperty("worldCamera").objectReferenceValue = camera;
        fields.FindProperty("placementValidator").objectReferenceValue = validator;
        fields.ApplyModifiedPropertiesWithoutUndo(); return selection;
    }
    static void TerrainChecks()
    {
        Fixture(); Expect(validator.Validate(site, camera).Allowed, "complete same-level footprint is allowed");
        for (int level = 1; level <= 4; level++)
        {
            ground.name = "Ground level" + level; validator.RefreshSurfaces();
            Expect(validator.Validate(site, camera).Allowed, "Ground level " + level + " is supported");
        }
        foreach (string name in new[] { "Ground", "Ground bottom", "Ground level0", "Ground level5", "Water" })
        {
            ground.name = name; validator.RefreshSurfaces(); Failure(BuildFailure.MissingGround, name + " is rejected");
        }
        ground.name = "Ground level1"; validator.RefreshSurfaces();
        for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++)
        {
            var cell = new Vector3Int(x, y, 0); ground.SetTile(cell, null);
            Failure(BuildFailure.MissingGround, "missing cell " + cell + " rejects entire footprint"); ground.SetTile(cell, tile);
        }
        var higher = Map("Ground level2", ground.transform.parent); higher.SetTile(Vector3Int.one - Vector3Int.forward, tile); validator.RefreshSurfaces();
        Failure(BuildFailure.MixedGroundLevels, "four tiles across different levels cannot combine");
        UnityEngine.Object.DestroyImmediate(higher.gameObject);
        site.anchor = new Vector3Int(-3, -3, 0); camera.transform.position = site.Center + Vector3.back * 10;
        Expect(validator.Validate(site, camera).Allowed, "negative cells work");
        Fixture(GridLayout.CellSwizzle.YXZ);
        Expect(validator.Validate(site, camera).Allowed, "actual scene YXZ grid swizzle works");
    }
    static void ViewChecks()
    {
        Fixture(); camera.orthographicSize = 2; camera.transform.position = new Vector3(-.25f, 1, -10);
        Expect(camera.WorldToViewportPoint(site.CellCenter(1, 1)).x < 1, "edge test has visible tile centers");
        Failure(BuildFailure.OutsideView, "visible centers cannot hide partly offscreen cell edges");
        camera.transform.position = new Vector3(1, 1, -10); camera.orthographicSize = 1;
        Expect(validator.Validate(site, camera).Allowed, "full footprint exactly at viewport edges is allowed");
        camera.orthographicSize = .99f; Failure(BuildFailure.OutsideView, "small zoom clipping any border is rejected");
        camera.orthographicSize = 3; camera.rect = new Rect(.2f, .1f, .5f, .7f); camera.aspect = 1;
        Expect(validator.Validate(site, camera).Allowed, "letterboxed camera viewport works");
        camera.transform.position = new Vector3(1, 1, 10); Failure(BuildFailure.OutsideView, "footprint behind camera is rejected");
    }
    static void RangeChecks()
    {
        Fixture();
        var obj = new GameObject("AttackRange", typeof(BoxCollider2D), typeof(TowerEnemyDetector));
        var range = obj.GetComponent<BoxCollider2D>(); range.isTrigger = true; range.size = new Vector2(.2f, .2f);
        obj.transform.position = new Vector3(2.05f, 1, 0);
        Failure(BuildFailure.TowerRange, "partial range intersection blocks even when cell centers are outside");
        obj.transform.position = new Vector3(1, 1, 0); range.size = new Vector2(.1f, .1f);
        Failure(BuildFailure.TowerRange, "range entirely inside footprint blocks");
        obj.transform.position = new Vector3(4, 4, 0);
        Expect(validator.Validate(site, camera).Allowed, "range outside footprint does not block");
        obj.transform.position = new Vector3(2.1f, 1, 0); range.size = new Vector2(.2f, .2f);
        Failure(BuildFailure.TowerRange, "attack range boundary contact is conservatively rejected");
    }
    static void OccupancyChecks()
    {
        Fixture();
        var rock = new GameObject("Ground child rock", typeof(BoxCollider2D)); rock.transform.SetParent(ground.transform);
        rock.transform.position = new Vector3(.4f, .4f); rock.GetComponent<BoxCollider2D>().size = new Vector2(.2f, .2f);
        Failure(BuildFailure.Occupied, "partial obstacle collider occupies whole 2x2 placement");
        UnityEngine.Object.DestroyImmediate(rock);
        rock = new GameObject("Ground child prop", typeof(SpriteRenderer)); rock.transform.SetParent(ground.transform);
        var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
        rock.GetComponent<SpriteRenderer>().sprite = sprite; rock.transform.position = new Vector3(1.5f, 1.5f);
        Failure(BuildFailure.Occupied, "ground child sprite blocks without a collider"); UnityEngine.Object.DestroyImmediate(rock); UnityEngine.Object.DestroyImmediate(sprite);
        var obstacle = new GameObject("Explicit obstacle", typeof(BuildObstacle)); obstacle.transform.position = new Vector3(1, 1);
        Failure(BuildFailure.Occupied, "explicit obstacle marker blocks outside Ground hierarchy"); UnityEngine.Object.DestroyImmediate(obstacle);
        var paths = Map("Paths", ground.transform.parent); paths.SetTile(Vector3Int.zero, tile); validator.RefreshSurfaces();
        Failure(BuildFailure.Occupied, "road overlay occupies ground"); paths.SetTile(Vector3Int.zero, null);
        var childMap = Map("Ground obstacles", ground.transform); childMap.SetTile(new Vector3Int(1, 0, 0), tile); validator.RefreshSurfaces();
        Failure(BuildFailure.Occupied, "obstacle tilemap under ground blocks"); childMap.ClearAllTiles();
        var surfaceCollider = ground.gameObject.AddComponent<TilemapCollider2D>();
        Expect(validator.Validate(site, camera).Allowed, "ground surface collider is not itself an obstacle");
        UnityEngine.Object.DestroyImmediate(surfaceCollider);
        var slot = Slot(site); var template = new GameObject("Check tower template");
        Expect(slot.TryBuild(template), "direct slot build succeeds on valid footprint");
        Failure(BuildFailure.Occupied, "built tower occupies footprint without needing an attack range");
        Expect(!slot.TryBuild(template), "same slot cannot build twice");
        Expect(slot.TryDestroyTower(), "demolition succeeds");
        Expect(validator.Validate(site, camera).Allowed, "demolition immediately releases footprint");
        Expect(slot.TryBuild(template), "released footprint can be rebuilt");
    }
    static void SelectionChecks()
    {
        Fixture(); var selection = Selection(); var template = new GameObject("Check tower template");
        Expect(selection.SelectSite(site), "valid site can open menu");
        camera.transform.position += Vector3.right * 100;
        Expect(!selection.TryBuildSelected(template), "moving camera invalidates selected site at final build");
        camera.transform.position -= Vector3.right * 100;
        Expect(selection.SelectSite(site), "site can be selected again after camera returns");
        var rock = new GameObject("New obstacle", typeof(BuildObstacle)); rock.transform.position = site.Center;
        Expect(!selection.TryBuildSelected(template), "new obstacle invalidates stale selected site"); UnityEngine.Object.DestroyImmediate(rock);
        Expect(selection.TryBuildSelected(template), "existing menu path builds on free ground");
        Expect(!selection.SelectSite(site), "second footprint cannot overlap newly built tower");
        Fixture();
        var outside = new GameObject("Slot outside allowed ground").AddComponent<BuildSlot>(); outside.transform.position = new Vector3(100, 100, 0);
        template = new GameObject("Check tower template");
        Expect(!outside.TryBuild(template), "original RED case: direct non-Ground construction is rejected");
    }
    static void SceneChecks()
    {
        BuildPlacementSetup.ConfigureProjectScenes();
        foreach (string path in new[] { "Assets/Scenes/Map.unity", "Assets/Scenes/MusicTest.unity" })
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var selection = UnityEngine.Object.FindObjectOfType<BuildSelection>();
            validator = selection.GetComponent<BuildPlacementValidator>(); camera = selection.WorldCamera;
            var maps = UnityEngine.Object.FindObjectsOfType<Tilemap>().Where(m => BuildPlacementValidator.GroundLevel(m) > 0).ToArray();
            Expect(maps.Length == (path.Contains("MusicTest") ? 1 : 4), "correct authored ground levels in " + path);
            Expect(validator != null && camera != null, "build manager and camera references in " + path);
            var fields = new SerializedObject(selection);
            Expect(fields.FindProperty("placementStatus").objectReferenceValue != null, "saved editable construction status UI in " + path);
            int allowed = 0;
            foreach (var map in maps)
            foreach (var cell in map.cellBounds.allPositionsWithin)
            {
                if (!map.HasTile(cell)) continue;
                var candidate = new BuildFootprint { surface = map, anchor = cell };
                if (validator.Validate(candidate, camera).Allowed) allowed++;
            }
            Expect(allowed > 0, "actual camera has legal free 2x2 sites in " + path + ": " + allowed);
        }
    }
    static void CapacityChecks()
    {
        Fixture(); camera.orthographicSize = 10;
        var selection = Selection();
        Expect(selection.MaxTowerCount == 4 && selection.AvailableTowerCount == 4,
            "world budget starts with four available towers");
        var templates = new[] { new GameObject("Bell template"), new GameObject("Chord template"), new GameObject("Duduk template") };
        var anchors = new[] { new Vector3Int(0, 0, 0), new Vector3Int(3, 0, 0), new Vector3Int(0, 3, 0), new Vector3Int(3, 3, 0), new Vector3Int(-3, -3, 0) };
        var slots = anchors.Select(a => Slot(new BuildFootprint { surface = ground, anchor = a })).ToArray();
        Expect(!slots[0].TryBuild(null) && selection.AvailableTowerCount == 4,
            "failed construction does not consume a tower slot");
        for (int i = 0; i < 4; i++)
        {
            Expect(slots[i].TryBuild(templates[i % templates.Length]), "mixed instrument build " + (i + 1));
            Expect(selection.BuiltTowerCount == i + 1 && selection.AvailableTowerCount == 3 - i,
                "each built tower consumes exactly one shared slot");
        }
        Expect(!slots[4].TryBuild(templates[0]) && !selection.CanBuildMore,
            "direct BuildSlot calls cannot exceed the shared limit");
        Expect(selection.SelectSite(new BuildFootprint { surface = ground, anchor = anchors[4] }),
            "a legal site can still be inspected at capacity");
        Expect(!selection.TryBuildSelected(templates[1]) && selection.BuiltTowerCount == 4,
            "menu construction also rejects a fifth tower");
        for (int repeat = 0; repeat < 8; repeat++)
        {
            Expect(slots[0].TryDestroyTower() && selection.AvailableTowerCount == 1,
                "demolition immediately restores capacity");
            Expect(!slots[0].TryDestroyTower() && selection.AvailableTowerCount == 1,
                "repeated demolition cannot grant extra capacity");
            Expect(slots[0].TryBuild(templates[repeat % templates.Length]) && selection.AvailableTowerCount == 0,
                "released capacity can be reused for any instrument repeatedly");
        }
        foreach (var slot in slots) slot.TryDestroyTower();
        Expect(selection.BuiltTowerCount == 0 && selection.AvailableTowerCount == 4,
            "level cleanup restores all four slots");
    }
    public static void RunBatch()
    {
        try
        {
            TerrainChecks(); ViewChecks(); RangeChecks(); OccupancyChecks(); SelectionChecks(); CapacityChecks(); SceneChecks();
            Debug.Log("BUILD_PLACEMENT_CHECK_SUCCESS checks=" + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }
}

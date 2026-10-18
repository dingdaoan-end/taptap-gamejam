using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class MapObstacleIntegrationChecks
{
    static int checks;
    static void Expect(bool condition, string message)
    {
        if (!condition) throw new Exception("MAP_ROCK_FAILED: " + message);
        checks++; Debug.Log("MAP_ROCK_PASS " + message);
    }
    public static void RunBatch()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Map.unity", OpenSceneMode.Single);
            BuildPlacementSetup.ConfigureCurrentScene();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            var maps = UnityEngine.Object.FindObjectsOfType<Tilemap>();
            var rocks = maps.Single(t => t.name == "Obstacle");
            var grounds = maps.Where(t => BuildPlacementValidator.GroundLevel(t) > 0).ToArray();
            var selection = UnityEngine.Object.FindObjectOfType<BuildSelection>();
            var validator = selection.GetComponent<BuildPlacementValidator>();
            var blockers = new SerializedObject(validator).FindProperty("blockedTilemaps");
            bool bound = false;
            for (int i = 0; i < blockers.arraySize; i++) if (blockers.GetArrayElementAtIndex(i).objectReferenceValue == rocks) bound = true;
            Expect(bound, "new Obstacle tilemap is saved in the validator's blocking references");
            Expect(rocks.GetComponent<Collider2D>() == null, "authored rocks have no collider; placement must read tiles");
            var rockCells = new System.Collections.Generic.List<Vector3Int>();
            foreach (var cell in rocks.cellBounds.allPositionsWithin) if (rocks.HasTile(cell)) rockCells.Add(cell);
            var cells = rockCells.ToArray();
            Expect(cells.Length > 0, "remote map contains rock cells: " + cells.Length);
            var camera = new GameObject("Rock placement verification camera", typeof(Camera)).GetComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 8; camera.aspect = 1;
            int tested = 0;
            foreach (var rock in cells)
            foreach (var ground in grounds)
            {
                var center = rocks.GetCellCenterWorld(rock);
                var anchor = ground.WorldToCell(center);
                for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++)
                {
                    var footprint = new BuildFootprint { surface = ground, anchor = anchor - new Vector3Int(x, y, 0) };
                    camera.transform.position = footprint.Center + Vector3.back * 10;
                    if (validator.Validate(footprint, camera).Allowed)
                        throw new Exception("MAP_ROCK_FAILED: tower can overlap rock " + rock + " on " + ground.name);
                    tested++;
                }
            }
            Expect(tested == cells.Length * grounds.Length * 4, "all rock cells reject every covering 2x2 anchor: " + tested);

            BuildFootprint free = default; bool found = false;
            foreach (var ground in grounds)
            {
                foreach (var cell in ground.cellBounds.allPositionsWithin)
                {
                    if (!ground.HasTile(cell)) continue;
                    var candidate = new BuildFootprint { surface = ground, anchor = cell };
                    camera.transform.position = candidate.Center + Vector3.back * 10;
                    if (!validator.Validate(candidate, camera).Allowed) continue;
                    free = candidate; found = true; break;
                }
                if (found) break;
            }
            Expect(found, "free same-level ground remains buildable on the new map");
            camera.transform.position = free.Center + Vector3.back * 10;
            var tile = rocks.GetTile(cells[0]);
            for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++)
            {
                var cell = rocks.WorldToCell(free.CellCenter(x, y));
                Expect(!rocks.HasTile(cell), "free footprint has no rock in corner " + x + "," + y);
                rocks.SetTile(cell, tile);
                Expect(validator.Validate(free, camera).failure == BuildFailure.Occupied, "painting one rock into corner blocks entire footprint " + x + "," + y);
                rocks.SetTile(cell, null);
                Expect(validator.Validate(free, camera).Allowed, "erasing that rock immediately releases footprint " + x + "," + y);
            }
            var selectionFields = new SerializedObject(selection);
            selectionFields.FindProperty("worldCamera").objectReferenceValue = camera;
            selectionFields.ApplyModifiedPropertiesWithoutUndo();
            typeof(BuildSelection).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(selection, null);
            var label = (UnityEngine.UI.Text)selectionFields.FindProperty("placementStatus").objectReferenceValue;
            Expect(label != null && !label.gameObject.activeInHierarchy && label.text == string.Empty, "construction status text remains hidden");
            Expect(selection.SelectSite(free), "existing menu can select free ground on new map");
            var blockedCell = rocks.WorldToCell(free.CellCenter(1, 1)); rocks.SetTile(blockedCell, tile);
            var tower = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tower_Bell.prefab");
            Expect(!selection.TryBuildSelected(tower), "painting rock after menu selection prevents final construction");
            rocks.SetTile(blockedCell, null);
            Expect(selection.TryBuildSelected(tower), "erasing rock allows original menu to build the combat-enabled tower");
            var built = UnityEngine.Object.FindObjectOfType<TapTapGameJam.Combat.TowerBeatAttack>();
            Expect(built != null, "tower built on integrated map retains beat attacks");
            Expect(!label.gameObject.activeInHierarchy && label.text == string.Empty, "successful construction also keeps status text hidden");
            Debug.Log("MAP_ROCK_CHECK_SUCCESS checks=" + checks + " rocks=" + cells.Length + " footprints=" + tested);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    public static void RunRed()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Map.unity", OpenSceneMode.Single);
            if (!UnityEngine.Object.FindObjectsOfType<Tilemap>().Any(t => t.name == "Obstacle"))
                throw new Exception("MAP_ROCK_RED: local Map does not include the latest GitHub Obstacle tilemap.");
            Debug.Log("MAP_ROCK_RED_CHECK_PASSED"); EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}

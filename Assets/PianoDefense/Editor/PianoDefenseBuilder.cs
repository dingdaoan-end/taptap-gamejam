using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TapTapGameJam.PianoDefense.Editor
{
    public static class PianoDefenseBuilder
    {
        public const string ScenePath = "Assets/Scenes/PianoDefense.unity";

        [MenuItem("Tools/Game Jam/Open Piano Defense Prototype")]
        public static void OpenPrototype()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else CreateScene();
        }

        public static void CreateScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 5;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.063f, 0.09f, 0.133f);
            camera.transform.position = new Vector3(0, 0, -10);
            camera.gameObject.AddComponent<AudioListener>();
            var root = new GameObject("Piano Defense");
            root.AddComponent<PianoAudio>(); root.AddComponent<PianoDefenseGame>(); root.AddComponent<PianoDefenseView>();
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            CreateEditableBoard();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("PIANO_SCENE_READY " + ScenePath);
        }

        [MenuItem("Tools/Game Jam/Create Editable Board In Scene")]
        public static void CreateEditableBoard()
        {
            var game = UnityEngine.Object.FindObjectOfType<PianoDefenseGame>();
            if (game == null) throw new Exception("Open the PianoDefense scene first.");
            if (game.Board != null) { Selection.activeGameObject = game.Board.gameObject; return; }

            const string folder = "Assets/PianoDefense/SceneArt";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/PianoDefense", "SceneArt");
            Material ground = GetMaterial(folder + "/Ground.mat", new Color(.10f, .15f, .21f));
            Material lane = GetMaterial(folder + "/Lane.mat", new Color(.17f, .23f, .31f));
            Material baseMat = GetMaterial(folder + "/Base.mat", new Color(.14f, .38f, .32f));
            Material spawn = GetMaterial(folder + "/Spawn.mat", new Color(.40f, .22f, .27f));
            Material towerMat = GetMaterial(folder + "/Tower.mat", new Color(.95f, .77f, .47f));
            Material white = GetMaterial(folder + "/PianoWhite.mat", new Color(.92f, .94f, .93f));
            Material black = GetMaterial(folder + "/PianoBlack.mat", new Color(.07f, .10f, .15f));
            Material enemyMat = GetMaterial(folder + "/Enemy.mat", new Color(.95f, .55f, .60f));

            var towerPrefab = GetTowerPrefab(folder + "/PianoTower.prefab", towerMat, white, black);
            var enemyPrefab = GetEnemyPrefab(folder + "/NoteEnemy.prefab", enemyMat);

            var boardObject = new GameObject("Editable Board");
            Undo.RegisterCreatedObjectUndo(boardObject, "Create editable board");
            boardObject.transform.SetParent(game.transform, false);
            var board = boardObject.AddComponent<PianoBoardScene>();
            var tiles = new GameObject("Tiles");
            tiles.transform.SetParent(boardObject.transform, false);
            for (int y = 0; y < DefenseSimulation.Height; y++)
            {
                var row = new GameObject("Row " + y);
                row.transform.SetParent(tiles.transform, false);
                for (int x = 0; x < DefenseSimulation.Width; x++)
                {
                    Material material = x == 0 ? baseMat : x == DefenseSimulation.Width - 1 ? spawn : DefenseSimulation.IsLane(y) ? lane : ground;
                    MakeCube("Cell " + x + "," + y, row.transform, PianoBoardScene.CellPosition(x, y), new Vector3(.96f, .96f, .06f), material);
                }
            }
            var towers = new GameObject("Starting Towers");
            towers.transform.SetParent(boardObject.transform, false);
            var layout = new List<StartingTower>(game.StartingTowers);
            foreach (var entry in layout)
            {
                if (!DefenseSimulation.IsBuildCell(entry.X, entry.Y)) continue;
                var tower = (GameObject)PrefabUtility.InstantiatePrefab(towerPrefab);
                tower.name = "Piano Tower " + entry.X + "," + entry.Y;
                tower.transform.SetParent(towers.transform, false);
                tower.transform.localPosition = PianoBoardScene.CellPosition(entry.X, entry.Y, -.18f);
                var authoring = tower.AddComponent<PianoTowerAuthoring>();
                var authoringData = new SerializedObject(authoring);
                authoringData.FindProperty("pitch").intValue = entry.Pitch;
                authoringData.FindProperty("mode").enumValueIndex = (int)entry.Mode;
                authoringData.ApplyModifiedPropertiesWithoutUndo();
            }

            var camera = Camera.main;
            if (camera == null) throw new Exception("Main Camera is missing.");
            camera.orthographic = true;
            camera.orthographicSize = 3.5f;
            camera.transform.position = new Vector3(0, 0, -10);
            var boardData = new SerializedObject(board);
            boardData.FindProperty("towerPrefab").objectReferenceValue = towerPrefab;
            boardData.FindProperty("enemyPrefab").objectReferenceValue = enemyPrefab;
            boardData.FindProperty("boardCamera").objectReferenceValue = camera;
            boardData.ApplyModifiedPropertiesWithoutUndo();
            var gameData = new SerializedObject(game);
            gameData.FindProperty("board").objectReferenceValue = board;
            gameData.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = boardObject;
            Debug.Log("PIANO_EDITABLE_BOARD_READY cells=84 towers=" + towers.transform.childCount);
        }

        static Material GetMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Unlit/Color"));
            material.color = color;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static GameObject MakeCube(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = size;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        static GameObject GetTowerPrefab(string path, Material body, Material white, Material black)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) return prefab;
            var prototype = MakeCube("Piano Tower", null, Vector3.zero, new Vector3(.72f, .70f, .15f), body);
            MakeCube("Keyboard", prototype.transform, new Vector3(0, -.06f, -.65f), new Vector3(.60f, .32f, .04f), white);
            for (int i = 0; i < 4; i++)
                MakeCube("Black Key " + i, prototype.transform, new Vector3(-.21f + i * .14f, .02f, -.9f), new Vector3(.06f, .18f, .04f), black);
            prefab = PrefabUtility.SaveAsPrefabAsset(prototype, path);
            UnityEngine.Object.DestroyImmediate(prototype);
            return prefab;
        }

        static GameObject GetEnemyPrefab(string path, Material material)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) return prefab;
            var prototype = MakeCube("Note Enemy", null, Vector3.zero, new Vector3(.46f, .46f, .16f), material);
            prefab = PrefabUtility.SaveAsPrefabAsset(prototype, path);
            UnityEngine.Object.DestroyImmediate(prototype);
            return prefab;
        }

        [MenuItem("Tools/Game Jam/Build Piano Defense for Windows")]
        public static void BuildWindows()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            PianoDefenseChecks.Run();
            if (!File.Exists(ScenePath)) CreateScene();
            PlayerSettings.companyName = "GameJamTeam";
            PlayerSettings.productName = "Nocturne Piano Defense";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 1000;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            string path = "Builds/PianoDefense-v0.2/NocturnePiano.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = path,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Piano build failed: " + report.summary.result + ", errors=" + report.summary.totalErrors);
            Debug.Log("PIANO_BUILD_SUCCESS " + Path.GetFullPath(path) + " bytes=" + report.summary.totalSize);
        }
    }
}

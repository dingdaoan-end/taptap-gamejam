using System;
using System.IO;
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
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("PIANO_SCENE_READY " + ScenePath);
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

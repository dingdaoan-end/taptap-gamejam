using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Integration smoke: catch a missing/blocked menu button or an incorrect scene transition.
public static class MemuSceneChecks
{
    const string Key = "Memu.PlayCheck";
    static double began;
    static int stage;
    static string failure;
    static int firstMusicSample;
    static double quitClickedAt;
    static float quitSoundLength;
    static bool heardQuit;

    public static void RunBatch()
    {
        if (!File.Exists("Assets/Scenes/Memu.unity"))
        {
            Debug.LogError("MEMU_CHECK_FAIL: Memu scene does not exist yet.");
            EditorApplication.Exit(1);
            return;
        }
        EditorSceneManager.OpenScene("Assets/Scenes/Memu.unity");
        SessionState.SetBool(Key, true);
        Resume();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        began = EditorApplication.timeSinceStartup;
        stage = 0;
        failure = null;
        firstMusicSample = -1;
        heardQuit = false;
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
    }

    static void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            failure = message;
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static Button Button(string name) => UnityEngine.Object.FindObjectsOfType<Button>().Single(b => b.name == name);

    static void Poll()
    {
        try
        {
            if (failure != null) throw new Exception(failure);
            if (EditorApplication.timeSinceStartup - began > 60) throw new Exception("Menu flow timed out at stage " + stage);
            if (!EditorApplication.isPlaying || Time.frameCount < 5) return;
            if (stage == 0)
            {
                Require(SceneManager.GetActiveScene().name == "Memu", "Wrong initial scene");
                var music = UnityEngine.Object.FindObjectsOfType<AudioSource>().Single(s => s.name == "MenuMusic");
                Require(music.clip != null && music.clip.name == "LV1_Full Song" && music.loop && music.playOnAwake && music.spatialBlend == 0, "Requested looping menu music missing");
                if (!music.isPlaying) return;
                if (firstMusicSample < 0) { firstMusicSample = music.timeSamples; return; }
                if (music.timeSamples - firstMusicSample < music.clip.frequency / 20) return;
                var canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
                var scaler = canvas.GetComponent<CanvasScaler>();
                Require(scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize && scaler.referenceResolution == new Vector2(1280, 720), "Menu must scale from 1280x720");
                Require(EditorBuildSettings.scenes.First(s => s.enabled).path == "Assets/Scenes/Memu.unity" && Application.CanStreamedLevelBeLoaded("Map"), "Menu/Map missing from launch build settings");
                var start = Button("StartGameButton");
                var quit = Button("QuitGameButton");
                Require(start.image.sprite != null && quit.image.sprite != null, "Button lettering sprites missing");
                Require(start.onClick.GetPersistentEventCount() == 1 && quit.onClick.GetPersistentEventCount() == 1, "Authored button actions missing");
                Require(EventSystem.current != null, "Menu input EventSystem missing");
                Capture(canvas);
                var corners = new Vector3[4];
                ((RectTransform)start.transform).GetWorldCorners(corners);
                var point = RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[2]) / 2);
                var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
                var hits = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                Require(hits.Count > 0 && hits[0].gameObject == start.gameObject, "Start button blocked by another UI image");
                var click = UnityEngine.Object.FindObjectsOfType<AudioSource>().SingleOrDefault(s => s.name == "MenuClickSound");
                Require(click != null && click.clip != null, "Menu click sound missing");
                ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Require(SceneManager.GetActiveScene().name == "Memu" && click.isPlaying && !start.interactable && !quit.interactable, "Start must play its sound before loading and reject duplicate clicks");
                stage = 1;
                return;
            }
            if (stage == 1)
            {
                if (SceneManager.GetActiveScene().name != "Map") return;
                Require(UnityEngine.Object.FindObjectsOfType<Canvas>().All(c => c.name != "MemuCanvas"), "Menu leaked into Map");
                Require(UnityEngine.Object.FindObjectsOfType<AudioSource>().All(s => s.name != "MenuMusic"), "Menu music leaked into Map");
                Require(UnityEngine.Object.FindObjectsOfType<AudioSource>().All(s => s.name != "MenuClickSound"), "Menu click sound leaked into Map");
                Require(Camera.main != null, "Map camera did not initialize");
                Require(UnityEngine.Object.FindObjectsOfType<AudioListener>().Count(a => a.isActiveAndEnabled) == 1, "Scene transition left duplicate audio listeners");
                Debug.Log("MEMU_START_PASS: real pointer click loaded Map and removed menu.");
                SceneManager.LoadScene("Memu");
                stage = 2;
                return;
            }
            if (stage == 2)
            {
                if (SceneManager.GetActiveScene().name != "Memu") return;
                var quit = Button("QuitGameButton");
                var corners = new Vector3[4];
                ((RectTransform)quit.transform).GetWorldCorners(corners);
                var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[2]) / 2), button = PointerEventData.InputButton.Left };
                stage = 3;
                var click = UnityEngine.Object.FindObjectsOfType<AudioSource>().Single(s => s.name == "MenuClickSound");
                quitClickedAt = EditorApplication.timeSinceStartup;
                quitSoundLength = click.clip.length;
                ExecuteEvents.Execute(quit.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Require(EditorApplication.isPlaying && click.isPlaying && !quit.interactable, "Quit must play its sound before exiting");
                return;
            }
            if (stage == 3)
            {
                var click = UnityEngine.Object.FindObjectsOfType<AudioSource>().SingleOrDefault(s => s.name == "MenuClickSound");
                heardQuit |= click != null && click.isPlaying && click.timeSamples > 0;
            }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }

    [InitializeOnLoadMethod]
    static void WatchExit()
    {
        EditorApplication.playModeStateChanged -= OnPlayState;
        EditorApplication.playModeStateChanged += OnPlayState;
    }

    static void OnPlayState(PlayModeStateChange state)
    {
        if (SessionState.GetBool(Key, false) && stage == 3 && state == PlayModeStateChange.ExitingPlayMode)
            Finish(heardQuit && EditorApplication.timeSinceStartup - quitClickedAt >= quitSoundLength,
                "Menu BGM, start/quit click playback before action, input guard, Map load and audio cleanup checked.");
    }

    static void Capture(Canvas canvas)
    {
        // Render the authored UI at its reference size without changing the saved scene.
        var camera = Camera.main;
        var target = RenderTexture.GetTemporary(1280, 720, 24);
        var oldMode = canvas.renderMode;
        var oldCamera = canvas.worldCamera;
        var oldTarget = camera.targetTexture;
        var oldActive = RenderTexture.active;
        Texture2D image = null;
        try
        {
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Artifacts/Memu");
            File.WriteAllBytes("Artifacts/Memu/menu-1280x720.png", image.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            RenderTexture.ReleaseTemporary(target);
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            Canvas.ForceUpdateCanvases();
        }
    }

    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Poll;
        Application.logMessageReceived -= OnLog;
        if (success) Debug.Log("MEMU_CHECK_PASS: " + message);
        else Debug.LogError("MEMU_CHECK_FAIL: " + message);
        EditorApplication.Exit(success ? 0 : 1);
    }
}

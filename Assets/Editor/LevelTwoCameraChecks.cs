using System;
using System.Linq;
using TapTapGameJam.Combat;
using TapTapGameJam.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class LevelTwoCameraChecks
{
    const string Key = "LevelTwoCamera.Play";
    static double began;
    static int lastFrame;
    static int stage;
    static double pausedAt;

    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void CheckFit(int width, int height, Rect expected)
    {
        var method = typeof(FixedWorldCameraFrame).GetMethod("FitViewport", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var actual = (Rect)method.Invoke(null, new object[] { new Rect(0, 0, 1, 1), (float)width, (float)height, 25f / 15 });
        Require(Vector4.Distance(new Vector4(actual.x, actual.y, actual.width, actual.height),
            new Vector4(expected.x, expected.y, expected.width, expected.height)) < .0001f, "Incorrect display fit for " + width + "x" + height);
    }

    static void CheckCoverage(Camera camera, int level)
    {
        float height = camera.orthographicSize * 2;
        float width = height * camera.aspect;
        Require(Mathf.Abs(width - 25) < .02f && Mathf.Abs(height - 15) < .02f, "Level " + level + " must cover exactly 25x15, actual=" + width + "x" + height);
        var expectedCenter = level == 2 ? new Vector2(24.5f, .5f) : level == 3 ? new Vector2(24.5f, -14.5f) : new Vector2(-.5f, -14.5f);
        Require(Vector2.Distance(camera.transform.position, expectedCenter) < .001f, "Incorrect region center for level " + level);
        var bottomLeft = camera.ViewportToWorldPoint(new Vector3(0, 0, 10));
        var topRight = camera.ViewportToWorldPoint(new Vector3(1, 1, 10));
        Require(Vector2.Distance(bottomLeft, expectedCenter - new Vector2(12.5f, 7.5f)) < .001f &&
            Vector2.Distance(topRight, expectedCenter + new Vector2(12.5f, 7.5f)) < .001f, "Camera must not reveal neighboring cells");
    }

    static void CaptureActual(Camera camera, int level)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var viewport = camera.rect;
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var target = RenderTexture.GetTemporary(1250, 750, 24);
        var image = new Texture2D(1250, 750, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.rect = new Rect(0, 0, 1, 1);
            camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1250, 750), 0, 0); image.Apply();
            System.IO.Directory.CreateDirectory("Artifacts/MergeCamera");
            System.IO.File.WriteAllBytes("Artifacts/MergeCamera/level" + level + "-actual.png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget; camera.rect = viewport; RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image);
        }
    }

    public static void RunBatch()
    {
        CheckFit(1920, 1080, new Rect(.03125f, 0, .9375f, 1));
        CheckFit(1024, 768, new Rect(0, .1f, 1, .8f));
        CheckFit(2500, 1500, new Rect(0, 0, 1, 1));
        CheckFit(1080, 1920, new Rect(0, .33125f, 1, .3375f));
        EditorSceneManager.OpenScene("Assets/Scenes/Map.unity", OpenSceneMode.Single);
        foreach (var map in UnityEngine.Object.FindObjectsOfType<Tilemap>())
        {
            if (map.name != "Paths" && !map.name.Contains("Ground")) continue;
            var cells = new System.Collections.Generic.List<Vector3>();
            foreach (var cell in map.cellBounds.allPositionsWithin) if (map.HasTile(cell)) cells.Add(map.GetCellCenterWorld(cell));
            var points = cells.ToArray();
            if (points.Length > 0) Debug.Log("CAMERA_INSPECT " + map.name + " centers x=" + points.Min(p => p.x) + ".." + points.Max(p => p.x) + " y=" + points.Min(p => p.y) + ".." + points.Max(p => p.y));
        }
        foreach (var source in UnityEngine.Object.FindObjectsOfType<EnemyAudioTestSpawner>(true).Where(s => s.Level == 2))
        {
            source.route.TryPrepare(out _);
            var points = Enumerable.Range(0, source.route.Count).Select(source.route.Point).ToArray();
            Debug.Log("CAMERA_INSPECT " + source.name + " x=" + points.Min(p => p.x) + ".." + points.Max(p => p.x) + " y=" + points.Min(p => p.y) + ".." + points.Max(p => p.y));
        }
        SessionState.SetBool(Key, true); Resume(); EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        began = EditorApplication.timeSinceStartup; lastFrame = -1; stage = 0;
        EditorApplication.update -= Poll; EditorApplication.update += Poll;
    }

    static void Poll()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - began > 45) throw new Exception("Camera transition timed out at stage " + stage);
            if (!EditorApplication.isPlaying || Time.frameCount < 4 || Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            var flow = UnityEngine.Object.FindObjectOfType<MapUIFlow>();
            var camera = flow.selection.WorldCamera;
            if (stage == 1)
            {
                CheckCoverage(camera, flow.CurrentLevel);
                if (EditorApplication.timeSinceStartup - pausedAt < .2) return;
                flow.TogglePause(); flow.RestartCurrentLevel(); CheckCoverage(camera, flow.CurrentLevel);
                CaptureActual(camera, flow.CurrentLevel);
                if (flow.CurrentLevel < 4) { flow.SkipCurrentLevel(); stage = 0; return; }
                flow.enabled = false;
                Require(camera.rect == new Rect(0, 0, 1, 1), "Leaving the flow must restore the full viewport");
                Require(!camera.GetComponent<FixedWorldCameraFrame>().IsActive &&
                    !camera.transform.Find("Camera frame background").GetComponent<Camera>().enabled, "Leaving the flow must release the black backdrop");
                Finish(true, "Levels 2/3/4 exact 25x15 regions, screen fitting, transitions, pause/retry and cleanup confirmed");
                return;
            }
            if (flow.CurrentLevel == 1)
            {
                if (flow.dialogue.IsPlaying) { flow.dialogue.Skip(); return; }
                if (flow.CurrentPhase == MapUIFlow.Phase.Preparation) flow.SkipCurrentLevel();
                return;
            }
            if (flow.CurrentPhase != MapUIFlow.Phase.Preparation) return;
            float height = camera.orthographicSize * 2;
            float width = height * camera.aspect;
            Debug.Log("CAMERA_ACTUAL level=" + flow.CurrentLevel + " center=" + camera.transform.position + " coverage=" + width + "x" + height + " viewport=" + camera.rect);
            CheckCoverage(camera, flow.CurrentLevel);
            Require(camera.GetComponentsInChildren<AudioListener>().Length == 1, "Framing must not add another audio listener");
            flow.TogglePause(); pausedAt = EditorApplication.timeSinceStartup; stage = 1;
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Poll;
        if (success) Debug.Log("LEVEL_TWO_CAMERA_PASS " + message); else Debug.LogError("LEVEL_TWO_CAMERA_FAIL " + message);
        EditorApplication.Exit(success ? 0 : 1);
    }
}

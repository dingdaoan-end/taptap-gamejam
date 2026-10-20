using System;
using System.Reflection;
using TapTapGameJam.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;

// Exercise the actual Map demolition UI at viewport edges and different Canvas scales.
public static class BuildMenuViewportChecks
{
    const string Key = "BuildMenuViewport.Play";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static double began;
    static int checks;
    static void Require(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);

    public static void RunBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Map.unity", OpenSceneMode.Single);
        SessionState.SetBool(Key, true); Resume(); EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        began = EditorApplication.timeSinceStartup; checks = 0;
        EditorApplication.update -= Poll; EditorApplication.update += Poll;
    }
    static void Poll()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - began > 35) throw new Exception("Menu check timed out");
            if (!EditorApplication.isPlaying || Time.frameCount < 5) return;
            var flow = UnityEngine.Object.FindObjectOfType<MapUIFlow>();
            if (flow.dialogue.IsPlaying) { flow.dialogue.Skip(); return; }
            if (flow.CurrentPhase != MapUIFlow.Phase.Preparation) return;
            Check(flow);
            Finish(true, "checks=" + checks + "; real Map menu bounds, Canvas scaling and demolition click passed");
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }

    static void Check(MapUIFlow flow)
    {
        var selection = flow.selection;
        var camera = selection.WorldCamera;
        var menu = Field<RectTransform>(selection, "buildMenu");
        var destroy = Field<GameObject>(selection, "destroyOption").GetComponent<Button>();
        var canvas = menu.GetComponentInParent<Canvas>().rootCanvas;
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null) scaler.enabled = false;
        var slot = new GameObject("Menu bounds test slot").AddComponent<BuildSlot>();
        var tower = new GameObject("Menu bounds test tower");
        typeof(BuildSlot).GetField("existingTower", Private).SetValue(slot, tower);
        var select = typeof(BuildSelection).GetMethod("SelectTower", Private);
        var position = typeof(BuildSelection).GetMethod("LateUpdate", Private);
        var corners = new Vector3[4];
        foreach (var viewport in new[] { new Rect(0, 0, 1, 1), new Rect(.08f, 0, .84f, 1), new Rect(0, .1f, 1, .8f), new Rect(.46f, .05f, .08f, .9f) })
        foreach (float scale in new[] { .75f, 1f, 2f })
        {
            camera.rect = viewport; canvas.scaleFactor = scale;
            camera.orthographicSize = 7.5f; camera.aspect = 25f / 15;
            foreach (float x in new[] { .01f, .5f, .99f })
            foreach (float y in new[] { .01f, .5f, .99f })
            {
                slot.transform.position = camera.ViewportToWorldPoint(new Vector3(x, y, 10));
                select.Invoke(selection, new object[] { slot });
                Canvas.ForceUpdateCanvases(); position.Invoke(selection, null); Canvas.ForceUpdateCanvases();
                Require(menu.gameObject.activeInHierarchy && destroy.gameObject.activeInHierarchy, "Demolition menu must stay visible");
                foreach (var graphic in menu.GetComponentsInChildren<Graphic>())
                {
                    if (!graphic.isActiveAndEnabled) continue;
                    graphic.rectTransform.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var point = RectTransformUtility.WorldToScreenPoint(null, corner);
                        var area = camera.pixelRect;
                        Require(point.x >= area.xMin - .5f && point.x <= area.xMax + .5f &&
                            point.y >= area.yMin - .5f && point.y <= area.yMax + .5f,
                            "Demolition UI outside viewport: anchor=" + x + "," + y + ", scale=" + scale + ", point=" + point + ", viewport=" + area);
                    }
                }
                if (x == .5f && y == .5f)
                {
                    var expected = camera.WorldToScreenPoint(slot.transform.position) + (Vector3)Field<Vector2>(selection, "menuOffset");
                    Require(Vector2.Distance(RectTransformUtility.WorldToScreenPoint(null, menu.position), expected) < .5f,
                        "Menu away from edges must keep its authored offset");
                }
            }
        }
        destroy.onClick.Invoke();
        Require(!slot.Occupied && !tower.activeSelf && !menu.gameObject.activeSelf,
            "Adapted demolition button must still remove the selected tower and close");

        // The construction buttons share this menu and extend beyond its root as well.
        camera.rect = new Rect(0, 0, 1, 1); canvas.scaleFactor = 1;
        var validator = selection.GetComponent<BuildPlacementValidator>();
        bool foundSite = false;
        foreach (var ground in UnityEngine.Object.FindObjectsOfType<Tilemap>())
        {
            if (foundSite || BuildPlacementValidator.GroundLevel(ground) < 1) continue;
            foreach (var cell in ground.cellBounds.allPositionsWithin)
            {
                var candidate = new BuildFootprint { surface = ground, anchor = cell };
                if (!validator.Validate(candidate, camera).Allowed) continue;
                selection.SetUnlockedPrefabs(new[] { flow.bellPrefab, flow.chordPrefab, flow.dudukPrefab });
                Require(selection.SelectSite(candidate), "Actual valid construction site must open menu");
                camera.rect = new Rect(.08f, 0, .84f, 1); canvas.scaleFactor = 2;
                foreach (float x in new[] { .01f, .5f, .99f })
                foreach (float y in new[] { .01f, .5f, .99f })
                {
                    var point = camera.ViewportToScreenPoint(new Vector3(x, y, 0));
                    typeof(BuildSelection).GetField("menuOffset", Private).SetValue(selection,
                        (Vector2)(point - camera.WorldToScreenPoint(candidate.Center)));
                    Canvas.ForceUpdateCanvases(); position.Invoke(selection, null); Canvas.ForceUpdateCanvases();
                    foreach (var graphic in menu.GetComponentsInChildren<Graphic>())
                    {
                        if (!graphic.isActiveAndEnabled) continue;
                        graphic.rectTransform.GetWorldCorners(corners);
                        foreach (var corner in corners)
                        {
                            var screen = RectTransformUtility.WorldToScreenPoint(null, corner);
                            var area = camera.pixelRect;
                            Require(screen.x >= area.xMin - .5f && screen.x <= area.xMax + .5f &&
                                screen.y >= area.yMin - .5f && screen.y <= area.yMax + .5f, "Construction menu must also stay inside viewport");
                        }
                    }
                }
                selection.CancelBuild(); foundSite = true; break;
            }
        }
        Require(foundSite, "Map must have a real buildable site for menu regression");
    }
    static void Finish(bool ok, string message)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Poll;
        if (ok) Debug.Log("BUILD_MENU_VIEWPORT_PASS " + message); else Debug.LogError("BUILD_MENU_VIEWPORT_FAIL " + message);
        EditorApplication.Exit(ok ? 0 : 1);
    }
}

using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public static class BuildPlacementSetup
{
    [MenuItem("Tools/Building/Configure Current Scene")]
    public static void ConfigureCurrentScene()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit Play mode before configuring construction.");
        var selection = UnityEngine.Object.FindObjectOfType<BuildSelection>();
        if (selection == null) throw new InvalidOperationException("Current scene needs the existing BuildSelection component.");
        foreach (var map in UnityEngine.Object.FindObjectsOfType<Tilemap>())
            if (map.name == "Ground") map.name = "Ground level1";
        var validator = selection.GetComponent<BuildPlacementValidator>();
        if (validator == null) validator = Undo.AddComponent<BuildPlacementValidator>(selection.gameObject);
        validator.RefreshSurfaces();
        var fields = new SerializedObject(selection);
        fields.FindProperty("placementValidator").objectReferenceValue = validator;
        if (fields.FindProperty("worldCamera").objectReferenceValue == null)
            fields.FindProperty("worldCamera").objectReferenceValue = Camera.main;
        var canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            Transform status = canvas.transform.Find("Build placement status");
            if (status == null)
            {
                var panel = new GameObject("Build placement status", typeof(RectTransform), typeof(Image));
                Undo.RegisterCreatedObjectUndo(panel, "Add placement status");
                panel.transform.SetParent(canvas.transform, false);
                var rect = panel.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(1, 0);
                rect.offsetMin = new Vector2(16, 10); rect.offsetMax = new Vector2(-16, 52);
                var image = panel.GetComponent<Image>(); image.color = new Color(.03f, .06f, .08f, .86f); image.raycastTarget = false;
                var textObj = new GameObject("Message", typeof(RectTransform), typeof(Text));
                textObj.transform.SetParent(panel.transform, false);
                var textRect = textObj.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(10, 2); textRect.offsetMax = new Vector2(-10, -2);
                var label = textObj.GetComponent<Text>();
                label.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Nightgazer-Bitmap-main/Nightgazer16.ttf");
                label.fontSize = 18; label.alignment = TextAnchor.MiddleLeft;
                label.color = new Color(.65f, 1, .72f); label.raycastTarget = false;
                label.text = "选择同层完整 2×2 地块建造 · 右键 / Esc 取消";
                status = panel.transform;
            }
            fields.FindProperty("placementStatus").objectReferenceValue = status.GetComponentInChildren<Text>();
        }
        fields.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(selection.gameObject.scene);
    }

    public static void ConfigureProjectScenes()
    {
        foreach (string path in new[] { "Assets/Scenes/Map.unity", "Assets/Scenes/MusicTest.unity" })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            ConfigureCurrentScene();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("BUILD_PLACEMENT_CONFIGURED " + path);
        }
        AssetDatabase.SaveAssets();
    }
}

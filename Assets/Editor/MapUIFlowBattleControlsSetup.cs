using System;
using System.IO;
using System.Linq;
using TapTapGameJam.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MapUIFlowBattleControlsSetup
{
    [MenuItem("Tools/UI Flow/Add Battle Controls and Unify Fonts")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Map.unity") throw new InvalidOperationException("Open Map first.");
        var canvas = scene.GetRootGameObjects().Single(g => g.name == "Canvas").transform;
        var flow = UnityEngine.Object.FindObjectsOfType<MapUIFlow>(true).Single(f => f.gameObject.scene == scene);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Dialogue/DialogueChinese.asset");
        if (font == null) throw new InvalidOperationException("Missing DialogueChinese font.");
        Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Add battle controls and unify fonts");
        Undo.RecordObject(flow, "Bind battle controls");
        var root = Child(canvas, "BattleControlsRoot");
        root.anchorMin = root.anchorMax = root.pivot = Vector2.one;
        root.anchoredPosition = new Vector2(-24, -20);
        root.sizeDelta = new Vector2(324, 100);
        root.SetSiblingIndex(canvas.Find("DialogueRoot").GetSiblingIndex());
        flow.battleControlsRoot = root.gameObject;
        flow.pauseButton = Button(root, "PauseButton", "暂停", font, 0, 140);
        flow.restartButton = Button(root, "RestartButton", "重新开始", font, 152, 172);
        flow.skipLevelButton = Button(root, "SkipLevelButton", "跳过本关", font, 152, 172);
        ((RectTransform)flow.skipLevelButton.transform).anchoredPosition = new Vector2(152, -56);
        flow.pauseLabel = flow.pauseButton.GetComponentInChildren<TMP_Text>(true);
        foreach (string name in new[] { "Button_1", "Button_2", "Button_3", "DestroyButton" })
        {
            var button = canvas.Find("BuildMenu/" + name);
            if (button == null) throw new InvalidOperationException("Missing BuildMenu/" + name);
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true)) SetFont(label, font);
        }
        root.gameObject.SetActive(false);
        EditorUtility.SetDirty(flow);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("Artifacts/UIBattleControls");
        File.WriteAllText("Artifacts/UIBattleControls/setup.txt", "BATTLE_CONTROLS_SETUP_PASS\n" +
            "Canvas/BattleControlsRoot: PauseButton, RestartButton, SkipLevelButton\n" +
            "BuildMenu fonts: " + string.Join(", ", canvas.Find("BuildMenu").GetComponentsInChildren<TMP_Text>(true).Select(t => t.font.name)) +
            "\nBuild callbacks: " + string.Join(", ", canvas.Find("BuildMenu").GetComponentsInChildren<Button>(true).Select(b => b.name + "=" + b.onClick.GetPersistentMethodName(0))));
        Selection.activeGameObject = root.gameObject;
        Debug.Log("BATTLE_CONTROLS_SETUP_PASS");
    }
    static RectTransform Child(Transform parent, string name)
    {
        var existing = parent.Find(name) as RectTransform;
        if (existing != null) return existing;
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
    static Button Button(Transform parent, string name, string caption, TMP_FontAsset font, float x, float width)
    {
        var rect = Child(parent, name);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, 0); rect.sizeDelta = new Vector2(width, 44);
        var image = rect.GetComponent<Image>() ?? Undo.AddComponent<Image>(rect.gameObject);
        image.color = new Color(.10f, .14f, .20f, .96f); image.raycastTarget = true;
        var button = rect.GetComponent<Button>() ?? Undo.AddComponent<Button>(rect.gameObject);
        button.targetGraphic = image;
        var labelRect = Child(rect, "Label");
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8, 2); labelRect.offsetMax = new Vector2(-8, -2);
        var label = labelRect.GetComponent<TextMeshProUGUI>() ?? Undo.AddComponent<TextMeshProUGUI>(labelRect.gameObject);
        SetFont(label, font); label.text = caption; label.fontSize = 24;
        label.color = Color.white; label.alignment = TextAlignmentOptions.Center;
        return button;
    }
    static void SetFont(TMP_Text text, TMP_FontAsset font)
    {
        text.font = font; text.fontSharedMaterial = font.material; text.raycastTarget = false;
    }
}

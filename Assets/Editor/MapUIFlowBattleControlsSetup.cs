using System;
using System.Linq;
using TapTapGameJam.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MapUIFlowBattleControlsSetup
{
    const string Art = "Assets/Sprites/UI/Menu/";
    [MenuItem("Tools/UI Flow/Configure Escape Pause Menu")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Map.unity") throw new InvalidOperationException("Open Map first.");
        var restartSprite = Import("RestartGame_Lettering_v1.png");
        var skipSprite = Import("SkipLevel_Lettering_v1.png");
        var menu = EditorSceneManager.OpenScene("Assets/Scenes/Memu.unity", OpenSceneMode.Additive);
        ColorBlock colors;
        Sprite quitSprite;
        AudioClip clickClip;
        try
        {
            var buttons = menu.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Button>(true)).ToArray();
            colors = buttons.Single(b => b.name == "StartGameButton").colors;
            quitSprite = buttons.Single(b => b.name == "QuitGameButton").image.sprite;
            clickClip = menu.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<AudioSource>(true))
                .Single(a => a.name == "MenuClickSound").clip;
        }
        finally { EditorSceneManager.CloseScene(menu, true); }
        var canvas = scene.GetRootGameObjects().Single(g => g.name == "Canvas").transform;
        var flow = UnityEngine.Object.FindObjectsOfType<MapUIFlow>(true).Single(f => f.gameObject.scene == scene);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Dialogue/Nightgazer16.asset");
        Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Create Esc pause menu");
        Undo.RecordObject(flow, "Bind Esc pause menu");
        var root = canvas.Find("PauseMenuRoot") as RectTransform ?? canvas.Find("BattleControlsRoot") as RectTransform;
        if (root == null) root = Child(canvas, "PauseMenuRoot");
        root.name = "PauseMenuRoot";
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.pivot = new Vector2(.5f, .5f);
        root.offsetMin = root.offsetMax = Vector2.zero;
        var overlay = root.GetComponent<Image>();
        if (overlay == null) overlay = root.gameObject.AddComponent<Image>();
        overlay.color = new Color(.015f, .045f, .045f, .78f); overlay.raycastTarget = true;
        var oldPause = root.Find("PauseButton");
        if (oldPause != null) UnityEngine.Object.DestroyImmediate(oldPause.gameObject);
        var panel = Child(root, "MenuPanel"); Center(panel, Vector2.zero, new Vector2(560, 560));
        var panelImage = panel.GetComponent<Image>();
        if (panelImage == null) panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(.025f, .10f, .11f, .94f); panelImage.raycastTarget = true;
        foreach (float y in new[] { -278f, 278f })
        {
            var line = Child(panel, y > 0 ? "TopEdge" : "BottomEdge"); Center(line, new Vector2(0, y), new Vector2(510, 2));
            var image = line.GetComponent<Image>();
        if (image == null) image = line.gameObject.AddComponent<Image>();
            image.color = new Color(.78f, .65f, .36f, .65f); image.raycastTarget = false;
        }
        Label(panel, "Title", "已暂停", font, 224, 32);
        Label(panel, "ResumeHint", "ESC  继续游戏", font, -230, 20);
        flow.restartButton = Button(root, panel, "RestartButton", restartSprite, colors, 110);
        flow.skipLevelButton = Button(root, panel, "SkipLevelButton", skipSprite, colors, 0);
        flow.quitButton = Button(root, panel, "QuitGameButton", quitSprite, colors, -110);
        var actions = new[] { flow.restartButton, flow.skipLevelButton, flow.quitButton };
        foreach (var action in actions) action.navigation = new Navigation { mode = Navigation.Mode.Vertical };
        root.SetAsLastSibling(); root.gameObject.SetActive(false);
        flow.pauseMenuRoot = root.gameObject;
        var soundObject = flow.transform.Find("PauseMenuClickSound");
        if (soundObject == null) { var go = new GameObject("PauseMenuClickSound"); go.transform.SetParent(flow.transform, false); soundObject = go.transform; }
        var sound = soundObject.GetComponent<AudioSource>();
        if (sound == null) sound = soundObject.gameObject.AddComponent<AudioSource>();
        sound.clip = clickClip; sound.playOnAwake = false; sound.loop = false; sound.spatialBlend = 0; sound.volume = .8f;
        var fields = new SerializedObject(flow); fields.FindProperty("menuClickSound").objectReferenceValue = sound; fields.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(flow); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root.gameObject;
        Debug.Log("MAP_PAUSE_MENU_SETUP_PASS");
    }
    static Sprite Import(string name)
    {
        string path = Art + name;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing menu artwork: " + path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp; importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 4096;
        importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static RectTransform Child(Transform parent, string name)
    {
        var existing = parent.Find(name) as RectTransform;
        if (existing != null) return existing;
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return (RectTransform)go.transform;
    }
    static void Center(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.localScale = Vector3.one; rect.anchoredPosition = position; rect.sizeDelta = size;
    }
    static Button Button(Transform root, Transform panel, string name, Sprite sprite, ColorBlock colors, float y)
    {
        var rect = root.Find(name) as RectTransform ?? panel.Find(name) as RectTransform ?? Child(panel, name);
        rect.SetParent(panel, false); Center(rect, new Vector2(0, y), new Vector2(420, 100));
        for (int i = rect.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(rect.GetChild(i).gameObject);
        var hitArea = rect.GetComponent<Image>();
        if (hitArea == null) hitArea = rect.gameObject.AddComponent<Image>();
        hitArea.sprite = null; hitArea.color = Color.clear; hitArea.raycastTarget = true;
        var art = Child(rect, "Lettering"); Center(art, Vector2.zero, new Vector2(420, 150));
        var image = art.gameObject.AddComponent<Image>();
        image.sprite = sprite; image.color = Color.white; image.preserveAspect = true; image.raycastTarget = false;
        var button = rect.GetComponent<Button>();
        if (button == null) button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image; button.colors = colors; button.interactable = true;
        button.onClick = new Button.ButtonClickedEvent(); return button;
    }
    static void Label(Transform parent, string name, string text, TMP_FontAsset font, float y, int size)
    {
        var rect = Child(parent, name); Center(rect, new Vector2(0, y), new Vector2(460, 50));
        var label = rect.GetComponent<TextMeshProUGUI>();
        if (label == null) label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.fontSharedMaterial = font.material; label.text = text; label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center; label.color = new Color(.96f, .91f, .75f); label.raycastTarget = false;
    }
}

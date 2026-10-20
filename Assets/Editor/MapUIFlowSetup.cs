using System;
using System.IO;
using System.Linq;
using TapTapGameJam.Combat;
using TapTapGameJam.Music;
using TapTapGameJam.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public static class MapUIFlowSetup
{
    const string Folder = "Assets/Dialogue";
    static Transform canvas;

    [MenuItem("Tools/UI Flow/Configure Map UI and Placeholders")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Map.unity") throw new InvalidOperationException("Open the authored Map scene first.");
        canvas = scene.GetRootGameObjects().Single(g => g.name == "Canvas").transform;
        string[] required = { "HUDRoot/LevelText", "HUDRoot/BaseHealthText", "HUDRoot/WaveText",
            "BuildMenu/Button_1", "BuildMenu/Button_2", "BuildMenu/Button_3", "BuildMenu/DestroyButton",
            "StartBattleButton", "DialogueRoot/PortraitImage", "DialogueRoot/DialogueBox/SpeakerNameText",
            "DialogueRoot/DialogueBox/DialogueContentText", "DialogueRoot/DialogueBox/NextButton",
            "ResultRoot/ResultTitleText", "ResultRoot/ResultBodyText", "TransitionRoot/FadeImage" };
        foreach (string path in required) Require(path);
        var selection = UnityEngine.Object.FindObjectOfType<BuildSelection>();
        var clock = UnityEngine.Object.FindObjectOfType<MusicClock>();
        if (selection == null || clock == null) throw new InvalidOperationException("Map needs its existing BuildSelection and MusicClock.");
        var fontSource = AssetDatabase.LoadAssetAtPath<Font>("Assets/PianoDefense/Art/Fonts/NotoSansCJKsc-Regular.otf");
        if (fontSource == null) throw new InvalidOperationException("Missing Chinese font source.");

        Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Configure Map UI flow");
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Folder + "/DialogueChinese.asset");
        if (font == null)
        {
            font = TMP_FontAsset.CreateFontAsset(fontSource, 36, 5, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            font.name = "DialogueChinese";
            AssetDatabase.CreateAsset(font, Folder + "/DialogueChinese.asset");
            foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
            AssetDatabase.AddObjectToAsset(font.material, font);
        }
        foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true))
        {
            text.font = font;
            text.raycastTarget = false;
        }
        Full(Require("HUDRoot"));
        Anchor(Require("HUDRoot/LevelText"), new Vector2(.5f, 1), new Vector2(0, -38), new Vector2(240, 48), new Vector2(.5f, .5f));
        Anchor(Require("HUDRoot/BaseHealthText"), new Vector2(0, 1), new Vector2(24, -38), new Vector2(360, 48), new Vector2(0, .5f));
        Anchor(Require("HUDRoot/WaveText"), new Vector2(0, 1), new Vector2(24, -90), new Vector2(420, 42), new Vector2(0, .5f));
        Text("HUDRoot/LevelText", "第一关", 28);
        Text("HUDRoot/BaseHealthText", "基地", 24);
        Text("HUDRoot/WaveText", "准备阶段", 24);
        Full(Require("DialogueRoot"));
        Require("DialogueRoot").GetComponent<Image>().color = new Color(0, 0, 0, .2f);
        Require("DialogueRoot").GetComponent<Image>().raycastTarget = true;
        var group = Require("DialogueRoot").GetComponent<CanvasGroup>();
        if (group == null) group = Undo.AddComponent<CanvasGroup>(Require("DialogueRoot").gameObject);
        group.alpha = 1; group.interactable = true; group.blocksRaycasts = true;
        var box = (RectTransform)Require("DialogueRoot/DialogueBox");
        box.anchorMin = Vector2.zero; box.anchorMax = Vector2.right; box.pivot = new Vector2(.5f, 0);
        box.sizeDelta = new Vector2(-64, 210); box.anchoredPosition = new Vector2(0, 24);
        box.GetComponent<Image>().color = new Color(.055f, .075f, .10f, .97f);
        var portrait = Require("DialogueRoot/PortraitImage").GetComponent<Image>();
        portrait.raycastTarget = false; portrait.preserveAspect = true;
        Anchor(portrait.transform, Vector2.zero, new Vector2(12, 24), new Vector2(476, 476), Vector2.zero);
        Anchor(Require("DialogueRoot/DialogueBox/SpeakerNameText"), new Vector2(0, 1), new Vector2(24, -16), new Vector2(400, 38), new Vector2(0, 1));
        var body = (RectTransform)Require("DialogueRoot/DialogueBox/DialogueContentText");
        body.anchorMin = Vector2.zero; body.anchorMax = Vector2.one;
        body.offsetMin = new Vector2(24, 62); body.offsetMax = new Vector2(-24, -62);
        var bodyText = body.GetComponent<TMP_Text>();
        bodyText.fontSize = 24; bodyText.enableWordWrapping = true;
        bodyText.margin = Vector4.zero;
        bodyText.alignment = TextAlignmentOptions.TopLeft; bodyText.color = Color.white;
        bodyText.text = "【占位对话】先建造铃铛塔，准备好后点击开始战斗。";
        Text("DialogueRoot/DialogueBox/SpeakerNameText", "引导者", 26);
        Anchor(Require("DialogueRoot/DialogueBox/NextButton"), Vector2.right, new Vector2(-20, 16), new Vector2(180, 38), Vector2.right);
        Anchor(Require("StartBattleButton"), Vector2.right, new Vector2(-32, 30), new Vector2(200, 44), Vector2.right);
        Require("StartBattleButton").GetComponentInChildren<TMP_Text>(true).text = "开始战斗";
        foreach (var button in canvas.GetComponentsInChildren<Button>(true))
        {
            // Retain all existing persistent OnClick bindings on build/destroy buttons.
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) { label.color = new Color(.1f, .12f, .15f); label.fontSize = 24; }
        }
        Full(Require("ResultRoot"));
        var resultImage = Require("ResultRoot").GetComponent<Image>();
        if (resultImage == null) resultImage = Undo.AddComponent<Image>(Require("ResultRoot").gameObject);
        resultImage.color = new Color(.03f, .04f, .06f, .94f); resultImage.raycastTarget = true;
        Anchor(Require("ResultRoot/ResultTitleText"), new Vector2(.5f,.5f), new Vector2(0,55), new Vector2(600,70), new Vector2(.5f,.5f));
        Anchor(Require("ResultRoot/ResultBodyText"), new Vector2(.5f,.5f), new Vector2(0,-25), new Vector2(760,90), new Vector2(.5f,.5f));
        Text("ResultRoot/ResultTitleText", "关卡结束", 36);
        Text("ResultRoot/ResultBodyText", "【占位】结算内容将在战斗流程接入后显示。", 24);
        foreach (var t in Require("ResultRoot").GetComponentsInChildren<TMP_Text>()) t.alignment = TextAlignmentOptions.Center;
        Full(Require("TransitionRoot")); Full(Require("TransitionRoot/FadeImage"));
        Require("TransitionRoot").SetAsLastSibling();
        Require("TransitionRoot/FadeImage").GetComponent<Image>().raycastTarget = true;

        var character = Asset<DialogueCharacter>("Guide", () =>
        {
            var c = ScriptableObject.CreateInstance<DialogueCharacter>();
            c.characterId = "guide"; c.displayName = "引导者"; c.portrait = portrait.sprite; return c;
        });
        var intro = Sequence("L1_Intro", character, "【第一关开场占位】欢迎来到这里。先选择一块合适的空地，建造铃铛塔。", "准备好后点击“开始战斗”。战斗期间也可以继续建造和拆除防御塔。");
        var fail = Sequence("Common_Fail", character, "【失败占位】基地已失守，本次关卡结束。");
        var victories = new[] {
            Sequence("L1_Clear", character, "【第一关胜利占位】你守住了第一片区域。接下来可以使用和弦塔。"),
            Sequence("L2_Clear", character, "【第二关胜利占位】接下来可以使用笛子塔，每四拍攻击一次。"),
            Sequence("L3_Clear", character, "【第三关胜利占位】笛子塔升级，将在每四拍的第一、第三拍攻击。"),
            Sequence("L4_Clear", character, "【最终胜利占位】你守住了所有区域。感谢游玩！") };
        var levels = new LevelDialogueSet[4];
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            levels[i] = Asset<LevelDialogueSet>("Level" + (i + 1), () =>
            {
                var level = ScriptableObject.CreateInstance<LevelDialogueSet>(); level.level = index + 1;
                level.intro = index == 0 ? intro : null; level.victory = victories[index]; level.defeat = fail; return level;
            });
        }

        var controller = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "UIFlowController");
        if (controller == null) { controller = new GameObject("UIFlowController"); Undo.RegisterCreatedObjectUndo(controller, "Create UI flow controller"); }
        var player = controller.GetComponent<DialoguePlayer>() ?? Undo.AddComponent<DialoguePlayer>(controller);
        player.panel = Require("DialogueRoot").gameObject;
        player.speakerName = Require("DialogueRoot/DialogueBox/SpeakerNameText").GetComponent<TMP_Text>();
        player.content = bodyText; player.portrait = portrait;
        player.nextButton = Require("DialogueRoot/DialogueBox/NextButton").GetComponent<Button>();
        player.nextLabel = player.nextButton.GetComponentInChildren<TMP_Text>(true); player.nextLabel.text = "继续";
        var flow = controller.GetComponent<MapUIFlow>() ?? Undo.AddComponent<MapUIFlow>(controller);
        flow.dialogue = player; flow.levelDialogues = levels; flow.selection = selection; flow.clock = clock;
        // Only this entry was enabled in the authored Map. Do not start all seven routes.
        flow.spawners = UnityEngine.Object.FindObjectsOfType<EnemyAudioTestSpawner>(true)
            .Where(s => s.gameObject.scene == scene && s.name == "NorthWest entry 1").ToArray();
        if (flow.spawners.Length != 1) throw new InvalidOperationException("Expected the authored NorthWest entry 1 test spawner.");
        flow.startBattleButton = Require("StartBattleButton").GetComponent<Button>();
        flow.resultRoot = Require("ResultRoot").gameObject; flow.transitionRoot = Require("TransitionRoot").gameObject;
        flow.levelText = Require("HUDRoot/LevelText").GetComponent<TMP_Text>();
        flow.healthText = Require("HUDRoot/BaseHealthText").GetComponent<TMP_Text>();
        flow.waveText = Require("HUDRoot/WaveText").GetComponent<TMP_Text>();
        flow.bellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tower_Bell.prefab");
        flow.chordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tower_Chord.prefab");
        flow.dudukPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tower_Duduk.prefab");
        Undo.RecordObject(selection, "Bind tower unlock options");
        var fields = new SerializedObject(selection);
        var options = fields.FindProperty("optionBindings"); options.arraySize = 3;
        var prefabs = new[] { flow.bellPrefab, flow.dudukPrefab, flow.chordPrefab };
        for (int i = 0; i < 3; i++)
        {
            options.GetArrayElementAtIndex(i).FindPropertyRelative("button").objectReferenceValue = Require("BuildMenu/Button_" + (i + 1)).gameObject;
            options.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue = prefabs[i];
        }
        fields.ApplyModifiedProperties();
        Undo.RecordObject(clock, "Start clock only with battle button");
        var clockFields = new SerializedObject(clock); clockFields.FindProperty("playOnStart").boolValue = false; clockFields.ApplyModifiedProperties();
        foreach (var spawner in flow.spawners) { Undo.RecordObject(spawner, "Gate test spawner"); spawner.enabled = false; }
        foreach (string path in new[] { "BuildMenu", "StartBattleButton", "DialogueRoot", "ResultRoot", "TransitionRoot" }) Require(path).gameObject.SetActive(false);
        EditorUtility.SetDirty(flow); EditorUtility.SetDirty(player);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("Artifacts/UIFlow");
        File.WriteAllText("Artifacts/UIFlow/setup.txt", "MAP_UI_SETUP_PASS\n" + Application.dataPath + "\nCanvas roots: " +
            string.Join(", ", Enumerable.Range(0, canvas.childCount).Select(i => canvas.GetChild(i).name)) +
            "\nPlaceholder dialogue assets: 6; level sets: 4\nBuild persistent callbacks retained.\n");
        Debug.Log("MAP_UI_SETUP_PASS: UI layout repaired and placeholder dialogue flow wired.");
        Selection.activeGameObject = controller;
    }

    static Transform Require(string path) => canvas.Find(path) ?? throw new InvalidOperationException("Missing Canvas/" + path);
    static void Full(Transform transform)
    {
        var r = (RectTransform)transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.pivot = new Vector2(.5f,.5f); r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }
    static void Anchor(Transform transform, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        var r = (RectTransform)transform; r.anchorMin = r.anchorMax = anchor; r.pivot = pivot; r.sizeDelta = size; r.anchoredPosition = position;
    }
    static void Text(string path, string value, float size)
    {
        var t = Require(path).GetComponent<TMP_Text>(); t.text = value; t.fontSize = size; t.color = Color.white;
    }
    static T Asset<T>(string name, Func<T> create) where T : ScriptableObject
    {
        string path = Folder + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing; // Never replace authored dialogue on rerun.
        var asset = create(); AssetDatabase.CreateAsset(asset, path); return asset;
    }
    static DialogueSequence Sequence(string id, DialogueCharacter character, params string[] lines) => Asset(id, () =>
    {
        var sequence = ScriptableObject.CreateInstance<DialogueSequence>(); sequence.dialogueId = id;
        sequence.lines = lines.Select(text => new DialogueSequence.Line { character = character, text = text }).ToArray();
        return sequence;
    });
}

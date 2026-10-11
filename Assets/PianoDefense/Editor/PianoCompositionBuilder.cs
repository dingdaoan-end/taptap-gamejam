using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TapTapGameJam.PianoDefense.Editor
{
    public static class PianoCompositionBuilder
    {
        const string Art = "Assets/PianoDefense/Art/";
        const string Scene = "Assets/Scenes/PianoDefense.unity";
        static Font font;
        static Sprite buttonSprite, dotSprite;
        static readonly Color Background = C(0x101922), Panel = C(0x192630), Ink = C(0xe8eee9), Muted = C(0x94a7ae), Mint = C(0x72dfbe), Gold = C(0xe8c982);

        [MenuItem("Tools/Game Jam/Upgrade to Composition v0.3")]
        public static void UpgradeScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Artifacts");
            File.Copy(Scene, "Artifacts/PianoDefense-before-v03-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".unity", false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ImportAssets();
            EditorSceneManager.OpenScene(Scene);
            var game = UnityEngine.Object.FindObjectOfType<PianoDefenseGame>();
            if (game == null || game.Board == null) throw new Exception("Editable piano scene is missing.");
            var oldView = game.GetComponent<PianoDefenseView>(); if (oldView != null) oldView.enabled = false;
            var oldUi = GameObject.Find("Composition UI"); if (oldUi != null) UnityEngine.Object.DestroyImmediate(oldUi);
            var camera = Camera.main; camera.backgroundColor = Background; camera.orthographicSize = 3.5f;
            UpdateBoard(game);
            CreateLessons(game);
            CreateUI(game);
            var audio = new SerializedObject(game.GetComponent<PianoAudio>());
            var samples = audio.FindProperty("pianoSamples"); samples.arraySize = 8;
            for (int i = 0; i < 8; i++) samples.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Art + "Audio/" + DefenseSimulation.PitchNames[i] + ".wav");
            audio.ApplyModifiedPropertiesWithoutUndo();
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
                new GameObject("UI Event System", typeof(EventSystem), typeof(StandaloneInputModule));
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scene, true) };
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), Scene);
            AssetDatabase.SaveAssets();
            Debug.Log("COMPOSITION_SCENE_READY lessons=4 ui=editable piano=sampled");
        }

        static void ImportAssets()
        {
            foreach (var file in Directory.GetFiles(Art + "UI", "*.png")) ImportSprite(file, new Vector4(5, 5, 5, 5));
            foreach (var file in Directory.GetFiles(Art + "Effects", "*.png")) ImportSprite(file, Vector4.zero);
            foreach (var file in Directory.GetFiles(Art + "Audio", "*.wav"))
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.PCM;
                importer.defaultSampleSettings = settings; importer.SaveAndReimport();
            }
            font = AssetDatabase.LoadAssetAtPath<Font>(Art + "Fonts/NotoSansCJKsc-Regular.otf");
            if (font == null) throw new Exception("Embedded Chinese font missing.");
            buttonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "UI/button_rectangle_flat.png");
            dotSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "Effects/circle_01.png");
        }

        static void ImportSprite(string file, Vector4 border)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        static Material Material(string name, Color color)
        {
            string path = "Assets/PianoDefense/SceneArt/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Unlit/Color")); AssetDatabase.CreateAsset(material, path); }
            material.color = color; return material;
        }

        static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 size, Material material, PrimitiveType shape = PrimitiveType.Cube)
        {
            var obj = GameObject.CreatePrimitive(shape); obj.name = name; obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position; obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            obj.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>()); return obj;
        }

        static TextMesh WorldText(string name, Transform parent, Vector3 pos, string value, float size, Color color)
        {
            var obj = new GameObject(name); obj.transform.SetParent(parent, false); obj.transform.localPosition = pos;
            var text = obj.AddComponent<TextMesh>(); text.font = font; text.fontSize = 64; text.characterSize = size * .25f;
            text.anchor = TextAnchor.UpperCenter; text.alignment = TextAlignment.Center; text.color = color; text.text = value;
            obj.GetComponent<MeshRenderer>().sharedMaterial = font.material; return text;
        }

        static GameObject PianoPrefab(Material body, Material ivory, Material black, Material mint)
        {
            var root = new GameObject("Piano Tower");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Models/tower-round-base.fbx");
            if (model != null)
            {
                var pedestal = UnityEngine.Object.Instantiate(model, root.transform); pedestal.name = "Kenney Pedestal";
                pedestal.transform.localRotation = Quaternion.Euler(90, 0, 0); pedestal.transform.localScale = Vector3.one * .55f;
                pedestal.transform.localPosition = new Vector3(0, 0, .05f);
                foreach (var renderer in pedestal.GetComponentsInChildren<Renderer>())
                { renderer.sharedMaterial = body; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            }
            Cube("Piano Case", root.transform, new Vector3(0, .04f, -.13f), new Vector3(.76f, .55f, .17f), black);
            Cube("Golden Lid", root.transform, new Vector3(0, .26f, -.24f), new Vector3(.76f, .10f, .055f), body);
            for (int i = 0; i < 8; i++) Cube("White Key " + i, root.transform, new Vector3(-.30f + i * .086f, -.035f, -.25f), new Vector3(.077f, .28f, .045f), ivory);
            foreach (int i in new[] { 0, 1, 3, 4, 5 }) Cube("Black Key " + i, root.transform, new Vector3(-.26f + i * .086f, .034f, -.29f), new Vector3(.047f, .15f, .035f), black);
            Cube("Beat Lamp", root.transform, new Vector3(.31f, .25f, -.30f), new Vector3(.055f, .055f, .025f), mint, PrimitiveType.Sphere);
            WorldText("Tower Caption", root.transform, new Vector3(0, -.30f, -.36f), "PIANO\n1 2 3 4", .095f, Ink);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/PianoDefense/SceneArt/PianoTower.prefab");
            UnityEngine.Object.DestroyImmediate(root); return prefab;
        }

        static GameObject EnemyPrefab(Material color)
        {
            var root = new GameObject("Note Enemy");
            Cube("Notehead", root.transform, new Vector3(-.08f, -.08f, -.08f), new Vector3(.37f, .29f, .12f), color, PrimitiveType.Sphere);
            Cube("Stem", root.transform, new Vector3(.10f, .14f, -.08f), new Vector3(.055f, .45f, .065f), color);
            var flag = Cube("Flag", root.transform, new Vector3(.20f, .30f, -.08f), new Vector3(.20f, .08f, .06f), color);
            flag.transform.localRotation = Quaternion.Euler(0, 0, -25);
            WorldText("Pitch Caption", root.transform, new Vector3(0, -.29f, -.16f), "do", .18f, Ink);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/PianoDefense/SceneArt/NoteEnemy.prefab");
            UnityEngine.Object.DestroyImmediate(root); return prefab;
        }

        static void UpdateBoard(PianoDefenseGame game)
        {
            var ground = Material("Ground", C(0x28353a)); var lane = Material("Lane", C(0x354b4b));
            var baseMat = Material("Base", C(0x28564a)); var spawn = Material("Spawn", C(0x45363e));
            var gold = Material("Tower", Gold); var ivory = Material("PianoWhite", Ink); var black = Material("PianoBlack", C(0x0d151e));
            var mint = Material("Mint", Mint); var enemy = Material("Enemy", C(0xed8b91));
            var towerPrefab = PianoPrefab(gold, ivory, black, mint); var enemyPrefab = EnemyPrefab(enemy);
            foreach (var renderer in game.Board.GetComponentsInChildren<Renderer>(true))
                if (renderer.gameObject.name.StartsWith("Cell ")) renderer.gameObject.isStatic = false;
            var old = game.Board.transform.Find("Starting Towers"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var anchors = new GameObject("Starting Towers"); anchors.transform.SetParent(game.Board.transform, false);
            for (int lesson = 0; lesson < 4; lesson++)
            {
                var group = new GameObject("Lesson " + (lesson + 1) + " - edit towers here"); group.transform.SetParent(anchors.transform, false);
                foreach (var entry in CompositionLessons.Layout(lesson))
                {
                    var obj = (GameObject)PrefabUtility.InstantiatePrefab(towerPrefab); obj.transform.SetParent(group.transform, false);
                    obj.name = "Piano " + entry.X + "," + entry.Y; obj.transform.localPosition = PianoBoardScene.CellPosition(entry.X, entry.Y, -.18f);
                    var anchor = obj.AddComponent<PianoTowerAuthoring>(); var data = new SerializedObject(anchor);
                    data.FindProperty("pitch").intValue = entry.Pitch; data.FindProperty("mode").enumValueIndex = (int)entry.Mode;
                    data.FindProperty("rhythm").enumValueIndex = (int)entry.Rhythm; data.FindProperty("lesson").intValue = lesson;
                    data.ApplyModifiedPropertiesWithoutUndo(); obj.SetActive(lesson == 0);
                }
            }
            var guide = game.Board.transform.Find("Lane Markers"); if (guide != null) UnityEngine.Object.DestroyImmediate(guide.gameObject);
            guide = new GameObject("Lane Markers").transform; guide.SetParent(game.Board.transform, false);
            for (int y = 1; y <= 5; y += 2)
            {
                for (int x = 1; x < 11; x++) Cube("Beat dash " + x + "," + y, guide, PianoBoardScene.CellPosition(x, y, -.04f) + new Vector3(0, .34f, 0), new Vector3(.35f, .015f, .01f), mint);
                WorldText("Incoming lane " + y, guide, PianoBoardScene.CellPosition(11, y, -.05f) + Vector3.up * .18f, "<<", .27f, Muted);
                WorldText("Base lane " + y, guide, PianoBoardScene.CellPosition(0, y, -.05f) + Vector3.up * .20f, "HOME", .12f, Mint);
            }
            var boardData = new SerializedObject(game.Board);
            boardData.FindProperty("towerPrefab").objectReferenceValue = towerPrefab;
            boardData.FindProperty("enemyPrefab").objectReferenceValue = enemyPrefab;
            boardData.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CreateLessons(PianoDefenseGame game)
        {
            const string folder = "Assets/PianoDefense/Lessons";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/PianoDefense", "Lessons");
            var data = new SerializedObject(game); data.FindProperty("useCompositionLessons").boolValue = true; data.FindProperty("lessonIndex").intValue = 0;
            var array = data.FindProperty("lessons"); array.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                string path = folder + "/Lesson" + (i + 1) + ".asset";
                var lesson = AssetDatabase.LoadAssetAtPath<CompositionLessonAsset>(path);
                if (lesson == null)
                {
                    lesson = ScriptableObject.CreateInstance<CompositionLessonAsset>();
                    lesson.Title = CompositionLessons.Titles[i]; lesson.Hint = CompositionLessons.Hints[i];
                    lesson.Bpm = CompositionLessons.Tempo(i); lesson.TotalBeats = CompositionLessons.Beats(i); lesson.StartingCoins = CompositionLessons.Budget(i);
                    foreach (var spawn in CompositionLessons.Waves(i)) lesson.Waves.Add(new WaveSpawn { Beat = spawn.Beat, Lane = spawn.Lane, Pitch = spawn.Pitch, Speed = spawn.Speed });
                    AssetDatabase.CreateAsset(lesson, path);
                }
                array.GetArrayElementAtIndex(i).objectReferenceValue = lesson;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform; rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
        }
        static Image Image(string name, Transform parent, float x, float y, float w, float h, Color color, Sprite sprite = null)
        {
            var rect = Rect(name, parent, x, y, w, h); var image = rect.gameObject.AddComponent<Image>(); image.color = color;
            image.sprite = sprite; image.raycastTarget = false; if (sprite != null && sprite.border != Vector4.zero) image.type = UnityEngine.UI.Image.Type.Sliced;
            return image;
        }
        static Text Text(string name, Transform parent, float x, float y, float w, float h, string value, int size, Color color, bool bold = false)
        {
            var rect = Rect(name, parent, x, y, w, h); var text = rect.gameObject.AddComponent<Text>(); text.font = font; text.fontSize = size;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; text.text = value; text.color = color;
            text.raycastTarget = false; text.alignment = TextAnchor.UpperLeft; text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
        static Button Button(string name, Transform parent, float x, float y, float w, float h, string caption, bool primary = false, int size = 17)
        {
            var image = Image(name, parent, x, y, w, h, primary ? C(0x355f53) : C(0x2e3d48), buttonSprite); image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f); colors.pressedColor = new Color(.75f, .9f, .84f); colors.disabledColor = new Color(.52f, .52f, .52f); button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var label = Text("Label", image.transform, 8, 0, w - 16, h, caption, size, Ink, true); label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
        static Text Label(Button button) { return button.GetComponentInChildren<Text>(); }
        static Color C(int rgb) { return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f); }

        static void CreateUI(PianoDefenseGame game)
        {
            var root = new GameObject("Composition UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var ui = root.AddComponent<PianoCompositionUI>(); ui.Game = game;
            Image("Top backdrop", root.transform, 0, 0, 1440, 204, Background);
            Image("Left margin", root.transform, 0, 204, 48, 560, Background);
            Image("Right backdrop", root.transform, 1008, 204, 432, 696, Background);
            Image("Bottom backdrop", root.transform, 0, 764, 1008, 136, Background);
            Text("Brand eyebrow", root.transform, 48, 21, 380, 24, "EMERGENCE  /  A PIANO DEFENSE", 13, Mint);
            Text("Title", root.transform, 46, 48, 570, 52, "夜之乐章", 38, Ink, true);
            Text("Tagline", root.transform, 245, 64, 490, 30, "让你的防守，涌现为音乐。", 17, Muted);
            ui.Status = Text("Resources", root.transform, 780, 49, 612, 40, "生命 10/10    乐章币 16    守护 0/16", 21, Ink, true);
            Image("Header rule", root.transform, 48, 109, 1344, 1, C(0x30444a));
            ui.LessonButtons = new Button[4];
            string[] tabs = { "01  初见", "02  交错", "03  共鸣", "04  涌现" };
            for (int i = 0; i < 4; i++) ui.LessonButtons[i] = Button("Lesson " + (i + 1), root.transform, 48 + i * 243, 128, 231, 42, tabs[i]);
            ui.LessonName = Text("Lesson title", root.transform, 48, 177, 550, 27, CompositionLessons.Titles[0], 17, Ink, true);
            ui.Progress = Text("Transport", root.transform, 635, 180, 374, 24, "100 BPM  /  准备演奏  /  来敌 ←", 14, Muted);

            var viewport = Image("Board - click cells", root.transform, 48, 204, 960, 560, new Color(0, 0, 0, 0));
            viewport.raycastTarget = true; ui.BoardViewport = viewport.rectTransform;
            var input = viewport.gameObject.AddComponent<CompositionBoardInput>(); input.Game = game;
            var hover = Image("Hovered cell", viewport.transform, 0, 0, 80, 80, new Color(.46f, .92f, .79f, .11f));
            input.Hover = hover.rectTransform; hover.gameObject.SetActive(false);
            Image("Board bottom rule", root.transform, 48, 765, 960, 1, C(0x416159));

            var panel = Image("Arrangement panel", root.transform, 1040, 128, 352, 636, Panel);
            Text("Section", panel.transform, 22, 16, 309, 25, "指挥台  /  ARRANGEMENT", 14, Mint, true);
            ui.Hint = Text("Lesson hint", panel.transform, 22, 49, 309, 81, CompositionLessons.Hints[0], 15, Muted);
            ui.StartButton = Button("Start pause", panel.transform, 22, 138, 309, 46, "开始演奏  [空格]", true, 20); ui.StartLabel = Label(ui.StartButton);
            ui.BuildButton = Button("Build piano", panel.transform, 22, 195, 309, 41, "＋ 建造钢琴塔  ·  8 币"); ui.BuildLabel = Label(ui.BuildButton);
            ui.ClearButton = Button("Clear arrangement", panel.transform, 22, 247, 149, 34, "清空布阵", false, 14);
            ui.RestoreButton = Button("Restore example", panel.transform, 182, 247, 149, 34, "恢复示例", false, 14);
            Image("Divider", panel.transform, 22, 296, 309, 1, C(0x354950));
            ui.TowerInfo = Text("Selected tower", panel.transform, 22, 308, 309, 52, "点击棋盘上的琴塔\n选择它的节拍与音调", 15, Ink);
            Text("Rhythm label", panel.transform, 22, 371, 309, 23, "攻击节拍", 13, Muted);
            ui.RhythmButtons = new Button[4]; string[] rhythms = { "每拍", "1 / 3", "2 / 4", "仅 1" };
            for (int i = 0; i < 4; i++) ui.RhythmButtons[i] = Button("Rhythm " + i, panel.transform, 22 + i * 79, 399, 72, 34, rhythms[i], false, 14);
            ui.ModeButtons = new Button[3]; string[] modes = { "全部", "独奏", "跳过" };
            for (int i = 0; i < 3; i++) ui.ModeButtons[i] = Button("Filter " + i, panel.transform, 22 + i * 106, 446, 97, 34, modes[i], false, 14);
            ui.PitchButtons = new Button[8];
            for (int i = 0; i < 8; i++) ui.PitchButtons[i] = Button("Pitch " + i, panel.transform, 22 + i % 4 * 79, 491 + i / 4 * 40, 72, 32, DefenseSimulation.NoteNames[i], false, 14);
            ui.SellButton = Button("Sell selected", panel.transform, 22, 583, 309, 32, "回收琴塔  [Delete]", false, 14);

            var goalPanel = Image("Resonance panel", root.transform, 1040, 780, 352, 101, Panel);
            ui.Goal = Text("Resonance rules", goalPanel.transform, 16, 10, 320, 87, "同拍 Do + Mi + Sol → 共鸣\n全场休止 1 拍 · +3 币", 14, Mint);
            ui.Notice = Text("Feedback", root.transform, 48, 770, 820, 25, "点击空地建塔 · 点击琴塔调整 · 下方琴键可试听", 13, Muted);
            ui.AudioButton = Button("Audio toggle", root.transform, 876, 770, 132, 27, "声音  开启", false, 12); ui.AudioLabel = Label(ui.AudioButton);
            ui.BeatLights = new Image[4];
            for (int i = 0; i < 4; i++) ui.BeatLights[i] = Image("Beat " + (i + 1), root.transform, 1312 + i * 20, 23, 12, 6, Mint);
            var staff = Image("Live score", root.transform, 48, 802, 960, 28, C(0x18272d));
            for (int i = 0; i < 5; i++) Image("Staff line " + i, staff.transform, 10, 4 + i * 5, 940, 1, C(0x3a5257));
            ui.NoteDots = new Image[128];
            for (int beat = 0; beat < 16; beat++) for (int pitch = 0; pitch < 8; pitch++)
            {
                var note = Image("Note " + beat + " " + pitch, staff.transform, 22 + beat * 58, 21 - pitch * 2.5f, 7, 7, PianoBoardScene.PitchColors[pitch], dotSprite);
                note.enabled = false; ui.NoteDots[beat * 8 + pitch] = note;
            }
            ui.PreviewButtons = new Button[8]; ui.KeyLights = new Image[8];
            for (int i = 0; i < 8; i++)
            {
                var key = Button("Piano preview " + DefenseSimulation.PitchNames[i], root.transform, 48 + i * 120, 842, 111, 39, DefenseSimulation.NoteNames[i] + "   " + DefenseSimulation.PitchNames[i], false, 15);
                Label(key).color = C(0x26333a); ui.PreviewButtons[i] = key; ui.KeyLights[i] = key.image;
            }

            var pause = Image("Pause overlay", root.transform, 0, 0, 1440, 900, new Color(.03f, .07f, .09f, .90f)); pause.raycastTarget = true;
            var pauseCard = Image("Pause card", pause.transform, 450, 300, 540, 275, Panel);
            Text("Pause title", pauseCard.transform, 40, 35, 460, 58, "乐章暂停", 32, Ink, true);
            Text("Pause subtitle", pauseCard.transform, 40, 108, 460, 45, "休息一下。按空格，继续这一拍。", 18, Muted);
            ui.ContinueButton = Button("Continue", pauseCard.transform, 40, 179, 460, 50, "继续演奏", true, 20); ui.PausePanel = pause.gameObject; ui.PausePanel.SetActive(false);
            var results = Image("Results overlay", root.transform, 0, 0, 1440, 900, new Color(.03f, .07f, .09f, .93f)); results.raycastTarget = true;
            var resultCard = Image("Results card", results.transform, 375, 230, 690, 440, Panel);
            Text("Result eyebrow", resultCard.transform, 40, 25, 610, 24, "YOUR ARRANGEMENT  /  本次演奏", 14, Mint);
            ui.ResultsTitle = Text("Result title", resultCard.transform, 40, 65, 610, 54, "你守住了一段乐章。", 30, Ink, true);
            ui.ResultsInfo = Text("Result details", resultCard.transform, 40, 137, 610, 112, "", 20, Muted);
            ui.ReplayButton = Button("Replay", resultCard.transform, 40, 268, 610, 49, "聆听我的乐章", true, 20); ui.ReplayLabel = Label(ui.ReplayButton);
            ui.RetryButton = Button("Retry", resultCard.transform, 40, 336, 297, 45, "重新演奏");
            ui.NextButton = Button("Next lesson", resultCard.transform, 352, 336, 298, 45, "下一乐章 →");
            Text("Credits", resultCard.transform, 40, 395, 610, 25, "钢琴 Alexander Holm · 图形 Kenney · 字体 Noto CJK", 12, Muted);
            ui.ResultsPanel = results.gameObject; ui.ResultsPanel.SetActive(false);
        }

        public static void UpgradeAndBuild() { UpgradeScene(); BuildWindows(); }

        [MenuItem("Tools/Game Jam/Build Composition v0.3 for Windows")]
        public static void BuildWindows()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            PianoDefenseChecks.Run(); VerifyScene();
            PlayerSettings.companyName = "GameJamTeam"; PlayerSettings.productName = "Nocturne - Piano Composition";
            PlayerSettings.defaultScreenWidth = 1440; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true; PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            const string path = "Builds/PianoDefense-v0.3/NocturnePiano.exe"; Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.Copy("THIRD_PARTY_NOTICES.md", "Builds/PianoDefense-v0.3/THIRD_PARTY_NOTICES.md", true);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { Scene }, locationPathName = path, target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Composition build failed: " + report.summary.result);
            Debug.Log("COMPOSITION_BUILD_SUCCESS " + Path.GetFullPath(path) + " bytes=" + report.summary.totalSize);
        }

        public static void VerifyScene()
        {
            EditorSceneManager.OpenScene(Scene);
            var game = UnityEngine.Object.FindObjectOfType<PianoDefenseGame>(); var ui = UnityEngine.Object.FindObjectOfType<PianoCompositionUI>();
            if (game == null || game.Board == null || ui == null || !game.CompositionMode) throw new Exception("Composition scene missing controller, board or Canvas.");
            for (int i = 0; i < 4; i++)
            {
                int count = 0; foreach (var entry in game.Board.ReadStartingTowers(i)) { if (!DefenseSimulation.IsBuildCell(entry.X, entry.Y)) throw new Exception("Invalid authored tower in lesson " + i); count++; }
                if (count == 0) throw new Exception("Lesson " + i + " has no authored example towers.");
            }
            if (ui.RhythmButtons.Length != 4 || ui.NoteDots.Length != 128 || ui.LessonButtons.Length != 4 || ui.BoardViewport == null) throw new Exception("UI references incomplete.");
            Debug.Log("COMPOSITION_SCENE_CHECKS_PASS lessons=4 realCanvas=true realBoard=true");
        }
    }
}

using System;
using System.IO;
using System.Linq;
using TapTapGameJam.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class MemuSceneSetup
{
    const string Art = "Assets/Sprites/UI/Menu/";
    const string ScenePath = "Assets/Scenes/Memu.unity";

    public static void CreateBatch()
    {
        try { Create(); Debug.Log("MEMU_SETUP_PASS"); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    public static void AddMusicBatch()
    {
        try { AddMusic(); Debug.Log("MEMU_MUSIC_SETUP_PASS"); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    public static void AddClickSoundBatch()
    {
        try { AddClickSound(); Debug.Log("MEMU_CLICK_SETUP_PASS"); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    [MenuItem("Tools/Menu/Assign Click Sound")]
    public static void AddClickSound()
    {
        const string path = "Assets/Audio/Menu/UI_Click_WoodBell.wav";
        // A tiny original wood/mallet strike with a soft bell overtone, matching the musical forest.
        if (!File.Exists(path))
        {
            const int rate = 48000;
            const double duration = .22;
            int samples = (int)(rate * duration);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var output = new BinaryWriter(File.Create(path)))
            {
                output.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); output.Write(36 + samples * 2);
                output.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); output.Write(16);
                output.Write((short)1); output.Write((short)1); output.Write(rate); output.Write(rate * 2);
                output.Write((short)2); output.Write((short)16);
                output.Write(System.Text.Encoding.ASCII.GetBytes("data")); output.Write(samples * 2);
                var noise = new System.Random(17);
                for (int i = 0; i < samples; i++)
                {
                    double t = i / (double)rate;
                    double phase = 2 * Math.PI * 659.255 * t;
                    double strike = Math.Sin(phase) * Math.Exp(-t / .055)
                        + .26 * Math.Sin(phase * 2.76) * Math.Exp(-t / .025)
                        + .10 * Math.Sin(phase * 5.4) * Math.Exp(-t / .012)
                        + .08 * Math.Sin(phase * 2) * Math.Exp(-t / .075)
                        + .12 * (noise.NextDouble() * 2 - 1) * Math.Exp(-t / .002);
                    double envelope = (1 - Math.Exp(-t / .0007)) * Math.Min(1, (duration - t) / .035);
                    output.Write((short)(Math.Max(-1, Math.Min(1, strike * envelope * .42)) * short.MaxValue));
                }
            }
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.PCM;
        importer.defaultSampleSettings = settings;
        importer.loadInBackground = false;
        importer.SaveAndReimport();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var obj = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "MenuClickSound");
        if (obj == null) obj = new GameObject("MenuClickSound");
        var sound = obj.GetComponent<AudioSource>();
        if (sound == null) sound = obj.AddComponent<AudioSource>();
        sound.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        sound.playOnAwake = false;
        sound.loop = false;
        sound.spatialBlend = 0;
        sound.volume = .8f;
        sound.priority = 32;
        var controller = UnityEngine.Object.FindObjectOfType<MemuController>();
        var fields = new SerializedObject(controller);
        fields.FindProperty("clickSound").objectReferenceValue = sound;
        fields.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tools/Menu/Assign Background Music")]
    public static void AddMusic()
    {
        const string audioPath = "Assets/Audio/Menu/LV1_Full Song.wav";
        AssetDatabase.ImportAsset(audioPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (AudioImporter)AssetImporter.GetAtPath(audioPath);
        if (importer == null) throw new FileNotFoundException(audioPath);
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.Streaming;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = .75f;
        importer.defaultSampleSettings = settings;
        importer.loadInBackground = true;
        importer.SaveAndReimport();
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);
        if (clip == null) throw new InvalidOperationException("Menu music could not be imported.");
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var musicObject = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "MenuMusic");
        if (musicObject == null) musicObject = new GameObject("MenuMusic");
        var music = musicObject.GetComponent<AudioSource>();
        if (music == null) music = musicObject.AddComponent<AudioSource>();
        music.clip = clip;
        music.playOnAwake = true;
        music.loop = true;
        music.volume = .45f;
        music.spatialBlend = 0;
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Menu music: " + clip.name + ", duration=" + clip.length + "s, streaming, loop, volume=0.45");
    }

    [MenuItem("Tools/Menu/Create Memu Scene")]
    public static void Create()
    {
        if (File.Exists(ScenePath))
            throw new InvalidOperationException("Memu already exists. Edit the saved scene directly; it will not be overwritten.");
        if (!File.Exists("Assets/Scenes/Map.unity")) throw new FileNotFoundException("Map scene missing");
        var background = Import("MenuBackground.png", 8192);
        var startSprite = Import("StartGame_Lettering_v1.png", 4096);
        var quitSprite = Import("QuitGame_Lettering_v1.png", 4096);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0, 0, -10);
        var camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.035f, .08f, .09f);

        var canvasObject = new GameObject("MemuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        // Fit the whole 16:9 composition inside any display; unused space keeps the forest color.
        var content = new GameObject("MenuComposition", typeof(RectTransform), typeof(AspectRatioFitter));
        content.transform.SetParent(canvasObject.transform, false);
        var frame = (RectTransform)content.transform;
        frame.anchorMin = frame.anchorMax = new Vector2(.5f, .5f);
        var fitter = content.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 16f / 9f;
        var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(frame, false);
        var backgroundRect = (RectTransform)backgroundObject.transform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
        var backgroundImage = backgroundObject.GetComponent<Image>();
        backgroundImage.sprite = background;
        backgroundImage.raycastTarget = false;

        var start = MakeButton(frame, "StartGameButton", startSprite, 300, 357);
        var quit = MakeButton(frame, "QuitGameButton", quitSprite, 300, 436);
        var controller = new GameObject("MenuActions", typeof(MemuController)).GetComponent<MemuController>();
        var fields = new SerializedObject(controller);
        fields.FindProperty("gameplayScene").stringValue = "Map";
        fields.FindProperty("startButton").objectReferenceValue = start;
        fields.FindProperty("quitButton").objectReferenceValue = quit;
        fields.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddPersistentListener(start.onClick, controller.StartGame);
        UnityEventTools.AddPersistentListener(quit.onClick, controller.QuitGame);
        var navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = quit, selectOnDown = quit };
        start.navigation = navigation;
        navigation.selectOnUp = navigation.selectOnDown = start;
        quit.navigation = navigation;
        var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).GetComponent<EventSystem>();
        events.firstSelectedGameObject = start.gameObject;

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] {
            new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene("Assets/Scenes/Map.unity", true)
        }.Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath && s.path != "Assets/Scenes/Map.unity")).ToArray();
        Selection.activeGameObject = canvasObject;
    }

    static Sprite Import(string name, int maximum)
    {
        var path = Art + name;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) throw new FileNotFoundException(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = maximum;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Button MakeButton(RectTransform parent, string name, Sprite sprite, float x, float y)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform;
        // Relative anchors keep the layout inside the artwork on every aspect ratio.
        rect.anchorMin = rect.anchorMax = new Vector2(x / 1280, 1 - y / 720);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(320, 320f * sprite.rect.height / sprite.rect.width);
        var image = obj.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        var button = obj.GetComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = colors.selectedColor = new Color(1, .96f, .76f, 1);
        colors.pressedColor = new Color(.65f, .83f, .78f, 1);
        colors.disabledColor = new Color(.7f, .7f, .7f, .65f);
        colors.fadeDuration = .08f;
        button.colors = colors;
        return button;
    }
}

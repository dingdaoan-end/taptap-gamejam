using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TapTapGameJam.PianoDefense;

namespace TapTapGameJam.MusicGarden.Editor
{
    public static class GardenBuilder
    {
        public const string ScenePath="Assets/Scenes/MusicGarden.unity";
        public const string ConfigPath="Assets/MusicGarden/Data/GardenConfig.asset";
        [Serializable] class MapDocument { public RegionDefinition[] regions; }
        static GardenConfig config;
        static readonly Color Navy=new Color(.045f,.075f,.10f,.97f),Panel=new Color(.08f,.13f,.17f,.98f),Ink=new Color(.15f,.23f,.28f),Ivory=new Color(.96f,.92f,.80f),Gold=new Color(.93f,.76f,.43f);
        [MenuItem("Tools/Music Garden/Create Playable Scene")]
        public static void Create()
        {
            AssetDatabase.Refresh();
            var actor=ImportSheet("Assets/MusicGarden/Art/Actors.png",3,2,new[]{"Piano","ChordPiano","Core","Note","FastNote","ChordNote"});
            var terrain=ImportSheet("Assets/MusicGarden/Art/Terrain.png",2,2,new[]{"Grass","Road","Blocker","Pad"});
            config=AssetDatabase.LoadAssetAtPath<GardenConfig>(ConfigPath);
            if(config==null){config=ScriptableObject.CreateInstance<GardenConfig>();AssetDatabase.CreateAsset(config,ConfigPath);}
            config.actors=actor;config.terrain=terrain;config.bpm=100;
            CreatePrefabs(actor);
            config.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/PianoDefense/Art/Fonts/NotoSansCJKsc-Regular.otf");
            config.circle=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PianoDefense/Art/Effects/circle_01.png");
            var tex=new Texture2D(8,8);tex.SetPixels(Enumerable.Repeat(Color.white,64).ToArray());tex.Apply();
            File.WriteAllBytes("Assets/MusicGarden/Art/Square.png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset("Assets/MusicGarden/Art/Square.png");
            var ti=(TextureImporter)AssetImporter.GetAtPath("Assets/MusicGarden/Art/Square.png");ti.textureType=TextureImporterType.Sprite;ti.spritePixelsPerUnit=8;ti.SaveAndReimport();config.square=AssetDatabase.LoadAssetAtPath<Sprite>(ti.assetPath);
            config.samples=new[]{"C4","D4","E4","F4","G4","A4","B4","C5"}.Select(n=>AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/PianoDefense/Art/Audio/"+n+".wav")).ToArray();
            config.regions=JsonUtility.FromJson<MapDocument>(File.ReadAllText("Assets/MusicGarden/Data/MapLayout.json")).regions;
            foreach(var r in config.regions)GardenContent.Prepare(r);
            string[] titles={"初见 · 一座琴，一段路","应答 · 让声部错开","合奏 · 单音与和弦","涌现 · 完成你的编排"};
            string[] instructions={"点空闲草地布塔，石台是推荐位置。选普通琴塔按 R，用 A / S / D 预约 Do / Mi / Sol；击杀才响音。","试着把一座琴塔改为反拍，让其他琴塔接话。障碍会遮挡攻击，建造前查看紫色有效格。","和弦怪有 3 生命。和弦琴塔在重拍清理它们；普通塔可慢慢拆解。三音与另一个声部同拍应答，可让敌群休止。","选择位置、节拍和领奏时机。四区共用核心；让多个真实击杀形成合奏，再听这局自己演出的音乐。"};
            config.stages=Enumerable.Range(0,4).Select(i=>new GardenStage { regionId=GardenContent.Order[i],budget=GardenContent.Budgets[i],segments=i>=2?3:2,title=titles[i],instruction=instructions[i],waves=GardenContent.Waves(i,i>=2?3:2) }).ToArray();
            EditorUtility.SetDirty(config);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var backdrop=new GameObject("界面背景清屏相机").AddComponent<Camera>();backdrop.clearFlags=CameraClearFlags.SolidColor;backdrop.backgroundColor=new Color(.035f,.06f,.08f);backdrop.cullingMask=0;backdrop.depth=-10;
            var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=13;camera.transform.position=new Vector3(24.5f,-12.5f,-10);camera.backgroundColor=new Color(.035f,.06f,.08f);camera.clearFlags=CameraClearFlags.SolidColor;camera.rect=GardenView.MapViewport(1600,900);camera.gameObject.AddComponent<AudioListener>();
            var root=new GameObject("夜之乐章 · Playable Game");var audio=root.AddComponent<PianoAudio>();var game=root.AddComponent<GardenGame>();game.config=config;game.boardCamera=camera;root.AddComponent<GardenPlaycheck>();
            var data=new SerializedObject(audio);data.FindProperty("pianoSamples").arraySize=8;for(int i=0;i<8;i++)data.FindProperty("pianoSamples").GetArrayElementAtIndex(i).objectReferenceValue=config.samples[i];data.ApplyModifiedPropertiesWithoutUndo();
            var view=new GameObject("World Presentation").AddComponent<GardenView>();view.config=config;game.view=view;
            view.towerRoot=new GameObject("Placed Pianos").transform;view.enemyRoot=new GameObject("Live Notes").transform;view.effectRoot=new GameObject("Attack and Range Effects").transform;
            foreach(var r in config.regions)
            {
                var regionRoot=new GameObject(r.id+" "+r.quadrant+" · Editable Region").transform;
                var tiles=new GameObject("地块 · 改 GardenTile.kind 可改地形").transform;tiles.SetParent(regionRoot,false);
                for(int y=0;y<r.rows;y++)for(int x=0;x<r.columns;x++)
                {
                    char code=r.cells[y][x];var sr=GardenView.Sprite(code+" "+x+","+y,tiles,terrain[code=='G'?0:code=='X'?2:1],1,0);sr.transform.position=GardenView.Position(new Cell(x,y),r.id);
                    var tile=sr.gameObject.AddComponent<GardenTile>();tile.config=config;tile.regionId=r.id;tile.cell=new Cell(x,y);tile.kind=code=='G'?GardenTerrain.Grass:code=='R'?GardenTerrain.Road:code=='X'?GardenTerrain.Blocker:GardenTerrain.Base;
                    if(code=='B')sr.color=new Color(.75f,.67f,.47f);
                }
                var pads=new GameObject("建造位 · GardenPad").transform;pads.SetParent(regionRoot,false);
                var chosen=new List<Cell>(GardenContent.Suggested(r));
                foreach(var p in r.pads.OrderByDescending(p=>r.route.Count(c=>MapRules.InRange(p,c,true)&&MapRules.Visible(r,p,c))))
                    if(chosen.Count<12&&!chosen.Contains(p)&&chosen.All(c=>Math.Abs(c.x-p.x)+Math.Abs(c.y-p.y)>=3))chosen.Add(p);
                r.pads=chosen.ToArray();
                foreach(var p in chosen)
                {
                    var sr=GardenView.Sprite("Build Pad "+p.x+","+p.y,pads,terrain[3],1,5);sr.transform.position=GardenView.Position(p,r.id);
                    var pad=sr.gameObject.AddComponent<GardenPad>();pad.regionId=r.id;pad.cell=p;pad.suggested=Array.IndexOf(chosen.ToArray(),p)<3;
                }
                // An editable preset in every region: these objects are read by the runtime.
                var presets=new GameObject("预设琴塔 · 可移动到石台或修改节拍").transform;presets.SetParent(regionRoot,false);
                var temporary=new GardenSimulation(r,config.stages.First(s=>s.regionId==r.id).waves,config.stages.First(s=>s.regionId==r.id).budget);GardenContent.Arrange(temporary);
                foreach(var t in temporary.towers)
                {
                    var obj=(GameObject)PrefabUtility.InstantiatePrefab(config.towerPrefabs[0],presets);obj.name="预设钢琴 "+t.id;obj.transform.position=GardenView.Position(t.cell,r.id);
                    var setup=obj.GetComponent<GardenTowerSetup>();setup.regionId=r.id;setup.rhythm=t.rhythm;
                }
            }
            var core=GardenView.Sprite("中央音乐核心",null,actor[2],6.8f,10);core.transform.position=new Vector3(24.5f,-12.5f,0);
            view.entrance=GardenView.Sprite("入口光圈",view.effectRoot,config.circle,1.2f,8);view.entrance.color=new Color(.93f,.48f,.39f,.55f);
            var marker=new GameObject("入口标记").AddComponent<TextMesh>();marker.font=config.font;marker.fontSize=48;marker.characterSize=.07f;marker.anchor=TextAnchor.MiddleCenter;marker.GetComponent<MeshRenderer>().sharedMaterial=config.font.material;marker.GetComponent<MeshRenderer>().sortingOrder=55;view.entranceLabel=marker;
            BuildHUD(game);
            new GameObject("EventSystem").AddComponent<EventSystem>().sendNavigationEvents=false;
            var input=UnityEngine.Object.FindObjectOfType<EventSystem>().gameObject.AddComponent<StandaloneInputModule>();input.horizontalAxis="Horizontal";input.verticalAxis="Vertical";input.submitButton="Submit";input.cancelButton="Cancel";
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ScenePath);EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
            Debug.Log("GARDEN_SCENE_READY tiles=1300 regions=4 scene="+ScenePath);
        }
        static Sprite[] ImportSheet(string path,int columns,int rows,string[] names)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;
            importer.GetSourceTextureWidthAndHeight(out int w,out int h);float cw=(float)w/columns,ch=(float)h/rows;importer.spritePixelsPerUnit=cw;
            importer.spritesheet=names.Select((n,i)=>new SpriteMetaData { name=n,rect=new Rect(i%columns*cw,(rows-1-i/columns)*ch,cw,ch),alignment=(int)SpriteAlignment.Center,pivot=new Vector2(.5f,.5f) }).ToArray();importer.SaveAndReimport();
            var assets=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();return names.Select(n=>assets.First(s=>s.name==n)).ToArray();
        }
        static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
        {
            var obj=new GameObject(name,typeof(RectTransform));var rt=obj.GetComponent<RectTransform>();rt.SetParent(parent,false);rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);return rt;
        }
        static Image PanelAt(string name,Transform parent,float x,float y,float w,float h,Color color)
        { var rt=Rect(name,parent,x,y,w,h);var image=rt.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image; }
        static Text TextAt(string name,Transform parent,float x,float y,float w,float h,string value,int size,Color color)
        { var rt=Rect(name,parent,x,y,w,h);var t=rt.gameObject.AddComponent<Text>();t.font=config.font;t.fontSize=size;t.color=color;t.text=value;t.raycastTarget=false;t.verticalOverflow=VerticalWrapMode.Truncate;t.resizeTextForBestFit=true;t.resizeTextMinSize=Mathf.Max(10,size-8);t.resizeTextMaxSize=size;return t; }
        static Button ButtonAt(string name,Transform parent,float x,float y,float w,float h,string label,Action action=null)
        {
            var rt=Rect(name,parent,x,y,w,h);var image=rt.gameObject.AddComponent<Image>();image.color=Ink;var b=rt.gameObject.AddComponent<Button>();b.targetGraphic=image;var nav=b.navigation;nav.mode=Navigation.Mode.None;b.navigation=nav;
            var txt=TextAt("Label",rt,5,1,w-10,h-2,label,16,Ivory);txt.alignment=TextAnchor.MiddleCenter;
            return b;
        }
        static void BuildHUD(GardenGame game)
        {
            var obj=new GameObject("Canvas · 可直接修改的界面",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));var canvas=obj.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=obj.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
            var hud=obj.AddComponent<GardenHUD>();hud.game=game;game.hud=hud;var root=obj.transform;
            PanelAt("Header",root,0,0,1600,96,Navy);PanelAt("Gold separator",root,32,95,1536,2,new Color(.7f,.57f,.32f,.5f));
            hud.title=TextAt("Title",root,40,15,280,48,"夜之乐章",34,Ivory);TextAt("Subtitle",root,42,64,360,24,"NOCTURNE  /  从防线中涌现音乐",14,Gold);
            hud.status=TextAt("Core and Economy",root,770,26,750,34,"",21,Ivory);
            hud.stage=TextAt("Stage",root,40,106,1010,32,"",21,Ivory);
            hud.mode=TextAt("Phase",root,1085,109,140,32,"整备",18,Gold);
            var side=PanelAt("Tower score",root,1260,110,310,590,Panel).transform;
            hud.selection=TextAt("Selection",side,20,18,270,30,"",22,Ivory);
            hud.normal=ButtonAt("Build Piano",side,20,63,130,40,"钢琴 · 8 币");hud.chord=ButtonAt("Build Chord",side,160,63,130,40,"和弦 · 16 币");
            hud.arrange=ButtonAt("Arrange",side,20,113,270,36,"使用示范编排");
            TextAt("Rhythm caption",side,20,163,270,25,"伴奏节拍 · 四拍一循环",14,Gold);
            hud.rhythms=Enumerable.Range(0,4).Select(i=>ButtonAt("Rhythm "+i,side,20+i*68,192,62,32,GardenView.RhythmName((GardenRhythm)i))).ToArray();
            TextAt("Filter caption",side,20,236,270,20,"音高筛选",14,Gold);
            hud.filters=Enumerable.Range(0,3).Select(i=>ButtonAt("Filter "+i,side,20+i*92,262,85,30,new[]{"全部","只弹","跳过"}[i])).ToArray();
            hud.pitches=Enumerable.Range(0,3).Select(i=>ButtonAt("Pitch "+i,side,20+i*92,300,85,30,new[]{"Do ○","Mi ◇","Sol △"}[i])).ToArray();
            hud.lead=ButtonAt("Lead",side,20,345,177,38,"设为领奏 R");hud.sell=ButtonAt("Sell",side,206,345,84,38,"回收");
            hud.forecast=TextAt("Forecast",side,20,400,270,185,"",15,Ivory);
            hud.help=TextAt("Tutorial",root,40,648,1190,47,"",16,new Color(.68f,.77f,.75f));
            hud.notice=TextAt("Feedback",root,40,699,1170,27,"",17,Ivory);
            var bottom=PanelAt("Performance desk",root,30,737,1540,123,Panel).transform;
            hud.slot=TextAt("Queued note",bottom,18,10,720,24,"",15,Gold);
            hud.keys=Enumerable.Range(0,3).Select(i=>ButtonAt("Play "+i,bottom,18+i*145,40,132,68,new[]{"A   Do ○","S   Mi ◇","D   Sol △"}[i])).ToArray();
            foreach(var b in hud.keys)b.GetComponentInChildren<Text>().color=Navy;
            hud.record=TextAt("Performance record",bottom,485,10,660,25,"",15,Ivory);
            hud.dots=new Image[48];for(int row=0;row<3;row++)for(int col=0;col<16;col++)
            { var image=PanelAt("Beat "+col+" Pitch "+row,bottom,493+col*29,43+row*22,18,13,Ink);image.sprite=config.circle;hud.dots[row*16+col]=image; }
            hud.beats=Enumerable.Range(0,4).Select(i=>PanelAt("Pulse "+i,bottom,1000+i*34,60,23,23,Ink)).ToArray();
            TextAt("Beat caption",bottom,994,10,190,32,"四拍节奏",15,Gold);
            hud.primary=ButtonAt("Primary",bottom,1170,22,348,52,"开始演奏 SPACE");hud.primary.GetComponent<Image>().color=new Color(.27f,.48f,.46f);
            TextAt("Controls",root,40,875,670,20,"左键：选草地 / 琴塔    R：领奏    A S D：选音    空格：开始 / 暂停",13,new Color(.6f,.7f,.7f));
            hud.replay=ButtonAt("Replay",root,800,870,175,28,"回放真实演奏");hud.retry=ButtonAt("Retry",root,985,870,145,28,"重试本区");hud.mute=ButtonAt("Mute",root,1140,870,145,28,"关闭声音");hud.restart=ButtonAt("Restart",root,1295,870,250,28,"重新编排整局");
        }
        static void CreatePrefabs(Sprite[] sprites)
        {
            Directory.CreateDirectory("Assets/MusicGarden/Prefabs");
            var prefabs=new List<GameObject>();
            foreach(int index in new[]{0,1,3,4,5})
            {
                var sr=GardenView.Sprite(sprites[index].name,null,sprites[index],index<2?1.28f:index==5?1.05f:.82f,index<2?30:45);
                if(index<2){var setup=sr.gameObject.AddComponent<GardenTowerSetup>();setup.chord=index==1;}
                prefabs.Add(PrefabUtility.SaveAsPrefabAsset(sr.gameObject,"Assets/MusicGarden/Prefabs/"+sprites[index].name+".prefab"));UnityEngine.Object.DestroyImmediate(sr.gameObject);
            }
            config.towerPrefabs=prefabs.Take(2).ToArray();config.enemyPrefabs=prefabs.Skip(2).ToArray();
        }
        [MenuItem("Tools/Music Garden/Open Playable Scene")]
        public static void Open() { EditorSceneManager.OpenScene(ScenePath); }
        [MenuItem("Tools/Music Garden/Build Windows")]
        public static void Build()
        {
            PlayerSettings.companyName="GameJamTeam";PlayerSettings.productName="夜之乐章 Nocturne";PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone,ScriptingImplementation.Mono2x);
            Directory.CreateDirectory("Builds/MusicGarden-v0.4");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{ScenePath},locationPathName="Builds/MusicGarden-v0.4/Nocturne.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None });
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
            Debug.Log("GARDEN_BUILD_SUCCESS bytes="+report.summary.totalSize);
        }
    }
}

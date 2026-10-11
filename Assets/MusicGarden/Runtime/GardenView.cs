using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TapTapGameJam.MusicGarden
{
    public sealed class GardenView : MonoBehaviour
    {
        public GardenConfig config;
        public Transform towerRoot,enemyRoot,effectRoot;
        public SpriteRenderer entrance;
        public TextMesh entranceLabel;
        readonly Dictionary<string,SpriteRenderer> towers=new Dictionary<string,SpriteRenderer>();
        readonly Dictionary<int,SpriteRenderer> enemies=new Dictionary<int,SpriteRenderer>();
        readonly Dictionary<string,Vector3> towerScales=new Dictionary<string,Vector3>();
        readonly Dictionary<string,Color> towerColors=new Dictionary<string,Color>();
        readonly Dictionary<string,float> pulses=new Dictionary<string,float>();
        readonly List<SpriteRenderer> range=new List<SpriteRenderer>();
        readonly List<LineRenderer> shots=new List<LineRenderer>();
        GardenPad[] pads;
        int current=-1;
        bool framed,framedOverview;
        int framedRegion=-1;
        float frameStarted,fromSize;
        Vector3 fromPosition;
        const float CameraMoveDuration=1.4f;
        public int TowerCount(int region) => towers.Keys.Count(k=>k.StartsWith(region+":"));
        public Transform TowerObject(int region,int id) => towers.TryGetValue(region+":"+id,out var sr)?sr.transform:null;
        Material lineMaterial;
        public static Vector3 Offset(int region) => new Vector3(region==2||region==4?25:0,region>=3?-13:0,0);
        public static Vector3 Position(Cell cell,int region) => Offset(region)+new Vector3(cell.x,-cell.y,0);
        public static Cell LocalCell(Vector3 p,int region) { p-=Offset(region);return new Cell(Mathf.RoundToInt(p.x),Mathf.RoundToInt(-p.y)); }
        public static Color PitchColor(int p) => p==0?new Color(.46f,.88f,.83f):p==2?new Color(.96f,.8f,.41f):new Color(.98f,.55f,.48f);
        public static Rect MapViewport(int screenWidth,int screenHeight,int columns=25,int rows=13)
        {
            const float x=.025f,y=.29f,width=.75f,height=.54f;
            float pixels=Mathf.Max(1,Mathf.Floor(Mathf.Min(width*screenWidth/columns,height*screenHeight/rows)));
            float w=pixels*columns,h=pixels*rows;
            float left=Mathf.Round((x+width*.5f)*screenWidth-w*.5f);
            float bottom=Mathf.Round((y+height*.5f)*screenHeight-h*.5f);
            return new Rect(left/screenWidth,bottom/screenHeight,w/screenWidth,h/screenHeight);
        }
        void Awake()
        {
            pads=FindObjectsOfType<GardenPad>();lineMaterial=new Material(Shader.Find("Sprites/Default"));
            foreach(var setup in FindObjectsOfType<GardenTowerSetup>().Where(s=>s.enabled&&s.gameObject.activeInHierarchy))setup.GetComponent<SpriteRenderer>().enabled=false;
            for(int i=0;i<25;i++){var sr=Sprite("有效射程 "+i,effectRoot,config.square,0,20);range.Add(sr);sr.enabled=false;}
            for(int i=0;i<24;i++)
            {
                var line=new GameObject("琴塔音波 "+i).AddComponent<LineRenderer>();line.transform.SetParent(effectRoot,false);line.sharedMaterial=lineMaterial;line.positionCount=2;line.startWidth=.04f;line.endWidth=.12f;line.sortingOrder=40;line.enabled=false;shots.Add(line);
            }
            current=-1;
        }
        public static SpriteRenderer Sprite(string name,Transform parent,Sprite sprite,float size,int order)
        {
            var obj=new GameObject(name);obj.transform.SetParent(parent,false);obj.transform.localScale=Vector3.one*size;
            var sr=obj.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sortingOrder=order;return sr;
        }
        TextMesh Label(Transform parent,string text,float y,float size=0.16f)
        {
            var obj=new GameObject("音符标识");obj.transform.SetParent(parent,false);obj.transform.localPosition=new Vector3(0,y,-.1f);
            var tm=obj.AddComponent<TextMesh>();tm.font=config.font;tm.fontSize=48;tm.characterSize=size;tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.text=text;
            obj.GetComponent<MeshRenderer>().sharedMaterial=config.font.material;obj.GetComponent<MeshRenderer>().sortingOrder=60;return tm;
        }
        public void EnsureTower(int region,GardenTower t)
        {
            string key=region+":"+t.id;if(towers.ContainsKey(key))return;
            var authored=FindObjectsOfType<GardenTowerSetup>().FirstOrDefault(s=>s.enabled&&s.gameObject.activeInHierarchy&&s.regionId==region&&LocalCell(s.transform.position,region).Equals(t.cell));
            var source=authored!=null?authored.gameObject:config.towerPrefabs[t.chord?1:0];
            var obj=Instantiate(source,towerRoot);obj.name=(t.chord?"和弦钢琴 ":"钢琴 ")+key;
            var setup=obj.GetComponent<GardenTowerSetup>();if(setup!=null)setup.enabled=false;
            var sr=obj.GetComponent<SpriteRenderer>();sr.enabled=true;
            sr.transform.position=Position(t.cell,region)+new Vector3(0,.06f,0);
            Label(sr.transform,t.chord?"和弦":"自动",-.46f,.04f);towers[key]=sr;towerScales[key]=sr.transform.localScale;towerColors[key]=sr.color;
        }
        public void RemoveTower(int region,int id) { string key=region+":"+id;if(towers.TryGetValue(key,out var sr)){Destroy(sr.gameObject);towers.Remove(key);} }
        public void ClearRegion(int region)
        {
            foreach(var key in towers.Keys.Where(k=>k.StartsWith(region+":")).ToArray()){Destroy(towers[key].gameObject);towers.Remove(key);}
        }
        public void ClearAllTowers() { foreach(var t in towers.Values)Destroy(t.gameObject);towers.Clear(); }
        public void PulseTower(int region,int id,int pitch) { pulses[region+":"+id]=Time.unscaledTime; }
        public void StageChanged(GardenGame g)
        {
            foreach(var e in enemies.Values)Destroy(e.gameObject);enemies.Clear();current=g.Stage.regionId;
            foreach(var t in g.model.towers)EnsureTower(current,t);
            if(entrance!=null)entrance.transform.position=Position(g.model.region.route[0],current);
            if(entranceLabel!=null) { entranceLabel.transform.position=Position(g.model.region.route[0],current)+new Vector3(0,.75f,0);entranceLabel.text="入口 →"; }
        }
        public void Refresh(GardenGame g)
        {
            if(pads==null)return;
            if(current!=g.Stage.regionId)StageChanged(g);
            bool overview=g.phase==GardenPhase.Overview||g.phase==GardenPhase.Won||g.replaying;
            Vector3 target=overview?new Vector3(24.5f,-12.5f,-10):Offset(current)+new Vector3(12,-6,-10);
            float scale=overview?13:6.5f;
            var viewport=MapViewport(Screen.width,Screen.height);
            g.boardCamera.pixelRect=new Rect(Mathf.Round(viewport.x*Screen.width),Mathf.Round(viewport.y*Screen.height),Mathf.Round(viewport.width*Screen.width),Mathf.Round(viewport.height*Screen.height));
            // Unity rounds the viewport to pixels; keep the projection on exact tile boundaries.
            g.boardCamera.aspect=25f/13f;
            if(!framed)
            {
                fromPosition=target;fromSize=scale;frameStarted=Time.unscaledTime-CameraMoveDuration;framed=true;framedOverview=overview;framedRegion=current;
            }
            else if(framedOverview!=overview||framedRegion!=current)
            {
                fromPosition=g.boardCamera.transform.position;fromSize=g.boardCamera.orthographicSize;frameStarted=Time.unscaledTime;framedOverview=overview;framedRegion=current;
            }
            float progress=Mathf.Clamp01((Time.unscaledTime-frameStarted)/CameraMoveDuration);
            progress=progress*progress*(3-2*progress);
            g.boardCamera.transform.position=Vector3.Lerp(fromPosition,target,progress);
            g.boardCamera.orthographicSize=Mathf.Lerp(fromSize,scale,progress);
            foreach(var pad in pads)
            {
                var sr=pad.GetComponent<SpriteRenderer>();bool occupied=towers.Values.Any(t=>Vector3.Distance(t.transform.position,Position(pad.cell,pad.regionId))<.3f);
                sr.enabled=!occupied;
                sr.color=pad.regionId==current?Color.white:new Color(.4f,.5f,.5f,.6f);
                if(g.selectedPad.HasValue&&pad.regionId==current&&pad.cell.Equals(g.selectedPad.Value))sr.color=new Color(1,1,.55f);
            }
            foreach(var t in g.display.towers)EnsureTower(current,t);
            foreach(var pair in towers)
            {
                bool active=pair.Key.StartsWith(current+":");var tower=g.display.towers.Find(t=>pair.Key==current+":"+t.id);
                float pulse=pulses.TryGetValue(pair.Key,out float time)?Mathf.Max(0,1-(Time.unscaledTime-time)*4):0;
                pair.Value.transform.localScale=towerScales[pair.Key]*(1+pulse*.11f);pair.Value.color=towerColors[pair.Key]*(active||overview?Color.white:new Color(.4f,.5f,.5f));
                if(tower!=null) { var label=pair.Value.GetComponentInChildren<TextMesh>();label.text=tower.id==g.model.leadId?"领奏 A S D":tower.chord?"和弦 ①":RhythmName(tower.rhythm);label.color=tower.id==g.model.leadId?PitchColor(0):new Color(.98f,.91f,.72f); }
            }
            foreach(int id in enemies.Keys.Where(id=>!g.display.enemies.Any(e=>e.id==id)).ToArray()){Destroy(enemies[id].gameObject);enemies.Remove(id);}
            foreach(var e in g.display.enemies)
            {
                if(!enemies.TryGetValue(e.id,out var sr))
                {
                    var obj=Instantiate(config.enemyPrefabs[e.chord?2:e.speed==2?1:0],enemyRoot);obj.name="音符小怪 "+e.id;
                    sr=obj.GetComponent<SpriteRenderer>();Label(sr.transform,"",.48f,.045f);enemies[e.id]=sr;
                }
                Cell destination=g.display.Position(e);var start=g.display.region.route[Mathf.Clamp(e.previous,0,g.display.region.route.Length-1)];
                float transition=g.paused?1:Mathf.Clamp01(g.BeatProgress*4);
                Vector3 forward=Position(destination,current)-Position(start,current);
                if(forward.sqrMagnitude<.01f&&e.progress+1<g.display.region.route.Length)forward=Position(g.display.region.route[e.progress+1],current)-Position(destination,current);
                var lane=new Vector3(-forward.y,forward.x,0).normalized*((e.pitch/2-1)*.28f);
                sr.transform.position=Vector3.Lerp(Position(start,current),Position(destination,current),transition)+lane+new Vector3(0,Mathf.Sin(Time.unscaledTime*5+e.id)*.035f,0);
                sr.color=e.hold>0?new Color(.75f,.75f,1):Color.white;
                var label=sr.GetComponentInChildren<TextMesh>();label.text=e.chord?"和"+e.hp:e.hold>0?"∥":new[]{"○","◇","△"}[e.pitch/2]+(e.speed==2?"»":"");label.color=PitchColor(e.pitch);
            }
            var selected=g.Selected;Cell? center=selected!=null?(Cell?)selected.cell:g.selectedPad;bool chord=selected!=null&&selected.chord;
            int n=0;
            if(center.HasValue&&!overview)
            {
                for(int y=0;y<g.model.region.rows;y++)for(int x=0;x<g.model.region.columns;x++)
                {
                    var c=new Cell(x,y);if(!MapRules.InRange(center.Value,c,chord,g.config.diamondChordRange)||n>=range.Count)continue;
                    var sr=range[n++];sr.enabled=true;sr.transform.position=Position(c,current);sr.transform.localScale=Vector3.one*.93f;
                    sr.color=MapRules.Visible(g.model.region,center.Value,c)?new Color(.65f,.49f,1,.27f):new Color(1,.32f,.25f,.28f);
                }
            }
            for(int i=n;i<range.Count;i++)range[i].enabled=false;
            float fade=Mathf.Clamp01(1-(float)(AudioSettings.dspTime-g.presentedTime)*3);
            for(int i=0;i<shots.Count;i++)
            {
                bool show=i<g.visibleShots.Count&&fade>0&&!overview;shots[i].enabled=show;if(!show)continue;
                var s=g.visibleShots[i];var color=PitchColor(s.pitch);color.a=fade;shots[i].startColor=color;shots[i].endColor=color;
                shots[i].SetPosition(0,Position(s.from,current));shots[i].SetPosition(1,Position(s.to,current));
            }
        }
        public static string RhythmName(GardenRhythm r) => new[]{"●●●●","●·●·","·●·●","●···"}[(int)r];
        void OnDestroy(){if(lineMaterial!=null)Destroy(lineMaterial);}
    }
}

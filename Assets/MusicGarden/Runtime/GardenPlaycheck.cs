using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TapTapGameJam.MusicGarden
{
    // Only enabled by an explicit command-line flag; never changes ordinary player sessions.
    public sealed class GardenPlaycheck : MonoBehaviour
    {
        static void Check(bool condition,string message) { if(!condition)throw new Exception("GARDEN_CHECK_FAIL "+message);Debug.Log("GARDEN_CHECK_PASS "+message); }
        IEnumerator Start()
        {
            if(!Environment.GetCommandLineArgs().Contains("-garden-check")){Destroy(this);yield break;}
            var g=FindObjectOfType<GardenGame>();yield return null;
            yield return new WaitForEndOfFrame();Capture("MusicGarden-overview.png");
            bool failed=false;
            try { Run(g); } catch(Exception e) { Debug.LogException(e);failed=true; }
            if(failed){Application.Quit(1);yield break;}
            yield return new WaitForSecondsRealtime(12);
            CheckFrame(g.boardCamera,-.5f,-12.5f,24.5f,.5f,"quarter camera excludes every neighboring tile");
            int width=Screen.width,height=Screen.height;
            foreach(var resolution in new[]{new Vector2Int(1024,768),new Vector2Int(1280,720)})
            {
                Screen.SetResolution(resolution.x,resolution.y,false);yield return new WaitForSecondsRealtime(.5f);
                CheckFrame(g.boardCamera,-.5f,-12.5f,24.5f,.5f,"quarter bounds survive window aspect "+resolution);
            }
            Screen.SetResolution(width,height,false);yield return new WaitForSecondsRealtime(.5f);
            g.RestartAll();g.selectedId=3;g.ToggleLead();g.Primary();
            yield return new WaitForSecondsRealtime(.9f);
            g.RequestNote(4);
            yield return new WaitForSecondsRealtime(.6f);g.RequestNote(2);
            yield return new WaitForSecondsRealtime(.6f);g.RequestNote(0);
            yield return new WaitForSecondsRealtime(.6f);
            g.TogglePause();g.view.Refresh(g);g.hud.Refresh(g);
            yield return new WaitForEndOfFrame();
            Capture("MusicGarden-playable.png");
            yield return new WaitForSecondsRealtime(.1f);
            Debug.Log("GARDEN_RUNTIME_CHECK_SUCCESS");Application.Quit(0);
        }
        void Capture(string name)
        {
            string folder=Path.Combine(Application.dataPath,"../Checks");Directory.CreateDirectory(folder);
            var screenshot=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            screenshot.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);screenshot.Apply();
            File.WriteAllBytes(Path.Combine(folder,name),screenshot.EncodeToPNG());Destroy(screenshot);
        }
        void Run(GardenGame g)
        {
            g.view.Refresh(g);CheckFrame(g.boardCamera,-.5f,-25.5f,49.5f,.5f,"opening camera frames the exact complete map");
            Check(g.model.towers.Count==3&&g.model.coins==0&&g.view.TowerCount(1)==3,"fresh application startup loads actual preset defense");
            var authored=FindObjectsOfType<GardenTowerSetup>().First(t=>t.enabled&&t.gameObject.activeInHierarchy&&t.regionId==1);
            var original=authored.GetComponent<SpriteRenderer>().sprite;
            authored.GetComponent<SpriteRenderer>().sprite=g.config.actors[1];
            g.RestartAll();
            var corresponding=g.model.towers.First(t=>t.cell.Equals(GardenView.LocalCell(authored.transform.position,1)));
            Check(g.view.TowerObject(1,corresponding.id).GetComponent<SpriteRenderer>().sprite==g.config.actors[1],"scene authored tower appearance is used at runtime");
            authored.GetComponent<SpriteRenderer>().sprite=original;
            g.RestartAll();g.phase=GardenPhase.Overview;g.Primary();
            Check(g.phase==GardenPhase.Preparation,"overview enters preparation");
            Check(g.model.towers.Count==3&&g.model.coins==0,"scene preset towers consume budget and enter model");
            Check(!EventSystem.current.sendNavigationEvents,"Space has only one navigation owner");
            g.hud.primary.onClick.Invoke();Check(g.phase==GardenPhase.Playing,"saved primary UI starts battle");
            double now=AudioSettings.dspTime;
            g.Tick(now+.13);Check(g.Locked&&g.model.beat==0&&g.display.beat==-1,"scheduled simulation remains hidden until audible beat");
            g.TogglePause();Check(g.paused&&g.Locked,"pause preserves locked pending beat");
            g.TogglePause();g.Tick(AudioSettings.dspTime+.13);g.Tick(AudioSettings.dspTime+.25);
            Check(g.display.beat==0&&g.model.beat==0,"resume presents pending beat exactly once");
            g.selectedId=3;g.ToggleLead();g.RequestNote(0);g.RequestNote(4);
            Check(g.queuedPitch==4,"last new key overwrites editable slot");g.ToggleLead();
            for(int level=0;level<4;level++)
            {
                if(g.phase==GardenPhase.Preparation)g.Primary();
                int safety=500;
                while(g.phase!=GardenPhase.RegionComplete&&g.phase!=GardenPhase.Won&&g.phase!=GardenPhase.Lost&&safety-->0)
                {
                    if(g.phase==GardenPhase.Interlude){Check(g.CanBuild,"interlude permits rebuilding");g.Primary();now=AudioSettings.dspTime;}
                    now+=g.BeatDuration;g.Tick(now);g.Tick(now+.1);
                }
                Check(g.phase!=GardenPhase.Lost&&safety>0,"region "+(level+1)+" controller completes");
                Check(g.model.hp>0,"shared core survives region "+(level+1));
                if(level<3){g.Primary();now=AudioSettings.dspTime;Check(g.model.coins==GardenContent.Budgets[level+1]-24,"region independent budget minus three presets");}
            }
            int count=g.performance.Count,coins=g.model.coins,hp=g.model.hp;
            Check(count>0&&g.performance.Select(n=>n.region).Distinct().Count()==4,"real logs include all four regions");
            g.StartReplay();Check(g.replaying,"replay starts from actual performance");
            now=AudioSettings.dspTime;int steps=g.performance.Max(n=>n.beat)+8;
            for(int i=0;i<steps&&g.replaying;i++){now+=g.BeatDuration;g.Tick(now);g.Tick(now+.1);}
            Check(!g.replaying&&g.model.coins==coins&&g.model.hp==hp&&g.performance.Count==count,"replay ends without battle or reward mutation");
            g.RestartAll();g.selectedId=g.model.towers[0].id;int before=g.model.coins;g.Sell();Check(g.model.coins==before+8,"UI recovery refunds once");g.Sell();Check(g.model.coins==before+8,"repeat recovery does not refund again");
            g.Arrange();Check(g.model.towers.Count==3,"arrange rebuilds editable defense");
            var grass=Enumerable.Range(0,g.model.region.rows).SelectMany(y=>Enumerable.Range(0,g.model.region.columns).Select(x=>new Cell(x,y))).First(c=>MapRules.At(g.model.region,c)=='G'&&!g.model.region.pads.Contains(c)&&!g.model.towers.Any(t=>t.cell.Equals(c)));
            g.selectedId=g.model.towers[0].id;g.Sell();g.selectedPad=grass;g.Build(false);
            Check(g.model.towers.Any(t=>t.cell.Equals(grass)),"controller builds outside recommended stone pads");g.Sell();g.Arrange();
            g.selectedId=g.model.towers[0].id;g.Sell();g.selectedId=g.model.towers[0].id;g.Sell();g.selectedPad=g.model.region.pads.Last();g.Build(true);
            Check(g.model.towers.Any(t=>t.chord),"prepare alternate chord layout before failure");
            g.model.towers.Clear();g.Primary();now=AudioSettings.dspTime;int limit=500;
            while(g.phase!=GardenPhase.Lost&&limit-->0){if(g.phase==GardenPhase.Interlude){g.Primary();now=AudioSettings.dspTime;}now+=g.BeatDuration;g.Tick(now);g.Tick(now+.1);}
            Check(g.phase==GardenPhase.Lost,"undefended roads can fail");g.Primary();Check(g.model.hp==10&&g.model.coins==0&&g.model.beat==-1,"primary retry resets entry health, initial budget and preset costs");
            Check(g.view.TowerCount(g.Stage.regionId)==g.model.towers.Count,"primary retry removes ghosts from altered layout");
            foreach(var t in g.model.towers)Check(Vector3.Distance(g.view.TowerObject(g.Stage.regionId,t.id).position,GardenView.Position(t.cell,g.Stage.regionId))<.2f,"retry visible tower position matches simulation");
        }
        static void CheckFrame(Camera camera,float left,float bottom,float right,float top,string label)
        {
            var min=camera.ViewportToWorldPoint(new Vector3(0,0,10));var max=camera.ViewportToWorldPoint(new Vector3(1,1,10));
            Check(Mathf.Abs(min.x-left)<.01f&&Mathf.Abs(min.y-bottom)<.01f&&Mathf.Abs(max.x-right)<.01f&&Mathf.Abs(max.y-top)<.01f,label+" min="+min+" max="+max+" aspect="+camera.aspect+" pixels="+camera.pixelRect);
        }
    }
}

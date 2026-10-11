using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using TapTapGameJam.PianoDefense;

namespace TapTapGameJam.MusicGarden
{
    public enum GardenPhase { Overview, Preparation, Playing, Interlude, RegionComplete, Won, Lost }
    [RequireComponent(typeof(PianoAudio))]
    public sealed class GardenGame : MonoBehaviour
    {
        public GardenConfig config;
        public GardenView view;
        public GardenHUD hud;
        public Camera boardCamera;
        public GardenSimulation model { get; private set; }
        public GardenSimulation display { get; private set; }
        public GardenPhase phase=GardenPhase.Overview;
        public int stageIndex,selectedId=-1,queuedPitch=-1;
        public Cell? selectedPad;
        public bool paused,replaying;
        public string notice="点击开始，进入第一乐章";
        public readonly List<GardenNote> performance=new List<GardenNote>();
        public readonly List<GardenShot> visibleShots=new List<GardenShot>();
        public GardenBeat lastBeat;
        public double presentedTime;
        public float resonanceTime=-100;
        readonly BeatClock clock=new BeatClock();
        PianoAudio sound;
        GardenBeat pending;
        RegionDefinition[] runtimeRegions;
        int entryHp=10,archiveOffset,replayBeat=-1,replayLength;
        bool archived;
        float introTime=-1;
        public int AvailableBeat => model.beat+1;
        public bool Locked => pending!=null;
        public bool CanBuild => !replaying&&(phase==GardenPhase.Preparation||phase==GardenPhase.Interlude);
        public GardenStage Stage => config.stages[stageIndex];
        public GardenTower Selected => model?.towers.Find(t=>t.id==selectedId);
        public double BeatDuration => 60.0/config.bpm;
        public float BeatProgress => phase==GardenPhase.Playing&&!paused?(float)Math.Max(0,Math.Min(1,(AudioSettings.dspTime-presentedTime)/BeatDuration)):0;
        void Awake()
        {
            sound=GetComponent<PianoAudio>();sound.Metronome=false;
            clock.SecondsPerBeat=BeatDuration;
            runtimeRegions=config.regions.Select(r=>new RegionDefinition { id=r.id,quadrant=r.quadrant,columns=r.columns,rows=r.rows,cells=(string[])r.cells.Clone(),route=(Cell[])r.route.Clone(),pads=(Cell[])r.pads.Clone() }).ToArray();
            foreach(var tile in FindObjectsOfType<GardenTile>())
            {
                var r=runtimeRegions.First(x=>x.id==tile.regionId);var row=r.cells[tile.cell.y].ToCharArray();row[tile.cell.x]=tile.Code;r.cells[tile.cell.y]=new string(row);
            }
            foreach(var r in runtimeRegions)
            {
                r.pads=FindObjectsOfType<GardenPad>().Where(p=>p.regionId==r.id&&p.gameObject.activeInHierarchy).Select(p=>p.cell).ToArray();
                string error=MapRules.ValidateRoute(r);if(error!=null)throw new Exception(error);
            }
            if(EventSystem.current!=null) EventSystem.current.sendNavigationEvents=false;
            LoadStage(0,10);
            phase=GardenPhase.Overview;introTime=-1;
        }
        void LoadStage(int index,int hp)
        {
            StopClock();stageIndex=index;entryHp=hp;archived=false;
            model=new GardenSimulation(runtimeRegions.First(r=>r.id==Stage.regionId),Stage.waves,Stage.budget,hp,config.diamondChordRange);
            foreach(var setup in FindObjectsOfType<GardenTowerSetup>().Where(t=>t.enabled&&t.gameObject.activeInHierarchy&&t.regionId==Stage.regionId))
            {
                var c=GardenView.LocalCell(setup.transform.position,Stage.regionId);var tower=model.Build(c,setup.chord);
                if(tower!=null) { tower.rhythm=setup.rhythm;tower.filter=setup.filter;tower.pitch=setup.pitch; }
            }
            display=model.Copy();selectedId=-1;selectedPad=null;lastBeat=null;visibleShots.Clear();queuedPitch=-1;
            phase=GardenPhase.Preparation;notice=Stage.instruction;
            if(view!=null)view.StageChanged(this);
        }
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.Space))Primary();
            if(Input.GetKeyDown(KeyCode.R))ToggleLead();
            if(Input.GetKeyDown(KeyCode.Escape)) { if(selectedId>=0||selectedPad.HasValue){selectedId=-1;selectedPad=null;}else TogglePause(); }
            if(Input.GetKeyDown(KeyCode.A))RequestNote(0);
            if(Input.GetKeyDown(KeyCode.S))RequestNote(2);
            if(Input.GetKeyDown(KeyCode.D))RequestNote(4);
            if(Input.GetMouseButtonDown(0)&&phase!=GardenPhase.Overview&&!replaying&&!paused&&boardCamera.pixelRect.Contains(Input.mousePosition)&&(EventSystem.current==null||!EventSystem.current.IsPointerOverGameObject()))
            {
                Vector3 world=boardCamera.ScreenToWorldPoint(Input.mousePosition);Cell c=GardenView.LocalCell(world,Stage.regionId);
                var tower=model.towers.Find(t=>t.cell.Equals(c));
                selectedId=tower?.id??-1;selectedPad=tower==null&&MapRules.At(model.region,c)=='G'?c:(Cell?)null;
                notice=tower!=null?"选择节拍，或用 R 接管这座普通钢琴":selectedPad.HasValue?"草地已选中 · 点击右侧琴塔卡片建造":"可在空闲草地建造；道路、障碍和基地不可建造";
            }
            if(phase==GardenPhase.Overview) { if(introTime<0)introTime=Time.unscaledTime;else if(Time.unscaledTime-introTime>3)phase=GardenPhase.Preparation; }
            Tick(AudioSettings.dspTime);
            if(view!=null)view.Refresh(this);
            if(hud!=null)hud.Refresh(this);
        }
        public void Tick(double now)
        {
            if(paused)return;
            if(replaying)
            {
                if(clock.TrySchedule(now))
                {
                    var notes=performance.Where(n=>n.beat==replayBeat+1).Select(n=>new NoteEvent(n.beat,n.pitch,n.tower)).ToArray();
                    sound.Schedule(notes,replayBeat+1,clock.NextTime);
                }
                if(clock.TryPresent(now))
                {
                    replayBeat++;presentedTime=now;
                    foreach(var n in performance.Where(n=>n.beat==replayBeat))view.PulseTower(n.region,n.tower,n.pitch);
                    if(replayBeat>=replayLength)StopReplay();
                }
                return;
            }
            if(phase!=GardenPhase.Playing)return;
            if(clock.TrySchedule(now))
            {
                if(pending==null) { pending=model.Step(queuedPitch);queuedPitch=-1; }
                sound.Schedule(pending.notes.Select(n=>new NoteEvent(n.beat,n.pitch,n.tower)).ToArray(),pending.beat,clock.NextTime);
            }
            if(clock.TryPresent(now))Present(now);
        }
        void Present(double now)
        {
            if(pending==null)return;
            lastBeat=pending;pending=null;display=model.Copy();presentedTime=now;
            visibleShots.Clear();visibleShots.AddRange(lastBeat.shots);
            foreach(var n in lastBeat.notes)view.PulseTower(n.region,n.tower,n.pitch);
            if(lastBeat.resonance) { notice="合奏休止！多座琴塔同拍奏出 Do · Mi · Sol，存活敌人下一拍停步";resonanceTime=Time.unscaledTime; }
            else if(lastBeat.rest.Length>0)notice=lastBeat.rest;
            else if(lastBeat.leaks>0)notice="音符进入核心 · 生命 -"+lastBeat.leaks;
            if(model.Lost) { phase=GardenPhase.Lost;StopClock(false);notice="核心失守 · 可重试本区，恢复进入时的生命与预算"; }
            else if(model.Complete) { Archive();phase=stageIndex==3?GardenPhase.Won:GardenPhase.RegionComplete;StopClock(false);notice="乐章完成 · 击杀 "+model.kills+" · 共鸣 "+model.resonances+" · 手奏 "+model.log.Count(n=>n.manual); }
            else if((model.beat+1)%16==0&&model.beat+1<Stage.segments*16) { phase=GardenPhase.Interlude;StopClock(false);notice="间奏整备 · 可花费击杀获得的金币补塔或全额回收，继续下一段"; }
        }
        void Archive()
        {
            if(archived)return;
            foreach(var note in model.log) { var n=note;n.beat+=archiveOffset;performance.Add(n); }
            archiveOffset+=model.beat+5;archived=true;
        }
        public void Primary()
        {
            if(replaying) { StopReplay();return; }
            if(phase==GardenPhase.Overview) { phase=GardenPhase.Preparation;return; }
            if(phase==GardenPhase.Preparation||phase==GardenPhase.Interlude)
            {
                phase=GardenPhase.Playing;paused=false;clock.Start(AudioSettings.dspTime);notice="自动伴奏已开始 · 选普通琴塔，R 领奏，再按 A / S / D 预约下一拍";return;
            }
            if(phase==GardenPhase.Playing) { TogglePause();return; }
            if(phase==GardenPhase.RegionComplete) { LoadStage(stageIndex+1,Math.Min(10,model.hp+2));return; }
            if(phase==GardenPhase.Lost) { Retry();return; }
            if(phase==GardenPhase.Won)StartReplay();
        }
        void StopClock(bool stopSound=true) { clock.Stop();pending=null;queuedPitch=-1;paused=false;if(stopSound&&sound!=null)sound.StopAll(); }
        public void TogglePause()
        {
            if(!replaying&&phase!=GardenPhase.Playing)return;
            paused=!paused;queuedPitch=-1;
            if(paused) { clock.Pause();sound.StopAll();notice="已暂停 · 再按空格接回同一拍"; }
            else { clock.Resume(AudioSettings.dspTime);notice="准备接拍…"; }
        }
        public void RequestNote(int pitch)
        {
            if(replaying||paused||phase!=GardenPhase.Playing)return;
            if(model.leadId<0){notice="先选普通琴塔并按 R，接管领奏声部";return;}
            queuedPitch=pitch;notice="预约第 "+(AvailableBeat+1)+" 拍 · "+PitchName(pitch)+(model.CanPlay(pitch)?" · 有可击杀目标":" · 当前预测无目标，可留白或换音");
        }
        public void ToggleLead()
        {
            if(replaying||paused||Selected==null)return;
            if(Selected.chord){notice="和弦琴塔负责自动伴奏；请选择普通琴塔领奏";return;}
            model.leadId=model.leadId==selectedId?-1:selectedId;queuedPitch=-1;SyncIfIdle();
            notice=model.leadId<0?"恢复自动伴奏 · 下一未锁定拍生效":"已接管领奏 · A Do / S Mi / D Sol；不按即休止";
        }
        public void Build(bool chord)
        {
            if(!CanBuild||!selectedPad.HasValue)return;
            var tower=model.Build(selectedPad.Value,chord);
            if(tower==null){notice="金币不足或已达六塔上限";return;}
            selectedId=tower.id;selectedPad=null;SyncIfIdle();view.EnsureTower(Stage.regionId,tower);
            notice="琴塔已建好 · 普通 3×3 / 和弦 5×5，有效射程受障碍遮挡";
        }
        public void Sell()
        {
            if(!CanBuild||Selected==null)return;view.RemoveTower(Stage.regionId,selectedId);model.Sell(selectedId);selectedId=-1;SyncIfIdle();notice="全额回收 · 可重新编排";
        }
        public void Arrange()
        {
            if(!CanBuild)return;
            foreach(var t in model.towers.ToArray()) { view.RemoveTower(Stage.regionId,t.id);model.Sell(t.id); }
            GardenContent.Arrange(model);foreach(var t in model.towers)view.EnsureTower(Stage.regionId,t);
            selectedId=model.towers.FirstOrDefault()?.id??-1;SyncIfIdle();notice="示范编排：三座钢琴接力。可移动布局、改节拍或接管一座尝试自己的演奏";
        }
        public void SetRhythm(int rhythm) { if(!replaying&&!paused&&Selected!=null&&!Selected.chord) { Selected.rhythm=(GardenRhythm)rhythm;SyncIfIdle();notice="节拍设置将在下一未锁定拍生效"; } }
        public void SetFilter(int filter) { if(!replaying&&!paused&&Selected!=null) { Selected.filter=(PitchFilter)filter;SyncIfIdle(); } }
        public void SetPitch(int pitch) { if(!replaying&&!paused&&Selected!=null) { Selected.pitch=pitch;SyncIfIdle(); } }
        void SyncIfIdle() { if(phase!=GardenPhase.Playing)display=model.Copy(); }
        public void Retry()
        {
            if(replaying)StopReplay();
            if(archived){notice="完成的乐章已保留；通关后可重新开始整局";return;}
            view.ClearRegion(Stage.regionId);LoadStage(stageIndex,entryHp);
        }
        public void RestartAll()
        {
            StopReplay();performance.Clear();archiveOffset=0;view.ClearAllTowers();LoadStage(0,10);
        }
        public void StartReplay()
        {
            if(phase==GardenPhase.Playing||phase==GardenPhase.Interlude){notice="完成当前乐章后可回放真实演奏";return;}
            if(performance.Count==0){notice="还没有完成的演奏记录";return;}
            StopClock();replaying=true;replayBeat=-1;replayLength=performance.Max(n=>n.beat)+2;clock.Start(AudioSettings.dspTime);notice="正在回放真实击杀乐谱 · 保留原始空拍与手奏，不产生战斗奖励";
        }
        public void StopReplay() { if(!replaying)return;replaying=false;StopClock();notice="回放结束 · 演奏记录已保留"; }
        public static string PitchName(int p) => p==0?"Do ○":p==2?"Mi ◇":"Sol △";
        public void Mute() { sound.SetMuted(!sound.Muted);notice=sound.Muted?"声音已关闭":"声音已开启"; }
        public bool Muted => sound!=null&&sound.Muted;
        void OnApplicationFocus(bool focus) { if(!focus&&!paused&&(phase==GardenPhase.Playing||replaying))TogglePause(); }
        void OnDestroy() { if(sound!=null)sound.StopAll(); }
    }
}

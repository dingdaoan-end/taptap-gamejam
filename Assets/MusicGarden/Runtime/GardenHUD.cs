using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.MusicGarden
{
    public sealed class GardenHUD : MonoBehaviour
    {
        public GardenGame game;
        public Text title,status,stage,mode,help,notice,selection,slot,record,forecast;
        public Button primary,arrange,normal,chord,lead,sell,replay,retry,restart,mute;
        public Button[] rhythms,filters,pitches,keys;
        public Image[] beats,dots;
        static readonly Color Ivory=new Color(.96f,.92f,.80f),Gold=new Color(.93f,.76f,.43f),Teal=new Color(.30f,.65f,.62f),Dim=new Color(.15f,.23f,.28f);
        void Awake()
        {
            primary.onClick.AddListener(()=>game.Primary());arrange.onClick.AddListener(()=>game.Arrange());
            normal.onClick.AddListener(()=>game.Build(false));chord.onClick.AddListener(()=>game.Build(true));
            lead.onClick.AddListener(()=>game.ToggleLead());sell.onClick.AddListener(()=>game.Sell());
            replay.onClick.AddListener(()=> { if(game.replaying)game.StopReplay();else game.StartReplay(); });
            retry.onClick.AddListener(()=>game.Retry());restart.onClick.AddListener(()=>game.RestartAll());mute.onClick.AddListener(()=>game.Mute());
            for(int i=0;i<4;i++){int n=i;rhythms[i].onClick.AddListener(()=>game.SetRhythm(n));}
            for(int i=0;i<3;i++) { int n=i;filters[i].onClick.AddListener(()=>game.SetFilter(n));int pitch=i*2;pitches[i].onClick.AddListener(()=>game.SetPitch(pitch));keys[i].onClick.AddListener(()=>game.RequestNote(pitch)); }
        }
        static void Label(Button button,string value) { button.GetComponentInChildren<Text>().text=value; }
        static void Tint(Button button,bool active) { button.GetComponent<Image>().color=active?Teal:Dim; }
        public void Refresh(GardenGame g)
        {
            if(g.display==null)return;
            title.text="夜之乐章";
            status.text="核心  "+g.display.hp+" / 10     |     乐章币  "+g.model.coins+"     |     琴塔  "+g.model.towers.Count+" / 6";
            stage.text=(g.stageIndex+1)+" / 4  ·  "+g.Stage.title+"     /     "+g.config.bpm+" BPM";
            mode.text=g.replaying?"真实演奏回放":g.paused?"暂停":g.phase==GardenPhase.Preparation?"整备":g.phase==GardenPhase.Playing?"演奏":g.phase==GardenPhase.Interlude?"间奏":g.phase==GardenPhase.Lost?"失守":g.phase==GardenPhase.Overview?"四区全览":"乐章完成";
            help.text=g.Stage.instruction;notice.text=g.notice;
            if(Time.unscaledTime-g.resonanceTime<1.2f)notice.color=Gold;else notice.color=Ivory;
            string action=g.replaying?"停止回放":g.phase==GardenPhase.Overview?"进入第一乐章":g.phase==GardenPhase.Preparation?"开始演奏  SPACE":g.phase==GardenPhase.Interlude?"继续下一段  SPACE":g.phase==GardenPhase.Playing?(g.paused?"继续演奏  SPACE":"暂停演奏  SPACE"):g.phase==GardenPhase.RegionComplete?"下一乐章  SPACE":g.phase==GardenPhase.Lost?"重试本区  SPACE":"回放全曲  SPACE";
            Label(primary,action);Label(mute,g.Muted?"开启声音":"关闭声音");
            var t=g.Selected;
            selection.text=t!=null?(t.chord?"和弦琴塔":"普通钢琴")+"  #"+t.id:g.selectedPad.HasValue?"草地 · 选择琴塔":"编排你的防线";
            normal.interactable=g.CanBuild&&g.selectedPad.HasValue&&g.model.coins>=8;chord.interactable=g.CanBuild&&g.selectedPad.HasValue&&g.model.coins>=16;
            arrange.interactable=g.CanBuild;lead.interactable=t!=null&&!t.chord&&!g.replaying&&!g.paused;sell.interactable=g.CanBuild&&t!=null;
            Label(lead,t!=null&&g.model.leadId==t.id?"恢复自动伴奏  R":"设为领奏  R");
            for(int i=0;i<4;i++) { rhythms[i].interactable=t!=null&&!t.chord&&g.model.leadId!=t.id&&!g.paused&&!g.replaying;Tint(rhythms[i],t!=null&&(int)t.rhythm==i); }
            for(int i=0;i<3;i++)
            {
                filters[i].interactable=t!=null&&!g.paused&&!g.replaying;pitches[i].interactable=t!=null&&!g.paused&&!g.replaying;
                Tint(filters[i],t!=null&&(int)t.filter==i);Tint(pitches[i],t!=null&&t.pitch==i*2);
                bool can=g.phase==GardenPhase.Playing&&!g.paused&&!g.replaying&&g.model.leadId>=0;
                keys[i].interactable=can;keys[i].GetComponent<Image>().color=g.queuedPitch==i*2?GardenView.PitchColor(i*2):can&&g.model.CanPlay(i*2)?new Color(.82f,.79f,.68f):new Color(.40f,.45f,.44f);
            }
            bool hasLead=g.model.leadId>=0;
            slot.text=g.replaying?"正在重演真实音符":hasLead?"领奏 #"+g.model.leadId+" · 预约第 "+(g.AvailableBeat+1)+" 拍："+(g.queuedPitch<0?"休止":GardenGame.PitchName(g.queuedPitch))+(g.Locked?"  [前拍已锁定]":"  [可修改]"):"A / S / D 选择音符 · 先选普通琴塔并按 R";
            forecast.text="第 "+(g.display.beat+2)+" 拍   ·   "+Mathf.Min(g.Stage.segments,g.display.beat/16+1)+" / "+g.Stage.segments+" 段\n"+(hasLead?"下一可演奏拍：\n"+string.Join("  ",new[]{0,2,4}.Select(p=>GardenGame.PitchName(p)+(g.model.CanPlay(p)?" ✓":" ·"))):"紫色：有效射程\n红色：被障碍阻挡")+"\n\n普通：3×3，每次 1 伤害\n和弦：5×5，四拍一次\n多塔同拍三音 → 合奏休止";
            record.text="真实乐句   ·   击杀 "+g.display.kills+"   /   手奏 "+g.display.log.Count(n=>n.manual)+"   /   共鸣 "+g.display.resonances;
            for(int i=0;i<4;i++)beats[i].color=g.display.beat>=0&&g.display.beat%4==i?Gold:Dim;
            int first=Mathf.Max(0,g.display.beat-15);
            for(int i=0;i<48;i++)dots[i].color=new Color(.17f,.27f,.30f);
            foreach(var note in g.display.log.Where(n=>n.beat>=first))
            {
                int row=note.pitch/2,column=note.beat-first;if(row<0||row>=3||column<0||column>=16)continue;
                var color=GardenView.PitchColor(note.pitch);if(!note.manual)color*=.82f;color.a=1;dots[row*16+column].color=color;
            }
            replay.interactable=g.performance.Count>0&&g.phase!=GardenPhase.Playing&&g.phase!=GardenPhase.Interlude;
            retry.interactable=!g.replaying&&g.phase!=GardenPhase.Won&&g.phase!=GardenPhase.RegionComplete;
            restart.gameObject.SetActive(g.phase==GardenPhase.Won);
        }
    }
}

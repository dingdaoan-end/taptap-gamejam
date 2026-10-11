using System;
using System.Linq;
using System.IO;
using System.Text.Json;
using TapTapGameJam.MusicGarden;
class Program
{
 static void Assert(bool b,string m) { if(!b) throw new Exception(m); }
 static void Main()
 {
  var r=new RegionDefinition { columns=4,rows=3,cells=new[]{"GGRB","RRRG","GXGG"}};
  Assert(!MapRules.Visible(r,new Cell(0,2),new Cell(2,2)),"RED: blocker must stop shots");
  Assert(!MapRules.Visible(r,new Cell(0,1),new Cell(2,3)),"corner touching blocker must stop shots");
  var path=MapRules.Route(r,new Cell(0,1));
  Assert(path.Length==4,"route must reach base-adjacent road");
  Assert(path.All(c=>MapRules.At(r,c)=='R'),"route only follows painted roads");
  r.route=Array.Empty<Cell>();Assert(MapRules.ValidateRoute(r)!=null,"RED: reject empty authored route");
  r.route=new[]{new Cell(0,1),new Cell(2,1)};Assert(MapRules.ValidateRoute(r)!=null,"reject route jumps");
  r.route=new[]{new Cell(0,1),new Cell(1,1)};Assert(MapRules.ValidateRoute(r)!=null,"reject premature ending away from base");
  r.route=path;Assert(MapRules.ValidateRoute(r)==null,"valid connected base route accepted");
  Console.WriteLine("PASS exact route and blocker LOS");
  var arena=new RegionDefinition { id=1,columns=8,rows=3,cells=new[]{"GGGGGGGB","RRRRRRRB","GGGGGGGB"},route=Enumerable.Range(0,7).Select(x=>new Cell(x,1)).ToArray(),pads=new[]{new Cell(0,0),new Cell(1,0),new Cell(1,2),new Cell(2,0)} };
  var freeRegion=new RegionDefinition { id=1,columns=8,rows=3,cells=new[]{"GGGGGGXB","RRRRRRRB","GGGGGGGB"},route=arena.route,pads=arena.pads };
  var freeBuild=new GardenSimulation(freeRegion,Array.Empty<GardenSpawn>(),40);
  Assert(freeBuild.Build(new Cell(5,0),false)!=null,"RED: empty grass outside recommended pads must permit construction");
  Assert(freeBuild.coins==32&&freeBuild.Build(new Cell(5,0),false)==null,"grass construction charges once and rejects occupancy");
  foreach(var invalid in new[]{new Cell(5,1),new Cell(6,0),new Cell(7,0),new Cell(-1,0)})Assert(freeBuild.Build(invalid,false)==null,"road, obstacle, core and out of bounds remain unbuildable");
  Console.WriteLine("PASS free grass construction with terrain and occupancy restrictions");
  var s=new GardenSimulation(arena,new[]{new GardenSpawn(0,0),new GardenSpawn(0,2),new GardenSpawn(0,4),new GardenSpawn(0,0),new GardenSpawn(1,4)});
  var a=s.Build(new Cell(0,0),false);var b=s.Build(new Cell(1,0),false);var c=s.Build(new Cell(1,2),false);s.leadId=c.id;
  var result=s.Step(4);
  Assert(result.notes.First().manual&&result.notes.First().pitch==4,"lead must resolve before accompaniment");
  Assert(result.resonance&&s.coins==3,"triad triggers but adds only three kill coins");
  int held=s.enemies.Single().id;foreach(var t in s.towers)t.rhythm=GardenRhythm.FirstBeat;s.Step();
  Assert(s.enemies.FirstOrDefault(e=>e.id==held)==null || s.enemies.First(e=>e.id==held).progress==0,"survivor skips one move");
  Assert(s.enemies.Any(e=>e.id==5&&e.hold==0),"newborn does not inherit rest");
  Console.WriteLine("PASS manual priority, multi-kill resonance, rewards and newborn rest");
  s=new GardenSimulation(arena,new[]{new GardenSpawn(0,0,1,true)});s.Build(new Cell(0,0),true);
  result=s.Step();Assert(result.notes.Count==3&&!result.resonance&&s.coins==9,"single chord cannot resonate or triple reward");
  Assert(!s.Sell(999),"missing tower cannot refund");int refund=s.coins;Assert(s.Sell(1)&&!s.Sell(1)&&s.coins==refund+16,"refund once");
  Console.WriteLine("PASS chord soft counter and exact refund");
  var options=new JsonSerializerOptions { IncludeFields=true };
  var root=JsonDocument.Parse(File.ReadAllText("docs/art/2026-10-11/map-layout-v0.2/map-layout.json"));
  int hp=10;
  foreach(var item in root.RootElement.GetProperty("regions").EnumerateArray())
  {
   var region=JsonSerializer.Deserialize<RegionDefinition>(item.GetRawText(),options);GardenContent.Prepare(region);
   for(int i=1;i<region.route.Length;i++) Assert(Math.Abs(region.route[i].x-region.route[i-1].x)+Math.Abs(region.route[i].y-region.route[i-1].y)==1,"route cannot jump");
   Assert(region.route.All(cell=>MapRules.At(region,cell)=='R'),"exact map traversal");
   int level=Array.IndexOf(GardenContent.Order,region.id);
   s=new GardenSimulation(region,GardenContent.Waves(level,level>=2?3:2),GardenContent.Budgets[level],hp);GardenContent.Arrange(s);
   int safety=500;while(!s.Lost&&!s.Complete&&safety-->0)s.Step();
   Assert(s.Complete,"region "+region.id+" default arrangement must win, HP="+s.hp+" leaks="+s.leaks);
   hp=Math.Min(10,s.hp+2);
   Console.WriteLine("PASS region "+region.id+" exact route "+region.route.Length+" cells; HP="+s.hp+" notes="+s.log.Count+" resonance="+s.resonances);
   if(region.id==1) {
    var variants=new string[2];
    for(int v=0;v<2;v++) {
     var manual=new GardenSimulation(region,GardenContent.Waves(0,2));GardenContent.Arrange(manual);manual.leadId=3;
     int choices=0;
     for(int i=0;i<150&&!manual.Complete&&!manual.Lost;i++) {
      var available=new[]{0,2,4}.Where(p=>manual.CanPlay(p)).ToArray();if(available.Length>=2)choices++;
      manual.Step(available.Length==0?-1:available[v==0?0:available.Length-1]);
     }
     Assert(manual.Complete&&manual.log.Any(n=>n.manual),"lead mode must stay winnable and create attributable notes");
     variants[v]=string.Join(",",manual.log.Select(n=>n.beat+":"+n.pitch+":"+n.manual));
     Console.WriteLine("  lead variant "+v+" choices="+choices+" manual="+manual.log.Count(n=>n.manual)+" resonance="+manual.resonances);
     Assert(choices>=4,"at least four genuine choices in lead scenario");
    }
    Assert(variants[0]!=variants[1],"two choices must produce different actual music logs");
    var responding=new GardenSimulation(region,GardenContent.Waves(0,2));GardenContent.Arrange(responding);responding.leadId=2;
    for(int i=0;i<150&&!responding.Complete&&!responding.Lost;i++) {
      int p=-1;if((responding.beat+1)%2==1)p=new[]{0,2,4}.FirstOrDefault(n=>responding.CanPlay(n),-1);
      responding.Step(p);
    }
    Assert(responding.Complete&&responding.resonances>0,"manual rests and offbeat response must affect combat through resonance");
    Console.WriteLine("  manual response resonates "+responding.resonances+" times; HP="+responding.hp);
    bool found=false;
    for(int phase=0;phase<4;phase++)for(int pattern=0;pattern<64;pattern++) {
     var w=GardenContent.Waves(0,2).Select(e=>new GardenSpawn(e.beat+phase,e.pitch,e.speed,e.chord));
     var trial=new GardenSimulation(region,w);GardenContent.Arrange(trial);
     for(int t=0;t<3;t++)trial.towers[t].rhythm=(GardenRhythm)((pattern>>(t*2))&3);
     for(int t=0;t<200&&!trial.Complete&&!trial.Lost;t++)trial.Step();
     if(trial.Complete&&trial.resonances>0) { found=true;Console.WriteLine("  resonance arrangement phase="+phase+" patterns="+pattern+" hp="+trial.hp+" resonance="+trial.resonances);break; }
    }
    Assert(found,"NW must support winnable distinct resonant arrangement");
   }
  }
  Console.WriteLine("ALL MUSIC GARDEN CORE TESTS PASSED");
 }
}

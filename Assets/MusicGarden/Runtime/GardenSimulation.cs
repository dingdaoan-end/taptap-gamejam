using System;
using System.Collections.Generic;
using System.Linq;

namespace TapTapGameJam.MusicGarden
{
    [Serializable] public struct Cell : IEquatable<Cell>
    {
        public int x, y;
        public Cell(int x, int y) { this.x=x; this.y=y; }
        public bool Equals(Cell c) => x==c.x && y==c.y;
        public override bool Equals(object o) => o is Cell c && Equals(c);
        public override int GetHashCode() => x*397^y;
    }
    [Serializable] public class RegionDefinition
    {
        public int id, columns=25, rows=13;
        public string quadrant;
        public string[] cells;
        public Cell[] route;
        public Cell[] pads;
    }
    public static class MapRules
    {
        public static string ValidateRoute(RegionDefinition r)
        {
            if(r==null||r.route==null||r.route.Length==0)return "区域路径不能为空";
            for(int i=0;i<r.route.Length;i++)
            {
                var c=r.route[i];
                if(At(r,c)!='R')return "区域 "+r.id+" 路径点 "+i+" 不是道路";
                if(i>0&&Math.Abs(c.x-r.route[i-1].x)+Math.Abs(c.y-r.route[i-1].y)!=1)return "区域 "+r.id+" 路径点 "+i+" 与前一点不相邻";
            }
            var end=r.route[r.route.Length-1];
            if(new[]{new Cell(end.x+1,end.y),new Cell(end.x-1,end.y),new Cell(end.x,end.y+1),new Cell(end.x,end.y-1)}.All(c=>At(r,c)!='B'))return "区域 "+r.id+" 路径终点必须邻接基地";
            return null;
        }
        public static char At(RegionDefinition r, Cell c) => c.x<0||c.y<0||c.x>=r.columns||c.y>=r.rows ? 'X' : r.cells[c.y][c.x];
        // Closed rectangle intersection: a line touching a blocking corner is blocked too.
        public static bool Visible(RegionDefinition r, Cell a, Cell b)
        {
            for(int y=Math.Max(0,Math.Min(a.y,b.y)-1);y<=Math.Min(r.rows-1,Math.Max(a.y,b.y)+1);y++)
            for(int x=Math.Max(0,Math.Min(a.x,b.x)-1);x<=Math.Min(r.columns-1,Math.Max(a.x,b.x)+1);x++)
            {
                if(At(r,new Cell(x,y))!='X') continue;
                double lo=0,hi=1;
                if(Slab(a.x,b.x-a.x,x-.5,x+.5,ref lo,ref hi) && Slab(a.y,b.y-a.y,y-.5,y+.5,ref lo,ref hi)) return false;
            }
            return true;
        }
        static bool Slab(double origin,double delta,double min,double max,ref double lo,ref double hi)
        {
            if(Math.Abs(delta)<1e-9) return origin>=min && origin<=max;
            double a=(min-origin)/delta,b=(max-origin)/delta;
            if(a>b) { double t=a;a=b;b=t; }
            lo=Math.Max(lo,a);hi=Math.Min(hi,b);return lo<=hi+1e-9;
        }
        public static Cell[] Route(RegionDefinition r, Cell start)
        {
            if(At(r,start)!='R') throw new ArgumentException("Entry must be a road cell");
            var queue=new Queue<Cell>();var previous=new Dictionary<Cell,Cell>();
            queue.Enqueue(start);previous[start]=start;
            Cell[] dirs={new Cell(1,0),new Cell(0,1),new Cell(-1,0),new Cell(0,-1)};
            while(queue.Count>0)
            {
                var c=queue.Dequeue();
                if(dirs.Any(d=>At(r,new Cell(c.x+d.x,c.y+d.y))=='B'))
                {
                    var path=new List<Cell>{c};while(!c.Equals(start)) { c=previous[c];path.Add(c); }
                    path.Reverse();return path.ToArray();
                }
                foreach(var d in dirs) { var n=new Cell(c.x+d.x,c.y+d.y);if(At(r,n)=='R'&&!previous.ContainsKey(n)) { previous[n]=c;queue.Enqueue(n); } }
            }
            throw new ArgumentException("Road entry has no connected base");
        }
        public static bool InRange(Cell a,Cell b,bool chord,bool diamond=false)
        {
            int dx=Math.Abs(a.x-b.x),dy=Math.Abs(a.y-b.y),radius=chord?2:1;
            return chord&&diamond ? dx+dy<=radius : Math.Max(dx,dy)<=radius;
        }
    }
    public enum GardenRhythm { EveryBeat, Downbeat, Offbeat, FirstBeat }
    public enum PitchFilter { All, Only, Except }
    [Serializable] public class GardenTower
    {
        public int id, pitch; public Cell cell; public bool chord;
        public GardenRhythm rhythm; public PitchFilter filter;
        public GardenTower Copy() => (GardenTower)MemberwiseClone();
    }
    public class GardenEnemy
    {
        public int id,pitch,hp,speed,progress=-1,previous=-1,hold;
        public bool chord;
        public GardenEnemy Copy() => (GardenEnemy)MemberwiseClone();
    }
    [Serializable] public struct GardenSpawn
    {
        public int beat,pitch,speed;public bool chord;
        public GardenSpawn(int b,int p,int s=1,bool c=false) { beat=b;pitch=p;speed=s;chord=c; }
    }
    public struct GardenNote
    {
        public int beat,pitch,tower,kill,region; public bool manual;
    }
    public struct GardenShot { public Cell from,to;public int pitch;public bool lethal; }
    public class GardenBeat
    {
        public int beat,leaks;public bool resonance;public string rest="";
        public readonly List<GardenNote> notes=new List<GardenNote>();
        public readonly List<GardenShot> shots=new List<GardenShot>();
    }
    public sealed class GardenSimulation
    {
        public readonly RegionDefinition region;
        public readonly List<GardenTower> towers=new List<GardenTower>();
        public readonly List<GardenEnemy> enemies=new List<GardenEnemy>();
        public readonly List<GardenNote> log=new List<GardenNote>();
        public readonly List<GardenSpawn> spawns;
        public int beat=-1,coins,hp,kills,leaks,resonances,leadId=-1,lastResonance=-100;
        public bool diamond;
        public int finalSpawnBeat;
        int nextTower=1,nextEnemy=1;
        public bool Lost => hp<=0;
        public bool Complete => !Lost && beat>=finalSpawnBeat && enemies.Count==0;
        public GardenSimulation(RegionDefinition region,IEnumerable<GardenSpawn> waves,int budget=24,int health=10,bool diamond=false)
        {
            string error=MapRules.ValidateRoute(region);if(error!=null)throw new ArgumentException(error);
            this.region=region;spawns=waves.OrderBy(s=>s.beat).ToList();coins=budget;hp=health;this.diamond=diamond;
            finalSpawnBeat=spawns.Count==0?0:spawns.Max(s=>s.beat);
        }
        public GardenSimulation Copy()
        {
            var s=new GardenSimulation(region,spawns,coins,hp,diamond) { beat=beat,kills=kills,leaks=leaks,resonances=resonances,leadId=leadId,lastResonance=lastResonance,nextTower=nextTower,nextEnemy=nextEnemy };
            s.towers.AddRange(towers.Select(t=>t.Copy()));s.enemies.AddRange(enemies.Select(e=>e.Copy()));s.log.AddRange(log);return s;
        }
        public GardenTower Build(Cell c,bool chord)
        {
            int cost=chord?16:8;
            if(coins<cost||towers.Count>=6||MapRules.At(region,c)!='G'||towers.Any(t=>t.cell.Equals(c))) return null;
            var t=new GardenTower { id=nextTower++,cell=c,chord=chord,rhythm=chord?GardenRhythm.FirstBeat:GardenRhythm.EveryBeat };
            towers.Add(t);coins-=cost;return t;
        }
        public bool Sell(int id)
        {
            var t=towers.Find(x=>x.id==id);if(t==null)return false;
            coins+=t.chord?16:8;towers.Remove(t);if(leadId==id)leadId=-1;return true;
        }
        public static bool Plays(GardenRhythm r,int beat) => r==GardenRhythm.EveryBeat||r==GardenRhythm.Downbeat&&beat%2==0||r==GardenRhythm.Offbeat&&beat%2==1||r==GardenRhythm.FirstBeat&&beat%4==0;
        public bool CanHit(GardenTower t,Cell c) => MapRules.InRange(t.cell,c,t.chord,diamond)&&MapRules.Visible(region,t.cell,c);
        public IEnumerable<GardenEnemy> Targets(GardenTower t) => enemies.Where(e=>CanHit(t,Position(e))&&(t.filter==PitchFilter.All||(t.filter==PitchFilter.Only?e.pitch==t.pitch:e.pitch!=t.pitch))).OrderByDescending(e=>e.progress).ThenBy(e=>e.id);
        public Cell Position(GardenEnemy e) => region.route[Math.Max(0,Math.Min(region.route.Length-1,e.progress))];
        public bool CanPlay(int pitch)
        {
            var test=Copy();test.MoveAndSpawn(new GardenBeat());var t=test.towers.Find(x=>x.id==leadId);
            return t!=null && test.enemies.Any(e=>!e.chord&&e.pitch==pitch&&test.CanHit(t,test.Position(e)));
        }
        void MoveAndSpawn(GardenBeat result)
        {
            beat++;result.beat=beat;
            foreach(var e in enemies.ToArray())
            {
                e.previous=e.progress;
                if(e.hold>0) e.hold--;else e.progress+=e.speed;
                if(e.progress>=region.route.Length) { enemies.Remove(e);hp=Math.Max(0,hp-1);leaks++;result.leaks++; }
            }
            foreach(var s in spawns.Where(x=>x.beat==beat)) enemies.Add(new GardenEnemy { id=nextEnemy++,pitch=s.pitch,speed=Math.Max(1,s.speed),hp=s.chord?3:1,chord=s.chord,progress=0,previous=0 });
        }
        public GardenBeat Step(int manualPitch=-1)
        {
            var result=new GardenBeat { beat=beat };
            if(Lost||Complete&&beat>=0)return result;
            MoveAndSpawn(result);
            if(Lost)return result;
            var lead=towers.Find(t=>t.id==leadId&&!t.chord);
            if(lead!=null&&manualPitch>=0)
            {
                var target=enemies.Where(e=>!e.chord&&e.pitch==manualPitch&&CanHit(lead,Position(e))).OrderByDescending(e=>e.progress).ThenBy(e=>e.id).FirstOrDefault();
                if(target!=null) Hit(lead,target,1,true,result);
                else result.rest="休止 · 下一拍没有可见的同音单音目标";
            }
            foreach(var t in towers.OrderBy(t=>t.id))
            {
                if(t.id==leadId||!(t.chord?beat%4==0:Plays(t.rhythm,beat))) continue;
                var candidates=Targets(t).ToList();
                if(t.chord)
                {
                    var chords=candidates.Where(e=>e.chord).Take(3).ToArray();
                    if(chords.Length>0) { foreach(var e in chords)Hit(t,e,3,false,result); }
                    else { var single=candidates.FirstOrDefault();if(single!=null)Hit(t,single,1,false,result); }
                }
                else { var target=candidates.FirstOrDefault(e=>!e.chord)??candidates.FirstOrDefault();if(target!=null)Hit(t,target,1,false,result); }
            }
            int mask=0;foreach(var n in result.notes)mask|=1<<n.pitch;
            if((mask&21)==21 && result.notes.Select(n=>n.tower).Distinct().Count()>=2 && result.notes.Select(n=>n.kill).Distinct().Count()>=2 && beat-lastResonance>=4)
            {
                result.resonance=true;lastResonance=beat;resonances++;
                foreach(var e in enemies)e.hold=1;
            }
            return result;
        }
        void Hit(GardenTower t,GardenEnemy e,int damage,bool manual,GardenBeat result)
        {
            e.hp-=damage;result.shots.Add(new GardenShot { from=t.cell,to=Position(e),pitch=e.pitch,lethal=e.hp<=0 });
            if(e.hp>0)return;
            enemies.Remove(e);coins++;kills++;
            int[] pitches=t.chord&&e.chord ? new[]{0,2,4}:new[]{e.pitch};
            foreach(int p in pitches) { var n=new GardenNote { beat=beat,pitch=p,tower=t.id,kill=e.id,region=region.id,manual=manual };result.notes.Add(n);log.Add(n); }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
namespace TapTapGameJam.MusicGarden
{
    public static class GardenContent
    {
        public static readonly int[] Order={1,2,4,3};
        public static readonly int[] Budgets={24,24,32,40};
        public static void Prepare(RegionDefinition r)
        {
            Cell start=r.id==1?new Cell(0,2):r.id==2?new Cell(24,0):r.id==3?new Cell(0,5):new Cell(24,12);
            r.route=MapRules.Route(r,start);
            var pads=new List<Cell>();
            for(int y=0;y<r.rows;y++)for(int x=0;x<r.columns;x++)
            {
                var c=new Cell(x,y);
                if(MapRules.At(r,c)=='G'&&r.route.Any(p=>MapRules.InRange(c,p,true)&&MapRules.Visible(r,c,p))) pads.Add(c);
            }
            r.pads=pads.ToArray();
        }
        public static Cell[] Suggested(RegionDefinition r)
        {
            // Select a small early cluster that can answer the same arriving group.
            foreach(var point in r.route.Take(20))
            {
                var candidates=r.pads.Where(p=>MapRules.InRange(p,point,false)&&MapRules.Visible(r,p,point))
                    .OrderByDescending(p=>r.route.Take(20).Count(c=>MapRules.InRange(p,c,false)&&MapRules.Visible(r,p,c))).Take(3).ToArray();
                if(candidates.Length==3)return candidates;
            }
            return r.pads.OrderBy(p=>Array.FindIndex(r.route,c=>MapRules.InRange(p,c,false)&&MapRules.Visible(r,p,c))).Take(3).ToArray();
        }
        public static GardenSpawn[] Waves(int level,int segments)
        {
            var waves=new List<GardenSpawn>();int[] notes={0,2,4};
            for(int bar=0;bar<segments*4;bar++)
            {
                for(int voice=0;voice<3;voice++)
                {
                    bool chord=level>=2&&voice==2&&bar%2==1;
                    int speed=level>=2&&voice==1&&bar%3==2?2:1;
                    waves.Add(new GardenSpawn(bar*4,notes[(bar+voice)%3],speed,chord));
                }
            }
            return waves.ToArray();
        }
        public static void Arrange(GardenSimulation s)
        {
            var cells=Suggested(s.region);
            foreach(var c in cells)s.Build(c,false);
            if(s.region.id==1&&s.towers.Count>=3) { s.towers[0].rhythm=GardenRhythm.Offbeat;s.towers[2].rhythm=GardenRhythm.Offbeat; }
        }
    }
}

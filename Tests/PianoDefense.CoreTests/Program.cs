using System;
using System.Collections.Generic;
using System.Linq;
using TapTapGameJam.PianoDefense;

static class Program
{
    static int failures;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Test(string name, Action body)
    {
        try { body(); Console.WriteLine("PASS " + name); }
        catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + ": " + e.Message); }
    }
    static NoteEvent N(int beat, int pitch) { return new NoteEvent(beat, pitch, 1); }
    static DefenseSimulation Sim(params SpawnEvent[] spawns) { return new DefenseSimulation(spawns, 32); }
    static void Rhythm(DefenseSimulation sim, int id, int preset)
    {
        var method = typeof(DefenseSimulation).GetMethod("SetRhythm");
        Check(method != null, "tower attack rhythm is not implemented");
        var value = Enum.ToObject(method.GetParameters()[1].ParameterType, preset);
        method.Invoke(sim, new object[] { id, value });
    }
    static void Main()
    {
        int[] melody = { 0, 2, 4, 7 };
        Test("presented snapshot stays unchanged while the next beat is computed", () => {
            var s=Sim(new SpawnEvent(0,1,0));s.TryBuild(8,0);s.Step();
            var displayed=s.Snapshot();s.Step();s.SetFilter(s.Towers[0].Id,TargetMode.Solo,2);
            Check(displayed.Enemies.Count==1&&displayed.Enemies[0].X==11&&displayed.Coins==16,"pending results leaked into presentation");
            Check(displayed.Towers[0].Mode==TargetMode.All&&displayed.Notes.Count==0,"snapshot shared mutable state");
            Check(s.Enemies.Count==0&&s.Coins==17,"simulation did not advance independently");
        });
        Test("clock schedules once ahead of the audible beat", () => {
            var c=new BeatClock();c.Start(10);
            Check(!c.TrySchedule(10.1)&&c.TrySchedule(10.15)&&!c.TrySchedule(10.16),"duplicate or late scheduling");
            Check(!c.TryPresent(10.19)&&c.TryPresent(10.21)&&!c.TryPresent(10.22),"duplicate beat presentation");
        });
        Test("editor tempo changes the audible beat interval", () => {
            var c=new BeatClock { SecondsPerBeat=0.75 }; c.Start(10); c.TrySchedule(10.15); c.TryPresent(10.21);
            Check(Math.Abs(c.NextTime-10.95)<0.0001,"configured tempo was ignored");
        });
        Test("pause preserves pending beat and resumes on a fresh DSP anchor", () => {
            var c=new BeatClock();c.Start(10);c.TrySchedule(10.15);c.Pause();
            Check(!c.TryPresent(11)&&!c.TrySchedule(11),"paused transport advanced");
            c.Resume(20);Check(c.TrySchedule(20.15)&&Math.Abs(c.NextTime-20.2)<0.0001,"pending beat not rescheduled");
            Check(c.TryPresent(20.21),"resumed beat missing");
        });
        Test("a stalled frame cannot burst overdue beats", () => {
            var c=new BeatClock();c.Start(0);c.TrySchedule(0.15);
            Check(c.TryPresent(5)&&!c.TrySchedule(5),"transport replayed time debt");
            Check(c.NextTime>5.1,"clock failed to re-anchor");
        });
        Test("piano buffer has sound with safe bounds and faded endpoints", () => {
            float[] pcm=PianoSynthesis.Render(0,22050); double energy=0;
            foreach(float v in pcm){Check(!float.IsNaN(v)&&Math.Abs(v)<=1,"invalid PCM");energy+=v*v;}
            Check(energy/pcm.Length>0.0001,"silent piano");
            Check(Math.Abs(pcm[0])<0.001&&Math.Abs(pcm[pcm.Length-1])<0.001,"endpoint click");
            Check(Math.Abs(PianoSynthesis.Frequency(7)/PianoSynthesis.Frequency(0)-2)<0.0001,"high do is not an octave");
        });
        Test("filler notes do not break ordered melody", () => Check(MelodyMatcher.BestPrefix(new[]{N(0,0),N(1,1),N(2,2),N(4,4),N(6,7)},melody,4)==4,"expected full melody"));
        Test("chord cannot complete a sequential melody", () => Check(MelodyMatcher.BestPrefix(new[]{N(0,0),N(0,2),N(0,4),N(0,7)},melody,4)==1,"same beat must not advance"));
        Test("a gap over four beats breaks the chain", () => Check(MelodyMatcher.BestPrefix(new[]{N(0,0),N(5,2),N(6,4),N(7,7)},melody,4)==1,"stale first note accepted"));
        Test("later restart can recover from an expired chain", () => Check(MelodyMatcher.BestPrefix(new[]{N(0,0),N(5,0),N(7,2),N(9,4),N(11,7)},melody,4)==4,"new start lost"));
        Test("missing first note cannot award progress", () => Check(MelodyMatcher.BestPrefix(new[]{N(0,2),N(2,4),N(4,7)},melody,4)==0,"suffix awarded as prefix"));
        Test("empty sequence has zero progress", () => Check(MelodyMatcher.BestPrefix(new NoteEvent[0],melody,4)==0,"empty result wrong"));
        Test("build validates lanes, bounds, occupancy and money", () => {
            var s=Sim(); Check(!s.TryBuild(5,1)&&!s.TryBuild(-1,2),"invalid placement accepted");
            Check(s.TryBuild(8,2)&&!s.TryBuild(8,2),"duplicate placement accepted");
            Check(s.TryBuild(5,4)&&s.TryBuild(2,2)&&s.Coins==0,"cost not paid");
            Check(!s.TryBuild(3,4),"overspending accepted");
        });
        Test("editor starting budget survives layout reset and snapshot", () => {
            var s=new DefenseSimulation(Array.Empty<SpawnEvent>(),32,40);
            foreach(var x in new[]{2,3,4,5,6}) Check(s.TryBuild(x,0),"configured budget did not permit five towers");
            Check(s.Coins==0&&s.Snapshot().Coins==0,"snapshot lost configured budget state");
            s.ClearLayout(); Check(s.Coins==40,"clear layout restored the hardcoded default budget");
        });
        Test("scene tower layout keeps valid positions and filters", () => {
            var s=Sim();
            int placed=s.ApplyStartingTowers(new[]{
                new StartingTower(8,2,TargetMode.Solo,4),
                new StartingTower(8,2,TargetMode.Skip,1),
                new StartingTower(5,1,TargetMode.All,0),
                new StartingTower(5,4,TargetMode.Skip,2)
            });
            Check(placed==2&&s.Towers.Count==2&&s.Coins==8,"invalid or duplicate scene towers were built");
            Check(s.TowerAt(8,2).Mode==TargetMode.Solo&&s.TowerAt(8,2).Pitch==4,"first tower filter was lost");
            Check(s.TowerAt(5,4).Mode==TargetMode.Skip&&s.TowerAt(5,4).Pitch==2,"second tower filter was lost");
        });
        Test("selling refunds correctly and never twice", () => {
            var s=Sim(); s.TryBuild(8,2); int id=s.Towers[0].Id;
            Check(s.Sell(id)&&s.Coins==24&&!s.Sell(id),"preparation refund invalid");
            s.TryBuild(8,2); s.Step(); Check(s.Sell(s.Towers[0].Id)&&s.Coins==22,"battle refund invalid");
        });
        Test("movement happens before tower range detection", () => {
            var s=Sim(new SpawnEvent(0,1,0)); s.TryBuild(8,0);
            Check(s.Step().Notes.Count==0&&s.Enemies[0].X==11,"spawn/move wrong");
            Check(s.Step().Notes.Count==1&&s.Killed==1,"boundary target not killed after movement");
        });
        Test("two towers cannot claim the same kill", () => {
            var s=Sim(new SpawnEvent(0,1,0)); s.TryBuild(9,0); s.TryBuild(9,2);
            var r=s.Step(); Check(r.Notes.Count==1&&s.Killed==1&&s.Coins==9,"duplicate kill reward");
        });
        Test("solo lets other pitches pass; skip excludes its chosen pitch", () => {
            var s=Sim(new SpawnEvent(0,1,0),new SpawnEvent(0,1,2)); s.TryBuild(9,0);
            int id=s.Towers[0].Id; s.SetFilter(id,TargetMode.Solo,2);
            Check(s.Step().Notes.Single().Pitch==2&&s.Enemies.Single().Pitch==0,"solo picked wrong pitch");
            s.SetFilter(id,TargetMode.Skip,0); Check(s.Step().Notes.Count==0,"skip killed muted pitch");
            s.SetFilter(id,TargetMode.All,0); Check(s.Step().Notes.Single().Pitch==0,"all did not recover target");
        });
        Test("leaking enemy damages base once", () => {
            var s=Sim(new SpawnEvent(0,1,0)); for(int i=0;i<13;i++)s.Step();
            Check(s.Hp==9&&s.Leaked==1&&s.Enemies.Count==0,"leak removal/damage wrong");
            s.Step(); Check(s.Hp==9,"same enemy leaked twice");
        });
        Test("full phrase awards only once and cadence creates no notes", () => {
            var s=new DefenseSimulation(new[]{new SpawnEvent(0,1,0),new SpawnEvent(2,1,2),new SpawnEvent(4,1,4),new SpawnEvent(6,1,7),new SpawnEvent(15,5,1),new SpawnEvent(15,5,3)},32);
            s.TryBuild(9,0); BeatResult r=null; for(int i=0;i<16;i++)r=s.Step();
            Check(s.CompleteMelodies==1&&s.Coins==24,"phrase bonus not once");
            Check(r.CadenceVictims.Count==1&&s.Notes.Count==4,"cadence must not write bonus notes");
            s.Step(); Check(s.CompleteMelodies==1,"phrase rewarded again");
        });
        Test("phrase boundary does not carry half a melody", () => {
            var s=new DefenseSimulation(new[]{new SpawnEvent(12,1,0),new SpawnEvent(14,1,2),new SpawnEvent(16,1,4),new SpawnEvent(18,1,7)},32);
            s.TryBuild(9,0); for(int i=0;i<32;i++)s.Step();
            Check(s.CompleteMelodies==0,"melody crossed phrase boundary");
        });
        Test("decorated notes advance two cells per beat and overtake the queue", () => {
            var s=Sim(new SpawnEvent(0,1,0),new SpawnEvent(0,1,5,2));
            s.Step();
            Check(s.Enemies.Single(e=>e.Pitch==5).X==10&&s.Enemies.Single(e=>e.Pitch==0).X==11,"speed two movement wrong");
        });
        Test("a fast filler steals the beat from a queued melody note", () => {
            var s=Sim(new SpawnEvent(0,1,0),new SpawnEvent(0,1,5,2)); s.TryBuild(9,0);
            var r=s.Step();
            Check(r.Notes.Count==1&&r.Notes[0].Pitch==5&&s.Enemies.Single().Pitch==0,"fast enemy did not claim the shot");
        });
        Test("partial phrases grant layered coins and two-layer healing", () => {
            var s=new DefenseSimulation(new[]{new SpawnEvent(0,1,1),new SpawnEvent(13,1,0),new SpawnEvent(15,1,2)},32);
            for(int i=0;i<13;i++)s.Step();
            Check(s.Hp==9&&s.Leaked==1,"leak setup failed");
            s.TryBuild(9,0); BeatResult r=null; for(int i=0;i<3;i++)r=s.Step();
            Check(r.PhraseProgress==2&&s.Coins==20,"layered coins wrong");
            Check(s.Hp==10&&r.Healed==1&&s.CompleteMelodies==0,"two-layer heal wrong");
        });
        Test("healing respects the base maximum", () => {
            var s=new DefenseSimulation(new[]{new SpawnEvent(0,1,0),new SpawnEvent(2,1,2),new SpawnEvent(4,1,4),new SpawnEvent(6,1,7)},32);
            s.TryBuild(9,0); BeatResult r=null; for(int i=0;i<16;i++)r=s.Step();
            Check(r.PhraseProgress==4&&r.Healed==0&&s.Hp==DefenseSimulation.MaxHp,"undamaged base healed anyway");
        });
        Test("empty defense reaches loss and freezes simulation", () => {
            var s=new DefenseSimulation(); for(int i=0;i<200&&!s.Finished;i++)s.Step();
            Check(s.Finished&&!s.Won&&s.Hp==0,"expected loss"); int b=s.Beat; s.Step(); Check(s.Beat==b,"finished game progressed");
        });
        Test("reference defense finishes and replay preserves beat and pitches", () => {
            var a=new DefenseSimulation(); a.ApplyExampleLayout();
            var b=new DefenseSimulation(); b.ApplyExampleLayout();
            for(int i=0;i<200&&!a.Finished;i++){a.Step();b.Step();}
            Check(a.Finished&&a.Won,"reference defense cannot win");
            Check(a.Killed+a.Leaked==a.TotalEnemies,"enemies lost from accounting");
            Check(a.Notes.Count>8&&a.CompleteMelodies>=1,"reference defense makes no melody");
            Check(a.Notes.Select(n=>$"{n.Beat}:{n.Pitch}").SequenceEqual(b.Notes.Select(n=>$"{n.Beat}:{n.Pitch}")),"non-deterministic score log");
            Console.WriteLine($"  example HP={a.Hp}, kills={a.Killed}/{a.TotalEnemies}, melodies={a.CompleteMelodies}, notes={a.Notes.Count}, score={a.Score}");
        });
        Test("offbeat towers wait for global beats two and four", () => {
            var s=Sim(new SpawnEvent(0,1,0),new SpawnEvent(2,1,2));s.TryBuild(9,0);
            Rhythm(s,s.Towers[0].Id,2);
            Check(s.Step().Notes.Count==0,"offbeat tower attacked beat one");
            Check(s.Step().Notes.Single().Pitch==0,"offbeat tower missed beat two");
            Check(s.Step().Notes.Count==0&&s.Step().Notes.Single().Pitch==2,"four beat phase drifted");
        });
        Test("a tower built mid-round shares the global rhythm phase", () => {
            var s=Sim(new SpawnEvent(1,1,0));s.Step();s.TryBuild(9,0);Rhythm(s,s.Towers[0].Id,1);
            Check(s.Step().Notes.Count==0,"new tower restarted the rhythm at its build time");
            Check(s.Step().Notes.Count==1,"new tower failed to attack global beat three");
        });
        Test("Do Mi Sol on one beat holds survivors for exactly one beat", () => {
            var s=Sim(new SpawnEvent(0,1,0),new SpawnEvent(0,3,2),new SpawnEvent(0,5,4),new SpawnEvent(0,5,1));
            s.TryBuild(9,0);s.TryBuild(9,2);s.TryBuild(9,4);
            Rhythm(s,s.Towers[0].Id,3);Rhythm(s,s.Towers[1].Id,3);Rhythm(s,s.Towers[2].Id,3);
            s.SetFilter(s.Towers[0].Id,TargetMode.Solo,0);s.SetFilter(s.Towers[1].Id,TargetMode.Solo,2);s.SetFilter(s.Towers[2].Id,TargetMode.Solo,4);
            var first=s.Step();Check(first.Notes.Count==3,"chord setup failed");
            var resonance=typeof(BeatResult).GetField("Resonance");Check(resonance!=null,"resonance is not implemented");
            Check((bool)resonance.GetValue(first),"distinct Do Mi Sol did not resonate");
            int x=s.Enemies.Single().X;s.Step();Check(s.Enemies.Single().X==x,"resonance did not hold survivor");
            s.Step();Check(s.Enemies.Single().X==x-1,"resonance hold lasted more than one beat");
            Check(s.Notes.Count==3,"resonance generated extra notes");
        });
        Test("repeated notes do not count as a major triad", () => {
            var s=Sim(new SpawnEvent(0,1,0),new SpawnEvent(0,3,0),new SpawnEvent(0,5,4));
            s.TryBuild(9,0);s.TryBuild(9,2);s.TryBuild(9,4);var r=s.Step();
            var resonance=typeof(BeatResult).GetField("Resonance");Check(resonance!=null,"resonance is not implemented");
            Check(!(bool)resonance.GetValue(r),"duplicate Do substituted for Mi");
        });
        Test("rhythm is preserved in a snapshot and starting layout", () => {
            var s=Sim();s.TryBuild(9,0);Rhythm(s,s.Towers[0].Id,2);var shown=s.Snapshot();Rhythm(s,s.Towers[0].Id,1);
            var rhythm=typeof(TowerState).GetField("Rhythm");Check(rhythm!=null,"snapshot has no rhythm state");
            Check(Convert.ToInt32(rhythm.GetValue(shown.Towers[0]))==2,"snapshot rhythm shared mutable state");
        });
        Test("four composition lessons have reproducible winnable arrangements", () => {
            var lessons=typeof(DefenseSimulation).Assembly.GetType("TapTapGameJam.PianoDefense.CompositionLessons");
            Check(lessons!=null,"four composition lessons are not implemented");
            for(int lesson=0;lesson<4;lesson++) {
                var method=lessons.GetMethod("CreateSimulation");
                var a=(DefenseSimulation)method.Invoke(null,new object[]{lesson,true});
                var b=(DefenseSimulation)method.Invoke(null,new object[]{lesson,true});
                for(int beat=0;beat<250&&!a.Finished;beat++){a.Step();b.Step();}
                Check(a.Won,"lesson "+lesson+" example cannot survive");
                Check(a.Killed+a.Leaked==a.TotalEnemies,"lesson lost enemy accounting");
                Check(a.Notes.Select(n=>$"{n.Beat}:{n.Pitch}").SequenceEqual(b.Notes.Select(n=>$"{n.Beat}:{n.Pitch}")),"lesson randomness changed retry");
                Console.WriteLine($"  lesson {lesson+1}: HP={a.Hp}, notes={a.Notes.Count}, melodies={a.CompleteMelodies}");
            }
        });
        Console.WriteLine(failures==0?"ALL CORE TESTS PASSED":$"{failures} TESTS FAILED");
        Environment.ExitCode=failures==0?0:1;
    }
}

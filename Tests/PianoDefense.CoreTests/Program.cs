using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
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
    static LevelDefinition LoadLevel(string id)
    {
        return JsonSerializer.Deserialize<LevelDefinition>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Levels", id + ".json")),
            new JsonSerializerOptions { IncludeFields = true });
    }
    static void Main()
    {
        int[] melody = { 0, 2, 4, 7 };
        Test("presented snapshot stays unchanged while the next beat is computed", () => {
            var s=Sim(new SpawnEvent(0,1,0));s.TryBuild(9,0);s.Step();
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
        Test("pause preserves pending beat and resumes on a fresh DSP anchor", () => {
            var c=new BeatClock();c.Start(10);c.TrySchedule(10.15);c.Pause();
            Check(!c.TryPresent(11)&&!c.TrySchedule(11),"paused transport advanced");
            c.Resume(20);Check(c.TrySchedule(20.15)&&Math.Abs(c.NextTime-20.2)<0.0001,"pending beat not rescheduled");
            Check(c.TryPresent(20.21),"resumed beat missing");
        });
        Test("stopping a paused transport resets it for a different level", () => {
            var c=new BeatClock();c.Start(10);c.TrySchedule(10.15);c.Pause();c.Stop();
            Check(!c.Paused&&!c.Running&&!c.Scheduled,"old pause leaked into new level");
            c.Start(20);Check(c.TrySchedule(20.15),"new level could not start");
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
        Test("selling refunds correctly and never twice", () => {
            var s=Sim(); s.TryBuild(8,2); int id=s.Towers[0].Id;
            Check(s.Sell(id)&&s.Coins==24&&!s.Sell(id),"preparation refund invalid");
            s.TryBuild(8,2); s.Step(); Check(s.Sell(s.Towers[0].Id)&&s.Coins==22,"battle refund invalid");
        });
        Test("movement happens before tower range detection", () => {
            var s=Sim(new SpawnEvent(0,1,0)); s.TryBuild(9,0);
            Check(s.Step().Notes.Count==0&&s.Enemies[0].X==11,"spawn/move wrong");
            Check(s.Step().Notes.Count==1&&s.Killed==1,"boundary target not killed after movement");
        });
        Test("two towers cannot claim the same kill", () => {
            var s=Sim(new SpawnEvent(0,1,0)); s.TryBuild(9,0); s.TryBuild(8,0); s.Step();
            var r=s.Step(); Check(r.Notes.Count==1&&s.Killed==1&&s.Coins==9,"duplicate kill reward");
        });
        Test("solo and skip filter only enemies directly below", () => {
            var s=Sim(new SpawnEvent(0,1,0),new SpawnEvent(0,1,2),new SpawnEvent(1,1,0),new SpawnEvent(2,1,0));
            s.TryBuild(9,0); int id=s.Towers[0].Id;
            s.SetFilter(id,TargetMode.Solo,2);s.Step();
            Check(s.Step().Notes.Single().Pitch==2,"solo picked wrong pitch");
            s.SetFilter(id,TargetMode.Skip,0);Check(s.Step().Notes.Count==0,"skip killed muted pitch");
            s.SetFilter(id,TargetMode.All,0);Check(s.Step().Notes.Single().Pitch==0,"all did not recover target");
        });
        Test("tower attacks exactly the three cells in the row below", () => {
            foreach(int lane in new[]{1,3,5}) {
                var s=Sim(new SpawnEvent(0,lane,0,1,4));s.TryBuild(9,2);
                Check(s.Step().Notes.Count==0,"attacked beyond horizontal reach");
                for(int i=0;i<3;i++){
                    var r=s.Step();
                    Check(r.Notes.Count==(lane==3?1:0),"wrong attack direction");
                    if(lane==3) Check(r.Shots.Single().ToX==10-i&&r.Shots.Single().ToY==3,"wrong target cell");
                }
                Check(s.Step().Notes.Count==0,"attacked after leaving range");
                Check(s.Enemies.Single().HitsRemaining==(lane==3?1:4),"unexpected damage");
            }
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
            var s=Sim(new SpawnEvent(0,1,0),new SpawnEvent(0,1,5,2)); s.TryBuild(10,0);
            var r=s.Step();
            Check(r.Notes.Count==1&&r.Notes[0].Pitch==5&&s.Enemies.Single().Pitch==0,"fast enemy did not claim the shot");
        });
        Test("partial phrases grant layered coins and two-layer healing", () => {
            var s=new DefenseSimulation(new[]{new SpawnEvent(0,1,1),new SpawnEvent(12,1,0),new SpawnEvent(14,1,2)},32);
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
            var s=new DefenseSimulation(LoadLevel("canon")); for(int i=0;i<200&&!s.Finished;i++)s.Step();
            Check(s.Finished&&!s.Won&&s.Hp==0,"expected loss"); int b=s.Beat; s.Step(); Check(s.Beat==b,"finished game progressed");
        });
        Test("reference defense finishes and replay preserves beat and pitches", () => {
            var a=new DefenseSimulation(LoadLevel("canon")); a.ApplyExampleLayout();
            var b=new DefenseSimulation(LoadLevel("canon")); b.ApplyExampleLayout();
            for(int i=0;i<200&&!a.Finished;i++){a.Step();b.Step();}
            Check(a.Finished&&a.Won&&a.Leaked==3,"only the four-hit coda should escape the three-cell range");
            Check(a.Killed+a.Leaked+a.Enemies.Count==a.Spawns.Count(x=>x.Beat<=a.Beat),"spawned enemies lost from accounting");
            Check(a.Notes.Count>0,"reference defense makes no notes");
            Check(a.Notes.Select(n=>$"{n.Beat}:{n.Pitch}").SequenceEqual(b.Notes.Select(n=>$"{n.Beat}:{n.Pitch}")),"non-deterministic score log");
            Console.WriteLine($"  example HP={a.Hp}, kills={a.Killed}/{a.TotalEnemies}, melodies={a.CompleteMelodies}, notes={a.Notes.Count}, score={a.Score}");
        });
        Test("Canon theme and two chord cycles end on a four-beat tonic", () => {
            var s=new DefenseSimulation(LoadLevel("canon")); s.ApplyExampleLayout();
            Check(s.Towers.Count==4 && s.Coins==0,"four towers must be affordable");
            string[] theme={"E6","D6","C6","B5","A5","G5","A5","B5","C6","B5","A5","G5","F5","E5","F5","D5"};
            string[][] expected={
                new[]{"G4","E4","C4"},new[]{"B4","G4","D4"},
                new[]{"E5","C5","A4"},new[]{"B4","G4","E4"},
                new[]{"C5","A4","F4"},new[]{"G4","E4","C4"},
                new[]{"C5","A4","F4"},new[]{"B4","G4","D4"}};
            Check(s.Spawns.Count(x=>x.Lane==1)==36 && s.Spawns.Where(x=>x.Lane==1).All(x=>x.Hits==1),"melody rhythm wrong");
            Check(s.Spawns.Where(x=>x.Lane!=1).All(x=>x.Hits==(x.Beat==32?4:2)),"accompaniment duration wrong");
            Check(s.Spawns.All(x=>x.Pitch!=10&&x.Pitch!=11),"old flat seventh still present");
            Check(DefenseSimulation.Bpm==55,"tempo changed");
            for(int beat=0;beat<36;beat++){
                int chord=beat<32?(beat/2)%8:0;
                string melody=beat<32?theme[beat/2]:"C5";
                var active=s.Spawns.Where(x=>x.Beat<=beat&&x.Beat+x.Hits>beat).OrderBy(x=>x.Lane).ToArray();
                Check(active.Select(x=>DefenseSimulation.PitchNames[x.Pitch]).SequenceEqual(new[]{melody}.Concat(expected[chord])),"wrong scheduled notes at beat "+beat);
                for(int i=0;i<3;i++) Check(PianoSynthesis.Frequency(active[i].Pitch)>PianoSynthesis.Frequency(active[i+1].Pitch),"voices not descending");
            }
            Check(s.TotalBeats==36&&s.TotalEnemies==87,"incorrect arrangement length");
            foreach(var spawn in s.Spawns) Check(PianoSynthesis.Frequency(spawn.Pitch)>0,"unsupported note");
        });
        Test("overlapping towers cannot hit one enemy twice in the same beat", () => {
            var s=Sim(new SpawnEvent(0,1,0,1,2));s.TryBuild(9,0);s.TryBuild(8,0);s.Step();
            var r=s.Step();var snapshot=s.Snapshot();
            Check(r.Notes.Count==1&&s.Killed==0&&s.Coins==8&&s.Enemies.Single().HitsRemaining==1,"first hit wrong");
            r=s.Step();
            Check(r.Notes.Count==1&&s.Killed==1&&s.Coins==9&&s.Enemies.Count==0,"downstream final hit wrong");
            Check(snapshot.Enemies.Single().HitsRemaining==1&&snapshot.Enemies.Single().LastHitBeat==1,"snapshot mutated");
            Check(r.Shots.All(x=>Math.Abs(x.ToX-x.FromX)<=1&&x.ToY==x.FromY+1),"shot is outside lower adjacent cell");
        });
        Test("4536251 saved example restores the tested melody and chord sequence", () => {
            var level=LoadLevel("progression-4536251");var s=new DefenseSimulation(level);s.ApplyExampleLayout();
            string[] melody={"A5","C6","B5","G5","G5","B5","A5","C6","F5","A5","B5","G5","E5","D5","C5","C5"};
            string[][] chords={new[]{"C5","A4","F4"},new[]{"D5","B4","G4"},new[]{"B4","G4","E4"},new[]{"E5","C5","A4"},new[]{"A4","F4","D4"},new[]{"D5","B4","G4"},new[]{"G4","E4","C4"}};
            Check(s.TotalBeats==16&&s.TotalEnemies==37&&s.Towers.Count==4&&s.Coins==0,"incorrect example setup");
            for(int beat=0;beat<16;beat++){
                var active=s.Spawns.Where(x=>x.Beat<=beat&&x.Beat+x.Hits>beat).OrderBy(x=>x.Lane);
                Check(active.Select(x=>DefenseSimulation.PitchNames[x.Pitch]).SequenceEqual(new[]{melody[beat]}.Concat(chords[Math.Min(beat/2,6)])),"incorrect saved notes");
            }
            Check(level.chords.Select(x=>x.name).SequenceEqual(new[]{"F","G","Em","Am","Dm","G","C"}),"wrong progression");
            s.Step();var snapshot=s.Snapshot();Check(snapshot.TotalBeats==16,"snapshot lost level");
            var fresh=new DefenseSimulation(LoadLevel("canon"));fresh.ApplyExampleLayout();
            Check(fresh.Beat==-1&&fresh.Notes.Count==0&&fresh.TotalEnemies==87&&fresh.Hp==10,"new level shares old state");
        });
        Test("saved level validation rejects invalid pitch and layout", () => {
            var level=LoadLevel("canon");level.spawns[0].pitch="invalid";
            bool rejected=false;try{new DefenseSimulation(level);}catch(ArgumentException){rejected=true;}
            Check(rejected,"unknown pitch accepted");
            level=LoadLevel("canon");level.towers[0].y=1;rejected=false;
            try{new DefenseSimulation(level);}catch(ArgumentException){rejected=true;}
            Check(rejected,"lane tower accepted");
        });
        Console.WriteLine(failures==0?"ALL CORE TESTS PASSED":$"{failures} TESTS FAILED");
        Environment.ExitCode=failures==0?0:1;
    }
}

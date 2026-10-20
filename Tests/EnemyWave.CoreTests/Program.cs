using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TapTapGameJam.Combat;
using TapTapGameJam.Music;

static class Program
{
    static int checks;
    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        checks++;
    }
    static void CheckClearGate(int[][] beats)
    {
        int Drain(EnemyWaveSchedule schedule, double now)
        {
            int count = 0;
            while (schedule.TryPeek(now, out _, out _)) { count++; schedule.Advance(); }
            return count;
        }
        var gate = new EnemyWaveGroupGate(0,16,16,2);
        var roads = beats.Select(b => new EnemyWaveSchedule(b,0,gate)).ToArray();
        Check(!gate.TryAdvance(5,false,0), "temporary early clearance cannot skip unspawned first-wave enemies");
        Check(Drain(roads[0],500) == 5 && Drain(roads[1],500) == 5,
            "frame stall cannot spawn the locked second wave");
        Check(roads.All(s => s.CurrentGroupComplete && !s.Complete), "first wave drained but level is incomplete");
        Check(!gate.TryAdvance(500,true,1), "one survivor on either road keeps both roads locked");
        Check(!gate.TryAdvance(500,false,0), "a delayed route cannot be bypassed even if the map is empty");
        Check(gate.TryAdvance(500.4,true,0) && gate.GroupStartBeat == 501, "late clear starts on next whole beat");
        Check(roads.All(s => !s.TryPeek(500.9,out _,out _)), "no overdue birth when gate opens");
        foreach (var road in roads)
        {
            Check(road.TryPeek(501,out int i,out double birth) && i == 5 && birth == 501, "both routes share new anchor");
            Check(Drain(road,501) == 1 && Drain(road,502) == 1, "blue pair keeps its one-beat spacing after delay");
        }
        Check(roads[0].TryPeek(505,out _,out double yellowBirth) && yellowBirth == 505,
            "road one yellow retains local beat five");
        Drain(roads[0],513); Drain(roads[1],510);
        Check(roads[1].TryPeek(513,out _,out yellowBirth) && yellowBirth == 513,
            "road two yellow retains local beat thirteen");
        Drain(roads[1],513);
        Check(roads.All(s => s.Complete) && !gate.TryAdvance(1000,true,0), "exactly two groups, never a third");

        var early = new EnemyWaveGroupGate(0,16,16,2);
        var earlyRoad = new EnemyWaveSchedule(beats[0],0,early);
        Drain(earlyRoad,12);
        Check(early.TryAdvance(12.5,true,0) && early.GroupStartBeat == 32, "early clear retains full sixteen-beat rest");
        Check(!earlyRoad.TryPeek(31.99,out _,out _) && earlyRoad.TryPeek(32,out _,out double onTime) && onTime == 32,
            "normal second wave starts on authored beat thirty-three");
        var retryGate = new EnemyWaveGroupGate(80,16,16,2);
        var retryRoad = new EnemyWaveSchedule(beats[0],80,retryGate);
        Check(retryRoad.TryPeek(80,out int first,out _) && first == 0 && retryGate.ActiveGroup == 0,
            "retry discards the old gate and all accumulated delay");
    }
    static void Main()
    {
        var scene = File.ReadAllText("Assets/Scenes/Map.unity").Replace("\r\n", "\n");
        var blocks = Regex.Split(scene, @"(?m)^--- !u!");
        var routes = blocks.Where(b => b.Contains("  spawnPoint:") && b.Contains("  level: 2\n"))
            .OrderBy(b => b.Contains("  route: {fileID: 1488182067}") ? 0 : 1).ToArray();
        Check(routes.Length == 2, "two level-two routes");
        string yellow = Regex.Match(File.ReadAllText("Assets/Prefabs/Enemy_Yellow.prefab.meta"), @"guid: (\w+)").Groups[1].Value;
        string blue = "b5f6ce5d2ed72be4198b331eabf878b9";
        var events = routes.Select(b => Regex.Matches(b, @"  - beat: (\d+)\s+prefab: \{fileID: (\d+), guid: (\w+), type: 3\}")
            .Select(m => (beat: int.Parse(m.Groups[1].Value), id: long.Parse(m.Groups[2].Value), guid: m.Groups[3].Value)).ToArray()).ToArray();
        int[] blueBeats = {1,2,9,10,33,34,41,42};
        int[][] yellowBeats = {new[] {5,37},new[] {13,45}};
        for (int road = 0; road < 2; road++)
        {
            var expected = blueBeats.Select(b => (beat: b, guid: blue))
                .Concat(yellowBeats[road].Select(b => (beat: b, guid: yellow))).OrderBy(e => e.beat);
            Check(events[road].Select(e => (e.beat, e.guid)).SequenceEqual(expected), "road " + (road+1) + ": exact mixed-color score");
            Check(events[road].Length == 10, "ten enemies per road");
            for (int group = 0; group < 2; group++)
                Check(events[road].Count(e => e.guid == yellow && e.beat >= group * 32 + 1 && e.beat <= group * 32 + 16) >= 1,
                    "each group supplies a yellow enemy on road " + (road + 1));
            for (int rest = 0; rest < 1; rest++)
                Check(!events[road].Any(e => e.beat >= rest * 32 + 17 && e.beat <= rest * 32 + 32),
                    "sixteen-beat rest has no new enemies on road " + (road + 1));
        }
        Check(routes.All(r => r.Contains("  unitsPerBeat: 0.5\n")), "both level-two routes move half a unit per beat");
        Check(Regex.IsMatch(File.ReadAllText("Assets/Prefabs/Enemy_Yellow.prefab"), @"propertyPath: maxHealth\s+value: 8\b"), "level-two yellow has eight HP");
        Check(Regex.IsMatch(File.ReadAllText("Assets/Prefabs/Enemy_Blue.prefab"), @"propertyPath: maxHealth\s+value: 8\b"), "blue retains eight HP");
        Check(Regex.IsMatch(File.ReadAllText("Assets/Prefabs/Enemy_Yellow.prefab"), @"propertyPath: maxHealth\s+value: 8\b"), "other levels retain original yellow HP");
        foreach (var entry in new[] { (yellow,"Yellow"),(blue,"Blue") })
        {
            var prefab = File.ReadAllText("Assets/Prefabs/Enemy_" + entry.Item2 + ".prefab");
            long instance = long.Parse(Regex.Match(prefab, @"!u!1001 &(\d+)").Groups[1].Value);
            Check(events.SelectMany(e => e).Where(e => e.guid == entry.Item1).All(e => e.id == (7066893076875323972L ^ instance)), "variant root reference: " + entry.Item2);
        }
        int[][] beats = events.Select(es => es.Select(e => e.beat).ToArray()).ToArray();
        CheckClearGate(beats);
        double start = EnemyBeatTiming.FirstBirth(42.3, 1);
        Check(start == 43, "shared anchor starts on next beat without waiting for a phrase");
        var schedules = beats.Select(b => new EnemyWaveSchedule(b, start)).ToArray();
        var observed = new List<(int route, int beat)>();
        for (int beat = 1; beat <= 52; beat++)
        {
            for (int route = 0; route < 2; route++)
                while (schedules[route].TryPeek(start + beat - 1, out int index, out double birth))
                {
                    Check(birth == start + beats[route][index] - 1, "event retains musical birth time");
                    observed.Add((route, beats[route][index])); schedules[route].Advance();
                }
            int expected = 2 * blueBeats.Count(b => b <= beat) + yellowBeats.Sum(row => row.Count(b => b <= beat));
            Check(observed.Count == expected, "cumulative spawns on beat " + beat);
            Check(WaveCompletionRules.HasWon(5,20,observed.Count,0) == (beat >= 45), "group rests cannot prematurely complete wave");
        }
        Check(schedules.All(s => s.Complete), "finite wave completes once");
        Check(!WaveCompletionRules.HasWon(5,20,20,1), "living enemy prevents victory");
        Check(!WaveCompletionRules.HasWon(0,20,20,0), "dead base prevents victory");
        var stall = new EnemyWaveSchedule(beats[0], start);
        int caughtUp = 0;
        while (stall.TryPeek(start + 200, out _, out _)) { caughtUp++; stall.Advance(); }
        Check(caughtUp == 10 && !stall.TryPeek(start + 201, out _, out _), "stall neither drops nor duplicates finite events");
        var retry = new EnemyWaveSchedule(beats[0], 80);
        Check(!retry.TryPeek(79.999, out _, out _) && retry.TryPeek(80, out int first, out _) && first == 0, "retry begins anew at its shared anchor");
        var timeline = new MusicTimeline(); timeline.Start(0,60);
        var paused = new EnemyWaveSchedule(beats[0],0); paused.Advance();
        timeline.Pause(.5);
        Check(!paused.TryPeek(timeline.BeatAt(100),out _,out _), "pause retains wave position");
        timeline.Resume(100);
        Check(!paused.TryPeek(timeline.BeatAt(100.49),out _,out _), "resume retains time before next enemy");
        Check(paused.TryPeek(timeline.BeatAt(100.5),out int second,out _) && second == 1, "resume retains second blue at beat two");
        foreach (var invalid in new[] { new[] {0},new[] {2,1} })
        {
            bool rejected = false;
            try { new EnemyWaveSchedule(invalid,0); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "invalid authored beats rejected");
        }
        Console.WriteLine("ENEMY_WAVE_TESTS_PASS checks=" + checks);
    }
}

using System;
using System.Collections.Generic;

namespace TapTapGameJam.PianoDefense
{
    public enum TargetMode { All, Solo, Skip }

    public sealed class NoteEvent
    {
        public readonly int Beat, Pitch, TowerId;
        public NoteEvent(int beat, int pitch, int towerId) { Beat = beat; Pitch = pitch; TowerId = towerId; }
    }

    public sealed class SpawnEvent
    {
        public readonly int Beat, Lane, Pitch, Speed;
        public SpawnEvent(int beat, int lane, int pitch) : this(beat, lane, pitch, 1) { }
        public SpawnEvent(int beat, int lane, int pitch, int speed) { Beat = beat; Lane = lane; Pitch = pitch; Speed = speed; }
    }

    public sealed class EnemyState
    {
        public int Id, X, PreviousX, Lane, Pitch, Speed = 1;
    }

    public sealed class TowerState
    {
        public int Id, X, Y, Pitch;
        public TargetMode Mode;
    }

    public sealed class ShotEvent
    {
        public int FromX, FromY, ToX, ToY, Pitch;
    }

    public sealed class BeatResult
    {
        public int Beat;
        public readonly List<NoteEvent> Notes = new List<NoteEvent>();
        public readonly List<ShotEvent> Shots = new List<ShotEvent>();
        public readonly List<EnemyState> Leaks = new List<EnemyState>();
        public readonly List<EnemyState> CadenceVictims = new List<EnemyState>();
        public int PhraseProgress = -1;
        public int Healed;
    }

    public sealed class DefenseSimulation
    {
        public const int Width = 12, Height = 7, MaxHp = 10, TowerCost = 8, Range = 3, MaxTowers = 8;
        public const double Bpm = 110, SecondsPerBeat = 60.0 / Bpm;
        public static readonly int[] TargetMelody = { 0, 2, 4, 7 };
        public static readonly string[] NoteNames = { "do", "re", "mi", "fa", "sol", "la", "si", "do′" };
        public static readonly string[] PitchNames = { "C4", "D4", "E4", "F4", "G4", "A4", "B4", "C5" };

        readonly List<TowerState> towers = new List<TowerState>();
        readonly List<EnemyState> enemies = new List<EnemyState>();
        readonly List<NoteEvent> notes = new List<NoteEvent>();
        readonly List<NoteEvent> phrase = new List<NoteEvent>();
        readonly List<SpawnEvent> spawns;
        int nextSpawn, nextEnemyId, nextTowerId, phrases;
        double completionSum;
        public int Beat { get; private set; } = -1;
        public int Coins { get; private set; } = 24;
        public int Hp { get; private set; } = MaxHp;
        public int Killed { get; private set; }
        public int Leaked { get; private set; }
        public int CompleteMelodies { get; private set; }
        public int Harmonies { get; private set; }
        public int LastPhraseProgress { get; private set; }
        public bool Finished { get; private set; }
        public bool Won { get { return Finished && Hp > 0; } }
        public int TotalBeats { get; private set; }
        public int TotalEnemies { get { return spawns.Count; } }
        public int MelodyProgress { get { return MelodyMatcher.BestPrefix(phrase, TargetMelody, 4); } }
        public IReadOnlyList<TowerState> Towers { get { return towers; } }
        public IReadOnlyList<EnemyState> Enemies { get { return enemies; } }
        public IReadOnlyList<NoteEvent> Notes { get { return notes; } }
        public IReadOnlyList<SpawnEvent> Spawns { get { return spawns; } }
        public double KillRate { get { return TotalEnemies == 0 ? 1 : (double)Killed / TotalEnemies; } }
        public int Score { get { return (int)Math.Round(600 * KillRate + 300 * Hp / MaxHp + 900 * (phrases == 0 ? 0 : completionSum / phrases) + 60 * Harmonies); } }
        public int Stars { get { return (KillRate >= 0.9 ? 1 : 0) + (Hp >= 8 ? 1 : 0) + (CompleteMelodies >= 2 ? 1 : 0); } }

        public DefenseSimulation() : this(CreateWaves(), 112) { }

        public DefenseSimulation(IEnumerable<SpawnEvent> wave, int totalBeats)
        {
            spawns = new List<SpawnEvent>(wave);
            // Stable sorting preserves simultaneous spawn priority.
            for (int i = 1; i < spawns.Count; i++)
            {
                var item = spawns[i]; int j = i - 1;
                while (j >= 0 && spawns[j].Beat > item.Beat) { spawns[j + 1] = spawns[j]; j--; }
                spawns[j + 1] = item;
            }
            foreach (var spawn in spawns)
                if (spawn.Beat < 0 || !IsLane(spawn.Lane) || spawn.Pitch < 0 || spawn.Pitch > 7 || spawn.Speed < 1 || spawn.Speed > 2)
                    throw new ArgumentException("Invalid wave entry.");
            TotalBeats = Math.Max(1, totalBeats);
        }

        public static bool IsLane(int y) { return y == 1 || y == 3 || y == 5; }
        public DefenseSimulation Snapshot()
        {
            var copy = new DefenseSimulation(spawns, TotalBeats)
            {
                Beat = Beat, Coins = Coins, Hp = Hp, Killed = Killed, Leaked = Leaked,
                CompleteMelodies = CompleteMelodies, Harmonies = Harmonies, LastPhraseProgress = LastPhraseProgress,
                Finished = Finished, nextSpawn = nextSpawn, nextEnemyId = nextEnemyId,
                nextTowerId = nextTowerId, phrases = phrases, completionSum = completionSum
            };
            foreach (var t in towers) copy.towers.Add(new TowerState { Id = t.Id, X = t.X, Y = t.Y, Pitch = t.Pitch, Mode = t.Mode });
            foreach (var e in enemies) copy.enemies.Add(new EnemyState { Id = e.Id, X = e.X, PreviousX = e.PreviousX, Lane = e.Lane, Pitch = e.Pitch, Speed = e.Speed });
            copy.notes.AddRange(notes); copy.phrase.AddRange(phrase);
            return copy;
        }
        public static bool IsBuildCell(int x, int y) { return x > 0 && x < Width - 1 && y >= 0 && y < Height && !IsLane(y); }
        public TowerState TowerAt(int x, int y) { return towers.Find(t => t.X == x && t.Y == y); }
        public TowerState TowerById(int id) { return towers.Find(t => t.Id == id); }

        public bool TryBuild(int x, int y)
        {
            if (Finished || !IsBuildCell(x, y) || TowerAt(x, y) != null || Coins < TowerCost || towers.Count >= MaxTowers) return false;
            towers.Add(new TowerState { Id = ++nextTowerId, X = x, Y = y, Mode = TargetMode.All });
            Coins -= TowerCost;
            return true;
        }

        public bool Sell(int id)
        {
            var tower = TowerById(id);
            if (Finished || tower == null) return false;
            towers.Remove(tower); Coins += Beat < 0 ? TowerCost : 6; return true;
        }

        public void SetFilter(int id, TargetMode mode, int pitch)
        {
            var tower = TowerById(id);
            if (Finished || tower == null || pitch < 0 || pitch > 7 || mode < TargetMode.All || mode > TargetMode.Skip) return;
            tower.Mode = mode; tower.Pitch = pitch;
        }

        public void ClearLayout()
        {
            if (Beat >= 0) return;
            towers.Clear(); Coins = 24; nextTowerId = 0;
        }

        public void ApplyExampleLayout()
        {
            if (Beat >= 0) return;
            ClearLayout(); TryBuild(8, 2); TryBuild(5, 4); TryBuild(2, 2);
        }

        public BeatResult Step()
        {
            var result = new BeatResult { Beat = Beat };
            if (Finished) return result;
            Beat++; result.Beat = Beat;
            while (nextSpawn < spawns.Count && spawns[nextSpawn].Beat <= Beat)
            {
                var spawn = spawns[nextSpawn++];
                enemies.Add(new EnemyState { Id = ++nextEnemyId, X = Width, PreviousX = Width, Lane = spawn.Lane, Pitch = spawn.Pitch, Speed = spawn.Speed });
            }
            foreach (var enemy in enemies)
            {
                // 装饰音怪（设计文档 §4.1）：速度 2 格/拍，从偶数列出生因此只经过偶数列。
                enemy.PreviousX = enemy.X; enemy.X -= enemy.Speed;
                if (enemy.X < 0) { Hp = Math.Max(0, Hp - 1); Leaked++; result.Leaks.Add(enemy); }
            }
            enemies.RemoveAll(e => e.X < 0);
            if (Hp == 0) { Finished = true; return result; }

            foreach (var tower in towers)
            {
                EnemyState target = null;
                foreach (var enemy in enemies)
                {
                    if (Math.Abs(enemy.X - tower.X) + Math.Abs(enemy.Lane - tower.Y) > Range) continue;
                    if (tower.Mode == TargetMode.Solo && enemy.Pitch != tower.Pitch) continue;
                    if (tower.Mode == TargetMode.Skip && enemy.Pitch == tower.Pitch) continue;
                    if (target == null || enemy.X < target.X || (enemy.X == target.X && enemy.Id < target.Id)) target = enemy;
                }
                if (target == null) continue;
                enemies.Remove(target); Killed++; Coins++;
                var note = new NoteEvent(Beat, target.Pitch, tower.Id);
                result.Notes.Add(note); notes.Add(note); phrase.Add(note);
                result.Shots.Add(new ShotEvent { FromX = tower.X, FromY = tower.Y, ToX = target.X, ToY = target.Lane, Pitch = target.Pitch });
            }
            if (result.Notes.Count >= 2) Harmonies++;
            if ((Beat + 1) % 16 == 0)
            {
                result.PhraseProgress = MelodyProgress;
                LastPhraseProgress = result.PhraseProgress;
                phrases++; completionSum += result.PhraseProgress / 4.0;
                if (result.PhraseProgress > 0)
                {
                    // 乐句祝福（设计文档 §6.1 延音式）：每层 +1 币；≥2 层据点回复 1，不超过上限。
                    Coins += result.PhraseProgress;
                    if (result.PhraseProgress >= 2 && Hp < MaxHp) { Hp++; result.Healed = 1; }
                }
                if (result.PhraseProgress == 4)
                {
                    CompleteMelodies++;
                    int count = (enemies.Count + 3) / 4;
                    enemies.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Id.CompareTo(b.Id));
                    for (int i = 0; i < count; i++) result.CadenceVictims.Add(enemies[i]);
                    enemies.RemoveRange(0, count); Killed += count;
                }
                phrase.Clear();
            }
            if (Beat + 1 >= TotalBeats && nextSpawn == spawns.Count && enemies.Count == 0) Finished = true;
            return result;
        }

        public static List<SpawnEvent> CreateWaves()
        {
            var wave = new List<SpawnEvent>();
            for (int i = 0; i < 8; i++) wave.Add(new SpawnEvent(i * 2, 3, TargetMelody[i % 4]));
            int[] second = { 0, 2, 1, 4, 7, 3, 0, 2, 4, 7, 1, 2 };
            for (int i = 0; i < second.Length; i++) wave.Add(new SpawnEvent(40 + i * 2, i % 2 == 0 ? 1 : 5, second[i]));
            int[] third = { 0, 2, 4, 7, 1, 0, 2, 4, 7, 3, 0, 2, 4, 7, 2, 0 };
            for (int i = 0; i < third.Length; i++) wave.Add(new SpawnEvent(80 + i * 2, 1 + (i % 3) * 2, third[i]));
            wave.Add(new SpawnEvent(90, 1, 4)); wave.Add(new SpawnEvent(94, 3, 2)); wave.Add(new SpawnEvent(98, 5, 7));
            // 装饰音怪（设计文档 §4.1）：速度 2 格/拍，填充音高，赶在队头抢拍打乱击杀顺序。
            wave.Add(new SpawnEvent(44, 1, 5, 2));
            wave.Add(new SpawnEvent(52, 5, 1, 2));
            wave.Add(new SpawnEvent(84, 3, 3, 2));
            wave.Add(new SpawnEvent(92, 1, 5, 2));
            wave.Add(new SpawnEvent(100, 5, 1, 2));
            return wave;
        }
    }
}

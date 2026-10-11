using System;
using System.Collections.Generic;

namespace TapTapGameJam.PianoDefense
{
    public static class CompositionLessons
    {
        public const int Count = 4;
        public static readonly string[] Titles = { "初见 · 音符的轨迹", "交错 · 两种节拍", "共鸣 · 三音成和弦", "涌现 · 你的合奏" };
        public static readonly string[] Hints = {
            "敌人从右向左移动。琴塔击杀时奏响敌人的音高。移动布塔位置，听见顺序变化。",
            "选择琴塔，让一座在 1 / 3 拍出击，另一座在 2 / 4 拍回应。所有塔共享同一节拍。",
            "三座琴塔分别独奏 Do、Mi、Sol，同拍击杀触发共鸣：全场休止一拍，获得 3 币。",
            "三条路线与快速音符共同出现。组合位置、节拍和音高筛选，让防守涌现为音乐。"
        };
        public static int Beats(int lesson) { return new[] { 48, 64, 48, 80 }[Clamp(lesson)]; }
        public static int Tempo(int lesson) { return new[] { 100, 108, 104, 112 }[Clamp(lesson)]; }
        public static int Budget(int lesson) { return lesson == 3 ? 32 : 24; }
        static int Clamp(int lesson) { return Math.Max(0, Math.Min(Count - 1, lesson)); }

        public static List<SpawnEvent> Waves(int lesson)
        {
            lesson = Clamp(lesson);
            var waves = new List<SpawnEvent>();
            if (lesson == 0)
            {
                for (int i = 0; i < 16; i++) waves.Add(new SpawnEvent(i * 2, 3, DefenseSimulation.TargetMelody[i % 4]));
            }
            else if (lesson == 1)
            {
                for (int i = 0; i < 12; i++)
                {
                    waves.Add(new SpawnEvent(i * 4, 1, DefenseSimulation.TargetMelody[i % 4]));
                    waves.Add(new SpawnEvent(i * 4 + 1, 5, DefenseSimulation.TargetMelody[(i + 1) % 4]));
                }
            }
            else if (lesson == 2)
            {
                for (int beat = 0; beat < 32; beat += 4)
                {
                    waves.Add(new SpawnEvent(beat, 1, 0));
                    waves.Add(new SpawnEvent(beat, 3, 2));
                    waves.Add(new SpawnEvent(beat, 5, 4));
                    if (beat % 8 == 0) waves.Add(new SpawnEvent(beat, 5, 1));
                }
            }
            else
            {
                for (int beat = 0; beat < 64; beat += 4)
                {
                    waves.Add(new SpawnEvent(beat, 1, 0));
                    waves.Add(new SpawnEvent(beat, 3, 2));
                    waves.Add(new SpawnEvent(beat, 5, 4));
                    waves.Add(new SpawnEvent(beat + 2, 3, DefenseSimulation.TargetMelody[(beat / 4) % 4]));
                    if (beat % 12 == 0) waves.Add(new SpawnEvent(beat + 1, 5, 5, 2));
                }
            }
            return waves;
        }

        public static List<StartingTower> Layout(int lesson)
        {
            if (lesson == 0) return new List<StartingTower> { new StartingTower(8, 2, TargetMode.All, 0) };
            if (lesson == 1) return new List<StartingTower> {
                new StartingTower(8, 0, TargetMode.All, 0, TowerRhythm.Downbeat),
                new StartingTower(8, 6, TargetMode.All, 0, TowerRhythm.Offbeat)
            };
            var result = new List<StartingTower> {
                new StartingTower(9, 0, TargetMode.Solo, 0, TowerRhythm.FirstBeat),
                new StartingTower(9, 2, TargetMode.Solo, 2, TowerRhythm.FirstBeat),
                new StartingTower(9, 4, TargetMode.Solo, 4, TowerRhythm.FirstBeat)
            };
            if (lesson == 3) result.Add(new StartingTower(5, 4, TargetMode.All, 0));
            return result;
        }

        public static DefenseSimulation CreateSimulation(int lesson, bool example)
        {
            var simulation = new DefenseSimulation(Waves(lesson), Beats(lesson), Budget(lesson));
            if (example) simulation.ApplyStartingTowers(Layout(lesson));
            return simulation;
        }
    }
}

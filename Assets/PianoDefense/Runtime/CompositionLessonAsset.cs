using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.PianoDefense
{
    [CreateAssetMenu(menuName = "Game Jam/Composition Lesson")]
    public sealed class CompositionLessonAsset : ScriptableObject
    {
        public string Title;
        [TextArea(2, 5)] public string Hint;
        [Range(40, 220)] public int Bpm = 104;
        [Range(0, 64)] public int StartingCoins = 24;
        [Min(16)] public int TotalBeats = 48;
        public List<WaveSpawn> Waves = new List<WaveSpawn>();

        public DefenseSimulation CreateSimulation()
        {
            var spawns = new List<SpawnEvent>();
            foreach (var entry in Waves)
                if (entry.Beat >= 0 && DefenseSimulation.IsLane(entry.Lane) && entry.Pitch >= 0 && entry.Pitch < 8 && entry.Speed >= 1 && entry.Speed <= 2)
                    spawns.Add(new SpawnEvent(entry.Beat, entry.Lane, entry.Pitch, entry.Speed));
            return new DefenseSimulation(spawns, TotalBeats, StartingCoins);
        }
    }
}

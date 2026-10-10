using System;
using System.Collections.Generic;

namespace TapTapGameJam.PianoDefense
{
    [Serializable]
    public sealed class LevelDefinition
    {
        public string id, title, summary, detail;
        public int totalBeats, initialCoins;
        public LevelSpawn[] spawns;
        public LevelTower[] towers;
        public LevelChord[] chords;

        public List<SpawnEvent> CreateSpawns()
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(title) || totalBeats < 1 ||
                initialCoins < 0 || spawns == null || towers == null || chords == null || chords.Length == 0)
                throw new ArgumentException("Incomplete level configuration.");
            var result = new List<SpawnEvent>();
            foreach (var s in spawns)
            {
                int pitch = s == null ? -1 : Array.IndexOf(DefenseSimulation.PitchNames, s.pitch);
                if (s == null || s.beat < 0 || s.hits < 1 || s.beat + s.hits > totalBeats ||
                    !DefenseSimulation.IsLane(s.lane) || pitch < 0 || s.speed < 1 || s.speed > 2)
                    throw new ArgumentException("Invalid level spawn in " + id);
                result.Add(new SpawnEvent(s.beat, s.lane, pitch, s.speed, s.hits));
            }
            var occupied = new HashSet<int>();
            foreach (var t in towers)
                if (t == null || !DefenseSimulation.IsBuildCell(t.x, t.y) || t.lane != t.y + 1 ||
                    !DefenseSimulation.IsLane(t.lane) || !occupied.Add(t.y * DefenseSimulation.Width + t.x))
                    throw new ArgumentException("Invalid example tower in " + id);
            if (towers.Length > DefenseSimulation.MaxTowers || towers.Length * DefenseSimulation.TowerCost > initialCoins)
                throw new ArgumentException("Example layout exceeds budget.");
            int previous = -1;
            foreach (var c in chords)
            {
                if (c == null || c.beat <= previous || c.beat >= totalBeats || string.IsNullOrEmpty(c.name))
                    throw new ArgumentException("Invalid chord timeline.");
                previous = c.beat;
            }
            if (chords[0].beat != 0) throw new ArgumentException("Chord timeline must start at beat zero.");
            return result;
        }

        public string ChordAt(int beat)
        {
            string name = chords[0].name;
            foreach (var chord in chords) { if (chord.beat > beat) break; name = chord.name; }
            return name;
        }
    }

    [Serializable] public sealed class LevelSpawn { public int beat, lane, speed, hits; public string pitch; }
    [Serializable] public sealed class LevelTower { public int x, y, lane; }
    [Serializable] public sealed class LevelChord { public int beat; public string name; }
}

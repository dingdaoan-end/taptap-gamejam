using System;
using System.Collections.Generic;

namespace TapTapGameJam.Combat
{
    public enum TowerInstrument { Duduk, Chord, Bell }
    public enum EnemyResistance { Any, Red, Blue, Yellow, Boss }

    // Keeps recent hits, including already credited ones, so a third instrument in
    // the same window also qualifies without counting earlier damage twice.
    public sealed class EnemyDamageRules
    {
        sealed class Hit
        {
            public TowerInstrument Instrument;
            public double Time;
            public int Damage;
            public bool Credited;
        }
        readonly List<Hit> hits = new List<Hit>();

        public static bool CanHit(EnemyResistance resistance, TowerInstrument instrument)
        {
            if (instrument < TowerInstrument.Duduk || instrument > TowerInstrument.Bell) return false;
            switch (resistance)
            {
                case EnemyResistance.Any:
                case EnemyResistance.Boss: return true;
                case EnemyResistance.Red: return instrument == TowerInstrument.Duduk;
                case EnemyResistance.Blue: return instrument == TowerInstrument.Chord;
                case EnemyResistance.Yellow: return instrument == TowerInstrument.Bell;
                default: return false;
            }
        }

        public void Reset() => hits.Clear();

        public int Resolve(EnemyResistance resistance, TowerInstrument instrument, int amount, double time, double window)
        {
            if (amount <= 0 || !CanHit(resistance, instrument) || double.IsNaN(time) || double.IsInfinity(time)) return 0;
            if (resistance != EnemyResistance.Boss) return amount;
            window = Math.Max(0, window);
            hits.RemoveAll(hit => time - hit.Time > window + 1e-9 || hit.Time > time);
            hits.Add(new Hit { Instrument = instrument, Time = time, Damage = amount });
            bool combined = hits.Exists(hit => hit.Instrument != instrument);
            if (!combined) return 0;
            int total = 0;
            foreach (var hit in hits)
            {
                if (hit.Credited) continue;
                total += hit.Damage;
                hit.Credited = true;
            }
            return total;
        }
    }
}

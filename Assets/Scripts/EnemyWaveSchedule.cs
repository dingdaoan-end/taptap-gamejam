using System;

namespace TapTapGameJam.Combat
{
    // One-based authored beats, anchored to a shared zero-based music beat.
    public sealed class EnemyWaveSchedule
    {
        readonly int[] beats;
        readonly double startBeat;
        readonly EnemyWaveGroupGate groupGate;
        public int NextIndex { get; private set; }
        public bool Complete => NextIndex >= beats.Length;
        public bool CurrentGroupComplete => Complete || (groupGate != null &&
            (beats[NextIndex] - 1) / groupGate.Period > groupGate.ActiveGroup);

        public EnemyWaveSchedule(int[] authoredBeats, double startBeat, EnemyWaveGroupGate groupGate = null)
        {
            if (authoredBeats == null) throw new ArgumentNullException(nameof(authoredBeats));
            if (double.IsNaN(startBeat) || double.IsInfinity(startBeat)) throw new ArgumentOutOfRangeException(nameof(startBeat));
            beats = (int[])authoredBeats.Clone();
            this.startBeat = startBeat;
            this.groupGate = groupGate;
            for (int i = 0; i < beats.Length; i++)
                if (beats[i] < 1 || (i > 0 && beats[i] < beats[i - 1]))
                    throw new ArgumentException("Wave beats must be positive and ordered.", nameof(authoredBeats));
        }

        public bool TryPeek(double currentBeat, out int index, out double birthBeat)
        {
            index = NextIndex;
            birthBeat = Complete ? double.PositiveInfinity : startBeat + beats[index] - 1;
            if (!Complete && groupGate != null)
                birthBeat = groupGate.BirthBeat(beats[index]);
            return !Complete && currentBeat >= birthBeat;
        }

        public void Advance() { if (!Complete) NextIndex++; }
    }

    // One shared gate for every road. Waiting affects future birth times only;
    // the music clock, existing enemies and tower attacks continue normally.
    public sealed class EnemyWaveGroupGate
    {
        public int Period { get; }
        readonly int groupCount;
        public int ActiveGroup { get; private set; }
        public double GroupStartBeat { get; private set; }

        public EnemyWaveGroupGate(double startBeat, int groupBeats, int restBeats, int groupCount)
        {
            if (double.IsNaN(startBeat) || double.IsInfinity(startBeat) || groupBeats < 1 || restBeats < 0 || groupCount < 1)
                throw new ArgumentOutOfRangeException();
            GroupStartBeat = startBeat;
            Period = checked(groupBeats + restBeats);
            this.groupCount = groupCount;
        }

        public double BirthBeat(int authoredBeat) => (authoredBeat - 1) / Period == ActiveGroup
            ? GroupStartBeat + (authoredBeat - 1) % Period : double.PositiveInfinity;

        public bool TryAdvance(double now, bool allRoadsFinishedGroup, int livingEnemies)
        {
            if (ActiveGroup + 1 >= groupCount || !allRoadsFinishedGroup || livingEnemies != 0) return false;
            // Early clearance reserves the normal next start. Late clearance joins
            // the next whole beat, never backdating a birth to the expired rest.
            GroupStartBeat = Math.Max(GroupStartBeat + Period, Math.Ceiling(now));
            ActiveGroup++;
            return true;
        }
    }
}

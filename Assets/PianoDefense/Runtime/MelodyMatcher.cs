using System;
using System.Collections.Generic;

namespace TapTapGameJam.PianoDefense
{
    public static class MelodyMatcher
    {
        // Notes arrive in beat order. Snapshot each beat so a chord cannot become a melody.
        // Keeping the latest reachable ending beat preserves chains with the best future window.
        public static int BestPrefix(IReadOnlyList<NoteEvent> notes, int[] target, int maxGap)
        {
            if (target.Length == 0) return 0;
            int[] latest = new int[target.Length];
            for (int i = 0; i < latest.Length; i++) latest[i] = int.MinValue;
            int best = 0;
            for (int i = 0; i < notes.Count;)
            {
                int beat = notes[i].Beat;
                int[] before = (int[])latest.Clone();
                do
                {
                    for (int k = 0; k < target.Length; k++)
                    {
                        if (notes[i].Pitch != target[k]) continue;
                        if (k > 0 && (before[k - 1] == int.MinValue || beat <= before[k - 1] || beat - before[k - 1] > maxGap)) continue;
                        latest[k] = beat;
                        best = Math.Max(best, k + 1);
                    }
                    i++;
                } while (i < notes.Count && notes[i].Beat == beat);
            }
            return best;
        }
    }
}

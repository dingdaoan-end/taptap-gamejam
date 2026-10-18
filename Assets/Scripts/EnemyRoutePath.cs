using System;
using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.Combat
{
    // A spawned enemy owns its route geometry even if scene waypoints are edited.
    public sealed class EnemyRoutePath
    {
        readonly Vector2[] points;
        readonly double[] ends;
        public double Length => ends[ends.Length - 1];
        public Vector2 End => points[points.Length - 1];
        public EnemyRoutePath(Vector2 origin, IReadOnlyList<Vector2> waypoints)
        {
            var list = new List<Vector2> { origin };
            foreach (var point in waypoints)
                if ((point - list[list.Count - 1]).sqrMagnitude > .000001f) list.Add(point);
            points = list.ToArray(); ends = new double[points.Length];
            for (int i = 1; i < points.Length; i++) ends[i] = ends[i - 1] + Vector2.Distance(points[i - 1], points[i]);
        }
        public Vector2 Sample(double distance, out Vector2 direction, out bool arrived)
        {
            arrived = distance >= Length;
            if (arrived || points.Length == 1) { direction = Vector2.zero; return End; }
            distance = Math.Max(0, distance);
            int low = 1, high = ends.Length - 1;
            while (low < high)
            {
                int middle = (low + high) / 2;
                if (ends[middle] <= distance) low = middle + 1; else high = middle;
            }
            direction = (points[low] - points[low - 1]).normalized;
            float fraction = (float)((distance - ends[low - 1]) / (ends[low] - ends[low - 1]));
            return Vector2.Lerp(points[low - 1], points[low], fraction);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TapTapGameJam.Combat
{
    public static class EnemyRoadPathfinder
    {
        public static IEnumerable<Vector3Int> Neighbors(Vector3Int cell)
        {
            yield return cell + Vector3Int.up; yield return cell + Vector3Int.right;
            yield return cell + Vector3Int.down; yield return cell + Vector3Int.left;
        }
        public static HashSet<Vector3Int> WalkableCells(Tilemap roads, Tilemap rocks)
        {
            var cells = new HashSet<Vector3Int>();
            if (roads == null) return cells;
            foreach (var cell in roads.cellBounds.allPositionsWithin)
                if (roads.HasTile(cell) && (rocks == null || !rocks.isActiveAndEnabled ||
                    !rocks.HasTile(rocks.WorldToCell(roads.GetCellCenterWorld(cell))))) cells.Add(cell);
            return cells;
        }
        public static bool TryFind(Tilemap roads, Tilemap rocks, Vector2 entrance, Vector2 goal,
            out Vector2[] points, out string reason)
        {
            points = new Vector2[0];
            var cells = WalkableCells(roads, rocks);
            if (cells.Count == 0) { reason = "No walkable road cells."; return false; }
            var start = roads.WorldToCell(entrance);
            var end = roads.WorldToCell(goal);
            // Markers must be on the route, not silently snapped across a missing tile.
            if (!cells.Contains(start) || !cells.Contains(end))
            { reason = "Entrance or goal is not on a walkable road cell."; return false; }
            if (start == end) { reason = "Entrance and goal must be on different road cells."; return false; }
            var queue = new Queue<Vector3Int>(); queue.Enqueue(start);
            var previous = new Dictionary<Vector3Int, Vector3Int> { [start] = start };
            while (queue.Count > 0 && !previous.ContainsKey(end))
            {
                var cell = queue.Dequeue();
                foreach (var next in Neighbors(cell))
                    if (cells.Contains(next) && !previous.ContainsKey(next))
                    { previous[next] = cell; queue.Enqueue(next); }
            }
            if (!previous.ContainsKey(end)) { reason = "Road is disconnected or blocked by rocks."; return false; }
            var path = new List<Vector2>();
            for (var cell = end; ; cell = previous[cell])
            {
                path.Add(roads.GetCellCenterWorld(cell)); if (cell == start) break;
            }
            path.Reverse(); points = path.ToArray(); reason = string.Empty; return true;
        }
    }
}

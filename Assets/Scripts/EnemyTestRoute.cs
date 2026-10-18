using UnityEngine;
using UnityEngine.Tilemaps;

namespace TapTapGameJam.Combat
{
    public sealed class EnemyTestRoute : MonoBehaviour
    {
        [SerializeField] Tilemap roadTiles;
        [SerializeField] Tilemap obstacleTiles;
        [SerializeField] Transform entrance;
        [SerializeField] Transform goal;
        [Tooltip("Rebuild from the edited road tiles when this spawner starts. Child waypoints remain the editor preview.")]
        [SerializeField] bool rebuildFromRoadOnStart;
        Vector2[] preparedPoints;
        public int Count => preparedPoints != null ? preparedPoints.Length : transform.childCount;
        public Vector2 Point(int index) => preparedPoints != null ? preparedPoints[index] : (Vector2)transform.GetChild(index).position;
        public bool TryPrepare(out string reason)
        {
            if (!rebuildFromRoadOnStart) { preparedPoints = null; reason = Count > 0 ? string.Empty : "Route has no waypoints."; return Count > 0; }
            if (entrance == null || goal == null || roadTiles == null)
            { reason = "Assign road tiles, entrance and goal."; return false; }
            if (!EnemyRoadPathfinder.TryFind(roadTiles, obstacleTiles, entrance.position, goal.position, out var points, out reason)) return false;
            preparedPoints = points; return true;
        }
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < Count; i++)
            {
                Gizmos.DrawWireSphere(Point(i), 0.15f);
                if (i > 0) Gizmos.DrawLine(Point(i - 1), Point(i));
            }
        }
    }
}

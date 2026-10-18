using UnityEngine;

namespace TapTapGameJam.Combat
{
    public sealed class EnemyTestRoute : MonoBehaviour
    {
        public int Count => transform.childCount;
        public Vector2 Point(int index) => transform.GetChild(index).position;
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

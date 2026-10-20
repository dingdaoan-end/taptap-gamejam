using UnityEngine;

namespace TapTapGameJam.Combat
{
    [DisallowMultipleComponent]
    public sealed class EnemyWalkVisual : MonoBehaviour
    {
        [SerializeField] SpriteRenderer sprite;
        [Tooltip("The supplied Minion art faces left before Flip X.")]
        [SerializeField] bool artworkFacesRight;

        void Awake()
        {
            if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
        }
        public void SetSpawnFacing(int level)
        {
            if (sprite == null) Awake();
            if (sprite == null) return;
            bool faceRight = level == 1 || level == 4;
            sprite.flipX = artworkFacesRight ? !faceRight : faceRight;
        }

    }
}

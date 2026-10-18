using UnityEngine;

namespace TapTapGameJam.Combat
{
    [DisallowMultipleComponent]
    public sealed class EnemyWalkVisual : MonoBehaviour
    {
        [SerializeField] SpriteRenderer sprite;
        [Tooltip("The supplied Minion art faces left before Flip X.")]
        [SerializeField] bool artworkFacesRight;
        [SerializeField, Min(0)] float bobHeight = .045f;
        [SerializeField, Min(.05f)] float strideDistance = .65f;
        [SerializeField, Range(0, 8)] float swayDegrees = 2;
        Vector3 restPosition;
        Quaternion restRotation;
        bool captured;

        void Awake()
        {
            if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
            if (sprite == null) return;
            restPosition = sprite.transform.localPosition;
            restRotation = sprite.transform.localRotation; captured = true;
        }
        public void Present(Vector2 direction, double distance, bool moving)
        {
            if (!captured) Awake();
            if (sprite == null) return;
            if (Mathf.Abs(direction.x) > .001f) sprite.flipX = artworkFacesRight ? direction.x < 0 : direction.x > 0;
            float phase = (float)(distance % Mathf.Max(.05f, strideDistance)) / Mathf.Max(.05f, strideDistance) * Mathf.PI * 2;
            sprite.transform.localPosition = restPosition + Vector3.up * (moving ? Mathf.Abs(Mathf.Sin(phase)) * bobHeight : 0);
            sprite.transform.localRotation = restRotation * Quaternion.Euler(0, 0, moving ? Mathf.Sin(phase) * swayDegrees : 0);
        }
        void OnDisable()
        {
            if (!captured || sprite == null) return;
            sprite.transform.localPosition = restPosition; sprite.transform.localRotation = restRotation;
        }
    }
}

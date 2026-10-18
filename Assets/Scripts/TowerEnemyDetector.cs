using System;
using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class TowerEnemyDetector : MonoBehaviour
    {
        [SerializeField] private LayerMask enemyLayers;
        private BoxCollider2D range;
        private readonly List<Collider2D> overlaps = new List<Collider2D>();
        private readonly List<Rigidbody2D> enemies = new List<Rigidbody2D>();

        public IReadOnlyList<Rigidbody2D> Enemies => enemies;
        public int EnemyCount => enemies.Count;
        public Rigidbody2D CurrentTarget { get; private set; }
        public bool HasTarget => CurrentTarget != null;
        public event Action TargetChanged;

        private void Reset() => enemyLayers = LayerMask.GetMask("Enemy");
        private void Awake() => range = GetComponent<BoxCollider2D>();

        // Query the actual collider instead of relying on enter/exit callbacks:
        // this also handles initially overlapping, disabled and destroyed enemies.
        private void FixedUpdate() => RefreshTargets();

        public void RefreshTargets()
        {
            if (range == null) range = GetComponent<BoxCollider2D>();
            enemies.Clear();
            if (isActiveAndEnabled && range.enabled && range.isTrigger)
            {
                var filter = new ContactFilter2D();
                filter.SetLayerMask(enemyLayers);
                filter.useTriggers = true;
                range.OverlapCollider(filter, overlaps);
                foreach (var collider in overlaps)
                {
                    var body = collider.attachedRigidbody;
                    if (body == null || !body.simulated || !body.gameObject.activeInHierarchy)
                        continue;
                    var health = body.GetComponent<EnemyHealth>();
                    if (health != null && !health.IsAlive) continue;
                    // Several colliders belonging to one enemy count as one target.
                    if (!enemies.Contains(body)) enemies.Add(body);
                }
            }

            // Keep the existing target until it leaves, avoiding target flicker.
            if (CurrentTarget != null && enemies.Contains(CurrentTarget)) return;
            Rigidbody2D next = null;
            float nearest = float.PositiveInfinity;
            foreach (var enemy in enemies)
            {
                float distance = ((Vector2)range.bounds.center - enemy.position).sqrMagnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    next = enemy;
                }
            }
            SetTarget(next);
        }

        private void SetTarget(Rigidbody2D next)
        {
            // ReferenceEquals also detects a destroyed Unity object becoming null.
            if (ReferenceEquals(CurrentTarget, next)) return;
            CurrentTarget = next;
            TargetChanged?.Invoke();
        }

        private void OnDisable()
        {
            enemies.Clear();
            overlaps.Clear();
            SetTarget(null);
        }
    }
}

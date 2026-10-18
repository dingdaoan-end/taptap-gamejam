using System.Collections.Generic;
using TapTapGameJam.Music;
using UnityEngine;
using UnityEngine.Serialization;

namespace TapTapGameJam.Combat
{
    public sealed class EnemyAudioTestSpawner : MonoBehaviour
    {
        [FormerlySerializedAs("enemyTemplate")]
        public GameObject enemyPrefab;
        public Transform spawnPoint;
        public EnemyTestRoute route;
        public MusicClock clock;
        [Header("Live tuning (also affects enemies already spawned)")]
        [Min(0)] public float unitsPerBeat = 1;
        [Min(1)] public int spawnIntervalBeats = 4;
        public float UnitsPerBeat => float.IsNaN(unitsPerBeat) || float.IsInfinity(unitsPerBeat) ? 1 : Mathf.Max(0, unitsPerBeat);
        private readonly List<GameObject> livingEnemies = new List<GameObject>();
        private double nextBirthBeat;
        private bool scheduleReady;
        private bool initialized;
        private int activeInterval;
        private double lastBirthBeat;
        private bool hasSpawned;
        private bool warnedInvalidRoad;
        [SerializeField] private int arrivedCount;
        public int ArrivedCount => arrivedCount;
        public int LivingCount => livingEnemies.Count;

        private void Start()
        {
            if (spawnPoint == null) spawnPoint = transform.Find("SpawnPoint");
            if (route != null && !route.TryPrepare(out string routeError))
            { Debug.LogError("Enemy route is invalid: " + routeError, this); enabled = false; return; }
            if (enemyPrefab == null || spawnPoint == null || route == null || route.Count < 1)
            {
                Debug.LogError("Assign Enemy Prefab, Spawn Point and a route with at least one waypoint.", this);
                enabled = false;
                return;
            }
            if (enemyPrefab.scene.IsValid())
            {
                Debug.LogError("Enemy Prefab must reference a prefab asset from Project, not a scene instance.", this);
                enabled = false;
                return;
            }
            if (clock == null) clock = FindObjectOfType<MusicClock>();
            if (clock == null)
            {
                Debug.LogError("Beat-based enemy spawning requires MusicClock.", this);
                enabled = false;
                return;
            }
            clock.TransportChanged += OnTransportChanged;
            initialized = true;
        }
        private void Update()
        {
            if (!initialized || clock == null || !clock.IsRunning || clock.IsPaused) return;
            if (route == null || route.Count < 1 || enemyPrefab == null || spawnPoint == null) { enabled = false; return; }
            if (!scheduleReady)
            {
                activeInterval = Mathf.Max(1, spawnIntervalBeats);
                nextBirthBeat = EnemyBeatTiming.FirstBirth(clock.CurrentBeat, activeInterval);
                scheduleReady = true;
            }
            if (activeInterval != Mathf.Max(1, spawnIntervalBeats))
            {
                activeInterval = Mathf.Max(1, spawnIntervalBeats);
                // Changing to a shorter interval never creates a burst of overdue enemies.
                nextBirthBeat = hasSpawned
                    ? System.Math.Max(lastBirthBeat + activeInterval, System.Math.Ceiling(clock.CurrentBeat))
                    : EnemyBeatTiming.FirstBirth(clock.CurrentBeat, activeInterval);
            }
            // Beat zero is held during the clock's scheduled startup lead-in.
            if (AudioSettings.dspTime < clock.GetDspTimeForBeat(nextBirthBeat)) return;
            // Recheck once per due batch so edited entries/goals/roads affect only
            // future enemies. Existing enemies keep their own geometry snapshot.
            if (!route.TryPrepare(out string routeError))
            {
                if (!warnedInvalidRoad) Debug.LogWarning("Enemy birth skipped: " + routeError, this);
                warnedInvalidRoad = true;
                nextBirthBeat = EnemyBeatTiming.FirstBirth(clock.CurrentBeat + .000001, activeInterval);
                return;
            }
            warnedInvalidRoad = false;
            livingEnemies.RemoveAll(enemy => enemy == null);
            // Skip enemies that would already have reached the end after a stall.
            double routeLength = Vector2.Distance(spawnPoint.position, route.Point(0));
            for (int i = 1; i < route.Count; i++) routeLength += Vector2.Distance(route.Point(i - 1), route.Point(i));
            double beat = clock.CurrentBeat;
            double travelBeats = UnitsPerBeat > 0 ? routeLength / UnitsPerBeat : double.PositiveInfinity;
            if (beat - nextBirthBeat >= travelBeats)
                nextBirthBeat += (System.Math.Floor((beat - travelBeats - nextBirthBeat) /
                    activeInterval) + 1) * activeInterval;
            for (int count = 0; nextBirthBeat <= beat && count < 64; count++)
            {
                Spawn(nextBirthBeat);
                lastBirthBeat = nextBirthBeat;
                hasSpawned = true;
                nextBirthBeat += activeInterval;
            }
        }
        private void Spawn(double birthBeat)
        {
            var livingEnemy = Instantiate(enemyPrefab, spawnPoint.position, Quaternion.identity);
            livingEnemy.name = "Enemy (Audio Test)";
            var mover = livingEnemy.GetComponent<EnemyPathMover>();
            if (mover == null) mover = livingEnemy.AddComponent<EnemyPathMover>();
            mover.Initialize(route, birthBeat, clock, this, spawnPoint.position);
            livingEnemy.SetActive(true);
            livingEnemies.Add(livingEnemy);
        }
        public void NotifyArrival(EnemyPathMover enemy)
        {
            if (enemy == null || !enemy.HasArrived || !livingEnemies.Remove(enemy.gameObject)) return;
            arrivedCount++;
        }
        private void OnTransportChanged()
        {
            // Pause/resume retains birth beats. Stop/restart starts a new wave.
            if (clock != null && !clock.IsRunning) ClearEnemies();
        }
        private void ClearEnemies()
        {
            foreach (var enemy in livingEnemies)
                if (enemy != null) { enemy.SetActive(false); Destroy(enemy); }
            livingEnemies.Clear();
            scheduleReady = false;
            hasSpawned = false;
            arrivedCount = 0;
            warnedInvalidRoad = false;
        }
        private void OnDisable()
        {
            ClearEnemies();
        }
        private void OnDestroy()
        {
            if (clock != null) clock.TransportChanged -= OnTransportChanged;
        }
        private void OnValidate()
        {
            if (spawnPoint == null) spawnPoint = transform.Find("SpawnPoint");
        }
    }
}

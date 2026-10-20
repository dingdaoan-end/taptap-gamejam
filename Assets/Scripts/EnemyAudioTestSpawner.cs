using System.Collections.Generic;
using TapTapGameJam.Music;
using UnityEngine;
using UnityEngine.Serialization;

namespace TapTapGameJam.Combat
{
    public sealed class EnemyAudioTestSpawner : MonoBehaviour
    {
        [System.Serializable]
        public sealed class WaveSpawn
        {
            [Min(1)] public int beat = 1;
            public GameObject prefab;
        }
        [FormerlySerializedAs("enemyTemplate")]
        public GameObject enemyPrefab;
        public Transform spawnPoint;
        public EnemyTestRoute route;
        public MusicClock clock;
        [Tooltip("Spawn facing: levels 1 and 4 face right; levels 2 and 3 face left.")]
        [SerializeField, Range(1, 4)] int level = 1;
        public int Level => level;
        [Tooltip("Total enemies per run. Zero keeps the unlimited test-spawning behaviour.")]
        [SerializeField, Min(0)] int spawnLimit;
        [Tooltip("Optional finite wave. Beat 1 is the shared wave start; keep entries ordered by beat.")]
        [SerializeField] WaveSpawn[] waveSpawns = new WaveSpawn[0];
        public bool HasAuthoredWave => waveSpawns != null && waveSpawns.Length > 0;
        EnemyWaveSchedule waveSchedule;
        public int LastAuthoredBeat => HasAuthoredWave ? waveSpawns[waveSpawns.Length - 1].beat : 0;
        public bool CurrentWaveGroupComplete => waveSchedule != null && waveSchedule.CurrentGroupComplete;
        public int SpawnedCount { get; private set; }
        public int PlannedCount => HasAuthoredWave ? waveSpawns.Length : Mathf.Max(0, spawnLimit);
        public bool SpawningComplete => PlannedCount > 0 && SpawnedCount >= PlannedCount;
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
        public event System.Action EnemyArrived;
        public int LivingCount
        {
            get
            {
                // Lethal hits deactivate immediately; Destroy is deferred until the
                // end of the frame. Arrival already removes its enemy explicitly.
                livingEnemies.RemoveAll(enemy => enemy == null ||
                    (enemy.TryGetComponent<EnemyHealth>(out var health) && health.CurrentHealth <= 0));
                return livingEnemies.Count;
            }
        }

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
            livingEnemies.RemoveAll(enemy => enemy == null);
            if (SpawningComplete) return;
            if (route == null || route.Count < 1 || enemyPrefab == null || spawnPoint == null) { enabled = false; return; }
            if (HasAuthoredWave) { UpdateAuthoredWave(); return; }
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
            // Skip enemies that would already have reached the end after a stall.
            double routeLength = Vector2.Distance(spawnPoint.position, route.Point(0));
            for (int i = 1; i < route.Count; i++) routeLength += Vector2.Distance(route.Point(i - 1), route.Point(i));
            double beat = clock.CurrentBeat;
            double travelBeats = UnitsPerBeat > 0 ? routeLength / UnitsPerBeat : double.PositiveInfinity;
            if (beat - nextBirthBeat >= travelBeats)
                nextBirthBeat += (System.Math.Floor((beat - travelBeats - nextBirthBeat) /
                    activeInterval) + 1) * activeInterval;
            for (int count = 0; nextBirthBeat <= beat && count < 64 && !SpawningComplete; count++)
            {
                Spawn(nextBirthBeat);
                lastBirthBeat = nextBirthBeat;
                hasSpawned = true;
                nextBirthBeat += activeInterval;
            }
        }
        // The level controller supplies one anchor for every route before enabling them.
        public void BeginWaveAt(double startBeat, EnemyWaveGroupGate groupGate = null)
        {
            if (!HasAuthoredWave) return;
            var beats = new int[waveSpawns.Length];
            for (int i = 0; i < beats.Length; i++)
            {
                var entry = waveSpawns[i];
                if (entry == null || entry.prefab == null || entry.prefab.scene.IsValid())
                    throw new System.InvalidOperationException(name + ": each wave entry needs a prefab asset.");
                beats[i] = entry.beat;
            }
            waveSchedule = new EnemyWaveSchedule(beats, startBeat, groupGate);
        }

        void UpdateAuthoredWave()
        {
            if (waveSchedule == null) BeginWaveAt(EnemyBeatTiming.FirstBirth(clock.CurrentBeat, 1));
            if (!waveSchedule.TryPeek(clock.CurrentBeat, out _, out double nextBeat) ||
                AudioSettings.dspTime < clock.GetDspTimeForBeat(nextBeat)) return;
            if (!route.TryPrepare(out string reason))
            {
                if (!warnedInvalidRoad) Debug.LogWarning("Enemy wave waiting for a valid route: " + reason, this);
                warnedInvalidRoad = true;
                return;
            }
            warnedInvalidRoad = false;
            // Finite waves never discard overdue births: each enemy must participate
            // in the wave's spawned/living/arrival accounting after a frame stall.
            for (int count = 0; count < 64 && waveSchedule.TryPeek(clock.CurrentBeat, out int index, out double birth); count++)
            {
                SpawnPrefab(birth, waveSpawns[index].prefab);
                waveSchedule.Advance();
            }
        }

        private void Spawn(double birthBeat) => SpawnPrefab(birthBeat, enemyPrefab);

        private void SpawnPrefab(double birthBeat, GameObject prefab)
        {
            var livingEnemy = Instantiate(prefab, spawnPoint.position, Quaternion.identity);
            livingEnemy.name = "Enemy (Audio Test)";
            var mover = livingEnemy.GetComponent<EnemyPathMover>();
            if (mover == null) mover = livingEnemy.AddComponent<EnemyPathMover>();
            mover.Initialize(route, birthBeat, clock, this, spawnPoint.position);
            livingEnemy.SetActive(true);
            livingEnemies.Add(livingEnemy);
            SpawnedCount++;
        }
        public void NotifyArrival(EnemyPathMover enemy)
        {
            if (enemy == null || !enemy.HasArrived || !livingEnemies.Remove(enemy.gameObject)) return;
            arrivedCount++;
            // Membership was removed above, so duplicate arrival notifications
            // cannot take another life from the base.
            EnemyArrived?.Invoke();
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
            waveSchedule = null;
            hasSpawned = false;
            SpawnedCount = 0;
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

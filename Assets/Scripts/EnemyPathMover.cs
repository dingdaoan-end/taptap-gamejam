using TapTapGameJam.Music;
using UnityEngine;

namespace TapTapGameJam.Combat
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class EnemyPathMover : MonoBehaviour
    {
        [SerializeField] private EnemyTestRoute route;
        private Rigidbody2D body;
        [SerializeField] private MusicClock clock;
        [SerializeField, Min(0)] private float unitsPerBeat = 1;
        private EnemyAudioTestSpawner settings;
        private double lastBeat;
        private double distanceTravelled;
        private float activeSpeed;
        private EnemyRoutePath path;
        public bool IsInitialized { get; private set; }
        public bool HasArrived { get; private set; }
        public double DistanceTravelled => distanceTravelled;
        public Vector2 MoveDirection { get; private set; }

        void Start()
        {
            if (!IsInitialized && route != null && clock != null && route.TryPrepare(out _))
                Initialize(route, clock.CurrentBeat, clock, null, transform.position);
        }
        public void Initialize(EnemyTestRoute path, double spawnBeat, MusicClock musicClock, EnemyAudioTestSpawner owner, Vector2 origin)
        {
            route = path;
            if (path == null || path.Count < 1 || musicClock == null) { IsInitialized = false; return; }
            settings = owner;
            clock = musicClock;
            var points = new Vector2[path.Count];
            for (int i = 0; i < points.Length; i++) points[i] = path.Point(i);
            this.path = new EnemyRoutePath(origin, points);
            var visual = GetComponent<EnemyWalkVisual>();
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = true;
            body.gravityScale = 0;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            // A delayed frame joins at the correct musical position instead of
            // permanently reducing the spacing between successive enemies.
            activeSpeed = settings != null ? settings.UnitsPerBeat : SafeOwnSpeed;
            lastBeat = clock.CurrentBeat;
            distanceTravelled = EnemyBeatTiming.Distance(lastBeat, spawnBeat) * activeSpeed;
            body.position = this.path.Sample(distanceTravelled, out var direction, out _);
            MoveDirection = direction; HasArrived = false; IsInitialized = true;
            if (visual != null && settings != null) visual.SetSpawnFacing(settings.Level);
        }
        private void FixedUpdate()
        {
            if (!isActiveAndEnabled || !IsInitialized || path == null || body == null) return;
            if (clock == null || !clock.IsRunning || clock.IsPaused) return;
            double beat = clock.CurrentBeat;
            double elapsedBeats = beat - lastBeat;
            if (elapsedBeats < 0) { lastBeat = beat; return; }
            distanceTravelled = EnemyBeatTiming.Advance(distanceTravelled, lastBeat, beat, activeSpeed);
            lastBeat = beat;
            activeSpeed = settings != null ? settings.UnitsPerBeat : SafeOwnSpeed;
            Vector2 position = path.Sample(distanceTravelled, out var direction, out bool arrived);
            MoveDirection = direction;
            if (arrived)
            {
                body.position = position; HasArrived = true; IsInitialized = false; MoveDirection = Vector2.zero;
                if (settings != null) settings.NotifyArrival(this);
                gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(gameObject);
                return;
            }
            // A DSP hitch can jump across many road segments. MovePosition has a
            // physics speed cap; recover directly instead of drifting off rhythm.
            if (elapsedBeats > .5) body.position = position; else body.MovePosition(position);
        }

        private float SafeOwnSpeed => float.IsNaN(unitsPerBeat) || float.IsInfinity(unitsPerBeat) ? 1 : Mathf.Max(0, unitsPerBeat);
    }
}

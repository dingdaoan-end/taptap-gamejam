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
        [SerializeField, Min(0)] private float unitsPerBeat = 0.5f;
        [Tooltip("Use this prefab speed instead of the spawner speed (for bosses).") ]
        [SerializeField] private bool overrideSpawnerSpeed;
        [Tooltip("Disable for test enemies that should disappear at the goal without damaging the base.")]
        [SerializeField] private bool damagesBaseOnArrival = true;
        [Header("Spawn facing")]
        [SerializeField] private SpriteRenderer sprite;
        [Tooltip("The supplied Minion art faces left before Flip X.")]
        [SerializeField] private bool artworkFacesRight;
        public bool DamagesBaseOnArrival => damagesBaseOnArrival;
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
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = true;
            body.gravityScale = 0;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            // A delayed frame joins at the correct musical position instead of
            // permanently reducing the spacing between successive enemies.
            activeSpeed = GetUnitsPerBeat(settings);
            lastBeat = clock.CurrentBeat;
            distanceTravelled = EnemyBeatTiming.Distance(lastBeat, spawnBeat) * activeSpeed;
            body.position = this.path.Sample(distanceTravelled, out var direction, out _);
            MoveDirection = direction; HasArrived = false; IsInitialized = true;
            if (settings != null) SetSpawnFacing(settings.Level);
        }

        public void SetSpawnFacing(int level)
        {
            if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
            if (sprite == null) return;
            // Facing stays fixed for the level, including when the route turns.
            bool faceRight = level == 1 || level == 4;
            sprite.flipX = artworkFacesRight ? !faceRight : faceRight;
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
            activeSpeed = GetUnitsPerBeat(settings);
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

        public float GetUnitsPerBeat(EnemyAudioTestSpawner owner) =>
            !overrideSpawnerSpeed && owner != null ? owner.UnitsPerBeat : SafeOwnSpeed;

        private float SafeOwnSpeed => float.IsNaN(unitsPerBeat) || float.IsInfinity(unitsPerBeat) ? 0.5f : Mathf.Max(0, unitsPerBeat);
    }
}

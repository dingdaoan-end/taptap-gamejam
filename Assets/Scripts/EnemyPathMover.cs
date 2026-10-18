using TapTapGameJam.Music;
using UnityEngine;

namespace TapTapGameJam.Combat
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class EnemyPathMover : MonoBehaviour
    {
        private EnemyTestRoute route;
        private Rigidbody2D body;
        private MusicClock clock;
        private EnemyAudioTestSpawner settings;
        private double lastBeat;
        private double distanceTravelled;
        private float activeSpeed;
        private Vector2 spawnPosition;
        public void Initialize(EnemyTestRoute path, double spawnBeat, MusicClock musicClock, EnemyAudioTestSpawner owner, Vector2 origin)
        {
            route = path;
            // Snapshot per enemy: moving SpawnPoint must not move existing enemies.
            spawnPosition = origin;
            settings = owner;
            clock = musicClock;
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = true;
            body.gravityScale = 0;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            // A delayed frame joins at the correct musical position instead of
            // permanently reducing the spacing between successive enemies.
            activeSpeed = settings.UnitsPerBeat;
            lastBeat = clock.CurrentBeat;
            distanceTravelled = EnemyBeatTiming.Distance(lastBeat, spawnBeat) * activeSpeed;
            body.position = PositionAt(distanceTravelled, out _);
        }
        private void FixedUpdate()
        {
            if (route == null || body == null) return;
            if (clock == null || !clock.IsRunning || clock.IsPaused) return;
            double beat = clock.CurrentBeat;
            distanceTravelled = EnemyBeatTiming.Advance(distanceTravelled, lastBeat, beat, activeSpeed);
            lastBeat = beat;
            if (settings != null) activeSpeed = settings.UnitsPerBeat;
            Vector2 position = PositionAt(distanceTravelled, out bool arrived);
            if (arrived)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            body.MovePosition(position);
        }

        private Vector2 PositionAt(double distance, out bool arrived)
        {
            Vector2 position = spawnPosition;
            for (int i = 0; i < route.Count; i++)
            {
                Vector2 target = route.Point(i);
                float length = Vector2.Distance(position, target);
                if (distance < length)
                {
                    arrived = false;
                    return Vector2.MoveTowards(position, target, (float)distance);
                }
                position = target;
                distance -= length;
            }
            arrived = true;
            return position;
        }
    }
}

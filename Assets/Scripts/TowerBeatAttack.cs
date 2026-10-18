using System;
using TapTapGameJam.Music;
using UnityEngine;

namespace TapTapGameJam.Combat
{
    [DisallowMultipleComponent]
    public sealed class TowerBeatAttack : MonoBehaviour
    {
        [SerializeField] MusicClock clock;
        [SerializeField] TowerEnemyDetector detector;
        [Header("Repeating attack score (0 = first beat)")]
        [SerializeField, Min(1)] int patternBeats = 4;
        [SerializeField] float[] attackBeats = { 0, 1, 2, 3 };
        [SerializeField, Min(1)] int damage = 1;
        [Tooltip("Late frames beyond this window skip the hit and rejoin the musical grid.")]
        [SerializeField, Range(.02f, .25f)] float maxLateSeconds = .12f;
        [SerializeField] Transform firepoint;
        [SerializeField] Color attackColor = new Color(.7f, 1, .85f);
        MusicClock subscribedClock;
        TowerAttackFeedback feedback;
        double pendingBeat = double.PositiveInfinity;
        double lastConsumedBeat = double.NegativeInfinity;
        public double ScheduledBeat => pendingBeat;
        public double LastAttackBeat { get; private set; } = double.NegativeInfinity;
        public int ShotsFired { get; private set; }

        void Awake()
        {
            if (detector == null) detector = GetComponentInChildren<TowerEnemyDetector>();
            if (firepoint == null) firepoint = transform.Find("Firepoint");
        }

        public void BindClock(MusicClock musicClock)
        {
            if (subscribedClock != null) subscribedClock.TransportChanged -= OnTransportChanged;
            if (clock != musicClock || subscribedClock != musicClock) lastConsumedBeat = double.NegativeInfinity;
            clock = musicClock; subscribedClock = musicClock;
            pendingBeat = double.PositiveInfinity;
            if (subscribedClock != null) subscribedClock.TransportChanged += OnTransportChanged;
        }

        // LateUpdate observes the most recent movement, then queries the real range
        // again at the hit. There is no damage or timer started by OnTriggerEnter.
        void LateUpdate()
        {
            if (clock == null)
                foreach (var candidate in FindObjectsOfType<MusicClock>())
                    if (candidate.gameObject.scene == gameObject.scene) { clock = candidate; break; }
            if (subscribedClock != clock) BindClock(clock);
            if (clock == null || !clock.isActiveAndEnabled || !clock.IsRunning || clock.IsPaused ||
                detector == null || !detector.isActiveAndEnabled)
            { pendingBeat = double.PositiveInfinity; return; }

            detector.RefreshTargets();
            if (!detector.HasTarget) { pendingBeat = double.PositiveInfinity; return; }
            double now = AudioSettings.dspTime;
            if (double.IsPositiveInfinity(pendingBeat)) ScheduleNext(now);
            if (double.IsPositiveInfinity(pendingBeat)) return; // empty score = rest
            double due = clock.GetDspTimeForBeat(pendingBeat);
            if (now < due) return;
            double beat = pendingBeat;
            pendingBeat = double.PositiveInfinity;
            lastConsumedBeat = beat;
            // Skip overdue damage after a hitch; never compress old attacks.
            bool overdue = now - due > Math.Min(maxLateSeconds, clock.SecondsPerBeat * .5);
            if (!overdue)
            {
                var target = SelectLivingTarget();
                if (target != null)
                {
                    Vector3 hitPosition = target.transform.position;
                    if (target.TakeDamage(Mathf.Max(1, damage)))
                    {
                        LastAttackBeat = beat; ShotsFired++;
                        if (Application.isPlaying)
                        {
                            if (feedback == null) feedback = gameObject.AddComponent<TowerAttackFeedback>();
                            feedback.Show(firepoint != null ? firepoint.position : transform.position, hitPosition, attackColor);
                        }
                    }
                }
            }
            ScheduleNext(now, overdue);
        }

        EnemyHealth SelectLivingTarget()
        {
            var current = detector.CurrentTarget;
            var health = current != null ? current.GetComponent<EnemyHealth>() : null;
            if (health != null && health.IsAlive) return health;
            foreach (var enemy in detector.Enemies)
            {
                health = enemy != null ? enemy.GetComponent<EnemyHealth>() : null;
                if (health != null && health.IsAlive) return health;
            }
            return null;
        }
        void ScheduleNext(double now, bool joining = true)
        {
            // Lead is needed to join the audio layer, not between marked hits:
            // applying it repeatedly would drop dense sixteenth-note attacks.
            double earliest = (now + (joining ? clock.ScheduleLeadSeconds : 0) - clock.GetDspTimeForBeat(0)) / clock.SecondsPerBeat;
            earliest = Math.Max(earliest, lastConsumedBeat + 1e-5);
            pendingBeat = AttackBeatGrid.Next(earliest, patternBeats, attackBeats);
        }
        void OnTransportChanged()
        {
            pendingBeat = double.PositiveInfinity;
            if (clock == null || !clock.IsRunning) lastConsumedBeat = double.NegativeInfinity;
            if (feedback != null) feedback.Hide();
        }
        void OnDisable()
        {
            pendingBeat = double.PositiveInfinity;
            if (feedback != null) feedback.Hide();
            if (subscribedClock != null) subscribedClock.TransportChanged -= OnTransportChanged;
            subscribedClock = null;
        }
    }
}

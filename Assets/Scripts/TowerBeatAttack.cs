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
        [SerializeField] TowerInstrument instrument;
        public TowerInstrument Instrument => instrument;
        [Header("Repeating attack score (0 = first beat)")]
        [SerializeField, Min(1)] int patternBeats = 4;
        [SerializeField] float[] attackBeats = { 0, 1, 2, 3 };
        [SerializeField, Min(1)] int damage = 1;
        [Tooltip("Late frames beyond this window skip the hit and rejoin the musical grid.")]
        [SerializeField, Range(.02f, .25f)] float maxLateSeconds = .12f;
        [SerializeField] Animator attackAnimator;
        [SerializeField] string attackAnimationState = "Base Layer.Tower_Bell_Attack";
        MusicClock subscribedClock;
        BellTowerMusicController bellMusic;
        LevelThreeTowerMusicController level3Music;
        int successfulHits;
        double pendingBeat = double.PositiveInfinity;
        double lastConsumedBeat = double.NegativeInfinity;
        public double ScheduledBeat => pendingBeat;
        public double LastAttackBeat { get; private set; } = double.NegativeInfinity;
        public int ShotsFired { get; private set; }

        void Awake()
        {
            bellMusic = GetComponent<BellTowerMusicController>();
            level3Music = GetComponent<LevelThreeTowerMusicController>();
            if (detector == null) detector = GetComponentInChildren<TowerEnemyDetector>();
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
            if (level3Music != null && level3Music.IsActiveForLevel) clock = level3Music.Clock;
            else if (bellMusic != null) clock = bellMusic.clock;
            else if (clock == null)
                foreach (var candidate in FindObjectsOfType<MusicClock>())
                    if (candidate.gameObject.scene == gameObject.scene) { clock = candidate; break; }
            if (subscribedClock != clock) BindClock(clock);
            if (clock == null || !clock.isActiveAndEnabled || !clock.IsRunning || clock.IsPaused ||
                detector == null || !detector.isActiveAndEnabled)
            { pendingBeat = double.PositiveInfinity; return; }

            detector.RefreshTargets();
            if (!detector.HasTarget) { pendingBeat = double.PositiveInfinity; return; }
            double now = AudioSettings.dspTime;
            // Bell hits share the audio reservation, including cancellation, loading,
            // pause/resume and the next-beat entry. Other towers keep their score.
            if (bellMusic != null || (clock.SelectedLevel >= 3 && level3Music != null) || double.IsPositiveInfinity(pendingBeat)) ScheduleNext(now);
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
                successfulHits = 0;
                if (instrument == TowerInstrument.Chord)
                {
                    // The range query deduplicates Rigidbody2Ds, so an enemy with
                    // several colliders still receives exactly one hit this beat.
                    foreach (var enemy in detector.Enemies)
                        if (enemy != null) HitTarget(enemy.GetComponent<EnemyHealth>(), now);
                }
                else HitTarget(SelectLivingTarget(), now);
                if (successfulHits > 0)
                {
                    // Count attack beats, not victims, so AOE does not inflate cadence.
                    LastAttackBeat = beat; ShotsFired++;
                    if (Application.isPlaying)
                    {
                        if (attackAnimator != null && attackAnimator.isActiveAndEnabled)
                        {
                            // Start the visual on the same scheduled hit as the audio.
                            attackAnimator.Play(attackAnimationState, 0, 0f);
                            attackAnimator.Update(0f);
                        }
                    }
                }
            }
            ScheduleNext(now, overdue);
        }

        void HitTarget(EnemyHealth target, double now)
        {
            if (target == null || !target.CanBeTargetedBy(instrument)) return;
            // Check again at the instant of damage, including the bell's second
            // hit. An earlier target/audio reservation never permits an outside hit.
            if (detector == null || !detector.ContainsEnemyCenter(target.GetComponent<Rigidbody2D>())) return;
            // Always use the enemy's typed hit entry point: color immunity and
            // the boss's two-instrument window also apply to every AOE victim.
            if (target.ReceiveHit(Mathf.Max(1, damage), instrument, now)) successfulHits++;
        }

        EnemyHealth SelectLivingTarget()
        {
            var current = detector.CurrentTarget;
            var health = current != null ? current.GetComponent<EnemyHealth>() : null;
            if (health != null && health.CanBeTargetedBy(instrument)) return health;
            foreach (var enemy in detector.Enemies)
            {
                health = enemy != null ? enemy.GetComponent<EnemyHealth>() : null;
                if (health != null && health.CanBeTargetedBy(instrument)) return health;
            }
            return null;
        }
        void ScheduleNext(double now, bool joining = true)
        {
            if (clock.SelectedLevel >= 3 && level3Music != null)
            {
                pendingBeat = level3Music.GetScheduledAttackBeat(lastConsumedBeat);
                return;
            }
            if (bellMusic != null)
            {
                pendingBeat = bellMusic.GetScheduledAttackBeat(lastConsumedBeat);
                return;
            }
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
        }
        void OnDisable()
        {
            pendingBeat = double.PositiveInfinity;
            if (subscribedClock != null) subscribedClock.TransportChanged -= OnTransportChanged;
            subscribedClock = null;
        }
    }
}

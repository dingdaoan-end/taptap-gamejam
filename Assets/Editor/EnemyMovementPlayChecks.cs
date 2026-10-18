using System;
using System.Linq;
using TapTapGameJam.Combat;
using TapTapGameJam.Music;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EnemyMovementPlayChecks
{
    const string Key = "EnemyMovement.PlaySmoke";
    const string FastKey = "EnemyMovement.FastPlaySmoke";
    static double started, pausedAt;
    static bool sawMovement, sawTurn, sawFacing, paused, resumed;
    static Vector2 previousDirection, heldPosition;
    static double heldDistance;
    static float minPose = float.PositiveInfinity, maxPose = float.NegativeInfinity;
    static EnemyPathMover firstEnemy;

    [InitializeOnLoadMethod]
    static void Initialize()
    {
        if (!SessionState.GetBool(Key, false)) return;
        started = EditorApplication.timeSinceStartup; EditorApplication.update -= Poll; EditorApplication.update += Poll;
    }
    public static void RunBatch()
    {
        if (Physics2D.simulationMode == SimulationMode2D.Script)
            throw new Exception("Real Play verification requires automatic Physics2D simulation.");
        EditorSceneManager.OpenScene("Assets/Scenes/Map.unity", OpenSceneMode.Single);
        var clock = UnityEngine.Object.FindObjectOfType<MusicClock>();
        var fields = new SerializedObject(clock); fields.FindProperty("pauseOnFocusLoss").boolValue = false; fields.ApplyModifiedPropertiesWithoutUndo();
        var spawner = UnityEngine.Object.FindObjectsOfType<EnemyAudioTestSpawner>().Single(s => s.enabled);
        spawner.unitsPerBeat = SessionState.GetBool(FastKey, false) ? 120 : 12; spawner.spawnIntervalBeats = 100;
        SessionState.SetBool(Key, true); Initialize(); EditorApplication.EnterPlaymode();
    }
    public static void RunFastBatch() { SessionState.SetBool(FastKey, true); RunBatch(); }
    static void Poll()
    {
        if (!EditorApplication.isPlaying)
        {
            if (EditorApplication.timeSinceStartup - started > 45) Finish(false, "Play did not start.");
            return;
        }
        try
        {
            var clock = UnityEngine.Object.FindObjectOfType<MusicClock>();
            var spawner = UnityEngine.Object.FindObjectsOfType<EnemyAudioTestSpawner>().Single(s => s.enabled);
            if (firstEnemy == null) firstEnemy = UnityEngine.Object.FindObjectsOfType<EnemyPathMover>().FirstOrDefault(e => e.IsInitialized);
            if (firstEnemy != null && firstEnemy.IsInitialized)
            {
                var bodyPosition = firstEnemy.GetComponent<Rigidbody2D>().position;
                if (SessionState.GetBool(FastKey, false))
                {
                    var points = new Vector2[spawner.route.Count];
                    for (int i = 0; i < points.Length; i++) points[i] = spawner.route.Point(i);
                    var expected = new EnemyRoutePath(spawner.spawnPoint.position, points).Sample(firstEnemy.DistanceTravelled, out _, out _);
                    if (Vector2.Distance(expected, bodyPosition) > .1f)
                        throw new Exception("Fast enemy physics lags behind road phase: expected=" + expected + ", actual=" + bodyPosition + ", cap=" + Physics2D.maxTranslationSpeed);
                }
                if (firstEnemy.DistanceTravelled > .1 && Vector2.Distance(bodyPosition, spawner.spawnPoint.position) > .15f) sawMovement = true;
                var direction = firstEnemy.MoveDirection;
                if (previousDirection.sqrMagnitude > .1f && direction.sqrMagnitude > .1f && Vector2.Dot(previousDirection, direction) < .5f) sawTurn = true;
                previousDirection = direction;
                var sprite = firstEnemy.GetComponentInChildren<SpriteRenderer>();
                if (Mathf.Abs(direction.x) > .1f && sprite.flipX == (direction.x > 0)) sawFacing = true;
                minPose = Mathf.Min(minPose, sprite.transform.localPosition.y); maxPose = Mathf.Max(maxPose, sprite.transform.localPosition.y);
                if (!SessionState.GetBool(FastKey, false) && !paused && firstEnemy.DistanceTravelled > 2)
                {
                    clock.PausePlayback(); paused = true; pausedAt = EditorApplication.timeSinceStartup;
                    heldPosition = firstEnemy.GetComponent<Rigidbody2D>().position; heldDistance = firstEnemy.DistanceTravelled;
                }
                else if (paused && !resumed && EditorApplication.timeSinceStartup - pausedAt > .25)
                {
                    if (Math.Abs(firstEnemy.DistanceTravelled - heldDistance) > .00001 || Vector2.Distance(heldPosition, firstEnemy.GetComponent<Rigidbody2D>().position) > .01f)
                        throw new Exception("Enemy drifted during actual music pause.");
                    clock.ResumePlayback(); resumed = true;
                }
            }
            if (spawner.ArrivedCount > 0)
            {
                if (SessionState.GetBool(FastKey, false))
                { Finish(sawMovement && spawner.ArrivedCount == 1, "fast actual physics retains road phase through arrival"); return; }
                if (!sawMovement || !sawTurn || !sawFacing || maxPose - minPose < .005f || !resumed || spawner.ArrivedCount != 1)
                    throw new Exception("Missing real movement state: moved=" + sawMovement + ", turned=" + sawTurn + ", faced=" + sawFacing + ", bob=" + (maxPose - minPose) + ", resumed=" + resumed);
                Finish(true, "actual Map: Minion spawned, followed road bends, faced movement, walked, froze on pause, resumed and arrived once; beat=" + clock.CurrentBeat);
                return;
            }
            if (EditorApplication.timeSinceStartup - started > 30)
                throw new Exception("Movement timed out: beat=" + clock.CurrentBeat + ", running=" + clock.IsRunning + ", paused=" + clock.IsPaused + ", living=" + spawner.LivingCount);
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Key, false); SessionState.SetBool(FastKey, false); EditorApplication.update -= Poll;
        if (success) Debug.Log("ENEMY_MOVEMENT_PLAY_PASS " + message); else Debug.LogError("ENEMY_MOVEMENT_PLAY_FAILED " + message);
        EditorApplication.Exit(success ? 0 : 1);
    }
}

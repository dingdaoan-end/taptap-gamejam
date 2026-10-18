using System;
using System.Linq;
using System.Reflection;
using TapTapGameJam.Combat;
using TapTapGameJam.Music;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Integration checks use the real prefabs, components, Physics2D and MusicClock.
public static class BeatAttackChecks
{
    static int checks;
    static MusicClock clock;
    static MusicTimeline timeline;
    static TowerBeatAttack attack;
    static TowerEnemyDetector detector;
    static EnemyHealth enemy;
    static GameObject tower;
    static void Expect(bool condition, string message)
    {
        if (!condition) throw new Exception("BEAT_ATTACK_FAILED: " + message);
        checks++; Debug.Log("BEAT_ATTACK_PASS " + message);
    }
    static void Invoke(object component, string method) => component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(component, null);
    static void SetBeat(double beat) => timeline.Start(AudioSettings.dspTime - beat * .6, 100);
    static void Tick() { Physics2D.SyncTransforms(); Invoke(attack, "LateUpdate"); }
    static EnemyHealth SpawnEnemy(Vector3 position)
    {
        var obj = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy.prefab"), position, Quaternion.identity);
        var health = obj.GetComponent<EnemyHealth>(); Invoke(health, "Awake"); Invoke(health, "OnEnable");
        var body = obj.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
        return health;
    }
    static void Fixture(string name = "Tower_Bell")
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        clock = new GameObject("Music clock", typeof(AudioSource), typeof(MusicClock)).GetComponent<MusicClock>();
        Invoke(clock, "Awake");
        timeline = (MusicTimeline)typeof(MusicClock).GetField("timeline", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(clock);
        // Real resume uses this clip to schedule its AudioSource.
        typeof(MusicClock).GetField("activeClip", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(clock,
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Lv.1&2/Lv.1 Drums.wav"));
        tower = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab"), Vector3.zero, Quaternion.identity);
        attack = tower.GetComponent<TowerBeatAttack>(); Invoke(attack, "Awake"); attack.BindClock(clock);
        detector = tower.GetComponentInChildren<TowerEnemyDetector>();
        enemy = SpawnEnemy(Vector3.zero); SetBeat(.25);
    }
    static void TimingAndRange()
    {
        Fixture(); Tick();
        Expect(enemy.CurrentHealth == 3 && attack.ShotsFired == 0, "entry between beats does not deal immediate damage");
        Expect(attack.ScheduledBeat == 1, "first hit scheduled on global beat 1");
        SetBeat(1.005); Tick();
        Expect(enemy.CurrentHealth == 2 && attack.LastAttackBeat == 1, "one damage lands on the scheduled music beat");
        Tick(); Tick(); Expect(enemy.CurrentHealth == 2, "many frames in one beat do not duplicate damage");
        enemy.transform.position = Vector3.right * 100; SetBeat(2.005); Tick();
        Expect(enemy.CurrentHealth == 2 && !detector.HasTarget, "leaving range before hit cancels pending damage");
        enemy.transform.position = Vector3.zero; SetBeat(2.25); Tick();
        Expect(enemy.CurrentHealth == 2 && attack.ScheduledBeat == 3, "reentry waits for a future global beat");
        detector.GetComponent<BoxCollider2D>().enabled = false; SetBeat(3.005); Tick();
        Expect(enemy.CurrentHealth == 2, "disabled attack collider cannot hit");
        detector.GetComponent<BoxCollider2D>().enabled = true; SetBeat(3.25); Tick();
        attack.enabled = false; Invoke(attack, "OnDisable"); SetBeat(4.005);
        Expect(double.IsPositiveInfinity(attack.ScheduledBeat) && enemy.CurrentHealth == 2, "demolition or disabling cancels pending hit");
    }
    static void PatternsAndStalls()
    {
        Fixture(); var fields = new SerializedObject(attack); var marks = fields.FindProperty("attackBeats");
        marks.arraySize = 0; fields.ApplyModifiedPropertiesWithoutUndo(); Tick(); SetBeat(1.005); Tick();
        Expect(enemy.CurrentHealth == 3 && double.IsPositiveInfinity(attack.ScheduledBeat), "empty attack score produces rests");
        fields.Update(); marks.arraySize = 2; marks.GetArrayElementAtIndex(0).floatValue = .5f; marks.GetArrayElementAtIndex(1).floatValue = 2.5f;
        fields.ApplyModifiedPropertiesWithoutUndo(); SetBeat(1.25); Tick();
        Expect(attack.ScheduledBeat == 2.5, "custom fractional score follows global pattern phase");
        SetBeat(2.505); Tick(); Expect(enemy.CurrentHealth == 2, "fractional marked beat deals damage");
        Fixture(); fields = new SerializedObject(attack); marks = fields.FindProperty("attackBeats");
        marks.arraySize = 5;
        for (int i = 0; i < 5; i++) marks.GetArrayElementAtIndex(i).floatValue = i * .25f;
        fields.ApplyModifiedPropertiesWithoutUndo(); SetBeat(.05); Tick(); SetBeat(.505); Tick();
        Expect(enemy.CurrentHealth == 2 && attack.ScheduledBeat == .75, "dense score retains sixteenth-note marks closer than the audio lead");
        SetBeat(.755); Tick(); Expect(enemy.CurrentHealth == 1 && attack.LastAttackBeat == .75, "next dense score mark lands without being skipped");
        Fixture(); Tick(); SetBeat(10.25); Tick();
        Expect(enemy.CurrentHealth == 3 && attack.ShotsFired == 0 && attack.ScheduledBeat == 11, "stall skips overdue attacks and schedules a later beat");
        SetBeat(11.005); Tick(); Expect(enemy.CurrentHealth == 2 && attack.ShotsFired == 1, "after stall only one current hit is applied");
    }
    static void Transport()
    {
        Fixture(); Tick(); clock.PausePlayback(); Tick();
        Expect(enemy.CurrentHealth == 3 && double.IsPositiveInfinity(attack.ScheduledBeat), "music pause cancels pending damage");
        clock.ResumePlayback(); Tick();
        Expect(enemy.CurrentHealth == 3, "resume scheduling lead does not attack early");
        SetBeat(1.005); Tick(); Expect(enemy.CurrentHealth == 2, "resume rejoins music beat");
        clock.StopPlayback(); Tick();
        Expect(enemy.CurrentHealth == 2 && double.IsPositiveInfinity(attack.ScheduledBeat), "music stop cancels future attacks");
        SetBeat(.25); Invoke(clock, "NotifyTransportChanged"); Tick();
        SetBeat(1.005); Tick(); Expect(enemy.CurrentHealth == 1 && attack.ShotsFired == 2, "restart allows new song beat numbers exactly once");
    }
    static void DamageAndRetarget()
    {
        Fixture();
        var extra = new GameObject("Second enemy collider", typeof(BoxCollider2D)); extra.transform.SetParent(enemy.transform, false); extra.layer = 7;
        Tick(); Expect(detector.EnemyCount == 1, "several enemy colliders count as one enemy");
        Expect(!enemy.TakeDamage(0) && !enemy.TakeDamage(-2) && enemy.CurrentHealth == 3, "zero and negative damage cannot heal or hit");
        enemy.TakeDamage(2); SetBeat(1.005); Tick();
        Expect(!enemy.IsAlive && !enemy.gameObject.activeSelf && enemy.CurrentHealth == 0, "lethal beat immediately removes enemy from gameplay");
        Expect(!enemy.TakeDamage(1), "dead enemy cannot be damaged again");
        var replacement = SpawnEnemy(Vector3.right * .3f); SetBeat(1.25); Tick();
        SetBeat(2.005); Tick(); Expect(replacement.CurrentHealth == 2 && detector.CurrentTarget == replacement.GetComponent<Rigidbody2D>(), "tower reacquires surviving enemy after a kill");
        Fixture(); var second = UnityEngine.Object.Instantiate(tower); var secondAttack = second.GetComponent<TowerBeatAttack>();
        Invoke(secondAttack, "Awake"); secondAttack.BindClock(clock); enemy.TakeDamage(2);
        Tick(); Physics2D.SyncTransforms(); Invoke(secondAttack, "LateUpdate");
        SetBeat(1.005); Tick(); Invoke(secondAttack, "LateUpdate");
        Expect(enemy.CurrentHealth == 0 && attack.ShotsFired + secondAttack.ShotsFired == 1, "two towers cannot claim the same lethal hit");
    }
    static void PrefabsAndScenes()
    {
        foreach (string name in new[] { "Tower", "Tower_Bell", "Tower_Chord", "Tower_Duduk" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");
            Expect(prefab.GetComponents<TowerBeatAttack>().Length == 1, "one inherited attack component in " + name);
        }
        Expect(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy.prefab").GetComponent<EnemyHealth>() != null, "spawned enemy prefab includes health");
        foreach (string scene in new[] { "Map", "MusicTest" })
        {
            EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity", OpenSceneMode.Single);
            var music = UnityEngine.Object.FindObjectOfType<MusicClock>();
            var source = (AudioSource)new SerializedObject(music).FindProperty("drumSource").objectReferenceValue;
            Expect(source != null && source.clip != null && Math.Abs(source.clip.length - 19.2) < .02, "matching 100 BPM drum loop assigned in " + scene);
            int buildButtons = 0;
            foreach (var button in UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Button>(true))
            {
                var calls = new SerializedObject(button).FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                for (int i = 0; i < calls.arraySize; i++)
                {
                    var call = calls.GetArrayElementAtIndex(i);
                    if (call.FindPropertyRelative("m_MethodName").stringValue != "Build") continue;
                    var prefab = call.FindPropertyRelative("m_Arguments.m_ObjectArgument").objectReferenceValue as GameObject;
                    Expect(prefab != null && prefab.GetComponent<TowerBeatAttack>() != null, "build menu creates combat-enabled " + (prefab != null ? prefab.name : "missing prefab") + " in " + scene);
                    buildButtons++;
                }
            }
            Expect(buildButtons == 3, "all three actual construction menu choices checked in " + scene);
        }
    }
    public static void RunBatch()
    {
        try
        {
            BeatAttackSetup.ConfigurePrefabs();
            TimingAndRange(); PatternsAndStalls(); Transport(); DamageAndRetarget(); PrefabsAndScenes();
            Debug.Log("BEAT_ATTACK_CHECK_SUCCESS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    public static void RunRed()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tower_Bell.prefab");
            var tower = UnityEngine.Object.Instantiate(prefab);
            if (!tower.GetComponents<MonoBehaviour>().Any(c => c != null && c.GetType().Name == "TowerBeatAttack"))
                throw new Exception("BEAT_ATTACK_RED: a built Bell tower has no beat attack component and cannot damage enemies.");
            Debug.Log("BEAT_ATTACK_RED_CHECK_PASSED");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}

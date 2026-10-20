using System;
using TapTapGameJam.Combat;
using UnityEditor;
using UnityEngine;

public static class BeatAttackSetup
{
    [MenuItem("Tools/Combat/Configure Beat Attack Prefabs")]
    public static void ConfigurePrefabs()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit Play mode before configuring prefabs.");
        Configure("Assets/Prefabs/Tower.prefab", root =>
        {
            var attack = root.GetComponent<TowerBeatAttack>();
            if (attack == null) attack = root.AddComponent<TowerBeatAttack>();
            var fields = new SerializedObject(attack);
            fields.FindProperty("detector").objectReferenceValue = root.GetComponentInChildren<TowerEnemyDetector>(true);
            fields.ApplyModifiedPropertiesWithoutUndo();
        });
        Configure("Assets/Prefabs/Enemy.prefab", root =>
        {
            if (root.GetComponent<EnemyHealth>() == null) root.AddComponent<EnemyHealth>();
        });
        AssetDatabase.SaveAssets();
        Debug.Log("BEAT_ATTACK_PREFABS_CONFIGURED");
    }
    static void Configure(string path, Action<GameObject> configure)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { configure(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}

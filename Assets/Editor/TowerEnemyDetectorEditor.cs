using TapTapGameJam.Combat;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TowerEnemyDetector))]
public sealed class TowerEnemyDetectorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var detector = (TowerEnemyDetector)target;
        var range = detector.GetComponent<BoxCollider2D>();
        if (!range.enabled || !range.isTrigger)
            EditorGUILayout.HelpBox("Enable the range collider and Is Trigger.", MessageType.Warning);
        if (!Application.isPlaying) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Enemies In Range", detector.EnemyCount.ToString());
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Current Target", detector.CurrentTarget, typeof(Rigidbody2D), true);
            foreach (var enemy in detector.Enemies)
                EditorGUILayout.ObjectField("Enemy", enemy, typeof(Rigidbody2D), true);
        }
        Repaint();
    }
}

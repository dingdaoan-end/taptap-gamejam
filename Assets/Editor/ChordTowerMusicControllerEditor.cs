using TapTapGameJam.Music;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ChordTowerMusicController))]
public sealed class ChordTowerMusicControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!Application.isPlaying) return;
        var controller = (ChordTowerMusicController)target;
        EditorGUILayout.LabelField("Current Segment (0 = idle)", controller.CurrentSegment.ToString());
        if (controller.clock != null)
            EditorGUILayout.LabelField("Global Segment (01-08)", controller.clock.CurrentPhrase.ToString("00"));
        EditorGUILayout.LabelField("Next Global Segment", controller.NextSegment.ToString("00"));
        Repaint();
    }
}

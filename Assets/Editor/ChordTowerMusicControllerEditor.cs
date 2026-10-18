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
            EditorGUILayout.LabelField("Global Phrase", controller.clock.CurrentPhrase.ToString());
        EditorGUILayout.LabelField("Next Global Phrase", controller.NextSegment.ToString());
        Repaint();
    }
}

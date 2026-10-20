using TapTapGameJam.Music;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BellTowerMusicController))]
public sealed class BellTowerMusicControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var controller = (BellTowerMusicController)target;
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("Assigned Loop (01-05)", controller.AssignedLoop.ToString("00"));
            EditorGUILayout.LabelField("Attack entry", "Next shared beat after target acquisition");
            EditorGUILayout.LabelField("Current Segment (0 = idle)", controller.CurrentSegment.ToString());
            EditorGUILayout.LabelField("Next Segment", controller.NextSegment.ToString());
            Repaint();
        }
    }

}

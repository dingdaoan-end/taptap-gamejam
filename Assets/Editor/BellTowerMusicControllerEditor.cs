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
            EditorGUILayout.LabelField("Deployment Section", controller.DeploymentSection);
            EditorGUILayout.LabelField("Selected Variation", controller.SelectedVariation == 0 ? "Unassigned" : controller.SelectedVariation.ToString());
            EditorGUILayout.LabelField("Current Segment (0 = idle)", controller.CurrentSegment.ToString());
            EditorGUILayout.LabelField("Next Segment", controller.NextSegment.ToString());
            Repaint();
        }
    }

}

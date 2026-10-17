using TapTapGameJam.Music;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MusicClock))]
public sealed class MusicClockEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!Application.isPlaying) return;
        var clock = (MusicClock)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Transport", clock.IsPreparing ? "Loading" :
            !clock.IsRunning ? "Stopped" : clock.IsPaused ? "Paused" : "Running / scheduled");
        EditorGUILayout.LabelField("Absolute beat (zero based)", clock.CurrentBeat.ToString("F3"));
        EditorGUILayout.LabelField("Loop beat (zero based)", clock.LoopBeat.ToString("F3"));
        EditorGUILayout.LabelField("Musical loop seconds", clock.LoopDuration.ToString("F3"));
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Restart")) clock.StartPlayback();
            if (GUILayout.Button(clock.IsPaused ? "Resume" : "Pause"))
            { if (clock.IsPaused) clock.ResumePlayback(); else clock.PausePlayback(); }
            if (GUILayout.Button("Stop")) clock.StopPlayback();
        }
        Repaint();
    }
}

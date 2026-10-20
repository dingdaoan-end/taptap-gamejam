using System;
using TapTapGameJam.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Repairs only the authored result controls, preserving the current scene's other settings.
public static class MapUIFlowResultSetup
{
    [MenuItem("Tools/UI Flow/Repair Result Replay Button")]
    public static void Repair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before repairing the scene.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Map.unity")
            throw new InvalidOperationException("Open Map first.");
        var flow = UnityEngine.Object.FindObjectOfType<MapUIFlow>();
        if (flow == null || flow.resultRoot == null || flow.NextLevelButton == null)
            throw new InvalidOperationException("Map needs its existing result panel and next-level button.");

        var parent = flow.resultRoot.transform;
        var existing = parent.Find("ReplayLevelButton");
        var replay = existing != null ? existing.GetComponent<Button>() : null;
        if (existing != null && replay == null)
            throw new InvalidOperationException("ReplayLevelButton exists without a Button component.");
        if (replay == null)
        {
            replay = UnityEngine.Object.Instantiate(flow.NextLevelButton, parent);
            replay.name = "ReplayLevelButton";
            Undo.RegisterCreatedObjectUndo(replay.gameObject, "Add result replay button");
            // MapUIFlow owns the listener; never copy an authored next-level action.
            replay.onClick = new Button.ButtonClickedEvent();
        }
        Undo.RecordObject(replay.GetComponentInChildren<TMP_Text>(true), "Label replay button");
        replay.GetComponentInChildren<TMP_Text>(true).text = "重玩本关";
        SetPosition(replay, -130);
        SetPosition(flow.NextLevelButton, 130);
        Undo.RecordObject(replay.gameObject, "Hide result replay until victory");
        replay.gameObject.SetActive(false);
        var serialized = new SerializedObject(flow);
        serialized.FindProperty("replayLevelButton").objectReferenceValue = replay;
        serialized.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Map.");
        Debug.Log("MAP_RESULT_REPLAY_SETUP_PASS: authored replay button saved and bound.");
    }

    static void SetPosition(Button button, float x)
    {
        var rect = (RectTransform)button.transform;
        Undo.RecordObject(rect, "Lay out result buttons");
        rect.anchoredPosition = new Vector2(x, -140);
        rect.sizeDelta = new Vector2(220, 54);
    }
}
